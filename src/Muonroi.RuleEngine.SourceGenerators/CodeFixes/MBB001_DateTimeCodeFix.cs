namespace Muonroi.RuleEngine.SourceGenerators.CodeFixes;

/// <summary>
/// Code fix provider for MBB001 diagnostics.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MBB001_DateTimeCodeFix)), Shared]
public sealed class MBB001_DateTimeCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ["MBB001"];

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
        if (node is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace with IMDateTimeService.Now",
                createChangedDocument: ct => ReplaceAsync(context.Document, memberAccess, ct),
                equivalenceKey: "MBB001_IMDateTimeService"),
            diagnostic);
    }

    private static async Task<Document> ReplaceAsync(
        Document document,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
        ExpressionSyntax replacement = SyntaxFactory.ParseExpression("mDateTimeService.Now")
            .WithTriviaFrom(memberAccess);
        editor.ReplaceNode(memberAccess, replacement);
        return editor.GetChangedDocument();
    }
}
