namespace Muonroi.CodeStandards.Tests;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Muonroi.CodeStandards.Analyzers;
using Muonroi.CodeStandards.CodeFixes;
using Xunit;

public class Mstd0002_NullForgivingCodeFixTests
{
    [Fact]
    public async Task Mstd0002_CodeFix_ReplacesNullForgivingWithMGuardNotNull()
    {
        string source = @"
#nullable enable
namespace Muonroi.MyService
{
    public class Product { public int Id; }
    public class MyService
    {
        public int Read(Product? product) { return product!.Id; }
    }
}
";
        string fixedSource = await ApplyCodeFixAsync(source);

        Assert.Contains("MGuard.NotNull(product)", fixedSource);
        Assert.DoesNotContain("product!", fixedSource);
        Assert.Contains("using Muonroi.Core.Abstractions.Guards", fixedSource);
    }

    private static async Task<string> ApplyCodeFixAsync(string source)
    {
        CSharpParseOptions parseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Latest);

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

        Compilation? compilation = await document.Project.GetCompilationAsync();
        Assert.NotNull(compilation);

        ImmutableArray<DiagnosticAnalyzer> analyzers =
            ImmutableArray.Create<DiagnosticAnalyzer>(new Mstd0002_NullForgivingAnalyzer());
        ImmutableArray<Diagnostic> diagnostics =
            await compilation!.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();

        Diagnostic? target = diagnostics.FirstOrDefault(d => d.Id == "MSTD0002");
        Assert.NotNull(target);

        Mstd0002_NullForgivingCodeFix codeFix = new Mstd0002_NullForgivingCodeFix();
        List<CodeAction> actions = new List<CodeAction>();
        CodeFixContext fixContext = new CodeFixContext(
            document, target!, (action, _) => actions.Add(action), CancellationToken.None);
        await codeFix.RegisterCodeFixesAsync(fixContext);
        Assert.NotEmpty(actions);

        ImmutableArray<CodeActionOperation> operations =
            await actions[0].GetOperationsAsync(CancellationToken.None);
        ApplyChangesOperation applyOp = operations.OfType<ApplyChangesOperation>().First();
        Document changedDoc = applyOp.ChangedSolution.GetDocument(document.Id)!;
        SourceText changedText = await changedDoc.GetTextAsync();
        return changedText.ToString();
    }
}
