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
/// Unit tests for the MBB010 MissingGuardCodeFix.
/// Verifies that the code fix inserts MGuard.NotNull(param, nameof(param)) as the first
/// statement in the method body for unguarded public method parameters.
/// </summary>
public class MBB010_MissingGuardCodeFixTests
{
    // ----------------------------------------------------------------
    // Test 1: Code fix inserts MGuard.NotNull(input, nameof(input))
    // ----------------------------------------------------------------
    [Fact]
    public async System.Threading.Tasks.Task MBB010_CodeFix_InsertsMGuardNotNull()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public void Process(string input) { var x = input.Length; }
    }
}
";
        string fixedSource = await ApplyCodeFixAsync(source);

        Assert.Contains("MGuard.NotNull(input, nameof(input))", fixedSource);
    }

    // ----------------------------------------------------------------
    // Test 2: Guard is inserted as the FIRST statement (before existing code)
    // ----------------------------------------------------------------
    [Fact]
    public async System.Threading.Tasks.Task MBB010_CodeFix_InsertsAsFirstStatement()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public void Process(string input)
        {
            var length = input.Length;
            var upper = input.ToUpper();
        }
    }
}
";
        string fixedSource = await ApplyCodeFixAsync(source);

        // Guard must appear in the output
        Assert.Contains("MGuard.NotNull(input, nameof(input))", fixedSource);

        // Guard must appear before the existing first statement
        int guardIndex = fixedSource.IndexOf("MGuard.NotNull(input, nameof(input))", System.StringComparison.Ordinal);
        int existingIndex = fixedSource.IndexOf("input.Length", System.StringComparison.Ordinal);
        Assert.True(guardIndex < existingIndex, "Guard should appear before existing first statement");
    }

    // ----------------------------------------------------------------
    // Test 3: Code fix handles method with multiple params (fixes first unguarded param)
    // ----------------------------------------------------------------
    [Fact]
    public async System.Threading.Tasks.Task MBB010_CodeFix_HandlesMultipleParams()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class MyService
    {
        public void Process(string input, object context) { }
    }
}
";
        string fixedSource = await ApplyCodeFixAsync(source);

        // At least one guard should be inserted for the first unguarded parameter
        Assert.True(
            fixedSource.Contains("MGuard.NotNull(input, nameof(input))", System.StringComparison.Ordinal)
            || fixedSource.Contains("MGuard.NotNull(context, nameof(context))", System.StringComparison.Ordinal),
            "At least one guard should be inserted for unguarded params");
    }

    // ----------------------------------------------------------------
    // Helper: apply the first MBB010 code fix and return resulting source text
    // ----------------------------------------------------------------
    private static async System.Threading.Tasks.Task<string> ApplyCodeFixAsync(string source)
    {
        CSharpParseOptions parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest);

        List<MetadataReference> references =
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(
                System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                    "System.Runtime.dll")),
        ];

        CSharpCompilation compilation = CSharpCompilation.Create(
            "TestCompilation",
            new[] { CSharpSyntaxTree.ParseText(source, parseOptions) },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

        // Get analyzer diagnostics
        ImmutableArray<DiagnosticAnalyzer> analyzers =
            ImmutableArray.Create<DiagnosticAnalyzer>(new Mbb010_MissingGuardAnalyzer());
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers(analyzers);
        ImmutableArray<Diagnostic> diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();

        Diagnostic? mbb010 = diagnostics.FirstOrDefault(d => d.Id == "MBB010");
        if (mbb010 is null)
        {
            return source; // No diagnostic — test will fail on assertion
        }

        // Build a workspace document from the compilation
        AdhocWorkspace workspace = new AdhocWorkspace();
        Project project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .WithCompilationOptions(
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithNullableContextOptions(NullableContextOptions.Enable))
            .WithParseOptions(parseOptions)
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

        Diagnostic? docMbb010 = docDiagnostics.FirstOrDefault(d => d.Id == "MBB010");
        if (docMbb010 is null)
        {
            return source;
        }

        // Apply the code fix
        MBB010_MissingGuardCodeFix codeFix = new MBB010_MissingGuardCodeFix();
        List<CodeAction> actions = new List<CodeAction>();

        CodeFixContext fixContext = new CodeFixContext(
            document,
            docMbb010,
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

        Document? changedDoc = applyOp.ChangedSolution.GetDocument(document.Id);
        if (changedDoc is null)
        {
            return source;
        }

        SourceText? changedText = await changedDoc.GetTextAsync();
        return changedText?.ToString() ?? source;
    }
}
