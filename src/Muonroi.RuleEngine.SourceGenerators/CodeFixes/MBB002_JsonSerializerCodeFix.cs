using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Editing;
using System.Composition;

namespace Muonroi.RuleEngine.SourceGenerators.CodeFixes;

/// <summary>
/// Code fix provider for MBB002 diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MBB002_JsonSerializerCodeFix)), Shared]
public sealed class MBB002_JsonSerializerCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ["MBB002"];

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc/>
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
