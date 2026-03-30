using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators;

/// <summary>
/// Analyzer MSP023 — hints when a <c>[SiteGrpcService]</c> class directly derives from a gRPC
/// proto base class but a concrete shared service exists that it could inherit from instead,
/// enabling reuse via the OOP inheritance pattern.
///
/// <para>
/// This is Info-level (not Warning) because the separate-class pattern is valid. The diagnostic
/// is a developer hint: "there's a shared implementation you could inherit from."
/// </para>
///
/// <para>
/// Detection strategy (two-pass via CompilationEnd):
/// <list type="number">
///   <item>Pass 1 (SymbolAction): classify every named type into one of two buckets:
///     <list type="bullet">
///       <item><c>_siteServices</c>: has [SiteGrpcService] AND directly inherits from a gRPC base
///         (immediate BaseType matches gRPC heuristic)</item>
///       <item><c>_sharedServices</c>: concrete, does NOT have [SiteGrpcService], AND inherits
///         from a gRPC base (key = gRPC base type).</item>
///     </list>
///   </item>
///   <item>Pass 2 (CompilationEndAction): for each site service, check if exactly one shared
///     service inherits from the same gRPC base. If so, report MSP023 with the candidate name.
///     If zero or multiple candidates: no diagnostic (ambiguous or not applicable).</item>
/// </list>
/// </para>
///
/// <para>
/// Skip rules:
/// <list type="bullet">
///   <item>Abstract classes and interfaces.</item>
///   <item>Compiler-generated types (name starts with '&lt;').</item>
///   <item>Classes in a ".Grpc" namespace — generated code.</item>
///   <item>Classes that ALREADY inherit from a non-proto-base class which itself chains to the
///     gRPC base — they already use the inheritance pattern.</item>
/// </list>
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InheritanceHintAnalyzer : DiagnosticAnalyzer
{
    /// <summary>MSP023 diagnostic identifier.</summary>
    public const string DiagnosticId = "MSP023";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Site gRPC service could inherit from shared service",
        messageFormat: "Consider inheriting from '{0}' to reuse shared RPC implementations instead of duplicating them",
        category: "Muonroi.SiteProfile",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "A [SiteGrpcService] class that directly derives from the gRPC proto base could instead inherit from an existing shared service implementation, avoiding duplication of shared RPC logic.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

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

            // Resolve [SiteGrpcService] attribute — if not present, no-op
            var siteGrpcServiceAttribute = compilation.GetTypeByMetadataName(
                "Muonroi.Tenancy.SiteProfile.Grpc.SiteGrpcServiceAttribute");

            if (siteGrpcServiceAttribute is null) return;

            // Two concurrent bags for the two-pass approach:
            // siteServices: [SiteGrpcService] classes that directly inherit from a gRPC base
            // sharedServices: concrete classes (no [SiteGrpcService]) that inherit from a gRPC base
            // Key for both: the gRPC base type (SymbolEqualityComparer.Default)
            var siteServices = new ConcurrentBag<SiteServiceEntry>();
            var sharedServices = new ConcurrentDictionary<INamedTypeSymbol, ConcurrentBag<INamedTypeSymbol>>(
                SymbolEqualityComparer.Default);

            // Pass 1: classify every named type
            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                if (symbolContext.Symbol is not INamedTypeSymbol symbol)
                    return;

                // Skip abstract, interfaces
                if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Interface)
                    return;

                // Skip compiler-generated types
                if (symbol.Name.Length > 0 && symbol.Name[0] == '<')
                    return;

                // Skip classes in ".Grpc" namespace — generated code
                var ownNamespace = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                if (ownNamespace.IndexOf(".Grpc", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return;

                // Skip types that are themselves gRPC base classes
                if (symbol.Name.EndsWith("Base", System.StringComparison.Ordinal))
                    return;

                // Determine if the IMMEDIATE base type is a gRPC proto base
                var immediateBase = symbol.BaseType;
                if (immediateBase is null || immediateBase.SpecialType == SpecialType.System_Object)
                    return;

                bool immediateBaseIsGrpc = IsGrpcBaseClass(immediateBase);

                bool hasSiteGrpcService = HasAttribute(symbol, siteGrpcServiceAttribute);

                if (hasSiteGrpcService)
                {
                    // This is a [SiteGrpcService] class — bucket as "site service" if it
                    // directly inherits from the gRPC proto base (not from another shared class).
                    // If immediateBase is NOT a gRPC base, the class already uses the inheritance
                    // pattern (inherits from a shared service that itself chains to the gRPC base).
                    if (immediateBaseIsGrpc)
                    {
                        siteServices.Add(new SiteServiceEntry(symbol, immediateBase));
                    }
                    // else: already inheriting from shared service — no diagnostic needed
                }
                else
                {
                    // Not a site service — if it inherits from a gRPC base it may be a shared service
                    // candidate. Walk full chain to find the gRPC base (the class itself doesn't need
                    // to directly inherit from proto base to be a shared service — one hop is fine).
                    INamedTypeSymbol? grpcBase = immediateBaseIsGrpc
                        ? immediateBase
                        : FindGrpcBaseType(symbol);

                    if (grpcBase is not null)
                    {
                        var bag = sharedServices.GetOrAdd(
                            grpcBase,
                            _ => new ConcurrentBag<INamedTypeSymbol>());
                        bag.Add(symbol);
                    }
                }
            }, SymbolKind.NamedType);

            // Pass 2: emit diagnostics at compilation end
            compilationContext.RegisterCompilationEndAction(endContext =>
            {
                foreach (var entry in siteServices)
                {
                    if (!sharedServices.TryGetValue(entry.GrpcBase, out var candidates))
                        continue; // no shared service for this proto base → no hint

                    // Convert to array for count check (ConcurrentBag may have duplicates from
                    // multiple syntax trees — deduplicate by symbol identity)
                    var uniqueCandidates = new System.Collections.Generic.HashSet<INamedTypeSymbol>(
                        SymbolEqualityComparer.Default);
                    foreach (var c in candidates)
                        uniqueCandidates.Add(c);

                    if (uniqueCandidates.Count != 1)
                        continue; // zero or multiple candidates — ambiguous, no hint

                    var sharedService = System.Linq.Enumerable.First(uniqueCandidates);

                    // Report on the identifier location of the site service class
                    var locations = entry.Symbol.Locations;
                    var location = locations.Length > 0 ? locations[0] : Location.None;

                    endContext.ReportDiagnostic(
                        Diagnostic.Create(Rule, location, sharedService.Name));
                }
            });
        });
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="symbol"/> is decorated with
    /// <paramref name="attributeType"/>.
    /// </summary>
    private static bool HasAttribute(INamedTypeSymbol symbol, INamedTypeSymbol attributeType)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Walks the base type chain of <paramref name="symbol"/> and returns the first type
    /// that satisfies the gRPC Base class heuristic, or <c>null</c> if none found.
    /// </summary>
    private static INamedTypeSymbol? FindGrpcBaseType(INamedTypeSymbol symbol)
    {
        var current = symbol.BaseType;
        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            if (IsGrpcBaseClass(current))
                return current;
            current = current.BaseType;
        }
        return null;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="type"/> satisfies the gRPC Base class heuristic:
    /// <list type="bullet">
    ///   <item>Name ends with "Base"</item>
    ///   <item>AND namespace contains ".Grpc" OR nested inside a class whose name matches the
    ///     stripped base name (standard gRPC codegen pattern).</item>
    /// </list>
    /// </summary>
    private static bool IsGrpcBaseClass(INamedTypeSymbol type)
    {
        if (!type.Name.EndsWith("Base", System.StringComparison.Ordinal))
            return false;

        // Check namespace contains ".Grpc"
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (ns.IndexOf(".Grpc", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        // Check gRPC nested class codegen pattern
        // e.g., outer class "TciFullContainerDelivery" contains nested "TciFullContainerDeliveryBase"
        var outerClass = type.ContainingType;
        if (outerClass is not null)
        {
            var expectedSuffix = type.Name.Substring(0, type.Name.Length - 4); // strip "Base"
            if (outerClass.Name.Equals(expectedSuffix, System.StringComparison.Ordinal))
                return true;

            var outerNs = outerClass.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (outerNs.IndexOf(".Grpc", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Captures a site service type and its direct gRPC base for the two-pass approach.
    /// </summary>
    private readonly struct SiteServiceEntry
    {
        internal SiteServiceEntry(INamedTypeSymbol symbol, INamedTypeSymbol grpcBase)
        {
            Symbol = symbol;
            GrpcBase = grpcBase;
        }

        internal INamedTypeSymbol Symbol { get; }
        internal INamedTypeSymbol GrpcBase { get; }
    }
}
