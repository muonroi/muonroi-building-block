namespace Muonroi.RuleEngine.SourceGen.Tests;

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Analyzers;
using Xunit;

/// <summary>
/// Unit tests for the MBB008 CrossCapabilityAnalyzer.
/// Verifies that cross-capability type references inside AddM* methods
/// without registry.Has() guard produce MBB008 warnings.
/// </summary>
public class MBB008_CrossCapabilityAnalyzerTests
{
    // ----------------------------------------------------------------
    // Test 1: POSITIVE — unguarded cross-capability reference triggers MBB008
    // ----------------------------------------------------------------
    [Fact]
    public void MBB008_AddMAuth_ReferencesMultiTenantType_WithoutGuard_ShouldWarn()
    {
        // AddMAuth method (Auth capability) referencing ITenantContext (MultiTenant capability) without guard
        string source = @"
using Muonroi.Tenancy;

namespace Muonroi.Tenancy
{
    public interface ITenantContext { string TenantId { get; } }
}

namespace Muonroi.Auth.Extensions
{
    public static class AuthExtensions
    {
        public static void AddMAuth(object registry)
        {
            ITenantContext ctx = null;
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == "MBB008");
    }

    // ----------------------------------------------------------------
    // Test 2: NEGATIVE — guarded cross-capability reference does NOT trigger MBB008
    // ----------------------------------------------------------------
    [Fact]
    public void MBB008_AddMAuth_ReferencesMultiTenantType_WithGuard_ShouldNotWarn()
    {
        // Same code but wrapped in if (registry.Has(MCapability.MultiTenant))
        string source = @"
using Muonroi.Tenancy;
using Muonroi.Auth.Extensions;

namespace Muonroi.Tenancy
{
    public interface ITenantContext { string TenantId { get; } }
}

namespace Muonroi.Auth.Extensions
{
    public enum MCapability { None = 0, Logging = 1, RuleEngine = 2, MultiTenant = 4, Auth = 8 }

    public interface IMEcosystemRegistry
    {
        bool Has(MCapability capability);
    }

    public static class AuthExtensions
    {
        public static void AddMAuth(IMEcosystemRegistry registry)
        {
            if (registry.Has(MCapability.MultiTenant))
            {
                ITenantContext ctx = null;
            }
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB008");
    }

    // ----------------------------------------------------------------
    // Test 3: NEGATIVE — same-capability reference does NOT trigger MBB008
    // ----------------------------------------------------------------
    [Fact]
    public void MBB008_AddMAuth_ReferencesSameCapabilityType_ShouldNotWarn()
    {
        // AddMAuth method referencing an IAuthEnforcer (Muonroi.Auth = Auth capability) — same capability
        string source = @"
using Muonroi.Auth.Security;

namespace Muonroi.Auth.Security
{
    public interface IAuthEnforcer { void EnsureValid(string feature); }
}

namespace Muonroi.Auth.Extensions
{
    public static class AuthExtensions
    {
        public static void AddMAuth(object registry)
        {
            IAuthEnforcer guard = null;
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB008");
    }

    // ----------------------------------------------------------------
    // Test 4: NEGATIVE — reference outside AddM* method does NOT trigger MBB008
    // ----------------------------------------------------------------
    [Fact]
    public void MBB008_NonAddMMethod_ReferencesCrossCapabilityType_ShouldNotWarn()
    {
        // A regular method (not AddM*) referencing cross-capability type
        string source = @"
using Muonroi.Tenancy;

namespace Muonroi.Tenancy
{
    public interface ITenantContext { string TenantId { get; } }
}

namespace Muonroi.Auth.Extensions
{
    public static class AuthExtensions
    {
        public static void ConfigureAuth(object registry)
        {
            ITenantContext ctx = null;
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB008");
    }

    // ----------------------------------------------------------------
    // Test 5: POSITIVE — AddMLogging referencing RuleEngine type without guard triggers MBB008
    // ----------------------------------------------------------------
    [Fact]
    public void MBB008_AddMLogging_ReferencesRuleEngineType_WithoutGuard_ShouldWarn()
    {
        // AddMLogging method (Logging capability) referencing IRuleOrchestrator (RuleEngine capability)
        string source = @"
using Muonroi.RuleEngine;

namespace Muonroi.RuleEngine
{
    public interface IRuleOrchestrator { void Execute(); }
}

namespace Muonroi.Logging.Extensions
{
    public static class LoggingExtensions
    {
        public static void AddMLogging(object registry)
        {
            IRuleOrchestrator orchestrator = null;
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == "MBB008");
    }

    // ----------------------------------------------------------------
    // Test 6: POSITIVE — Governance is its own capability anchor (not folded into Auth).
    // AddMAuth referencing a Muonroi.Governance type without guard triggers MBB008.
    // ----------------------------------------------------------------
    [Fact]
    public void MBB008_AddMAuth_ReferencesGovernanceType_WithoutGuard_ShouldWarn()
    {
        string source = @"
using Muonroi.Governance;

namespace Muonroi.Governance
{
    public interface ILicenseGuard { void EnsureValid(string feature); }
}

namespace Muonroi.Auth.Extensions
{
    public static class AuthExtensions
    {
        public static void AddMAuth(object registry)
        {
            ILicenseGuard guard = null;
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == "MBB008");
    }

    // ----------------------------------------------------------------
    // Test 7: NEGATIVE — logical-and short-circuit guard does NOT trigger MBB008.
    // ----------------------------------------------------------------
    [Fact]
    public void MBB008_AddMAuth_GuardedByLogicalAnd_ShouldNotWarn()
    {
        string source = @"
using Muonroi.Tenancy;
using Muonroi.Auth.Extensions;

namespace Muonroi.Tenancy
{
    public interface ITenantContext { string TenantId { get; } }
}

namespace Muonroi.Auth.Extensions
{
    public enum MCapability { None = 0, Logging = 1, RuleEngine = 2, MultiTenant = 4, Auth = 8 }

    public interface IMEcosystemRegistry { bool Has(MCapability capability); }

    public static class AuthExtensions
    {
        public static bool AddMAuth(IMEcosystemRegistry registry)
        {
            return registry.Has(MCapability.MultiTenant) && typeof(ITenantContext) != null;
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB008");
    }

    // ----------------------------------------------------------------
    // Test 8: NEGATIVE — negated early-exit guard does NOT trigger MBB008.
    // ----------------------------------------------------------------
    [Fact]
    public void MBB008_AddMAuth_GuardedByNegatedEarlyReturn_ShouldNotWarn()
    {
        string source = @"
using Muonroi.Tenancy;
using Muonroi.Auth.Extensions;

namespace Muonroi.Tenancy
{
    public interface ITenantContext { string TenantId { get; } }
}

namespace Muonroi.Auth.Extensions
{
    public enum MCapability { None = 0, Logging = 1, RuleEngine = 2, MultiTenant = 4, Auth = 8 }

    public interface IMEcosystemRegistry { bool Has(MCapability capability); }

    public static class AuthExtensions
    {
        public static void AddMAuth(IMEcosystemRegistry registry)
        {
            if (!registry.Has(MCapability.MultiTenant))
            {
                return;
            }

            ITenantContext ctx = null;
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "MBB008");
    }

    // ----------------------------------------------------------------
    // Test 9: POSITIVE — a guard for the WRONG capability does NOT suppress the warning.
    // ----------------------------------------------------------------
    [Fact]
    public void MBB008_AddMAuth_GuardedByWrongCapability_ShouldWarn()
    {
        string source = @"
using Muonroi.Tenancy;
using Muonroi.Auth.Extensions;

namespace Muonroi.Tenancy
{
    public interface ITenantContext { string TenantId { get; } }
}

namespace Muonroi.Auth.Extensions
{
    public enum MCapability { None = 0, Logging = 1, RuleEngine = 2, MultiTenant = 4, Auth = 8 }

    public interface IMEcosystemRegistry { bool Has(MCapability capability); }

    public static class AuthExtensions
    {
        public static void AddMAuth(IMEcosystemRegistry registry)
        {
            if (registry.Has(MCapability.RuleEngine))
            {
                ITenantContext ctx = null;
            }
        }
    }
}
";
        ImmutableArray<Diagnostic> diagnostics = GetDiagnostics(source);

        Assert.Contains(diagnostics, d => d.Id == "MBB008");
    }

    // ----------------------------------------------------------------
    // Helper: run analyzer and collect diagnostics
    // ----------------------------------------------------------------
    private static ImmutableArray<Diagnostic> GetDiagnostics(string source)
    {
        CSharpCompilation compilation = CreateCompilation(source);
        ImmutableArray<DiagnosticAnalyzer> analyzers =
            ImmutableArray.Create<DiagnosticAnalyzer>(new Mbb008_CrossCapabilityAnalyzer());

        CompilationWithAnalyzers compilationWithAnalyzers =
            compilation.WithAnalyzers(analyzers);

        return compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                    "System.Runtime.dll")),
        ];

        return CSharpCompilation.Create(
            "TestCompilation",
            new[] { CSharpSyntaxTree.ParseText(source) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
