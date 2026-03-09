using Muonroi.Core.Abstractions.Context;
using Muonroi.Governance.License;
using Muonroi.RuleEngine.Core.Tracing;
using RulesEngine.Models;
using System.Diagnostics;
using System.Dynamic;

namespace Muonroi.RuleEngine.Runtime.Web.Services;

public sealed class RuleDryRunService(
    IRuleSetStore store,
    IRuleSetDefinitionValidator validator,
    IServiceProvider serviceProvider,
    ISystemExecutionContextAccessor executionContextAccessor,
    ReSettings? settings = null,
    ILicenseGuard? licenseGuard = null,
    ProofTierAccessor? proofTierAccessor = null) : IRuleDryRunService
{
    private const string ContextTypeKey = "__contextType";
    private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(10);

    private readonly IRuleSetStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly IRuleSetDefinitionValidator _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly ISystemExecutionContextAccessor _executionContextAccessor =
        executionContextAccessor ?? throw new ArgumentNullException(nameof(executionContextAccessor));
    private readonly ReSettings _settings = settings ?? new ReSettings();
    private readonly ILicenseGuard? _licenseGuard = licenseGuard;
    private readonly ProofTierAccessor? _proofTierAccessor = proofTierAccessor;

    public async Task<RuleDryRunResult> RunAsync(
        string ruleSetContent,
        RuleSetFormat format,
        IReadOnlyDictionary<string, object?> inputFacts,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ruleSetContent))
        {
            return new RuleDryRunResult
            {
                Errors = ["Ruleset content is required."]
            };
        }

        if (format != RuleSetFormat.Json)
        {
            return new RuleDryRunResult
            {
                Errors = [$"Ruleset format '{format}' is not supported for dry-run."]
            };
        }

        Dictionary<string, object?> normalizedInputs = NormalizeInputs(inputFacts);
        RuleSetMetadata metadata = ParseMetadata(ruleSetContent);
        RuleSetValidationResult validation = _validator.Validate(metadata.WorkflowName, ruleSetContent);
        if (!validation.IsValid)
        {
            return new RuleDryRunResult
            {
                Errors = [.. validation.Errors.Select(x => $"{x.Code}: {x.Message}")],
                Traces = BuildInvalidValidationTraces(metadata)
            };
        }

        ISystemExecutionContext previousContext = _executionContextAccessor.Get();
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            _executionContextAccessor.Set(WithTenant(previousContext, tenantId!));
        }

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(MaxDuration);
        Stopwatch stopwatch = Stopwatch.StartNew();

        try
        {
            return metadata.IsLegacyWorkflow
                ? await RunLegacyAsync(ruleSetContent, metadata, normalizedInputs, stopwatch, timeoutCts.Token)
                : await RunCodeAsync(ruleSetContent, metadata, normalizedInputs, stopwatch, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new RuleDryRunResult
            {
                EvaluationTime = stopwatch.Elapsed,
                Errors = [$"Dry-run timed out after {MaxDuration.TotalSeconds:0} seconds."]
            };
        }
        finally
        {
            _executionContextAccessor.Set(previousContext);
        }
    }

    private async Task<RuleDryRunResult> RunLegacyAsync(
        string ruleSetContent,
        RuleSetMetadata metadata,
        IReadOnlyDictionary<string, object?> normalizedInputs,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        _proofTierAccessor?.RequireMinimumTier(LicenseTier.Licensed, FreeTierFeatures.Premium.RuleEngine);
        _licenseGuard?.EnsureFeature(FreeTierFeatures.Premium.RuleEngine);

        Workflow[] workflows = DeserializeLegacyWorkflows(ruleSetContent);
        if (workflows.Length == 0)
        {
            stopwatch.Stop();
            return new RuleDryRunResult
            {
                EvaluationTime = stopwatch.Elapsed,
                Errors = ["Legacy workflow ruleset cannot be parsed."]
            };
        }

        RulesEngine.RulesEngine engine = new(workflows, _settings);
        object dynamicInputs = ToDynamicObject(normalizedInputs);
        dynamic[] inputs =
        [
            new
            {
                value = dynamicInputs,
                input = dynamicInputs
            }
        ];

        List<RuleResultTree> results = await engine.ExecuteAllRulesAsync(metadata.WorkflowName, inputs);

        Dictionary<string, object?> outputFacts = new(StringComparer.OrdinalIgnoreCase);
        List<RuleExecutionTrace> traces = [];
        List<string> errors = [];

        foreach (RuleResultTree result in results)
        {
            string ruleName = result.Rule?.RuleName ?? "unknown";
            bool matched = result.IsSuccess &&
                           string.IsNullOrWhiteSpace(result.ExceptionMessage) &&
                           result.ActionResult?.Exception is null;
            string? failReason = matched ? null : BuildLegacyFailureReason(result);

            traces.Add(new RuleExecutionTrace
            {
                RuleName = ruleName,
                Matched = matched,
                FailReason = failReason
            });

            if (!string.IsNullOrWhiteSpace(failReason))
            {
                errors.Add($"{ruleName}: {failReason}");
            }

            if (result.ActionResult?.Exception is Exception actionEx)
            {
                errors.Add($"{ruleName}: {actionEx.Message}");
            }

            AppendLegacyOutputFacts(outputFacts, ruleName, result.ActionResult?.Output);
        }

        stopwatch.Stop();
        return new RuleDryRunResult
        {
            RulesMatched = traces.Any(x => x.Matched),
            Traces = traces,
            EvaluationTime = stopwatch.Elapsed,
            Errors = [.. errors.Distinct(StringComparer.Ordinal)],
            OutputFacts = outputFacts
        };
    }

    private async Task<RuleDryRunResult> RunCodeAsync(
        string ruleSetContent,
        RuleSetMetadata metadata,
        Dictionary<string, object?> normalizedInputs,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        _proofTierAccessor?.RequireMinimumTier(LicenseTier.Licensed, FreeTierFeatures.Premium.RuleEngine);
        _licenseGuard?.EnsureFeature(FreeTierFeatures.Premium.RuleEngine);

        string? explicitContextType = ExtractContextType(normalizedInputs);
        JsonElement context = JsonSerializer.SerializeToElement(normalizedInputs);

        CollectingRuleExecutionTracer tracer = new();
        DryRunServiceProvider dryRunServiceProvider = new(_serviceProvider, tracer);
        RulesEngineService tempService = new(
            _store,
            _settings,
            _licenseGuard,
            runtimeCache: null,
            notifier: null,
            serviceProvider: dryRunServiceProvider,
            validator: _validator,
            canaryRolloutService: null,
            executionContextAccessor: _executionContextAccessor);

        Dictionary<string, object?> outputFacts = new(StringComparer.OrdinalIgnoreCase);
        List<string> errors = [];
        try
        {
            string contextType =
                string.IsNullOrWhiteSpace(explicitContextType)
                    ? typeof(Dictionary<string, object?>).AssemblyQualifiedName!
                    : explicitContextType;

            FactBag facts = await tempService.DryRunAsync(
                metadata.WorkflowName,
                ruleSetContent,
                context,
                contextType,
                cancellationToken);

            foreach ((string key, object? value) in facts.AsReadOnly())
            {
                outputFacts[key] = value;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors.Add(ex.Message);
        }

        List<RuleExecutionTrace> traces = BuildCodeTraces(metadata.RuleCodes, tracer.GetEntries(), errors);
        stopwatch.Stop();
        return new RuleDryRunResult
        {
            RulesMatched = traces.Any(x => x.Matched),
            Traces = traces,
            EvaluationTime = stopwatch.Elapsed,
            Errors = errors,
            OutputFacts = outputFacts
        };
    }

    private static string? ExtractContextType(IDictionary<string, object?> inputs)
    {
        if (!inputs.TryGetValue(ContextTypeKey, out object? value))
        {
            return null;
        }

        inputs.Remove(ContextTypeKey);
        if (value is string s && !string.IsNullOrWhiteSpace(s))
        {
            return s.Trim();
        }

        return null;
    }

    private static Workflow[] DeserializeLegacyWorkflows(string ruleSetContent)
    {
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        using JsonDocument doc = JsonDocument.Parse(ruleSetContent);
        JsonElement root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<Workflow[]>(root.GetRawText(), options) ?? [];
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if ((TryGetProperty(root, "ruleName", out JsonElement _) ||
             TryGetProperty(root, "RuleName", out _)))
        {
            Rule? single = JsonSerializer.Deserialize<Rule>(root.GetRawText(), options);
            if (single is null)
            {
                return [];
            }

            string workflowName = TryGetString(root, "workflowName") ??
                                  TryGetString(root, "WorkflowName") ??
                                  "dry-run";
            return
            [
                new Workflow
                {
                    WorkflowName = workflowName,
                    Rules = [single]
                }
            ];
        }

        Workflow? workflow = JsonSerializer.Deserialize<Workflow>(root.GetRawText(), options);
        return workflow is null ? [] : [workflow];
    }

    private static RuleSetMetadata ParseMetadata(string ruleSetContent)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(ruleSetContent);
            JsonElement root = doc.RootElement;
            JsonElement workflowElement = root;

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
            {
                workflowElement = root[0];
            }

            string workflowName = TryGetString(workflowElement, "workflowName") ??
                                  TryGetString(workflowElement, "WorkflowName") ??
                                  "dry-run";

            if (workflowElement.ValueKind == JsonValueKind.Object &&
                TryGetRulesArray(workflowElement, out JsonElement rules) &&
                rules.GetArrayLength() > 0)
            {
                JsonElement first = rules[0];
                if (first.ValueKind == JsonValueKind.String)
                {
                    List<string> codes = [];
                    foreach (JsonElement code in rules.EnumerateArray())
                    {
                        if (code.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(code.GetString()))
                        {
                            codes.Add(code.GetString()!);
                        }
                    }

                    return new RuleSetMetadata(
                        workflowName,
                        IsLegacyWorkflow: false,
                        RuleCodes: [.. codes.Distinct(StringComparer.OrdinalIgnoreCase)]);
                }

                if (first.ValueKind == JsonValueKind.Object)
                {
                    return new RuleSetMetadata(workflowName, IsLegacyWorkflow: true, RuleCodes: []);
                }
            }

            if (workflowElement.ValueKind == JsonValueKind.Object &&
                (TryGetProperty(workflowElement, "ruleName", out _) ||
                 TryGetProperty(workflowElement, "RuleName", out _)))
            {
                return new RuleSetMetadata(workflowName, IsLegacyWorkflow: true, RuleCodes: []);
            }

            return new RuleSetMetadata(workflowName, IsLegacyWorkflow: true, RuleCodes: []);
        }
        catch (JsonException)
        {
            return new RuleSetMetadata("dry-run", IsLegacyWorkflow: true, RuleCodes: []);
        }
    }

    private static List<RuleExecutionTrace> BuildInvalidValidationTraces(RuleSetMetadata metadata)
    {
        if (metadata.RuleCodes.Count == 0)
        {
            return [];
        }

        return [.. metadata.RuleCodes.Select(x => new RuleExecutionTrace
        {
            RuleName = x,
            Matched = false,
            FailReason = "Ruleset validation failed."
        })];
    }

    private static List<RuleExecutionTrace> BuildCodeTraces(
        IReadOnlyList<string> declaredCodes,
        IReadOnlyList<RuleTraceEntry> entries,
        IReadOnlyList<string> errors)
    {
        List<RuleExecutionTrace> traces = entries
            .GroupBy(x => x.RuleName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                RuleTraceEntry latest = group
                    .OrderByDescending(x => x.ExecutedAt)
                    .ThenByDescending(x => x.Phase)
                    .First();

                bool matched = group
                    .Where(x => x.Phase == RuleTracePhase.AfterEval)
                    .OrderByDescending(x => x.ExecutedAt)
                    .Select(x => x.IsSuccess)
                    .FirstOrDefault();

                return new RuleExecutionTrace
                {
                    RuleName = group.Key,
                    Matched = matched,
                    FailReason = matched
                        ? null
                        : latest.FailureReason ?? latest.ExceptionMessage
                };
            })
            .OrderBy(x => x.RuleName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (traces.Count > 0 || declaredCodes.Count == 0)
        {
            return traces;
        }

        string? sharedError = errors.FirstOrDefault();
        return [.. declaredCodes.Select(code => new RuleExecutionTrace
        {
            RuleName = code,
            Matched = false,
            FailReason = sharedError ?? "Rule was not evaluated."
        })];
    }

    private static string? BuildLegacyFailureReason(RuleResultTree result)
    {
        if (!string.IsNullOrWhiteSpace(result.ExceptionMessage))
        {
            return result.ExceptionMessage;
        }

        if (result.ActionResult?.Exception is Exception actionException)
        {
            return actionException.Message;
        }

        if (result.IsSuccess)
        {
            return null;
        }

        return "Condition evaluated to false.";
    }

    private static void AppendLegacyOutputFacts(
        IDictionary<string, object?> outputFacts,
        string ruleName,
        object? output)
    {
        if (output is null)
        {
            return;
        }

        if (output is IDictionary<string, object?> typedDictionary)
        {
            foreach ((string key, object? value) in typedDictionary)
            {
                outputFacts[key] = NormalizeOutputValue(value);
            }

            return;
        }

        if (output is IDictionary<string, object> dictionary)
        {
            foreach ((string key, object value) in dictionary)
            {
                outputFacts[key] = NormalizeOutputValue(value);
            }

            return;
        }

        outputFacts[ruleName] = NormalizeOutputValue(output);
    }

    private static object? NormalizeOutputValue(object? output)
    {
        if (output is not JsonElement element)
        {
            return output;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out int i) => i,
            JsonValueKind.Number when element.TryGetInt64(out long l) => l,
            JsonValueKind.Number when element.TryGetDecimal(out decimal dec) => dec,
            JsonValueKind.Number when element.TryGetDouble(out double d) => d,
            JsonValueKind.Array => element.EnumerateArray().Select(x => NormalizeOutputValue(x)).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(x => x.Name, x => NormalizeOutputValue(x.Value), StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
    }

    private static object ToDynamicObject(object? value)
    {
        return value switch
        {
            null => new ExpandoObject(),
            IDictionary<string, object?> dictionary => ToExpando(dictionary),
            IReadOnlyDictionary<string, object?> readOnlyDictionary =>
                ToExpando(readOnlyDictionary.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)),
            IEnumerable<object?> list => list.Select(ToDynamicObject).ToList(),
            _ => value
        };
    }

    private static ExpandoObject ToExpando(IDictionary<string, object?> value)
    {
        IDictionary<string, object?> expando = new ExpandoObject();
        foreach ((string key, object? item) in value)
        {
            expando[key] = item switch
            {
                IDictionary<string, object?> nested => ToExpando(nested),
                IReadOnlyDictionary<string, object?> nestedReadOnly =>
                    ToExpando(nestedReadOnly.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase)),
                IEnumerable<object?> nestedList => nestedList.Select(ToDynamicObject).ToList(),
                _ => item
            };
        }

        return (ExpandoObject)expando;
    }

    private static Dictionary<string, object?> NormalizeInputs(IReadOnlyDictionary<string, object?> inputFacts)
    {
        Dictionary<string, object?> normalized = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string key, object? value) in inputFacts)
        {
            normalized[key] = NormalizeInputValue(value);
        }

        return normalized;
    }

    private static object? NormalizeInputValue(object? value)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out int i) => i,
            JsonValueKind.Number when element.TryGetInt64(out long l) => l,
            JsonValueKind.Number when element.TryGetDecimal(out decimal dec) => dec,
            JsonValueKind.Number when element.TryGetDouble(out double d) => d,
            JsonValueKind.Array => element.EnumerateArray().Select(x => NormalizeInputValue(x)).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(x => x.Name, x => NormalizeInputValue(x.Value), StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
    }

    private static ISystemExecutionContext WithTenant(ISystemExecutionContext current, string tenantId)
    {
        if (current is SystemExecutionContext system)
        {
            return system.With(tenantId: tenantId);
        }

        return new SystemExecutionContext(
            tenantId,
            current.UserId,
            current.Username,
            current.CorrelationId,
            current.AccessToken,
            current.ApiKey,
            current.IsAuthenticated,
            current.Permissions,
            current.SourceType);
    }

    private static bool TryGetRulesArray(JsonElement element, out JsonElement rules)
    {
        return (TryGetProperty(element, "rules", out rules) || TryGetProperty(element, "Rules", out rules)) &&
               rules.ValueKind == JsonValueKind.Array;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        return element.TryGetProperty(name, out value);
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private sealed class DryRunServiceProvider(IServiceProvider inner, IRuleExecutionTracer tracer) : IServiceProvider
    {
        private readonly IServiceProvider _inner = inner;
        private readonly IRuleExecutionTracer _tracer = tracer;

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IRuleExecutionTracer))
            {
                return _tracer;
            }

            return _inner.GetService(serviceType);
        }
    }

    private sealed class CollectingRuleExecutionTracer : IRuleExecutionTracer
    {
        private readonly object _sync = new();
        private readonly List<RuleTraceEntry> _entries = [];

        public bool IsEnabled(string? tenantId)
        {
            _ = tenantId;
            return true;
        }

        public ValueTask TraceAsync(RuleTraceEntry entry, CancellationToken ct = default)
        {
            _ = ct;
            lock (_sync)
            {
                _entries.Add(entry);
            }

            return ValueTask.CompletedTask;
        }

        public IReadOnlyList<RuleTraceEntry> GetEntries()
        {
            lock (_sync)
            {
                return [.. _entries];
            }
        }
    }

    private sealed record RuleSetMetadata(
        string WorkflowName,
        bool IsLegacyWorkflow,
        IReadOnlyList<string> RuleCodes);
}
