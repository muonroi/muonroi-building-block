using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Linq;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators;

/// <summary>
/// Analyzer MSP041 — hints when a concrete <c>ISiteProfile</c> implementation is found
/// in a referenced (external) assembly but the current project may not have listed
/// that assembly in <c>SiteAssemblies</c> within <c>AddSiteInfrastructure</c> /
/// <c>AddMultiSiteProfiles</c>.
///
/// <para>
/// Architecture: Uses <see cref="AnalysisContext.RegisterCompilationAction"/> to scan
/// all referenced assembly symbols for non-abstract ISiteProfile implementations.
/// Reports at <c>Location.None</c> (compilation level) because the type is defined
/// in a referenced assembly — no syntax node is available in the current compilation.
/// </para>
///
/// <para>
/// Skip rules:
/// <list type="bullet">
///   <item>ISiteProfile implementations declared in the current compilation's own assembly are skipped</item>
///   <item>Abstract classes are skipped</item>
///   <item>The ISiteProfile interface itself is skipped</item>
/// </list>
/// </para>
///
/// <para>
/// Severity: <c>Info</c> — purely informational. Reminds developers to verify that external
/// site assemblies are included in <c>SiteAssemblies</c> configuration (per D-16).
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AssemblyIsolationHintAnalyzer : DiagnosticAnalyzer
{
    /// <summary>MSP041 diagnostic identifier.</summary>
    public const string DiagnosticId = "MSP041";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "ISiteProfile in external assembly may not be discovered",
        messageFormat: "ISiteProfile '{0}' is in referenced assembly '{1}' but may not be included in SiteAssemblies. Add the assembly to AddSiteInfrastructure(options => options.SiteAssemblies = [...]) to ensure discovery.",
        category: "Muonroi.SiteProfile",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "ISiteProfile implementations in referenced assemblies must be explicitly listed in SiteAssemblies to be discovered at runtime. This hint fires whenever an external concrete ISiteProfile is detected so developers can verify their configuration.",
        customTags: ["CompilationEnd"]);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationAction(compilationContext =>
        {
            var compilation = compilationContext.Compilation;

            // Resolve ISiteProfile interface by metadata name
            var iSiteProfile = compilation
                .GetTypeByMetadataName("Muonroi.Tenancy.SiteProfile.ISiteProfile");
            if (iSiteProfile is null) return;

            // Get the name of the current assembly to skip types defined here
            string? currentAssemblyName = compilation.Assembly.Name;

            // The fully-qualified metadata name of ISiteProfile — used for name-based matching
            // across assembly boundaries where symbol equality may not hold due to separate compilations.
            const string iSiteProfileMetadataName = "Muonroi.Tenancy.SiteProfile.ISiteProfile";

            // Scan all referenced assembly symbols for concrete ISiteProfile implementations
            foreach (var reference in compilation.References)
            {
                var symbol = compilation.GetAssemblyOrModuleSymbol(reference);

                if (symbol is not IAssemblySymbol assemblySymbol)
                    continue;

                // Skip the current assembly's own symbol (redundant guard — references are external)
                if (assemblySymbol.Name == currentAssemblyName)
                    continue;

                // Scan all types in this referenced assembly
                foreach (var type in GetAllTypes(assemblySymbol.GlobalNamespace))
                {
                    // Skip interfaces and abstract classes
                    if (type.TypeKind == TypeKind.Interface || type.IsAbstract)
                        continue;

                    // Check if this type implements ISiteProfile using name-based matching.
                    // Symbol equality may not hold across separate compilations (test projects compile
                    // each assembly independently), so we match by fully-qualified metadata name.
                    bool implementsISiteProfile = type.AllInterfaces.Any(i =>
                        i.ToDisplayString() == iSiteProfileMetadataName
                        || SymbolEqualityComparer.Default.Equals(i, iSiteProfile));

                    if (!implementsISiteProfile)
                        continue;

                    // Concrete ISiteProfile found in a referenced assembly — report hint
                    compilationContext.ReportDiagnostic(
                        Diagnostic.Create(
                            Rule,
                            Location.None,
                            type.Name,
                            assemblySymbol.Name));
                }
            }
        });
    }

    /// <summary>
    /// Recursively enumerates all named type symbols within a namespace symbol,
    /// including nested types.
    /// </summary>
    private static System.Collections.Generic.IEnumerable<INamedTypeSymbol> GetAllTypes(
        INamespaceSymbol namespaceSymbol)
    {
        foreach (var member in namespaceSymbol.GetMembers())
        {
            if (member is INamedTypeSymbol typeSymbol)
            {
                yield return typeSymbol;

                // Recurse into nested types
                foreach (var nested in GetAllNestedTypes(typeSymbol))
                    yield return nested;
            }
            else if (member is INamespaceSymbol nestedNamespace)
            {
                foreach (var type in GetAllTypes(nestedNamespace))
                    yield return type;
            }
        }
    }

    /// <summary>
    /// Recursively enumerates nested type symbols within a named type.
    /// </summary>
    private static System.Collections.Generic.IEnumerable<INamedTypeSymbol> GetAllNestedTypes(
        INamedTypeSymbol typeSymbol)
    {
        foreach (var member in typeSymbol.GetTypeMembers())
        {
            yield return member;
            foreach (var nested in GetAllNestedTypes(member))
                yield return nested;
        }
    }
}
