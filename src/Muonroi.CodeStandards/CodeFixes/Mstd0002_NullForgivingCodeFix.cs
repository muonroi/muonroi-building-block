using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Muonroi.CodeStandards.CodeFixes;

/// <summary>
/// MSTD0002 CodeFixProvider: replaces <c>expr!</c> with <c>MGuard.NotNull(expr)</c>
/// and adds <c>using Muonroi.Core.Abstractions.Guards;</c> when missing.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Mstd0002_NullForgivingCodeFix)), Shared]
public sealed class Mstd0002_NullForgivingCodeFix : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("MSTD0002");

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxNode flagged = root.FindNode(diagnostic.Location.SourceSpan);
        PostfixUnaryExpressionSyntax? postfix =
            flagged as PostfixUnaryExpressionSyntax
            ?? flagged.FirstAncestorOrSelf<PostfixUnaryExpressionSyntax>();

        if (postfix is null || !postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace '!' with MGuard.NotNull(...)",
                createChangedDocument: ct => ReplaceAsync(context.Document, postfix, ct),
                equivalenceKey: "MSTD0002_MGuardNotNull"),
            diagnostic);
    }

    private static async Task<Document> ReplaceAsync(
        Document document,
        PostfixUnaryExpressionSyntax postfix,
        CancellationToken cancellationToken)
    {
        SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        InvocationExpressionSyntax replacement = SyntaxFactory.InvocationExpression(
                SyntaxFactory.ParseExpression("MGuard.NotNull"),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(postfix.Operand.WithoutTrivia()))))
            .WithTriviaFrom(postfix);

        SyntaxNode updatedRoot = root.ReplaceNode(postfix, replacement);

        CompilationUnitSyntax compilationUnit = (CompilationUnitSyntax)updatedRoot;
        bool hasUsing = compilationUnit.Usings.Any(u =>
            u.Name?.ToString() == "Muonroi.Core.Abstractions.Guards");

        if (!hasUsing)
        {
            UsingDirectiveSyntax usingDirective = SyntaxFactory.UsingDirective(
                    SyntaxFactory.ParseName("Muonroi.Core.Abstractions.Guards"))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
            updatedRoot = compilationUnit.AddUsings(usingDirective);
        }

        return document.WithSyntaxRoot(updatedRoot);
    }
}
