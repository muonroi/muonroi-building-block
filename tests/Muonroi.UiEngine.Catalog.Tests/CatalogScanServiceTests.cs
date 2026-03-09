using System.CodeDom.Compiler;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Logging.Abstractions;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.RuleEngine.Runtime.Rules;
using Muonroi.UiEngine.Catalog.Attributes;
using Muonroi.UiEngine.Catalog.Models;
using Muonroi.UiEngine.Catalog.Services;

namespace Muonroi.UiEngine.Catalog.Tests;

public sealed class CatalogScanServiceTests
{
    private readonly EmptyModelMetadataProvider _metadataProvider = new();

    [Fact]
    public async Task ScanApisAsync_ShouldNormalizeRoutes_AndCaptureContracts()
    {
        CatalogScanService service = CreateService([
            CreateApiDescription(
                relativePath: "api/v1/catalog/orders/{id}?debug=true",
                method: "post",
                controller: "Orders",
                action: "Save",
                metadata: [new AuthorizeAttribute { AuthenticationSchemes = "Bearer,ApiKey", Policy = "catalog.write" }],
                bodyType: typeof(FakeCreateRequest),
                responseType: typeof(FakeCreateResponse))
        ]);

        IReadOnlyList<MUiEngineCatalogApiDescriptor> apis = await service.ScanApisAsync();

        MUiEngineCatalogApiDescriptor api = Assert.Single(apis, x => x.Route == "/api/v1/catalog/orders/{id}");
        Assert.Equal("POST", api.HttpMethod);
        Assert.EndsWith(nameof(FakeCreateRequest), api.RequestType, StringComparison.Ordinal);
        Assert.EndsWith(nameof(FakeCreateResponse), api.ResponseType, StringComparison.Ordinal);
        Assert.Equal(["ApiKey", "Bearer"], api.AuthSchemes);
        Assert.Equal(["catalog.write"], api.Policies);
        Assert.True(api.RequiresTenantId);
    }

    [Fact]
    public async Task ScanApisAsync_ShouldHonorAnonymousWithoutTenantMetadata()
    {
        CatalogScanService service = CreateService([
            CreateApiDescription(
                relativePath: "api/v1/public/health",
                method: "get",
                controller: "Health",
                action: "Get",
                metadata: [new AllowAnonymousAttribute()]),
            CreateApiDescription(
                relativePath: "api/v1/public/info",
                method: "get",
                controller: "Info",
                action: "Get",
                metadata: [new AllowAnonymousWithoutTenantAttribute()])
        ]);

        IReadOnlyList<MUiEngineCatalogApiDescriptor> apis = await service.ScanApisAsync();

        Assert.False(apis.Single(x => x.Route == "/api/v1/public/health").RequiresTenantId);
        Assert.False(apis.Single(x => x.Route == "/api/v1/public/info").RequiresTenantId);
    }

    [Fact]
    public async Task ScanRulesAsync_ShouldDiscoverDescriptors_AndPreferLowestOrderDuplicate()
    {
        CatalogScanService service = CreateService();

        IReadOnlyList<MUiEngineCatalogRuleDescriptor> rules = await service.ScanRulesAsync();

        MUiEngineCatalogRuleDescriptor rule = Assert.Single(rules, x =>
            x.ContextType == typeof(CatalogScanTestsContext).FullName &&
            x.Code == CatalogScanTestsDependentRule.RuleCode);
        Assert.Equal(10, rule.Order);
        Assert.Equal(["CatalogScanTests_Root"], rule.DependsOn);
        Assert.Equal("AfterRule", rule.HookPoint);
        Assert.Equal("Validation", rule.RuleType);
    }

    [Fact]
    public async Task ScanRulesAsync_ShouldMarkCompensatableAndRulegenSource()
    {
        CatalogScanService service = CreateService();

        IReadOnlyList<MUiEngineCatalogRuleDescriptor> rules = await service.ScanRulesAsync();

        MUiEngineCatalogRuleDescriptor rule = Assert.Single(rules, x => x.Code == CatalogScanTestsGeneratedCompensatableRule.RuleCode);
        Assert.True(rule.IsCompensatable);
        Assert.Equal("rulegen", rule.Source);
    }

