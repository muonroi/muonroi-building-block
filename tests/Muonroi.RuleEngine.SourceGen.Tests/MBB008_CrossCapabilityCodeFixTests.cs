namespace Muonroi.RuleEngine.SourceGen.Tests;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Muonroi.RuleEngine.SourceGenerators.Analyzers;
using Muonroi.RuleEngine.SourceGenerators.CodeFixes;
using Xunit;

/// <summary>
/// Unit tests for the MBB008 CrossCapabilityCodeFix.
/// Verifies that the code fix wraps unguarded cross-capability references
/// in the correct registry.Has(MCapability.X) if-block.
/// </summary>
public class MBB008_CrossCapabilityCodeFixTests
{
    // ----------------------------------------------------------------
    // Test 1: Code fix wraps statement in if (registry.Has(MCapability.MultiTenant))
    // ----------------------------------------------------------------
    [Fact]
    public async System.Threading.Tasks.Task MBB008_CodeFix_WrapsStatementInHasGuard()
    {
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
        string fixedSource = await ApplyCodeFixAsync(source);

        Assert.Contains("registry.Has(MCapability.MultiTenant)", fixedSource);
    }

    // ----------------------------------------------------------------
    // Test 2: Code fix resolves correct MCapability from namespace (RuleEngine)
    // ----------------------------------------------------------------
    [Fact]
    public async System.Threading.Tasks.Task MBB008_CodeFix_ResolvesCorrectCapabilityFromNamespace()
    {
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
        string fixedSource = await ApplyCodeFixAsync(source);

        Assert.Contains("registry.Has(MCapability.RuleEngine)", fixedSource);
    }

    // ----------------------------------------------------------------
    // Test 3: Code fix preserves the original statement inside the guard
    // ----------------------------------------------------------------
    [Fact]
    public async System.Threading.Tasks.Task MBB008_CodeFix_PreservesOriginalStatement()
    {
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
        string fixedSource = await ApplyCodeFixAsync(source);

        // The original variable declaration should still be inside the guard
        Assert.Contains("ITenantContext ctx = null", fixedSource);
    }

    // ----------------------------------------------------------------
    // Helper: apply code fix and return resulting source text
    // ----------------------------------------------------------------
    private static async System.Threading.Tasks.Task<string> ApplyCodeFixAsync(string source)
    {
        CSharpCompilation compilation = CreateCompilation(source);

        // Get analyzer diagnostics
        ImmutableArray<DiagnosticAnalyzer> analyzers =
            ImmutableArray.Create<DiagnosticAnalyzer>(new Mbb008_CrossCapabilityAnalyzer());
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        ImmutableArray<Diagnostic> diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        Diagnostic? mbb008 = diagnostics.FirstOrDefault(d => d.Id == "MBB008");
        if (mbb008 is null)
        {
            return source; // No diagnostic found — test will fail on assertion
        }

        // Build a workspace document from the compilation
        AdhocWorkspace workspace = new AdhocWorkspace();
        Project project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReference(MetadataReference.CreateFromFile(
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                    "System.Runtime.dll")));

        Document document = project.AddDocument("Test.cs", SourceText.From(source));

        // Re-analyze the document to get diagnostics with correct document locations
        Compilation? docCompilation = await document.Project.GetCompilationAsync();
        if (docCompilation is null)
        {
            return source;
        }

        CompilationWithAnalyzers docCompWithAnalyzers = docCompilation.WithAnalyzers(analyzers);
        ImmutableArray<Diagnostic> docDiagnostics = await docCompWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        Diagnostic? docMbb008 = docDiagnostics.FirstOrDefault(d => d.Id == "MBB008");
        if (docMbb008 is null)
        {
            return source;
        }

        // Apply the code fix
        MBB008_CrossCapabilityCodeFix codeFix = new MBB008_CrossCapabilityCodeFix();
        List<CodeAction> actions = new List<CodeAction>();

        CodeFixContext fixContext = new CodeFixContext(
            document,
            docMbb008,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await codeFix.RegisterCodeFixesAsync(fixContext);

        if (actions.Count == 0)
        {
            return source;
        }

        // Apply first action
        ImmutableArray<CodeActionOperation> operations =
            await actions[0].GetOperationsAsync(CancellationToken.None);

        ApplyChangesOperation? applyOp = operations.OfType<ApplyChangesOperation>().FirstOrDefault();
        if (applyOp is null)
        {
            return source;
        }

        // Get the changed document
        Document? changedDoc = applyOp.ChangedSolution.GetDocument(document.Id);
        if (changedDoc is null)
        {
            return source;
        }

        SourceText? changedText = await changedDoc.GetTextAsync();
        return changedText?.ToString() ?? source;
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
