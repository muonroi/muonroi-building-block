using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators.Tests;

/// <summary>
/// Tests for MSP021 MissingSiteGrpcServiceAttributeAnalyzer.
///
/// Strategy: include the SiteGrpcServiceAttribute source in each test compilation so the analyzer
/// can resolve it by metadata name "Muonroi.Tenancy.SiteProfile.Grpc.SiteGrpcServiceAttribute".
/// The gRPC base class stubs are also included inline — their abstract / Base-suffixed names ensure
/// the skip rules apply to the stubs themselves while still triggering for the concrete subclass.
/// </summary>
public sealed class MissingSiteGrpcServiceAttributeAnalyzerTests
{
    // Minimal replica of SiteGrpcServiceAttribute — required so the analyzer can resolve
    // the type at compilation start (compilation.GetTypeByMetadataName(...)).
    private const string AttributeSource = """
        namespace Muonroi.Tenancy.SiteProfile.Grpc
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false)]
            public sealed class SiteGrpcServiceAttribute : System.Attribute
            {
                public string SiteId { get; }
                public string? Reason { get; set; }
                public SiteGrpcServiceAttribute(string siteId) { SiteId = siteId; }
            }
        }
        """;

    // Simulates gRPC nested-class codegen: outer class "MyService" with nested abstract
    // "MyServiceBase".  The nested class satisfies both skip-rules for the stub itself:
    //   - abstract => skipped
    //   - name ends with "Base" => skipped
    private const string NestedGrpcStubSource = """
        namespace Shared.Grpc
        {
            public static class MyService
            {
                public abstract class MyServiceBase { }
            }
        }
        """;

    // Simulates gRPC namespace-pattern codegen: abstract base in a ".Grpc" namespace.
    // Skip rules for the stub: abstract + name ends with "Base".
    private const string NamespaceGrpcStubSource = """
        namespace Shared.Grpc
        {
            public abstract class SomeServiceBase { }
        }
        """;

    // -------------------------------------------------------------------------
    // Test 1 — POSITIVE: nested gRPC pattern, missing [SiteGrpcService] => MSP021
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NestedGrpcBase_MissingAttribute_Reports_MSP021()
    {
        const string testSource = """
            namespace SiteAssembly
            {
                public class {|#0:BravoGrpcService|} : Shared.Grpc.MyService.MyServiceBase { }
            }
            """;

        var expected = CSharpAnalyzerVerifier<MissingSiteGrpcServiceAttributeAnalyzer>
            .Diagnostic(MissingSiteGrpcServiceAttributeAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("BravoGrpcService", "MyServiceBase");

        var test = new CSharpAnalyzerTest<MissingSiteGrpcServiceAttributeAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("AttributeSource.cs", AttributeSource),
                    ("NestedGrpcStub.cs", NestedGrpcStubSource),
                    ("BravoGrpcService.cs", testSource),
                },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // Test 2 — POSITIVE: namespace gRPC pattern, missing [SiteGrpcService] => MSP021
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NamespaceGrpcBase_MissingAttribute_Reports_MSP021()
    {
        const string testSource = """
            namespace SiteAssembly
            {
                public class {|#0:AlphaGrpcService|} : Shared.Grpc.SomeServiceBase { }
            }
            """;

        var expected = CSharpAnalyzerVerifier<MissingSiteGrpcServiceAttributeAnalyzer>
            .Diagnostic(MissingSiteGrpcServiceAttributeAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("AlphaGrpcService", "SomeServiceBase");

        var test = new CSharpAnalyzerTest<MissingSiteGrpcServiceAttributeAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("AttributeSource.cs", AttributeSource),
                    ("NamespaceGrpcStub.cs", NamespaceGrpcStubSource),
                    ("AlphaGrpcService.cs", testSource),
                },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        test.ExpectedDiagnostics.Add(expected);
        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // Test 3 — NEGATIVE: [SiteGrpcService] attribute present => 0 diagnostics
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GrpcDerivedClass_WithAttribute_NoDiagnostic()
    {
        const string testSource = """
            using Muonroi.Tenancy.SiteProfile.Grpc;

            namespace SiteAssembly
            {
                [SiteGrpcService("BRAVO")]
                public class BravoGrpcService : Shared.Grpc.MyService.MyServiceBase { }
            }
            """;

        var test = new CSharpAnalyzerTest<MissingSiteGrpcServiceAttributeAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("AttributeSource.cs", AttributeSource),
                    ("NestedGrpcStub.cs", NestedGrpcStubSource),
                    ("BravoGrpcService.cs", testSource),
                },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        // Expect 0 diagnostics
        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // Test 4 — NEGATIVE: abstract class deriving from gRPC base => 0 diagnostics (skip rule)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AbstractGrpcDerivedClass_MissingAttribute_NoDiagnostic()
    {
        const string testSource = """
            namespace SiteAssembly
            {
                public abstract class AbstractGrpcService : Shared.Grpc.MyService.MyServiceBase { }
            }
            """;

        var test = new CSharpAnalyzerTest<MissingSiteGrpcServiceAttributeAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("AttributeSource.cs", AttributeSource),
                    ("NestedGrpcStub.cs", NestedGrpcStubSource),
                    ("AbstractGrpcService.cs", testSource),
                },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        // Abstract class — skip rule applies, 0 diagnostics
        await test.RunAsync();
    }

    // -------------------------------------------------------------------------
    // Test 5 — NEGATIVE: non-gRPC base class (doesn't satisfy heuristic) => 0 diagnostics
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NonGrpcBase_NoDiagnostic()
    {
        // This base class does NOT end with "Base" and is NOT in a ".Grpc" namespace.
        const string normalBaseSource = """
            namespace Common
            {
                public class ServiceBase2 { }
            }
            """;

        const string testSource = """
            namespace SiteAssembly
            {
                public class MyRegularService : Common.ServiceBase2 { }
            }
            """;

        var test = new CSharpAnalyzerTest<MissingSiteGrpcServiceAttributeAnalyzer, DefaultVerifier>
        {
            TestState =
            {
                Sources =
                {
                    ("AttributeSource.cs", AttributeSource),
                    ("NormalBase.cs", normalBaseSource),
                    ("MyRegularService.cs", testSource),
                },
            },
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
        };
        // Non-gRPC base — no diagnostic expected
        await test.RunAsync();
    }
}
