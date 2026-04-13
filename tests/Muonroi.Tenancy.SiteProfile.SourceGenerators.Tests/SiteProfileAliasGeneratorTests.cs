using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using System.Collections.Immutable;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators.Tests;

/// <summary>
/// Tests for SiteProfileAliasGenerator — verifies that [SiteProfileAlias] source generator:
/// 1. Emits alias registration code when [SiteProfileAlias] + [GenerateSiteProfile] are present
/// 2. Emits MSP030 warning when [SiteProfileAlias] + [SiteProfileBehavior] are combined
/// 3. Emits MSP031 error when [SiteProfileAlias] is used without [GenerateSiteProfile]
/// 4. Does nothing when neither [SiteProfileAlias] nor [GenerateSiteProfile] is present
/// </summary>
public sealed class SiteProfileAliasGeneratorTests
{
    // -----------------------------------------------------------------------
    // Shared attribute stubs (included in every test compilation)
    // -----------------------------------------------------------------------

    private const string AliasAttributeSource = """
        using System;
        namespace Muonroi.Tenancy.SiteProfile
        {
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
            public sealed class SiteProfileAliasAttribute : Attribute
            {
                public string TargetSiteId { get; }
                public SiteProfileAliasAttribute(string targetSiteId) { TargetSiteId = targetSiteId; }
            }
        }
        """;

    private const string GenerateAttributeSource = """
        using System;
        namespace Muonroi.Tenancy.SiteProfile
        {
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
            public sealed class GenerateSiteProfileAttribute : Attribute
            {
                public string SiteId { get; }
                public Type DbContextType { get; }
                public bool SkipDbContextRegistration { get; set; }
                public GenerateSiteProfileAttribute(string siteId, Type dbContextType) { SiteId = siteId; DbContextType = dbContextType; }
            }
        }
        """;

    private const string BehaviorAttributeSource = """
        using System;
        namespace Muonroi.Tenancy.SiteProfile
        {
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
            public sealed class SiteProfileBehaviorAttribute : Attribute
            {
                public Type BehaviorType { get; }
                public SiteProfileBehaviorAttribute(Type behaviorType) { BehaviorType = behaviorType; }
            }
        }
        """;

    private const string DbContextStubSource = """
        namespace TestApp { public class DefaultOrderContext { } }
        """;

    // -----------------------------------------------------------------------
    // Helper: run generator and return generated source outputs + diagnostics
    // -----------------------------------------------------------------------

    private static (ImmutableArray<Diagnostic> diagnostics, IReadOnlyList<string> generatedSources) RunGenerator(string testSource, params string[] additionalSources)
    {
        var sources = new List<string> { AliasAttributeSource, GenerateAttributeSource, BehaviorAttributeSource, DbContextStubSource, testSource };
        sources.AddRange(additionalSources);

        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new SiteProfileAliasGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var runResult = driver.GetRunResult();
        var generatedSources = runResult.GeneratedTrees
            .Select(t => t.ToString())
            .ToList();

        return (diagnostics, generatedSources);
    }

    // -----------------------------------------------------------------------
    // Test 1 — POSITIVE: [SiteProfileAlias] + [GenerateSiteProfile] => alias registration code
    // -----------------------------------------------------------------------

    [Fact]
    public void AliasAttribute_WithGenerateSiteProfile_EmitsAliasRegistrationCode()
    {
        const string testSource = """
            using Muonroi.Tenancy.SiteProfile;
            namespace TestApp
            {
                [SiteProfileAlias("DEFAULT")]
                [GenerateSiteProfile("CTL", typeof(DefaultOrderContext))]
                public partial class CtlSiteProfile
                {
                    public string SiteId => "CTL";
                }
            }
            """;

        var (diagnostics, generatedSources) = RunGenerator(testSource);

        // Verify no error diagnostics (MSP031 should not fire since [GenerateSiteProfile] is present)
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.Empty(errors);

        // Verify generated source contains alias registration
        Assert.True(generatedSources.Count > 0, "Expected at least one generated source file");

        var aliasSource = generatedSources.FirstOrDefault(s => s.Contains("RegisterAliasServices"));
        Assert.NotNull(aliasSource);

        // Should contain AddKeyedScoped or GetRequiredKeyedService forwarding
        Assert.True(
            aliasSource!.Contains("AddKeyedScoped") || aliasSource.Contains("GetRequiredKeyedService"),
            $"Expected alias forwarding pattern in generated code. Got:\n{aliasSource}");
    }

