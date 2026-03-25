using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators;

/// <summary>
/// Analyzer MSP001 — warns when a string literal matches a known SiteId value
/// but the code doesn't use the generated <c>SiteIds.{name}</c> constant.
///
/// Rationale: SiteId string literals are typo-prone. The generated SiteIds class
/// provides compile-time safety. This analyzer nudges consumers to adopt constants.
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
        description: "String literals matching known SiteId values should use the generated SiteIds constants to prevent typos.");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Use CompilationStartAction to collect known SiteIds once per compilation,
        // then register SemanticModelAction to avoid calling GetSemanticModel() directly (RS1030).
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var iSiteProfile = compilationContext.Compilation
                .GetTypeByMetadataName("Muonroi.Tenancy.SiteProfile.ISiteProfile");

            // If ISiteProfile is not referenced, this project has no profiles — skip
            if (iSiteProfile is null) return;

            // Collect known SiteId string values via SemanticModelAction (RS1030 compliant)
            // We'll accumulate them in a concurrent-safe dictionary via SemanticModelAction
            var knownSiteIds = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(
                System.StringComparer.OrdinalIgnoreCase);

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

            compilationContext.RegisterSyntaxNodeAction(
                ctx => AnalyzeStringLiteral(ctx, knownSiteIds),
                SyntaxKind.StringLiteralExpression);
        });
    }

    private static void AnalyzeStringLiteral(
        SyntaxNodeAnalysisContext context,
        System.Collections.Concurrent.ConcurrentDictionary<string, string> knownSiteIds)
    {
        if (knownSiteIds.Count == 0) return;

        var literalExpr = (LiteralExpressionSyntax)context.Node;
        var value = literalExpr.Token.ValueText;

        if (!knownSiteIds.TryGetValue(value, out var constantName)) return;

        // Skip if inside an ISiteProfile.SiteId property definition — that's the source of truth
        var containingProp = literalExpr.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        if (containingProp?.Identifier.ValueText == "SiteId") return;

        // Skip literals inside an ISiteProfile implementing class (allows the definition itself)
        var containingClass = literalExpr.FirstAncestorOrSelf<ClassDeclarationSyntax>();
        if (containingClass is not null
            && context.SemanticModel.GetDeclaredSymbol(containingClass) is INamedTypeSymbol classSymbol
            && classSymbol.AllInterfaces.Any(i => i.Name == "ISiteProfile"))
            return;

        // Skip if already part of a member access (e.g. SiteIds.TCI — not a bare literal)
        if (literalExpr.Parent is MemberAccessExpressionSyntax) return;

        // Report MSP001: suggest using SiteIds.{constantName} instead
        context.ReportDiagnostic(
            Diagnostic.Create(Rule, literalExpr.GetLocation(), constantName, value));
    }

    /// <summary>
    /// Extracts the compile-time SiteId string value from an ISiteProfile implementation.
    /// Handles arrow expression, auto-property initializer, and simple return statement patterns.
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

            // Arrow expression: string SiteId => "VALUE"
            if (propDecl.ExpressionBody?.Expression is LiteralExpressionSyntax arrowLiteral)
                return arrowLiteral.Token.ValueText;

            // Auto-property with initializer: string SiteId { get; } = "VALUE"
            if (propDecl.Initializer?.Value is LiteralExpressionSyntax initLiteral)
                return initLiteral.Token.ValueText;

            // Getter with return statement: string SiteId { get { return "VALUE"; } }
            var getter = propDecl.AccessorList?.Accessors
                .FirstOrDefault(a => a.Keyword.ValueText == "get");
            if (getter?.Body?.Statements.FirstOrDefault() is ReturnStatementSyntax ret
                && ret.Expression is LiteralExpressionSyntax retLiteral)
                return retLiteral.Token.ValueText;
        }

        return null;
    }

    /// <summary>
    /// Converts a SiteId string value to a valid C# identifier (uppercase, non-alphanumeric -> underscore).
    /// </summary>
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
