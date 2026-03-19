using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Muonroi.Core.Abstractions.SeedWorks;
using Muonroi.Core.Helpers;
using Muonroi.RuleGen.Mcp.Infrastructure;
using Muonroi.RuleGen.Mcp.Prompts;
using Muonroi.RuleGen.Mcp.Resources;
using Muonroi.RuleGen.Mcp.Tools.Compliance;
using Muonroi.RuleGen.Mcp.Tools.DecisionTableGen;
using Muonroi.RuleGen.Mcp.Tools.Policy;
using Muonroi.RuleGen.Mcp.Tools.RuleGen;
using Muonroi.RuleGen.Mcp.Tools.Scaffold;

namespace Muonroi.RuleGen.Mcp.Tests;

[Collection("NonParallel")]
public sealed class DeveloperMcpToolTests
{
    private readonly MJsonSerializeService _jsonService = new();
    private readonly MDateTimeService _dateTimeService = new();

    [Fact]
    public async Task ComplianceScanner_Finds_Mbb001_Mbb005_And_Mbb006()
    {
        using TestWorkspace workspace = new();
        string sourcePath = workspace.WriteFile(
            "SampleService.cs",
            """
            using System;

            namespace Sample.App;

            public sealed class SampleService
            {
                public DateTime GetNow()
                {
                    return DateTime.UtcNow;
                }
            }

            public static class Bootstrap
            {
                public static void AddFeature(object services)
                {
                    services.AddMassTransit();
                }
            }
            """);
        _ = workspace.WriteFile(
            "Sample.Abstractions.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
              </ItemGroup>
            </Project>
            """);

        ComplianceScanner scanner = new();
        string payload = await new CheckMbbViolationsTool(scanner, _jsonService)
            .ExecuteAsync([workspace.RootPath], ct: CancellationToken.None);

        JsonElement root = JsonSerializer.Deserialize<JsonElement>(payload);
        string[] codes = root.GetProperty("Violations")
            .EnumerateArray()
            .Select(x => x.GetProperty("Code").GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToArray();

        Assert.Contains("MBB001", codes);
        Assert.Contains("MBB005", codes);
        Assert.Contains("MBB006", codes);
        Assert.Equal(sourcePath, root.GetProperty("AnalyzedFiles").EnumerateArray().First(x => x.GetString()!.EndsWith("SampleService.cs")).GetString());
    }

    [Fact]
    public void ScaffoldRuleClassTool_Generates_Extractable_Method_Source()
    {
        ScaffoldRuleClassTool tool = new(_jsonService);

        string payload = tool.Execute("ORDER_CHECK", "OrderContext", "Sample.Rules", order: 2);
        JsonElement root = JsonSerializer.Deserialize<JsonElement>(payload);
        string code = root.GetProperty("Code").GetString() ?? string.Empty;

        Assert.Equal("ORDER_CHECKRuleSource.cs", root.GetProperty("Filename").GetString());
        Assert.Contains("public sealed class ORDER_CHECKRuleSource", code, StringComparison.Ordinal);
        Assert.Contains("[MExtractAsRule(\"ORDER_CHECK\", Order = 2, HookPoint = HookPoint.BeforeRule)]", code, StringComparison.Ordinal);
        Assert.Contains("public async Task<RuleResult> EvaluateAsync(OrderContext ctx, FactBag facts, CancellationToken ct = default)", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PolicyTools_Sign_And_Verify_RoundTrip()
    {
        using TestWorkspace workspace = new();
        using RSA rsa = RSA.Create(2048);
        workspace.WriteFile("private.pem", rsa.ExportPkcs8PrivateKeyPem());
        workspace.WriteFile("public.pem", rsa.ExportSubjectPublicKeyInfoPem());

        SignPolicyTool signTool = new(_jsonService, _dateTimeService);
        string signPayload = await signTool.ExecuteAsync(
            "private.pem",
            "LIC-123",
            workingDirectory: workspace.RootPath,
            ct: CancellationToken.None);
        JsonElement signResult = JsonSerializer.Deserialize<JsonElement>(signPayload);
        string policyPath = signResult.GetProperty("OutputPath").GetString() ?? string.Empty;

        VerifyPolicyTool verifyTool = new(_jsonService, _dateTimeService);
        string verifyPayload = await verifyTool.ExecuteAsync(
            "policy.json",
            "public.pem",
            workspace.RootPath,
            CancellationToken.None);
        JsonElement verifyResult = JsonSerializer.Deserialize<JsonElement>(verifyPayload);

        Assert.True(File.Exists(policyPath));
        Assert.True(verifyResult.GetProperty("IsValid").GetBoolean());
        Assert.True(verifyResult.GetProperty("SignatureValid").GetBoolean());
        Assert.False(verifyResult.GetProperty("IsExpired").GetBoolean());
        Assert.Equal("LIC-123", verifyResult.GetProperty("LicenseId").GetString());
    }

    [Fact]
    public async Task RuleGenTools_Extract_And_Register_Generate_Artifacts()
    {
        using TestWorkspace workspace = new();
        workspace.WriteFile(
            "Sample.csproj",
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="{{workspace.GetRepoRelativePath(@"src\Muonroi.RuleEngine.Abstractions\Muonroi.RuleEngine.Abstractions.csproj")}}" />
              </ItemGroup>
            </Project>
            """);

        workspace.WriteFile(
            Path.Combine("Handlers", "OrderRuleSource.cs"),
            """
            using Muonroi.RuleEngine.Abstractions;

            namespace Sample.Handlers;

            public sealed class OrderRuleSource
            {
                [MExtractAsRule("ORDER_CHECK", Order = 0, HookPoint = HookPoint.BeforeRule)]
                public Task<RuleResult> EvaluateOrderAsync(OrderContext ctx, FactBag facts, CancellationToken ct = default)
                {
                    return Task.FromResult(RuleResult.Passed());
                }
            }

            public sealed class OrderContext
            {
                public decimal Amount { get; set; }
            }
            """);

        ExtractRulesTool extractTool = new(_jsonService);
        string extractPayload = await extractTool.ExecuteAsync(
            Path.Combine("Handlers"),
            Path.Combine("Generated", "Rules"),
            @namespace: "Generated.Rules",
            workingDirectory: workspace.RootPath,
            ct: CancellationToken.None);
        JsonElement extractResult = JsonSerializer.Deserialize<JsonElement>(extractPayload);

        RegisterRulesTool registerTool = new(_jsonService);
        string registerPayload = await registerTool.ExecuteAsync(
            Path.Combine("Generated", "Rules"),
            Path.Combine("Generated", "MGeneratedRuleRegistrationExtensions.g.cs"),
            @namespace: "Generated.Rules",
            workingDirectory: workspace.RootPath,
            ct: CancellationToken.None);
        JsonElement registerResult = JsonSerializer.Deserialize<JsonElement>(registerPayload);

        string generatedRule = Path.Combine(workspace.RootPath, "Generated", "Rules", "ORDER_CHECK.g.cs");
        string registrationFile = Path.Combine(workspace.RootPath, "Generated", "MGeneratedRuleRegistrationExtensions.g.cs");

        Assert.Equal(1, extractResult.GetProperty("ExtractedCount").GetInt32());
        Assert.True(File.Exists(generatedRule));
        Assert.True(File.Exists(registrationFile));
        Assert.Equal(1, registerResult.GetProperty("RuleCount").GetInt32());
        Assert.Contains("AddMGeneratedRules", await File.ReadAllTextAsync(registrationFile, CancellationToken.None), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DecisionTableTools_Import_Validate_And_Export_Work()
    {
        using TestWorkspace workspace = new();
        workspace.WriteMinimalExcel(
            "eligibility.xlsx",
            [
                ["in:Age", "out:Approved"],
                [">18", "true"],
                ["<=18", "false"]
            ]);

        ImportExcelTool importTool = new(_jsonService);
        string importPayload = await importTool.ExecuteAsync(
            "eligibility.xlsx",
            "eligibility.table.json",
            workingDirectory: workspace.RootPath,
            ct: CancellationToken.None);
        JsonElement importResult = JsonSerializer.Deserialize<JsonElement>(importPayload);

        ValidateDecisionTableTool validateTool = new(_jsonService);
        string validatePayload = await validateTool.ExecuteAsync(
            "eligibility.table.json",
            workspace.RootPath,
            CancellationToken.None);
        JsonElement validateResult = JsonSerializer.Deserialize<JsonElement>(validatePayload);

        ExportDecisionTableJsonTool exportJsonTool = new(_jsonService);
        _ = await exportJsonTool.ExecuteAsync(
            "eligibility.table.json",
            "workflow.json",
            workspace.RootPath,
            CancellationToken.None);

        ExportDecisionTableDmnTool exportDmnTool = new(_jsonService);
        _ = await exportDmnTool.ExecuteAsync(
            "eligibility.table.json",
            "workflow.dmn.xml",
            workspace.RootPath,
            CancellationToken.None);

        Assert.Equal("eligibility", importResult.GetProperty("TableName").GetString());
        Assert.True(validateResult.GetProperty("IsValid").GetBoolean());
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "workflow.json")));
        Assert.True(File.Exists(Path.Combine(workspace.RootPath, "workflow.dmn.xml")));
    }

    [Fact]
    public void SuggestWrapperTool_Returns_Muonroi_Replacements_For_Common_Violations()
    {
        SuggestEcosystemWrapperTool tool = new(_jsonService);

        JsonElement dateTimePayload = JsonSerializer.Deserialize<JsonElement>(tool.Execute("DateTime.UtcNow"));
        JsonElement jsonPayload = JsonSerializer.Deserialize<JsonElement>(
            tool.Execute("JsonSerializer.Serialize(payload)", violationType: "JsonSerializer"));

        Assert.Equal("MBB001", dateTimePayload.GetProperty("MbbRule").GetString());
        Assert.Equal("_dateTimeService.UtcNow()", dateTimePayload.GetProperty("Corrected").GetString());
        Assert.Equal("IMDateTimeService _dateTimeService", dateTimePayload.GetProperty("RequiredDependency").GetString());

        Assert.Equal("MBB002", jsonPayload.GetProperty("MbbRule").GetString());
        Assert.Equal("_jsonSerializeService.Serialize(value)", jsonPayload.GetProperty("Corrected").GetString());
        Assert.True(jsonPayload.GetProperty("ExemptionAvailable").GetBoolean());
    }

    [Fact]
    public void TranslateFeelTool_Translates_Both_Directions()
    {
        TranslateFeelTool tool = new(_jsonService);

        JsonElement feelToCSharp = JsonSerializer.Deserialize<JsonElement>(
            tool.Execute("order.total = 10 and status = 'ok'", "feel-to-csharp"));
        JsonElement cSharpToFeel = JsonSerializer.Deserialize<JsonElement>(
            tool.Execute("ctx.Order.Total == 10 && ctx.Status == \"ok\"", "csharp-to-feel"));

        Assert.Equal("ctx.Order.Total == 10 && status == \"ok\"", feelToCSharp.GetProperty("translated").GetString());
        Assert.Equal("order.total = 10 and status = \"ok\"", cSharpToFeel.GetProperty("translated").GetString());
    }

    [Fact]
    public async Task WatchRulesTool_Detects_Changes_And_Regenerates_Output()
    {
        using TestWorkspace workspace = new();
        WriteRuleGenProject(workspace);
        _ = workspace.WriteFile(
            Path.Combine("Handlers", "OrderRuleSource.cs"),
            BuildRuleSource("ORDER_CHECK", "OrderRuleSource", "EvaluateOrderAsync", includeContext: true));

        WatchRulesTool tool = new(_jsonService);

        Task writeTask = Task.Run(async () =>
        {
            await Task.Delay(400);
            _ = workspace.WriteFile(
                Path.Combine("Handlers", "ReviewRuleSource.cs"),
                BuildRuleSource("REVIEW_CHECK", "ReviewRuleSource", "EvaluateReviewAsync"));
        });

        string payload = await tool.ExecuteAsync(
            Path.Combine("Handlers"),
            Path.Combine("Generated", "Rules"),
            @namespace: "Generated.Rules",
            debounceMs: 150,
            durationSeconds: 2,
            workingDirectory: workspace.RootPath,
            ct: CancellationToken.None);
        await writeTask;

        JsonElement result = JsonSerializer.Deserialize<JsonElement>(payload);
        string reviewRuleFile = Path.Combine(workspace.RootPath, "Generated", "Rules", "REVIEW_CHECK.g.cs");

        Assert.True(result.GetProperty("Started").GetBoolean());
        Assert.True(result.GetProperty("InitialExtractSucceeded").GetBoolean());
        Assert.True(result.GetProperty("EventCount").GetInt32() > 0);
        Assert.True(File.Exists(reviewRuleFile));
        Assert.Contains(
            reviewRuleFile,
            result.GetProperty("Files").EnumerateArray().Select(x => x.GetString()).Where(x => x is not null).Cast<string>(),
            StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyRulesTool_Passes_After_Extract_Generates_Expected_Files()
    {
        using TestWorkspace workspace = new();
        WriteRuleGenProject(workspace);
        _ = workspace.WriteFile(
            Path.Combine("Handlers", "OrderRuleSource.cs"),
            BuildRuleSource("ORDER_CHECK", "OrderRuleSource", "EvaluateOrderAsync", includeContext: true));

        ExtractRulesTool extractTool = new(_jsonService);
        _ = await extractTool.ExecuteAsync(
            Path.Combine("Handlers"),
            Path.Combine("Generated", "Rules"),
            @namespace: "Generated.Rules",
            workingDirectory: workspace.RootPath,
            ct: CancellationToken.None);

        VerifyRulesTool verifyTool = new(_jsonService);
        string payload = await verifyTool.ExecuteAsync(
            Path.Combine("Handlers"),
            Path.Combine("Generated", "Rules"),
            @namespace: "Generated.Rules",
            workingDirectory: workspace.RootPath,
            ct: CancellationToken.None);
        JsonElement result = JsonSerializer.Deserialize<JsonElement>(payload);

        Assert.True(result.GetProperty("Passed").GetBoolean());
        Assert.Empty(result.GetProperty("MissingFiles").EnumerateArray());
        Assert.Empty(result.GetProperty("ExtraFiles").EnumerateArray());
    }

    [Fact]
    public async Task GenerateTestsTool_Creates_Rule_Test_Scaffolds()
    {
        using TestWorkspace workspace = new();
        WriteRuleGenProject(workspace);
        _ = workspace.WriteFile(
            Path.Combine("Handlers", "OrderRuleSource.cs"),
            BuildRuleSource("ORDER_CHECK", "OrderRuleSource", "EvaluateOrderAsync", includeContext: true));

        ExtractRulesTool extractTool = new(_jsonService);
        _ = await extractTool.ExecuteAsync(
            Path.Combine("Handlers"),
            Path.Combine("Generated", "Rules"),
            @namespace: "Generated.Rules",
            workingDirectory: workspace.RootPath,
            ct: CancellationToken.None);

        GenerateTestsTool generateTestsTool = new(_jsonService);
        string payload = await generateTestsTool.ExecuteAsync(
            Path.Combine("Generated", "Rules"),
            Path.Combine("Generated", "Tests"),
            @namespace: "Generated.Rules",
            workingDirectory: workspace.RootPath,
            ct: CancellationToken.None);
        JsonElement result = JsonSerializer.Deserialize<JsonElement>(payload);

        Assert.Equal(1, result.GetProperty("GeneratedCount").GetInt32());
        string testFile = result.GetProperty("Files").EnumerateArray().Select(x => x.GetString()).First(x => !string.IsNullOrWhiteSpace(x))!;
        Assert.True(File.Exists(testFile));
        Assert.Contains("Tests", Path.GetFileNameWithoutExtension(testFile), StringComparison.Ordinal);
    }

    [Fact]
    public void DeveloperResources_ShouldExpose_Rules_And_Patterns()
    {
        DeveloperResourceHandler resourceHandler = new(_jsonService);

        JsonElement rulesPayload = JsonSerializer.Deserialize<JsonElement>(resourceHandler.GetRules());
        JsonElement patternsPayload = JsonSerializer.Deserialize<JsonElement>(resourceHandler.GetPatterns());

        string[] codes = rulesPayload.EnumerateArray()
            .Select(item => item.GetProperty("Code").GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToArray();

        Assert.Contains("MBB001", codes);
        Assert.Contains("MBB007", codes);
        Assert.Contains("IMDateTimeService", patternsPayload.GetProperty("time").GetString(), StringComparison.Ordinal);
        Assert.Contains("IMJsonSerializeService", patternsPayload.GetProperty("serialization").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DeveloperResources_ShouldExpose_OssBoundary_And_ToolingReference()
    {
        DeveloperResourceHandler resourceHandler = new(_jsonService);

        JsonElement ossBoundaryPayload = JsonSerializer.Deserialize<JsonElement>(resourceHandler.GetOssBoundary());
        JsonElement toolingPayload = JsonSerializer.Deserialize<JsonElement>(resourceHandler.GetToolingReference());

        string[] ossPackages = ossBoundaryPayload.GetProperty("oss").EnumerateArray()
            .Select(item => item.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToArray();
        string[] ruleGenTools = toolingPayload.GetProperty("ruleGen").EnumerateArray()
            .Select(item => item.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToArray();

        Assert.Contains("Muonroi.RuleEngine.Abstractions", ossPackages);
        Assert.Contains("watch", ruleGenTools);
        Assert.Contains("feel translate", ruleGenTools);
    }

    [Fact]
    public void CreateRulePrompt_Should_Describe_Mandatory_Workflow()
    {
        CreateRuleFromRequirementsPrompt prompt = new();

        ChatMessage message = prompt.GetPrompt("amount > 100", "OrderContext", "Sample.Rules");

        Assert.Contains("Read muonroi://ecosystem/rules", message.Text, StringComparison.Ordinal);
        Assert.Contains("muonroi_rulegen_extract", message.Text, StringComparison.Ordinal);
        Assert.Contains("muonroi_rulegen_register", message.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void FixMbbViolationsPrompt_Should_Reference_Compliance_Tools()
    {
        FixMbbViolationsPrompt prompt = new();

        ChatMessage message = prompt.GetPrompt(["src/OrderService.cs"]);

        Assert.Contains("muonroi_compliance_check", message.Text, StringComparison.Ordinal);
        Assert.Contains("muonroi_compliance_suggest_wrapper", message.Text, StringComparison.Ordinal);
        Assert.Contains("src/OrderService.cs", message.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ScaffoldNewFeaturePrompt_Should_Reference_Scaffolding_Sequence()
    {
        ScaffoldNewFeaturePrompt prompt = new();

        ChatMessage message = prompt.GetPrompt("OrderReview", "OrderContext", "Sample.Features");

        Assert.Contains("muonroi_scaffold_service", message.Text, StringComparison.Ordinal);
        Assert.Contains("muonroi_scaffold_repository", message.Text, StringComparison.Ordinal);
        Assert.Contains("muonroi_compliance_check", message.Text, StringComparison.Ordinal);
    }

    private static void WriteRuleGenProject(TestWorkspace workspace)
    {
        workspace.WriteFile(
            "Sample.csproj",
            $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="{{workspace.GetRepoRelativePath(@"src\Muonroi.RuleEngine.Abstractions\Muonroi.RuleEngine.Abstractions.csproj")}}" />
              </ItemGroup>
            </Project>
            """);
    }

    private static string BuildRuleSource(string ruleCode, string className, string methodName, bool includeContext = false)
    {
        string contextBlock = includeContext
            ? """

        public sealed class OrderContext
        {
            public decimal Amount { get; set; }
        }
        """
            : string.Empty;

        return $$"""
        using Muonroi.RuleEngine.Abstractions;

        namespace Sample.Handlers;

        public sealed class {{className}}
        {
            [MExtractAsRule("{{ruleCode}}", Order = 0, HookPoint = HookPoint.BeforeRule)]
            public Task<RuleResult> {{methodName}}(OrderContext ctx, FactBag facts, CancellationToken ct = default)
            {
                return Task.FromResult(RuleResult.Passed());
            }
        }
        {{contextBlock}}
        """;
    }
}
