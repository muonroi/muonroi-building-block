using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Muonroi.RuleEngine.SourceGenerators.CodeFixes;

/// <summary>
/// MBB009 CodeFixProvider: replaces a raw non-MException throw with the corresponding
/// MException subclass and ensures the required using directive is present.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MBB009_ForbiddenRawExceptionCodeFix)), Shared]
public sealed class MBB009_ForbiddenRawExceptionCodeFix : CodeFixProvider
{
    private const string ExceptionsNamespace = "Muonroi.Core.Abstractions.Exceptions";

    // Maps raw BCL exception type names to MException subclass names
    private static readonly Dictionary<string, string> ExceptionTypeMap = new Dictionary<string, string>
    {
        ["ArgumentException"]            = "MArgumentException",
        ["ArgumentNullException"]        = "MArgumentException",
        ["ArgumentOutOfRangeException"]  = "MArgumentException",
        ["InvalidOperationException"]    = "MInternalException",
        ["NotImplementedException"]      = "MInternalException",
        ["NotSupportedException"]        = "MInternalException",
        ["KeyNotFoundException"]         = "MNotFoundException",
        ["UnauthorizedAccessException"]  = "MUnauthorizedException",
        ["Exception"]                    = "MInternalException",
    };

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("MBB009");

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
        SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node is null)
        {
            return;
        }

        // Walk up to find the ObjectCreationExpressionSyntax
        ObjectCreationExpressionSyntax? creation = node as ObjectCreationExpressionSyntax
            ?? node.AncestorsAndSelf().OfType<ObjectCreationExpressionSyntax>().FirstOrDefault();

        if (creation is null)
        {
            return;
        }

        // Extract the raw type name (strip namespace qualifier if present)
        string rawTypeName = creation.Type.ToString();
        int lastDot = rawTypeName.LastIndexOf('.');
        if (lastDot >= 0)
        {
            rawTypeName = rawTypeName.Substring(lastDot + 1);
        }

        if (!ExceptionTypeMap.TryGetValue(rawTypeName, out string? mType) || mType is null)
        {
            return; // No mapping — no fix available
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Replace with {mType}",
                createChangedDocument: ct => ReplaceExceptionTypeAsync(context.Document, creation, mType, ct),
                equivalenceKey: $"MBB009_{mType}"),
            diagnostic);
    }

    private static async Task<Document> ReplaceExceptionTypeAsync(
        Document document,
        ObjectCreationExpressionSyntax creation,
        string mType,
        CancellationToken cancellationToken)
    {
        DocumentEditor editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        // Replace the type name inside the ObjectCreationExpression
        TypeSyntax newType = SyntaxFactory.ParseTypeName(mType)
            .WithTriviaFrom(creation.Type);
        editor.ReplaceNode(creation.Type, newType);

        // Add using directive if not already present
        SyntaxNode changedRoot = editor.GetChangedRoot();
        if (changedRoot is CompilationUnitSyntax compilationUnit &&
            !HasUsingDirective(compilationUnit, ExceptionsNamespace))
        {
            UsingDirectiveSyntax usingDirective = SyntaxFactory.UsingDirective(
                SyntaxFactory.ParseName(ExceptionsNamespace))
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

            SyntaxNode newRoot = compilationUnit.AddUsings(usingDirective);
            return document.WithSyntaxRoot(newRoot);
        }

        return editor.GetChangedDocument();
    }

    private static bool HasUsingDirective(CompilationUnitSyntax root, string namespaceName)
    {
        return root.Usings.Any(u => u.Name?.ToString() == namespaceName);
    }
}
