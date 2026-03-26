using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators;

/// <summary>
/// Analyzer MSP001 — warns when a string literal matches a known SiteId value
/// but the code doesn't use the generated <c>SiteIds.{name}</c> constant.
///
/// Architecture:
/// 1. SemanticModelAction: collect ISiteProfile implementations → extract SiteId string values
/// 2. SyntaxNodeAction: collect candidate string literals that MIGHT match a SiteId (deferred)
/// 3. CompilationEndAction: cross-reference candidates against collected SiteIds → report diagnostics
///
/// This 3-phase approach avoids the race condition where SyntaxNodeAction runs
/// before SemanticModelAction has populated knownSiteIds.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SiteIdLiteralAnalyzer : DiagnosticAnalyzer
{
    /// <summary>MSP001 diagnostic identifier.</summary>
    public const string DiagnosticId = "MSP001";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Use SiteIds constant instead of string literal",
        messageFormat: "Use SiteIds.{0} instead of \"{1}\" literal for compile-time safety",
        category: "Muonroi.SiteProfile",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "String literals matching known SiteId values should use the generated SiteIds constants to prevent typos.",
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var iSiteProfile = compilationContext.Compilation
                .GetTypeByMetadataName("Muonroi.Tenancy.SiteProfile.ISiteProfile");

            if (iSiteProfile is null) return;

            // Phase 1 output: known SiteId values (populated by SemanticModelAction)
            var knownSiteIds = new ConcurrentDictionary<string, string>(
                System.StringComparer.OrdinalIgnoreCase);

            // Phase 2 output: candidate literals to check (populated by SyntaxNodeAction)
            var candidates = new ConcurrentBag<(Location location, string value)>();

            // Phase 1: Collect known SiteId values from ISiteProfile implementations
            compilationContext.RegisterSemanticModelAction(semanticModelContext =>
            {
                var model = semanticModelContext.SemanticModel;
                var classDeclarations = model.SyntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>();

                foreach (var classDecl in classDeclarations)
                {
                    if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
                        continue;

                    if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Interface)
                        continue;

                    if (!symbol.AllInterfaces.Any(i =>
                        SymbolEqualityComparer.Default.Equals(i, iSiteProfile)))
                        continue;

                    string? siteIdValue = ExtractSiteIdLiteralValue(symbol);
                    if (siteIdValue is null) continue;

                    string constantName = SanitizeIdentifier(siteIdValue);
                    knownSiteIds.TryAdd(siteIdValue, constantName);
                }
            });

            // Phase 2: Collect ALL string literals that pass skip rules (cheap, no semantic model needed)
            compilationContext.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var literalExpr = (LiteralExpressionSyntax)syntaxContext.Node;
                var value = literalExpr.Token.ValueText;

                // Quick reject: empty or whitespace-only strings are never SiteIds
                if (string.IsNullOrWhiteSpace(value)) return;

                // Skip if inside SiteId property definition — that's the source of truth
                var containingProp = literalExpr.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
                if (containingProp?.Identifier.ValueText == "SiteId") return;

                // Skip if already part of a member access (e.g. SiteIds.TCI)
                if (literalExpr.Parent is MemberAccessExpressionSyntax) return;

                // Collect as candidate — will check against knownSiteIds in Phase 3
                candidates.Add((literalExpr.GetLocation(), value));

            }, SyntaxKind.StringLiteralExpression);

            // Phase 3: Cross-reference candidates against known SiteIds (runs AFTER phases 1+2)
            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                if (knownSiteIds.Count == 0) return;

                foreach (var (location, value) in candidates)
                {
                    if (knownSiteIds.TryGetValue(value, out var constantName))
                    {
                        endContext.ReportDiagnostic(
                            Diagnostic.Create(Rule, location, constantName, value));
                    }
                }
            });
        });
    }

    /// <summary>
    /// Extracts the compile-time SiteId string value from an ISiteProfile implementation.
    /// </summary>
    private static string? ExtractSiteIdLiteralValue(INamedTypeSymbol profileSymbol)
    {
        var siteIdProp = profileSymbol.GetMembers("SiteId")
            .OfType<IPropertySymbol>()
            .FirstOrDefault();
        if (siteIdProp is null) return null;

        foreach (var syntaxRef in siteIdProp.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax();
            if (syntax is not PropertyDeclarationSyntax propDecl) continue;

            if (propDecl.ExpressionBody?.Expression is LiteralExpressionSyntax arrowLiteral)
                return arrowLiteral.Token.ValueText;

            if (propDecl.Initializer?.Value is LiteralExpressionSyntax initLiteral)
                return initLiteral.Token.ValueText;

            var getter = propDecl.AccessorList?.Accessors
                .FirstOrDefault(a => a.Keyword.ValueText == "get");
            if (getter?.Body?.Statements.FirstOrDefault() is ReturnStatementSyntax ret
                && ret.Expression is LiteralExpressionSyntax retLiteral)
                return retLiteral.Token.ValueText;
        }

        return null;
    }

    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value)) return "_EMPTY";

        var sb = new System.Text.StringBuilder(value.Length);
        foreach (char c in value.ToUpperInvariant())
        {
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        if (char.IsDigit(sb[0]))
            sb.Insert(0, '_');

        return sb.ToString();
    }
}
