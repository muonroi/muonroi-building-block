namespace Muonroi.Tenancy.SiteProfile.SourceGenerators;

/// <summary>
/// Analyzer MSP020 — warns when a class in the compiling assembly shares its simple name with
/// a proto-generated message class from a referenced shared assembly.
///
/// <para>
/// Detection strategy: at compilation start, collect all public non-abstract named types in
/// referenced assemblies whose namespace contains ".Grpc" (case-insensitive). Then for each
/// class declaration in the compiling assembly, report if the class name matches one of those
/// shared proto message names and the class is NOT itself in a ".Grpc" namespace.
/// </para>
///
/// <para>
/// Skip rules:
/// <list type="bullet">
///   <item>Abstract classes (cannot be instantiated as a proto message).</item>
///   <item>Interface types.</item>
///   <item>Compiler-generated types (name starts with '&lt;').</item>
///   <item>Classes whose own namespace contains ".Grpc" — they ARE proto-generated files.</item>
/// </list>
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateProtoMessageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>MSP020 diagnostic identifier.</summary>
    public const string DiagnosticId = "MSP020";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Duplicate proto message name",
        messageFormat: "Class '{0}' duplicates proto message name from shared assembly '{1}'. Rename or reuse the shared message.",
        category: "Muonroi.SiteProfile",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A class in a site assembly shares its name with a proto-generated message in a referenced shared assembly, causing potential deserialization ambiguity.");

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

            // Build set of shared proto message names from all referenced assemblies.
            // Key = simple type name, Value = assembly name (for diagnostic message).
            var sharedProtoNames = BuildSharedProtoNameMap(compilation);

            if (sharedProtoNames.Count == 0) return;

            compilationContext.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var classDecl = (ClassDeclarationSyntax)syntaxContext.Node;
                var model = syntaxContext.SemanticModel;

                if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
                    return;

                // Skip abstract, interfaces, and compiler-generated types
                if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Interface)
                    return;

                if (symbol.Name.Length > 0 && symbol.Name[0] == '<')
                    return;

                // Skip if this class is itself in a ".Grpc" namespace — it IS a proto file
                var ownNamespace = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                if (ownNamespace.IndexOf(".Grpc", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return;

                // Check for name collision with shared proto messages
                if (!sharedProtoNames.TryGetValue(symbol.Name, out var assemblyName))
                    return;

                syntaxContext.ReportDiagnostic(
                    Diagnostic.Create(
                        Rule,
                        classDecl.Identifier.GetLocation(),
                        symbol.Name,
                        assemblyName));

            }, SyntaxKind.ClassDeclaration);
        });
    }

    /// <summary>
    /// Iterates all referenced assembly symbols and collects simple names of public non-abstract
    /// named types whose namespace contains ".Grpc" (case-insensitive).
    /// Returns a dictionary: simple type name → first matching assembly name.
    /// </summary>
    private static Dictionary<string, string> BuildSharedProtoNameMap(Compilation compilation)
    {
        var result = new Dictionary<string, string>(System.StringComparer.Ordinal);

        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assemblySymbol)
                continue;

            CollectGrpcTypesFromAssembly(assemblySymbol.GlobalNamespace, assemblySymbol.Name, result);
        }

        return result;
    }

    /// <summary>
    /// Recursively walks the namespace tree of an assembly and collects public non-abstract
    /// named types whose full namespace contains ".Grpc" (case-insensitive).
    /// </summary>
    private static void CollectGrpcTypesFromAssembly(
        INamespaceSymbol ns,
        string assemblyName,
        Dictionary<string, string> result)
    {
        var nsDisplay = ns.ToDisplayString();

        foreach (var type in ns.GetTypeMembers())
        {
            if (type.DeclaredAccessibility != Accessibility.Public)
                continue;
            if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
                continue;
            if (type.Name.Length > 0 && type.Name[0] == '<')
                continue;

            // Only collect types whose namespace contains ".Grpc"
            if (nsDisplay.IndexOf(".Grpc", System.StringComparison.OrdinalIgnoreCase) >= 0
                || nsDisplay.Equals("Grpc", System.StringComparison.OrdinalIgnoreCase))
            {
                if (!result.ContainsKey(type.Name))
                    result[type.Name] = assemblyName;
            }
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            CollectGrpcTypesFromAssembly(childNs, assemblyName, result);
        }
    }
}
