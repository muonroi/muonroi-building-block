using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators;

/// <summary>
/// Analyzer MSP021 — warns when a non-abstract class derives from a gRPC-generated *Base class
/// but is missing the <c>[SiteGrpcService]</c> attribute.
///
/// <para>
/// Detection strategy: resolve <c>SiteGrpcServiceAttribute</c> at compilation start.
/// If the attribute type is not found (package not referenced), the analyzer no-ops.
/// For each class declaration, walk the base type chain looking for a type satisfying
/// the gRPC Base class heuristic:
/// <list type="bullet">
///   <item>Base type name ends with "Base"</item>
///   <item>AND the base type's namespace contains ".Grpc" (case-insensitive), OR
///         the base type is nested inside an outer class whose name equals the base class
///         name minus the "Base" suffix (gRPC nested class codegen pattern).</item>
/// </list>
/// If a gRPC base is detected and <c>[SiteGrpcService]</c> is absent, MSP021 is reported.
/// </para>
///
/// <para>
/// Skip rules:
/// <list type="bullet">
///   <item>Abstract classes — they may be intermediate gRPC base layers.</item>
///   <item>Classes whose namespace contains ".Grpc" — generated files themselves.</item>
///   <item>Classes whose name ends with "Base" — they are themselves base layers.</item>
///   <item>Compiler-generated types (name starts with '&lt;').</item>
/// </list>
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MissingSiteGrpcServiceAttributeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>MSP021 diagnostic identifier.</summary>
    public const string DiagnosticId = "MSP021";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "gRPC service missing [SiteGrpcService] attribute",
        messageFormat: "Class '{0}' derives from gRPC base class '{1}' but is missing [SiteGrpcService(\"SITE_ID\")]. Add the attribute or remove the per-site proto inheritance.",
        category: "Muonroi.SiteProfile",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A class deriving from a gRPC-generated *Base class must be decorated with [SiteGrpcService] so MapSiteGrpcServices() can auto-discover it.");

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
            var compilation = compilationContext.Compilation;

            // Resolve SiteGrpcServiceAttribute — if not present, this analyzer is not applicable
            var siteGrpcServiceAttribute = compilation.GetTypeByMetadataName(
                "Muonroi.Tenancy.SiteProfile.Grpc.SiteGrpcServiceAttribute");

            if (siteGrpcServiceAttribute is null) return;

            compilationContext.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var classDecl = (ClassDeclarationSyntax)syntaxContext.Node;
                var model = syntaxContext.SemanticModel;

                if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
                    return;

                // Skip abstract classes — intermediate gRPC base layers
                if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Interface)
                    return;

                // Skip compiler-generated types
                if (symbol.Name.Length > 0 && symbol.Name[0] == '<')
                    return;

                // Skip classes whose name ends with "Base" — they are base layers themselves
                if (symbol.Name.EndsWith("Base", System.StringComparison.Ordinal))
                    return;

                // Skip classes in a ".Grpc" namespace — generated files
                var ownNamespace = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                if (ownNamespace.IndexOf(".Grpc", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return;

                // Check if the class already has [SiteGrpcService]
                bool hasAttribute = false;
                foreach (var attr in symbol.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, siteGrpcServiceAttribute))
                    {
                        hasAttribute = true;
                        break;
                    }
                }
                if (hasAttribute) return;

                // Walk base type chain looking for a gRPC *Base class
                INamedTypeSymbol? grpcBase = FindGrpcBaseType(symbol);
                if (grpcBase is null) return;

                syntaxContext.ReportDiagnostic(
                    Diagnostic.Create(
                        Rule,
                        classDecl.Identifier.GetLocation(),
                        symbol.Name,
                        grpcBase.Name));

            }, SyntaxKind.ClassDeclaration);
        });
    }

    /// <summary>
    /// Walks the base type chain of <paramref name="symbol"/> and returns the first type
    /// that satisfies the gRPC Base class heuristic, or <c>null</c> if none found.
    /// </summary>
    /// <remarks>
    /// gRPC Base heuristic:
    /// <list type="bullet">
    ///   <item>Base type name ends with "Base".</item>
    ///   <item>AND the base type's containing namespace contains ".Grpc" (case-insensitive),
    ///         OR the base type is nested inside an outer class whose name equals the base
    ///         class name with "Base" stripped (e.g., outer class "TciFcd" contains nested
    ///         class "TciFcdBase" — standard gRPC codegen pattern).</item>
    /// </list>
    /// </remarks>
    private static INamedTypeSymbol? FindGrpcBaseType(INamedTypeSymbol symbol)
    {
        var current = symbol.BaseType;

        while (current is not null
               && current.SpecialType != SpecialType.System_Object)
        {
            if (IsGrpcBaseClass(current))
                return current;

            current = current.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="type"/> satisfies the gRPC Base class heuristic.
    /// </summary>
    private static bool IsGrpcBaseClass(INamedTypeSymbol type)
    {
        if (!type.Name.EndsWith("Base", System.StringComparison.Ordinal))
            return false;

        // Check namespace contains ".Grpc"
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ns.IndexOf(".Grpc", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        // Check gRPC nested class codegen pattern: outer class name == base class name minus "Base"
        // e.g., outer class "TciFullContainerDelivery" contains nested "TciFullContainerDeliveryBase"
        var outerClass = type.ContainingType;
        if (outerClass is not null)
        {
            var expectedSuffix = type.Name.Substring(0, type.Name.Length - 4); // strip "Base"
            if (outerClass.Name.Equals(expectedSuffix, System.StringComparison.Ordinal))
                return true;

            // Also check outer class's namespace for ".Grpc"
            var outerNs = outerClass.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (outerNs.IndexOf(".Grpc", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
}
