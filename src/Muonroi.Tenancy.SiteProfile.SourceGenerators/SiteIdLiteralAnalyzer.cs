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
/// <para>
/// Architecture: Uses <see cref="AnalysisContext.RegisterCompilationStartAction"/> to collect
/// all ISiteProfile SiteId values upfront from the <see cref="Compilation"/> object (no SemanticModelAction needed).
/// Then RegisterSyntaxNodeAction checks each string literal
/// against the pre-populated dictionary. This ensures IDE squiggles appear inline (not just in Error List).
/// </para>
///
/// <para>
/// Skip rules:
/// <list type="bullet">
///   <item>String literals inside <c>SiteId</c> property definitions (source of truth)</item>
///   <item>String literals that are already part of a member access expression (e.g., <c>SiteIds.TCI</c>)</item>
/// </list>
/// </para>
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

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var iSiteProfile = compilationContext.Compilation
                .GetTypeByMetadataName("Muonroi.Tenancy.SiteProfile.ISiteProfile");

            if (iSiteProfile is null) return;

            // Collect ALL known SiteId values upfront from Compilation — no race condition.
            // Compilation.SyntaxTrees + GetSemanticModel is safe inside CompilationStartAction.
            var knownSiteIds = CollectSiteIds(compilationContext.Compilation, iSiteProfile);

            if (knownSiteIds.Count == 0) return;

            // Register SyntaxNodeAction — runs per literal, has full syntax context for IDE squiggles
            compilationContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeStringLiteral(syntaxContext, knownSiteIds),
                SyntaxKind.StringLiteralExpression);
        });
    }

    /// <summary>
    /// Scans all syntax trees in the compilation for ISiteProfile implementations
    /// and extracts their SiteId string values. Runs once per compilation in CompilationStartAction.
    /// </summary>
    /// <remarks>
    /// RS1030 suppressed: GetSemanticModel() is called intentionally in CompilationStartAction
    /// to collect SiteIds synchronously BEFORE registering SyntaxNodeAction. This avoids
    /// the race condition where SyntaxNodeAction runs before SemanticModelAction populates
    /// the knownSiteIds dictionary, which caused MSP001 to silently skip all literals.
    /// </remarks>
#pragma warning disable RS1030 // Do not invoke Compilation.GetSemanticModel() method within a diagnostic analyzer
    private static Dictionary<string, string> CollectSiteIds(
        Compilation compilation, INamedTypeSymbol iSiteProfile)
    {
        var result = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var classDeclarations = tree.GetRoot()
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
                if (!result.ContainsKey(siteIdValue))
                    result[siteIdValue] = constantName;
            }
        }

        return result;
    }
#pragma warning restore RS1030

    /// <summary>
    /// Analyzes a single string literal expression. Reports MSP001 if the literal
    /// matches a known SiteId and is not inside a skip zone (SiteId property or member access).
    /// </summary>
    private static void AnalyzeStringLiteral(
        SyntaxNodeAnalysisContext context,
        Dictionary<string, string> knownSiteIds)
    {
        var literalExpr = (LiteralExpressionSyntax)context.Node;
        var value = literalExpr.Token.ValueText;

        if (!knownSiteIds.TryGetValue(value, out var constantName)) return;

        // Skip if inside SiteId property definition — that's the source of truth
        var containingProp = literalExpr.FirstAncestorOrSelf<PropertyDeclarationSyntax>();
        if (containingProp?.Identifier.ValueText == "SiteId") return;

        // Skip if inside a const field declaration (e.g., OrderSiteIds.HICT = "HICT")
        var containingField = literalExpr.FirstAncestorOrSelf<FieldDeclarationSyntax>();
        if (containingField is not null
            && containingField.Modifiers.Any(SyntaxKind.ConstKeyword))
            return;

        // Skip if already part of a member access (e.g. SiteIds.TCI — not a bare literal)
        if (literalExpr.Parent is MemberAccessExpressionSyntax) return;

        // Report MSP001
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
    /// Converts a SiteId string value to a valid C# identifier (uppercase, non-alphanumeric replaced with underscore).
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