    // -----------------------------------------------------------------------
    // Test 2 — POSITIVE: [SiteProfileAlias] + [SiteProfileBehavior] => MSP030 warning
    // -----------------------------------------------------------------------

    [Fact]
    public void AliasAttribute_WithBehaviorAttribute_EmitsMSP030Warning()
    {
        const string testSource = """
            using Muonroi.Tenancy.SiteProfile;
            namespace TestApp
            {
                public class DummyBehavior { }

                [SiteProfileAlias("DEFAULT")]
                [GenerateSiteProfile("CTL", typeof(DefaultOrderContext))]
                [SiteProfileBehavior(typeof(DummyBehavior))]
                public partial class CtlSiteProfile
                {
                    public string SiteId => "CTL";
                }
            }
            """;

        var (diagnostics, _) = RunGenerator(testSource);

        var msp030 = diagnostics.Where(d => d.Id == "MSP030").ToList();
        Assert.True(msp030.Count > 0, $"Expected MSP030 diagnostic. Got diagnostics: {string.Join(", ", diagnostics.Select(d => d.Id + ": " + d.GetMessage()))}");
        Assert.Equal(DiagnosticSeverity.Warning, msp030[0].Severity);
    }

    // -----------------------------------------------------------------------
    // Test 3 — POSITIVE: [SiteProfileAlias] without [GenerateSiteProfile] => MSP031 error
    // -----------------------------------------------------------------------

    [Fact]
    public void AliasAttribute_WithoutGenerateSiteProfile_EmitsMSP031Error()
    {
        const string testSource = """
            using Muonroi.Tenancy.SiteProfile;
            namespace TestApp
            {
                [SiteProfileAlias("DEFAULT")]
                public partial class CtlSiteProfile
                {
                    public string SiteId => "CTL";
                }
            }
            """;

        var (diagnostics, _) = RunGenerator(testSource);

        var msp031 = diagnostics.Where(d => d.Id == "MSP031").ToList();
        Assert.True(msp031.Count > 0, $"Expected MSP031 diagnostic. Got diagnostics: {string.Join(", ", diagnostics.Select(d => d.Id + ": " + d.GetMessage()))}");
        Assert.Equal(DiagnosticSeverity.Error, msp031[0].Severity);
    }

    // -----------------------------------------------------------------------
    // Test 4 — NEGATIVE: no [SiteProfileAlias] => no alias output generated
    // -----------------------------------------------------------------------

    [Fact]
    public void NoAliasAttribute_NoAliasOutput()
    {
        const string testSource = """
            using Muonroi.Tenancy.SiteProfile;
            namespace TestApp
            {
                [GenerateSiteProfile("DEFAULT", typeof(DefaultOrderContext))]
                public partial class DefaultSiteProfile
                {
                    public string SiteId => "DEFAULT";
                }
            }
            """;

        var (diagnostics, generatedSources) = RunGenerator(testSource);

        // No MSP030 or MSP031
        var msp030 = diagnostics.Where(d => d.Id == "MSP030").ToList();
        var msp031 = diagnostics.Where(d => d.Id == "MSP031").ToList();
        Assert.Empty(msp030);
        Assert.Empty(msp031);

        // No alias source generated
        var aliasSource = generatedSources.FirstOrDefault(s => s.Contains("RegisterAliasServices"));
        Assert.Null(aliasSource);
    }

    // -----------------------------------------------------------------------
    // Test 5 — POSITIVE: generated hint name is predictable
    // -----------------------------------------------------------------------

    [Fact]
    public void AliasAttribute_GeneratesHintNamedAfterClass()
    {
        const string testSource = """
            using Muonroi.Tenancy.SiteProfile;
            namespace TestApp
            {
                [SiteProfileAlias("DEFAULT")]
                [GenerateSiteProfile("CTL", typeof(DefaultOrderContext))]
                public partial class CtlSiteProfile
                {
                    public string SiteId => "CTL";
                }
            }
            """;

        var sources = new[] { AliasAttributeSource, GenerateAttributeSource, BehaviorAttributeSource, DbContextStubSource, testSource };
        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };
        var compilation = CSharpCompilation.Create(
            "TestAssembly", syntaxTrees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new SiteProfileAliasGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

        var runResult = driver.GetRunResult();
        var generatedTrees = runResult.GeneratedTrees;

        var aliasTree = generatedTrees.FirstOrDefault(t => t.FilePath.Contains("RegisterAlias"));
        Assert.NotNull(aliasTree);
        // Hint name should include the class name
        Assert.Contains("CtlSiteProfile", aliasTree!.FilePath);
    }
}
