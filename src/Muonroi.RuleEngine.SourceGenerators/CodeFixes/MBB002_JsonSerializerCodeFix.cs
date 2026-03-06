using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace Muonroi.RuleEngine.SourceGenerators.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MBB002_JsonSerializerCodeFix)), Shared]
public sealed class MBB002_JsonSerializerCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("MBB002");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan);
        InvocationExpressionSyntax? invocation = node as InvocationExpressionSyntax ??
            node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace with IMJsonSerializeService call",
                createChangedDocument: ct => ReplaceAsync(context.Document, invocation, ct),
                equivalenceKey: "MBB002_IMJsonSerializeService"),
            diagnostic);
    }

    private static async Task<Document> ReplaceAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        ExpressionSyntax replacement = SyntaxFactory.ParseExpression("mJsonSerializeService.Serialize(/* value */)")
            .WithTriviaFrom(invocation);
        editor.ReplaceNode(invocation, replacement);
        return editor.GetChangedDocument();
    }
}
