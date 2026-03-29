using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators.Tests;

/// <summary>
/// Tests for MSP020 DuplicateProtoMessageAnalyzer.
///
/// Strategy: each test constructs a two-project Roslyn solution.
/// "SharedAssembly" contains a public class in a ".Grpc" namespace (simulating a proto message).
/// The main project references "SharedAssembly" so the analyzer can detect the collision.
/// </summary>
public sealed class DuplicateProtoMessageAnalyzerTests
{
    // ---------------------------------------------------------------------------
    // Test 1 — POSITIVE: class name collides with a shared proto message name
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ClassNameDuplicatesSharedProtoMessage_Reports_MSP020()
    {
        // The "shared assembly" contains a proto-generated message type.
        const string sharedSource = """
            namespace Shared.Grpc
            {
                public class OrderMessage { }
            }
            """;

        // The site assembly has a class with the same simple name NOT in a ".Grpc" namespace.
        const string siteSource = """
            namespace SiteAssembly
            {
                public class {|#0:OrderMessage|} { }
            }
            """;

        var expected = CSharpAnalyzerVerifier<DuplicateProtoMessageAnalyzer>
            .Diagnostic(DuplicateProtoMessageAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("OrderMessage", "SharedAssembly");

        var test = new CSharpAnalyzerTest<DuplicateProtoMessageAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { siteSource },
                AdditionalProjects =
                {
                    ["SharedAssembly"] =
                    {
                        Sources = { ("SharedProtos.cs", sharedSource) },
                    }
                },
                AdditionalProjectReferences = { "SharedAssembly" },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    // ---------------------------------------------------------------------------
    // Test 2 — NEGATIVE: class name does NOT collide with any shared proto name
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ClassNameDoesNotMatchAnyProtoMessage_NoDiagnostic()
    {
        const string sharedSource = """
            namespace Shared.Grpc
            {
                public class OrderMessage { }
            }
            """;

        // This class has a completely different name — no collision.
        const string siteSource = """
            namespace SiteAssembly
            {
                public class MyUniqueClass { }
            }
            """;

        var test = new CSharpAnalyzerTest<DuplicateProtoMessageAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { siteSource },
                AdditionalProjects =
                {
                    ["SharedAssembly"] =
                    {
                        Sources = { ("SharedProtos.cs", sharedSource) },
                    }
                },
                AdditionalProjectReferences = { "SharedAssembly" },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        // No expected diagnostics — expects zero MSP020
        await test.RunAsync();
    }

    // ---------------------------------------------------------------------------
    // Test 3 — NEGATIVE: class IS in a ".Grpc" namespace — skip rule applies
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ClassInGrpcNamespace_NoDiagnostic()
    {
        const string sharedSource = """
            namespace Shared.Grpc
            {
                public class OrderMessage { }
            }
            """;

        // This class has the same simple name as a shared proto type,
        // but it is itself in a ".Grpc" namespace — the analyzer should skip it.
        const string siteSource = """
            namespace SiteAssembly.Grpc
            {
                public class OrderMessage { }
            }
            """;

        var test = new CSharpAnalyzerTest<DuplicateProtoMessageAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { siteSource },
                AdditionalProjects =
                {
                    ["SharedAssembly"] =
                    {
                        Sources = { ("SharedProtos.cs", sharedSource) },
                    }
                },
                AdditionalProjectReferences = { "SharedAssembly" },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        await test.RunAsync();
    }

    // ---------------------------------------------------------------------------
    // Test 4 — NEGATIVE: abstract class — skip rule applies
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task AbstractClassWithSameName_NoDiagnostic()
    {
        const string sharedSource = """
            namespace Shared.Grpc
            {
                public class OrderMessage { }
            }
            """;

        // Abstract class with the same name — the analyzer skips abstract types.
        const string siteSource = """
            namespace SiteAssembly
            {
                public abstract class OrderMessage { }
            }
            """;

        var test = new CSharpAnalyzerTest<DuplicateProtoMessageAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources = { siteSource },
                AdditionalProjects =
                {
                    ["SharedAssembly"] =
                    {
                        Sources = { ("SharedProtos.cs", sharedSource) },
                    }
                },
                AdditionalProjectReferences = { "SharedAssembly" },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        await test.RunAsync();
    }
}
