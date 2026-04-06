using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Muonroi.RuleEngine.SourceGenerators.CodeFixes;

/// <summary>
/// MBB010 CodeFixProvider: inserts <c>MGuard.NotNull(param, nameof(param));</c>
/// as the first statement of a public method that is missing a null guard for a parameter.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MBB010_MissingGuardCodeFix)), Shared]
public sealed class MBB010_MissingGuardCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("MBB010");

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

        // The diagnostic location points to the ParameterSyntax
        SyntaxNode? flaggedNode = root.FindNode(diagnostic.Location.SourceSpan);
        ParameterSyntax? parameter = flaggedNode?.FirstAncestorOrSelf<ParameterSyntax>();
        if (parameter is null)
        {
            return;
        }

        string paramName = parameter.Identifier.ValueText;

        // Walk up to find the containing method
        MethodDeclarationSyntax? method = parameter.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add MGuard.NotNull({paramName})",
                createChangedDocument: ct => InsertGuardAsync(context.Document, method, paramName, ct),
                equivalenceKey: $"MBB010_{paramName}"),
            diagnostic);
    }

    private static async Task<Document> InsertGuardAsync(
        Document document,
        MethodDeclarationSyntax method,
        string paramName,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Build: MGuard.NotNull(param, nameof(param));
        StatementSyntax guardStatement = SyntaxFactory
            .ParseStatement($"MGuard.NotNull({paramName}, nameof({paramName}));")
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

        if (method.Body is not null)
        {
            if (method.Body.Statements.Count > 0)
            {
                // Insert before the first existing statement
                StatementSyntax firstStatement = method.Body.Statements[0];
                editor.InsertBefore(firstStatement, guardStatement.WithLeadingTrivia(firstStatement.GetLeadingTrivia()));
            }
            else
            {
                // Empty body — replace with block containing the guard
                BlockSyntax newBody = SyntaxFactory.Block(guardStatement)
                    .WithOpenBraceToken(method.Body.OpenBraceToken)
                    .WithCloseBraceToken(method.Body.CloseBraceToken);
                editor.ReplaceNode(method.Body, newBody);
            }
        }

        // Add using Muonroi.Core.Abstractions.Guards if not present
        SyntaxNode currentRoot = editor.GetChangedRoot();
        CompilationUnitSyntax compilationUnit = (CompilationUnitSyntax)currentRoot;

        bool hasUsing = compilationUnit.Usings.Any(u =>
            u.Name?.ToString() == "Muonroi.Core.Abstractions.Guards");

        if (!hasUsing)
        {
            UsingDirectiveSyntax usingDirective = SyntaxFactory.UsingDirective(
                SyntaxFactory.ParseName("Muonroi.Core.Abstractions.Guards"))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

            editor.ReplaceNode(compilationUnit, compilationUnit.AddUsings(usingDirective));
        }

        return editor.GetChangedDocument();
    }
}
