using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators.Tests;

/// <summary>
/// Tests for MSP041 AssemblyIsolationHintAnalyzer.
///
/// Strategy: each test constructs a two-project Roslyn solution.
/// "SiteAssembly" contains an ISiteProfile implementation in a referenced assembly.
/// The main project references "SiteAssembly" — MSP041 fires if any concrete, non-abstract
/// ISiteProfile is found in a referenced (external) assembly.
///
/// MSP041 reports at Location.None (compilation level) because the type is
/// defined in a referenced assembly, not in the current compilation's syntax trees.
///
/// Positive tests (MSP041 fired): concrete ISiteProfile in referenced assembly.
/// Negative tests (no diagnostic): ISiteProfile in same assembly, abstract class, or no ISiteProfile.
/// </summary>
public sealed class AssemblyIsolationHintAnalyzerTests
{
    // ---------------------------------------------------------------------------
    // Shared infrastructure — minimal ISiteProfile stub (no external deps)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Minimal ISiteProfile interface replica so the analyzer resolves
    /// "Muonroi.Tenancy.SiteProfile.ISiteProfile" in the test compilation.
    /// Uses simple string/void types to avoid Microsoft.Extensions assembly references.
    /// </summary>
    private const string ISiteProfileSource = """
        namespace Muonroi.Tenancy.SiteProfile
        {
            public interface ISiteProfile
            {
                string SiteId { get; }
                void RegisterServices();
            }
        }
        """;

    // ---------------------------------------------------------------------------
    // Test 1 — POSITIVE: ISiteProfile in referenced assembly → MSP041 fires
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ISiteProfile_InReferencedAssembly_Reports_MSP041()
    {
        // The "site assembly" contains a concrete ISiteProfile implementation.
        const string siteAssemblySource = """
            using Muonroi.Tenancy.SiteProfile;
            namespace MyApp.Sites.Alpha
            {
                public class AlphaProfile : ISiteProfile
                {
                    public string SiteId => "alpha";
                    public void RegisterServices() { }
                }
            }
            """;

        // Main project references SiteAssembly but has no ISiteProfile implementations.
        const string mainSource = """
            namespace MyApp
            {
                public class Startup { }
            }
            """;

        // MSP041 is reported at Location.None (compilation level) because the ISiteProfile
        // implementation is in a referenced assembly, not in the main compilation's syntax.
        var expected = CSharpAnalyzerVerifier<AssemblyIsolationHintAnalyzer>
            .Diagnostic(AssemblyIsolationHintAnalyzer.DiagnosticId)
            .WithNoLocation()
            .WithArguments("AlphaProfile", "SiteAssembly");

        var test = new CSharpAnalyzerTest<AssemblyIsolationHintAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { mainSource, ISiteProfileSource },
                AdditionalProjects =
                {
                    ["SiteAssembly"] =
                    {
                        Sources = { ("AlphaProfile.cs", siteAssemblySource), ("ISiteProfileRef.cs", ISiteProfileSource) },
                    }
                },
                AdditionalProjectReferences = { "SiteAssembly" },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    // ---------------------------------------------------------------------------
    // Test 2 — NEGATIVE: ISiteProfile in same assembly → no diagnostic
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ISiteProfile_InSameAssembly_NoDiagnostic()
    {
        // ISiteProfile implementation is in the SAME compilation — no hint needed.
        const string sameAssemblySource = """
            using Muonroi.Tenancy.SiteProfile;
            namespace MyApp
            {
                public class DefaultProfile : ISiteProfile
                {
                    public string SiteId => "default";
                    public void RegisterServices() { }
                }
            }
            """;

        var test = new CSharpAnalyzerTest<AssemblyIsolationHintAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { sameAssemblySource, ISiteProfileSource },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        // No expected diagnostics — implementation in same assembly
        await test.RunAsync();
    }

    // ---------------------------------------------------------------------------
    // Test 3 — NEGATIVE: Abstract ISiteProfile in referenced assembly → no diagnostic
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AbstractISiteProfile_InReferencedAssembly_NoDiagnostic()
    {
        // Abstract class cannot be instantiated — does not need SiteAssemblies listing.
        const string siteAssemblySource = """
            using Muonroi.Tenancy.SiteProfile;
            namespace MyApp.Sites.Base
            {
                public abstract class BaseProfile : ISiteProfile
                {
                    public abstract string SiteId { get; }
                    public abstract void RegisterServices();
                }
            }
            """;

        const string mainSource = """
            namespace MyApp
            {
                public class Startup { }
            }
            """;

        var test = new CSharpAnalyzerTest<AssemblyIsolationHintAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { mainSource, ISiteProfileSource },
                AdditionalProjects =
                {
                    ["SiteAssembly"] =
                    {
                        Sources = { ("BaseProfile.cs", siteAssemblySource), ("ISiteProfileRef.cs", ISiteProfileSource) },
                    }
                },
                AdditionalProjectReferences = { "SiteAssembly" },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        // No expected diagnostics — abstract class is skipped
        await test.RunAsync();
    }

    // ---------------------------------------------------------------------------
    // Test 4 — EDGE: No ISiteProfile implementations in referenced assembly → no diagnostic
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task NoISiteProfileInReferencedAssembly_NoDiagnostic()
    {
        // Referenced assembly has classes but none implementing ISiteProfile.
        const string sharedSource = """
            namespace MyApp.Shared
            {
                public class SharedHelper { }
                public class AnotherClass { }
            }
            """;

        const string mainSource = """
            namespace MyApp
            {
                public class Startup { }
            }
            """;

        var test = new CSharpAnalyzerTest<AssemblyIsolationHintAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { mainSource, ISiteProfileSource },
                AdditionalProjects =
                {
                    ["SiteAssembly"] =
                    {
                        Sources = { ("SharedHelper.cs", sharedSource) },
                    }
                },
                AdditionalProjectReferences = { "SiteAssembly" },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        // No expected diagnostics
        await test.RunAsync();
    }
}
