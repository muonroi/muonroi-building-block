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

/// <summary>
/// MBB008 CodeFixProvider: wraps cross-capability reference statements in
/// an if (registry.Has(MCapability.X)) { ... } guard.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MBB008_CrossCapabilityCodeFix)), Shared]
public sealed class MBB008_CrossCapabilityCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("MBB008");

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

        // Read the capability name stored in diagnostic properties by the analyzer
        if (!diagnostic.Properties.TryGetValue("Capability", out string? capability) || capability is null)
        {
            return;
        }

        // Find the flagged node
        SyntaxNode? flaggedNode = root.FindNode(diagnostic.Location.SourceSpan);
        if (flaggedNode is null)
        {
            return;
        }

        // Find the containing statement to wrap
        StatementSyntax? statement = flaggedNode.FirstAncestorOrSelf<StatementSyntax>();
        if (statement is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Wrap in registry.Has(MCapability.{capability}) guard",
                createChangedDocument: ct => WrapInHasGuardAsync(context.Document, statement, capability, ct),
                equivalenceKey: $"MBB008_{capability}"),
            diagnostic);
    }

    private static async Task<Document> WrapInHasGuardAsync(
        Document document,
        StatementSyntax statement,
        string capability,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Build: if (registry.Has(MCapability.{capability})) { <original statement> }
        IfStatementSyntax ifGuard = SyntaxFactory.IfStatement(
            condition: SyntaxFactory.ParseExpression($"registry.Has(MCapability.{capability})"),
            statement: SyntaxFactory.Block(statement.WithoutLeadingTrivia()))
            .WithLeadingTrivia(statement.GetLeadingTrivia())
            .WithTrailingTrivia(statement.GetTrailingTrivia());

        editor.ReplaceNode(statement, ifGuard);
        return editor.GetChangedDocument();
    }
}
