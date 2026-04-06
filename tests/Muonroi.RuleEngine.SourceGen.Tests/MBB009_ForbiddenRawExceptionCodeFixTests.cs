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
/// Unit tests for the MBB009 ForbiddenRawExceptionCodeFix.
/// Verifies that the code fix replaces raw exception types with mapped MException subclasses.
/// </summary>
public class MBB009_ForbiddenRawExceptionCodeFixTests
{
    // ----------------------------------------------------------------
    // Test 1: ArgumentException → MArgumentException
    // ----------------------------------------------------------------
    [Fact]
    public async System.Threading.Tasks.Task MBB009_CodeFix_ReplacesArgumentExceptionWithMArgumentException()
    {
        string source = @"
namespace Muonroi.MyService
{
    public class MyService
    {
        public void DoWork(string input)
        {
            throw new System.ArgumentException(""bad input"");
        }
    }
}
";
        string fixedSource = await ApplyCodeFixAsync(source);

        Assert.Contains("new MArgumentException(", fixedSource);
    }

    // ----------------------------------------------------------------
    // Test 2: InvalidOperationException → MInternalException
    // ----------------------------------------------------------------
    [Fact]
    public async System.Threading.Tasks.Task MBB009_CodeFix_ReplacesInvalidOperationExceptionWithMInternalException()
    {
        string source = @"
namespace Muonroi.Data
{
    public class DataService
    {
        public void Load()
        {
            throw new System.InvalidOperationException(""oops"");
        }
    }
}
";
        string fixedSource = await ApplyCodeFixAsync(source);

        Assert.Contains("new MInternalException(", fixedSource);
    }

    // ----------------------------------------------------------------
    // Test 3: Custom unmapped exception type — no fix applied, source unchanged
    // ----------------------------------------------------------------
    [Fact]
    public async System.Threading.Tasks.Task MBB009_CodeFix_NoFixForUnmappedCustomException()
    {
        // CustomDomainException has no mapping in ExceptionTypeMap
        string source = @"
namespace Muonroi.MyService
{
    public class CustomDomainException : System.Exception
    {
        public CustomDomainException(string msg) : base(msg) { }
    }

    public class MyService
    {
        public void DoWork()
        {
            throw new CustomDomainException(""domain error"");
        }
    }
}
";
        string fixedSource = await ApplyCodeFixAsync(source);

        // The source should not be mutated since no mapping exists for CustomDomainException
        // (either no diagnostic fires or no fix is registered)
        // We just verify it doesn't accidentally replace with an MException type
        Assert.DoesNotContain("new MInternalException(", fixedSource);
        Assert.DoesNotContain("new MArgumentException(", fixedSource);
    }

    // ----------------------------------------------------------------
    // Helper: apply code fix and return resulting source text
    // ----------------------------------------------------------------
    private static async System.Threading.Tasks.Task<string> ApplyCodeFixAsync(string source)
    {
        CSharpCompilation compilation = CreateCompilation(source);

        // Get analyzer diagnostics
        ImmutableArray<DiagnosticAnalyzer> analyzers =
            ImmutableArray.Create<DiagnosticAnalyzer>(new Mbb009_ForbiddenRawExceptionAnalyzer());
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        ImmutableArray<Diagnostic> diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        Diagnostic? mbb009 = diagnostics.FirstOrDefault(d => d.Id == "MBB009");
        if (mbb009 is null)
        {
            return source; // No diagnostic — test will rely on assertion for outcome
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

        // Re-analyze from the workspace document to get diagnostics with correct document locations
        Compilation? docCompilation = await document.Project.GetCompilationAsync();
        if (docCompilation is null)
        {
            return source;
        }

        CompilationWithAnalyzers docCompWithAnalyzers = docCompilation.WithAnalyzers(analyzers);
        ImmutableArray<Diagnostic> docDiagnostics = await docCompWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        Diagnostic? docMbb009 = docDiagnostics.FirstOrDefault(d => d.Id == "MBB009");
        if (docMbb009 is null)
        {
            return source;
        }

        // Apply the code fix
        MBB009_ForbiddenRawExceptionCodeFix codeFix = new MBB009_ForbiddenRawExceptionCodeFix();
        List<CodeAction> actions = new List<CodeAction>();

        CodeFixContext fixContext = new CodeFixContext(
            document,
            docMbb009,
            (action, _) => actions.Add(action),
            CancellationToken.None);

        await codeFix.RegisterCodeFixesAsync(fixContext);

        if (actions.Count == 0)
        {
            return source; // No fix registered (unmapped type)
        }

        // Apply first action
        ImmutableArray<CodeActionOperation> operations =
            await actions[0].GetOperationsAsync(CancellationToken.None);

        ApplyChangesOperation? applyOp = operations.OfType<ApplyChangesOperation>().FirstOrDefault();
        if (applyOp is null)
        {
            return source;
        }

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