    [Fact]
    public async Task ScanRulesAsync_ShouldApplyTenantOverridesFromExecutionContext()
    {
        CatalogScanService service = CreateService(
            ruleOptions: new RuleOptions
            {
                TenantRuleToggles = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["tenant-alpha"] = new(StringComparer.OrdinalIgnoreCase)
                    {
                        [CatalogScanTestsDependentRule.RuleCode] = false
                    }
                }
            },
            executionContextAccessor: new TestExecutionContextAccessor("tenant-alpha"));

        IReadOnlyList<MUiEngineCatalogRuleDescriptor> rules = await service.ScanRulesAsync();

        MUiEngineCatalogRuleDescriptor rule = Assert.Single(rules, x => x.Code == CatalogScanTestsDependentRule.RuleCode);
        Assert.False(rule.IsEnabled);
    }

    [Fact]
    public async Task BuildBindingsAsync_ShouldUseBindRuleContextAttribute()
    {
        CatalogScanService service = CreateService([
            CreateApiDescription(
                relativePath: "api/v1/catalog/scan-tests/run",
                method: "post",
                controller: "Catalog",
                action: "Run",
                metadata: [new BindRuleContextAttribute(typeof(CatalogScanTestsContext)) { WorkflowName = "scan-tests" }])
        ]);

        IReadOnlyList<MUiEngineCatalogBinding> bindings = await service.BuildBindingsAsync();

        MUiEngineCatalogBinding binding = Assert.Single(bindings, x => x.EndpointRoute == "/api/v1/catalog/scan-tests/run");
        Assert.Equal(typeof(CatalogScanTestsContext).FullName, binding.ContextType);
        Assert.Equal("scan-tests", binding.WorkflowName);
        Assert.Contains(binding.Rules, x => x.Code == CatalogScanTestsRootRule.RuleCode);
        Assert.Contains(binding.Rules, x => x.Code == CatalogScanTestsDependentRule.RuleCode);
    }

    [Fact]
    public async Task BuildBindingsAsync_ShouldResolveContextByConvention_AndDefaultWorkflow()
    {
        CatalogScanService service = CreateService([
            CreateApiDescription(
                relativePath: "api/v1/rule-engine/cep/catalogscanrules",
                method: "get",
                controller: "Cep",
                action: "List")
        ]);

        IReadOnlyList<MUiEngineCatalogBinding> bindings = await service.BuildBindingsAsync();

        MUiEngineCatalogBinding binding = Assert.Single(bindings, x => x.EndpointRoute == "/api/v1/rule-engine/cep/catalogscanrules");
        Assert.Equal(typeof(CatalogScanRulesContext).FullName, binding.ContextType);
        Assert.Equal("cep", binding.WorkflowName);
        Assert.Contains(binding.Rules, x => x.Code == CatalogScanRulesConventionRule.RuleCode);
    }

    [Fact]
    public async Task BuildGraphAsync_ShouldCreateTriggerAndDependencyEdges()
    {
        CatalogScanService service = CreateService([
            CreateApiDescription(
                relativePath: "api/v1/catalog/scan-tests/run",
                method: "post",
                controller: "Catalog",
                action: "Run",
                metadata: [new BindRuleContextAttribute(typeof(CatalogScanTestsContext)) { WorkflowName = "scan-tests" }])
        ]);

        MUiEngineCatalogGraph graph = await service.BuildGraphAsync();

        Assert.Contains(graph.Nodes, x => x.Id == "endpoint:POST:/api/v1/catalog/scan-tests/run");
        Assert.Contains(graph.Nodes, x => x.Id == $"rule:{CatalogScanTestsRootRule.RuleCode.ToLowerInvariant()}");
        Assert.Contains(graph.Nodes, x => x.Id == $"rule:{CatalogScanTestsDependentRule.RuleCode.ToLowerInvariant()}");
        Assert.Contains(graph.Edges, x =>
            x.From == "endpoint:POST:/api/v1/catalog/scan-tests/run" &&
            x.To == $"rule:{CatalogScanTestsRootRule.RuleCode.ToLowerInvariant()}" &&
            x.EdgeType == "triggers");
        Assert.Contains(graph.Edges, x =>
            x.From == $"rule:{CatalogScanTestsRootRule.RuleCode.ToLowerInvariant()}" &&
            x.To == $"rule:{CatalogScanTestsDependentRule.RuleCode.ToLowerInvariant()}" &&
            x.EdgeType == "depends-on");
    }

    private CatalogScanService CreateService(
        IReadOnlyList<ApiDescription>? apis = null,
        RuleOptions? ruleOptions = null,
        ISystemExecutionContextAccessor? executionContextAccessor = null)
    {
        Mock<IApiDescriptionGroupCollectionProvider> apiProvider = new();
        apiProvider.Setup(x => x.ApiDescriptionGroups).Returns(
            new ApiDescriptionGroupCollection(
                [new ApiDescriptionGroup("catalog", apis ?? [])],
                version: 1));

        Mock<IOptionsMonitor<RuleOptions>> optionsMonitor = new();
        optionsMonitor.SetupGet(x => x.CurrentValue).Returns(ruleOptions ?? new RuleOptions());

        return new CatalogScanService(
            apiProvider.Object,
            new ServiceCollection().BuildServiceProvider(),
            Mock.Of<IMLog<CatalogScanService>>(),
            optionsMonitor.Object,
            executionContextAccessor);
    }

    private ApiDescription CreateApiDescription(
        string relativePath,
        string method,
        string controller,
        string action,
        IReadOnlyList<object>? metadata = null,
        Type? bodyType = null,
        Type? responseType = null)
    {
        ControllerActionDescriptor actionDescriptor = new()
        {
            ControllerName = controller,
            ActionName = action,
            RouteValues = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["controller"] = controller,
                ["action"] = action
            },
            EndpointMetadata = metadata?.ToList() ?? []
        };

        ApiDescription description = new()
        {
            RelativePath = relativePath,
            HttpMethod = method,
            ActionDescriptor = actionDescriptor
        };

        if (bodyType is not null)
        {
            description.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "body",
                Source = BindingSource.Body,
                Type = bodyType,
                ModelMetadata = _metadataProvider.GetMetadataForType(bodyType)
            });
        }

        if (responseType is not null)
        {
            description.SupportedResponseTypes.Add(new ApiResponseType
            {
                StatusCode = 200,
                Type = responseType
            });
        }

        return description;
    }

    private sealed class TestExecutionContextAccessor(string? tenantId) : ISystemExecutionContextAccessor
    {
        private readonly ISystemExecutionContext _context = new SystemExecutionContext(
            tenantId,
            userId: null,
            username: null,
            correlationId: Guid.NewGuid().ToString("N"),
            accessToken: null,
            apiKey: null,
            isAuthenticated: false,
            permissions: [],
            sourceType: "test");

        public ISystemExecutionContext Get() => _context;
        public void Set(ISystemExecutionContext context) => throw new NotSupportedException();
        public void Clear()
        {
        }
    }

    private sealed class AllowAnonymousWithoutTenantAttribute : Attribute
    {
    }

    private sealed class FakeCreateRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class FakeCreateResponse
    {
        public string Id { get; set; } = string.Empty;
    }
}

