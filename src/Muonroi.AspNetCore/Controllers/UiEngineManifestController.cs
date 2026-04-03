using Muonroi.UiEngine.Catalog.Models;
using Muonroi.UiEngine.Catalog.Services;

namespace Muonroi.AspNetCore.Controllers;

/// <inheritdoc />
[ApiController]
[Authorize]
[Route("api/v1/ui-engine/manifest")]
public sealed class UiEngineManifestController(ICatalogScanService catalogScanService) : ControllerBase
{
    private static readonly HashSet<string> AllowedMethods = ["GET", "POST", "PUT", "PATCH", "DELETE"];
    private static readonly HashSet<string> AllowedTokenSources = ["header", "cookie", "localStorage"];
    private static readonly HashSet<string> AllowedFailurePolicies = ["redirect", "401", "retry"];

    /// <inheritdoc />
    [HttpPost("validate")]
    public ActionResult<MUiEngineManifestValidationResponse> Validate([FromBody] MUiEngineManifest manifest)
    {
        List<string> errors = CollectValidationErrors(manifest);
        List<string> warnings = CollectValidationWarnings(manifest);
        return Ok(new MUiEngineManifestValidationResponse
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        });
    }

    /// <inheritdoc />
    [HttpPost("generate-prompt")]
    public async Task<ActionResult<MUiEngineManifestPromptResponse>> GeneratePrompt(
        [FromBody] MUiEngineGeneratePromptRequest request,
        CancellationToken cancellationToken)
    {
        MUiEngineManifest manifest = request.Manifest ?? new MUiEngineManifest();
        IReadOnlyList<MUiEngineCatalogApiDescriptor> apis = request.CatalogApis is { Count: > 0 }
            ? request.CatalogApis
            : await catalogScanService.ScanApisAsync(cancellationToken);
        IReadOnlyList<MUiEngineCatalogRuleDescriptor> rules = request.CatalogRules is { Count: > 0 }
            ? request.CatalogRules
            : await catalogScanService.ScanRulesAsync(cancellationToken);
        IReadOnlyList<MUiEngineCatalogBinding> bindings = await catalogScanService.BuildBindingsAsync(cancellationToken);

        List<string> missingFields = CollectMissingFields(manifest);
        string template = JsonSerializer.Serialize(BuildTemplate(manifest, apis, bindings), new JsonSerializerOptions
        {
            WriteIndented = true
        }); // MBB002-exempt: requires WriteIndented formatting for AI prompt template output not available in wrapper

        StringBuilder builder = new();
        builder.AppendLine("You are completing Muonroi UI Engine manifest v2.");
        builder.AppendLine("Task: fill missing fields and keep schema backward-compatible.");
        builder.AppendLine();
        builder.AppendLine("Required checks:");
        builder.AppendLine("1. Ensure schemaVersion is mui.engine.v2.");
        builder.AppendLine("2. Ensure appShell/authProfile are set.");
        builder.AppendLine("3. Align apiContracts + ruleBindings with backend catalog.");
        builder.AppendLine("4. Keep existing navigation/screens/actions/dataSources unchanged unless missing.");
        builder.AppendLine();
        builder.AppendLine("Missing fields:");
        foreach (string missing in missingFields)
        {
            builder.AppendLine($"- {missing}");
        }

        builder.AppendLine();
        builder.AppendLine("Catalog APIs:");
        foreach (MUiEngineCatalogApiDescriptor api in apis.Take(30))
        {
            builder.AppendLine($"- [{api.HttpMethod}] {api.Route} ({api.ControllerName}.{api.ActionName})");
        }

        builder.AppendLine();
        builder.AppendLine("Catalog Rules:");
        foreach (MUiEngineCatalogRuleDescriptor rule in rules.Take(50))
        {
            builder.AppendLine($"- {rule.Code} | ctx={rule.ContextType} | order={rule.Order} | dependsOn=[{string.Join(", ", rule.DependsOn)}]");
        }

        builder.AppendLine();
        builder.AppendLine("Template JSON:");
        builder.AppendLine(template);

        return Ok(new MUiEngineManifestPromptResponse
        {
            Prompt = builder.ToString(),
            MissingFields = missingFields,
            ApiCount = apis.Count,
            RuleCount = rules.Count
        });
    }

    private static List<string> CollectValidationErrors(MUiEngineManifest manifest)
    {
        List<string> errors = [];

        if (manifest.SchemaVersion is not MUiEngineManifest.MSchemaVersionV1 and
            not MUiEngineManifest.MSchemaVersionV2)
        {
            errors.Add($"schemaVersion must be '{MUiEngineManifest.MSchemaVersionV1}' or '{MUiEngineManifest.MSchemaVersionV2}'.");
        }

        if (manifest.SchemaVersion == MUiEngineManifest.MSchemaVersionV2)
        {
            if (manifest.AppShell is null)
            {
                errors.Add("appShell is required for schema v2.");
            }
            else if (string.IsNullOrWhiteSpace(manifest.AppShell.RootLayout))
            {
                errors.Add("appShell.rootLayout is required.");
            }

            if (manifest.AuthProfile is null)
            {
                errors.Add("authProfile is required for schema v2.");
            }
            else
            {
                if (!AllowedTokenSources.Contains(manifest.AuthProfile.TokenSource))
                {
                    errors.Add("authProfile.tokenSource must be one of: header, cookie, localStorage.");
                }

                if (!AllowedFailurePolicies.Contains(manifest.AuthProfile.FailurePolicy))
                {
                    errors.Add("authProfile.failurePolicy must be one of: redirect, 401, retry.");
                }
            }
        }

        foreach (MUiEngineApiContract api in manifest.ApiContracts ?? [])
        {
            if (string.IsNullOrWhiteSpace(api.OperationId))
            {
                errors.Add("apiContracts.operationId is required.");
            }

            if (string.IsNullOrWhiteSpace(api.EndpointPath) || !api.EndpointPath.StartsWith('/'))
            {
                errors.Add($"apiContracts endpointPath '{api.EndpointPath}' must start with '/'.");
            }

            if (!AllowedMethods.Contains(api.HttpMethod.ToUpperInvariant()))
            {
                errors.Add($"apiContracts httpMethod '{api.HttpMethod}' is invalid.");
            }
        }

        foreach (MUiEngineRuleBinding binding in manifest.RuleBindings ?? [])
        {
            if (string.IsNullOrWhiteSpace(binding.EndpointRoute))
            {
                errors.Add("ruleBindings.endpointRoute is required.");
            }

            if (binding.OrderedRules.Count == 0)
            {
                errors.Add($"ruleBindings '{binding.EndpointRoute}' must have orderedRules.");
            }

            int distinctCount = binding.OrderedRules.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (distinctCount != binding.OrderedRules.Count)
            {
                errors.Add($"ruleBindings '{binding.EndpointRoute}' has duplicate rule codes in orderedRules.");
            }
        }

        return errors;
    }

    private static List<string> CollectValidationWarnings(MUiEngineManifest manifest)
    {
        List<string> warnings = [];

        if (manifest.ApiContracts is null || manifest.ApiContracts.Count == 0)
        {
            warnings.Add("apiContracts is empty. Sync script cannot generate typed API service map.");
        }

        if (manifest.RuleBindings is null || manifest.RuleBindings.Count == 0)
        {
            warnings.Add("ruleBindings is empty. Rule drag/drop graph will not map endpoints to rules.");
        }

        if (manifest.GenerationHints is null)
        {
            warnings.Add("generationHints is missing. Output paths will fallback to defaults.");
        }

        return warnings;
    }

    private static List<string> CollectMissingFields(MUiEngineManifest manifest)
    {
        List<string> missing = [];
        if (manifest.AppShell is null)
        {
            missing.Add("appShell");
        }

        if (manifest.AuthProfile is null)
        {
            missing.Add("authProfile");
        }

        if (manifest.ApiContracts is null || manifest.ApiContracts.Count == 0)
        {
            missing.Add("apiContracts");
        }

        if (manifest.RuleBindings is null || manifest.RuleBindings.Count == 0)
        {
            missing.Add("ruleBindings");
        }

        if (manifest.GenerationHints is null)
        {
            missing.Add("generationHints");
        }

        return missing;
    }

    private static MUiEngineManifest BuildTemplate(
        MUiEngineManifest current,
        IReadOnlyList<MUiEngineCatalogApiDescriptor> apis,
        IReadOnlyList<MUiEngineCatalogBinding> bindings)
    {
        MUiEngineManifest clone = new()
        {
            SchemaVersion = MUiEngineManifest.MSchemaVersionV2,
            UserId = current.UserId,
            TenantId = current.TenantId,
            LicenseTier = current.LicenseTier,
            NavigationGroups = current.NavigationGroups,
            Screens = current.Screens,
            Actions = current.Actions,
            DataSources = current.DataSources,
            ComponentRegistry = current.ComponentRegistry,
            AppShell = current.AppShell ?? new MUiEngineAppShell
            {
                RootLayout = "m-shell-default",
                Slots = ["header", "sidebar", "content", "footer"],
                Theme = "default"
            },
            AuthProfile = current.AuthProfile ?? new MUiEngineAuthProfile
            {
                TokenSource = "header",
                TokenKey = "Authorization",
                RefreshPath = "/api/v1/auth/refresh-token",
                TenantHeaderKey = "X-Tenant-Id",
                CorrelationIdKey = "X-Correlation-Id",
                FailurePolicy = "401"
            },
            ApiContracts = current.ApiContracts is { Count: > 0 }
                ? current.ApiContracts
                : [.. apis.Select(x => new MUiEngineApiContract
                {
                    OperationId = $"{x.ControllerName}_{x.ActionName}",
                    EndpointPath = x.Route,
                    HttpMethod = x.HttpMethod,
                    RequestSchemaRef = x.RequestType,
                    ResponseSchemaRef = x.ResponseType,
                    Tags = [x.ControllerName]
                })],
            RuleBindings = current.RuleBindings is { Count: > 0 }
                ? current.RuleBindings
                : [.. bindings.Select(x => new MUiEngineRuleBinding
                {
                    EndpointRoute = x.EndpointRoute,
                    WorkflowName = x.WorkflowName,
                    ContextType = x.ContextType,
                    OrderedRules = [.. x.Rules.OrderBy(r => r.Order).Select(r => r.Code)]
                })],
            GenerationHints = current.GenerationHints ?? new MUiEngineGenerationHints
            {
                OutputBasePath = "./src/generated/ui-engine",
                CoreOutputPath = "./src/generated/ui-engine/core",
                ApiOutputPath = "./src/generated/ui-engine/api",
                ModelsOutputPath = "./src/generated/ui-engine/models",
                FeaturesOutputPath = "./src/generated/ui-engine/features",
                WatchEnabled = true
            }
        };

        return clone;
    }
}

/// <inheritdoc />
public sealed class MUiEngineManifestValidationResponse
{
    /// <inheritdoc />
    public bool IsValid { get; set; }
    /// <inheritdoc />
    public List<string> Errors { get; set; } = [];
    /// <inheritdoc />
    public List<string> Warnings { get; set; } = [];
}

/// <inheritdoc />
public sealed class MUiEngineGeneratePromptRequest
{
    /// <inheritdoc />
    public MUiEngineManifest? Manifest { get; set; }
    /// <inheritdoc />
    public List<MUiEngineCatalogApiDescriptor>? CatalogApis { get; set; }
    /// <inheritdoc />
    public List<MUiEngineCatalogRuleDescriptor>? CatalogRules { get; set; }
}

/// <inheritdoc />
public sealed class MUiEngineManifestPromptResponse
{
    /// <inheritdoc />
    public string Prompt { get; set; } = string.Empty;
    /// <inheritdoc />
    public List<string> MissingFields { get; set; } = [];
    /// <inheritdoc />
    public int ApiCount { get; set; }
    /// <inheritdoc />
    public int RuleCount { get; set; }
}
