using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators;

/// <summary>
/// Analyzer MSP022 — warns when a class decorated with [SiteGrpcService] is absent from the
/// source-generated <c>SiteGrpcServiceRegistry</c>.
///
/// <para>
/// Two scenarios are detected:
/// <list type="number">
///   <item>Registry type is absent (Phase 58 source generator hasn't run yet) → MSP022 fires on
///   every [SiteGrpcService] class with a "registry not found" message.</item>
///   <item>Registry type is present but the class name does not appear in the
///   <c>GetAllSiteGrpcServices()</c> method body (stale generated file) → MSP022 fires.</item>
/// </list>
/// </para>
///
/// <para>
/// The check uses a name-substring heuristic against the generated method body because the
/// registry is source-generated code and its body is always constructed by the generator using
/// the simple class name inside <c>typeof()</c> expressions.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SiteGrpcServiceRegistryAnalyzer : DiagnosticAnalyzer
{
    /// <summary>MSP022 diagnostic identifier.</summary>
    public const string DiagnosticId = "MSP022";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "[SiteGrpcService] type missing from generated registry",
        messageFormat: "'{0}' is decorated with [SiteGrpcService] but is not present in SiteGrpcServiceRegistry. Rebuild to regenerate the registry or ensure [SiteGrpcService] is properly configured.",
        category: "Muonroi.SiteProfile",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "All [SiteGrpcService]-decorated classes must appear in the source-generated SiteGrpcServiceRegistry so MapSiteGrpcServices() can discover them without runtime reflection.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // RegisterCompilationAction runs at compilation end — correct for whole-compilation checks
        // that require seeing all types before emitting diagnostics.
        context.RegisterCompilationAction(compilationContext =>
        {
            var compilation = compilationContext.Compilation;

            // 1. Resolve [SiteGrpcService] attribute symbol
            var siteGrpcServiceAttribute = compilation
                .GetTypeByMetadataName("Muonroi.Tenancy.SiteProfile.Grpc.SiteGrpcServiceAttribute");
            if (siteGrpcServiceAttribute is null) return; // attribute package not referenced

            // 2. Collect all [SiteGrpcService]-decorated non-generated class symbols
#pragma warning disable RS1030 // Intentional: GetSemanticModel in CompilationAction for whole-compilation analysis
            var siteGrpcTypes = CollectSiteGrpcTypes(compilation, siteGrpcServiceAttribute);
#pragma warning restore RS1030

            if (siteGrpcTypes.Count == 0) return;

            // 3. Resolve SiteGrpcServiceRegistry type (may be null if Phase 58 not yet run)
            var registryType = compilation
                .GetTypeByMetadataName("Muonroi.Tenancy.SiteProfile.Generated.SiteGrpcServiceRegistry");

            if (registryType is null)
            {
                // Registry absent — report MSP022 on every [SiteGrpcService] class
                foreach (var (symbol, location) in siteGrpcTypes)
                {
                    compilationContext.ReportDiagnostic(
                        Diagnostic.Create(Rule, location, symbol.Name));
                }
                return;
            }

            // 4. Registry present — build registry body text from GetAllSiteGrpcServices() method
            string registryBodyText = BuildRegistryBodyText(registryType);

            // 5. Report MSP022 for each type whose name does not appear in the registry body
            foreach (var (symbol, location) in siteGrpcTypes)
            {
                // Simple name check — generated code always uses simple names in typeof()
                if (!registryBodyText.Contains(symbol.Name))
                {
                    compilationContext.ReportDiagnostic(
                        Diagnostic.Create(Rule, location, symbol.Name));
                }
            }
        });
    }

    /// <summary>
    /// Collects all non-abstract, non-interface named type symbols that carry [SiteGrpcService],
    /// skipping source-generated files (those ending with ".g.cs").
    /// Returns pairs of (symbol, identifier-location) for diagnostic reporting.
    /// </summary>
#pragma warning disable RS1030 // Intentional: GetSemanticModel called from CompilationAction for whole-compilation analysis
    private static List<(INamedTypeSymbol Symbol, Location Location)> CollectSiteGrpcTypes(
        Compilation compilation,
        INamedTypeSymbol siteGrpcServiceAttribute)
    {
        var result = new List<(INamedTypeSymbol, Location)>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            // Skip generated files — heuristic: file path ends with ".g.cs"
            if (tree.FilePath.EndsWith(".g.cs", System.StringComparison.OrdinalIgnoreCase))
                continue;

            var model = compilation.GetSemanticModel(tree);

            foreach (var classDecl in tree.GetRoot().DescendantNodes()
                .OfType<ClassDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
                    continue;

                if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Interface)
                    continue;

                bool hasSiteGrpcService = symbol.GetAttributes().Any(a =>
                    SymbolEqualityComparer.Default.Equals(a.AttributeClass, siteGrpcServiceAttribute));

                if (!hasSiteGrpcService) continue;

                result.Add((symbol, classDecl.Identifier.GetLocation()));
            }
        }

        return result;
    }
#pragma warning restore RS1030

    /// <summary>
    /// Extracts the full text of the <c>GetAllSiteGrpcServices()</c> method body from the
    /// <c>SiteGrpcServiceRegistry</c> type's declaring syntax references.
    /// Returns an empty string if the method or its syntax is unavailable.
    /// </summary>
    private static string BuildRegistryBodyText(INamedTypeSymbol registryType)
    {
        var method = registryType
            .GetMembers("GetAllSiteGrpcServices")
            .OfType<IMethodSymbol>()
            .FirstOrDefault();

        if (method is null) return string.Empty;

        var sb = new System.Text.StringBuilder();
        foreach (var syntaxRef in method.DeclaringSyntaxReferences)
        {
            sb.Append(syntaxRef.GetSyntax().ToFullString());
        }

        return sb.ToString();
    }
}