internal sealed class CatalogScanTestsContext
{
}

internal sealed class CatalogScanRulesContext
{
}

internal sealed class CatalogScanTestsRootRule : IRule<CatalogScanTestsContext>
{
    public const string RuleCode = "CatalogScanTests_Root";
    public string Code => RuleCode;
    public int Order => 1;
}

internal sealed class CatalogScanTestsDependentRule : IRule<CatalogScanTestsContext>
{
    public const string RuleCode = "CatalogScanTests_Dependent";
    public string Code => RuleCode;
    public int Order => 10;
    public IReadOnlyList<string> DependsOn => [CatalogScanTestsRootRule.RuleCode];
    public HookPoint HookPoint => HookPoint.AfterRule;
}

internal sealed class CatalogScanTestsDuplicateRule : IRule<CatalogScanTestsContext>
{
    public string Code => CatalogScanTestsDependentRule.RuleCode;
    public int Order => 99;
}

[GeneratedCode("catalog-scan-tests", "1.0.0")]
internal sealed class CatalogScanTestsGeneratedCompensatableRule : ICompensatableRule<CatalogScanTestsContext>
{
    public const string RuleCode = "CatalogScanTests_Generated";
    public string Code => RuleCode;
    public Task CompensateAsync(CatalogScanTestsContext context, FactBag facts, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

internal sealed class CatalogScanRulesConventionRule : IRule<CatalogScanRulesContext>
{
    public const string RuleCode = "CatalogScanRules_Convention";
    public string Code => RuleCode;
    public int Order => 5;
}
