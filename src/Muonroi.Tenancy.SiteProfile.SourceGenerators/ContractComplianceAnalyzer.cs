namespace Muonroi.Tenancy.SiteProfile.SourceGenerators;

/// <summary>
/// Analyzer MSP002 — warns when a non-generated ISiteProfile class is missing a keyed DI registration
/// for a service type registered via <c>AddSiteResolvedService&lt;T&gt;()</c>.
///
/// <para>
/// Architecture: Uses <see cref="AnalysisContext.RegisterCompilationStartAction"/> to synchronously
/// collect all <c>AddSiteResolvedService&lt;T&gt;()</c> types from the compilation upfront, then
/// RegisterSyntaxNodeAction on ClassDeclarationSyntax to check each ISiteProfile class inline.
/// This ensures IDE squiggles appear on the class name (not just in Error List).
/// </para>
///
/// <para>
/// Skip rule: classes decorated with <c>[GenerateSiteProfile]</c> are excluded — those sites use
/// the generated <c>RegisterAdditionalServices</c> partial method for consumer-specific keyed DI.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ContractComplianceAnalyzer : DiagnosticAnalyzer
{
    /// <summary>MSP002 diagnostic identifier.</summary>
    public const string DiagnosticId = "MSP002";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Site missing keyed service registration",
        messageFormat: "Site '{0}' does not register keyed service for '{1}'. Add services.AddKeyedScoped<{1}, {2}>(\"{0}\") in RegisterServices().",
        category: "Muonroi.SiteProfile",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Each ISiteProfile.RegisterServices() must call AddKeyedScoped/AddKeyedSingleton/AddKeyedTransient for every service type registered via AddSiteResolvedService<T>().");

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

            var iSiteProfile = compilation
                .GetTypeByMetadataName("Muonroi.Tenancy.SiteProfile.ISiteProfile");
            if (iSiteProfile is null) return;

            var generateSiteProfileAttribute = compilation
                .GetTypeByMetadataName("Muonroi.Tenancy.SiteProfile.GenerateSiteProfileAttribute");

            // Collect ALL AddSiteResolvedService<T> types upfront (sync, no race condition)
#pragma warning disable RS1030 // Intentional: sync collection before registering per-node action
            var requiredServiceTypes = CollectRequiredServiceTypes(compilation);
#pragma warning restore RS1030

            if (requiredServiceTypes.Count == 0) return;

            // Check each class declaration — reports inline for IDE squiggles
            compilationContext.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var classDecl = (ClassDeclarationSyntax)syntaxContext.Node;
                var model = syntaxContext.SemanticModel;

                if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
                    return;

                if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Interface)
                    return;

                if (!symbol.AllInterfaces.Any(i =>
                    SymbolEqualityComparer.Default.Equals(i, iSiteProfile)))
                    return;

                // Skip [GenerateSiteProfile]-decorated classes
                if (generateSiteProfileAttribute is not null
                    && symbol.GetAttributes().Any(a =>
                        SymbolEqualityComparer.Default.Equals(a.AttributeClass, generateSiteProfileAttribute)))
                    return;

                string? siteIdValue = ExtractSiteIdLiteralValue(symbol);
                if (siteIdValue is null) return;

                // Walk RegisterServices() body for AddKeyed* calls
                var registeredTypes = new HashSet<string>(System.StringComparer.Ordinal);
                var registerMethod = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.ValueText == "RegisterServices");

                if (registerMethod is not null)
                {
                    foreach (var invocation in registerMethod.DescendantNodes()
                        .OfType<InvocationExpressionSyntax>())
                    {
                        if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodSymbol)
                            continue;

                        if (!methodSymbol.Name.StartsWith("AddKeyed", System.StringComparison.Ordinal))
                            continue;

                        if (methodSymbol.TypeArguments.Length >= 1)
                        {
                            registeredTypes.Add(methodSymbol.TypeArguments[0]
                                .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                        }
                    }
                }

                // Report missing registrations — squiggle on class name
                foreach (var required in requiredServiceTypes)
                {
                    if (registeredTypes.Contains(required.Key)) continue;

                    string interfaceShortName = GetShortName(required.Key);
                    string siteIdPascal = ToPascalCase(siteIdValue);
                    string suggestedImpl = siteIdPascal + StripLeadingI(interfaceShortName);

                    syntaxContext.ReportDiagnostic(
                        Diagnostic.Create(
                            Rule,
                            classDecl.Identifier.GetLocation(),
                            siteIdValue,
                            interfaceShortName,
                            suggestedImpl));
                }

            }, SyntaxKind.ClassDeclaration);
        });
    }

    /// <summary>
    /// Scans all syntax trees for <c>AddSiteResolvedService&lt;T&gt;()</c> invocations
    /// and returns the set of required service type names.
    /// </summary>
#pragma warning disable RS1030 // Intentional: sync collection in CompilationStartAction to avoid race condition
    private static Dictionary<string, bool> CollectRequiredServiceTypes(Compilation compilation)
    {
        var result = new Dictionary<string, bool>(System.StringComparer.Ordinal);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);

            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol methodSymbol)
                    continue;

                if (!methodSymbol.Name.Equals("AddSiteResolvedService", System.StringComparison.Ordinal))
                    continue;

                if (methodSymbol.TypeArguments.Length >= 1)
                {
                    var typeName = methodSymbol.TypeArguments[0]
                        .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    if (!result.ContainsKey(typeName))
                        result[typeName] = true;
                }
            }
        }

        return result;
    }
#pragma warning restore RS1030

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

    /// <summary>Gets the short (unqualified) type name from a fully-qualified name.</summary>
    private static string GetShortName(string fullyQualifiedName)
    {
        int lastDot = fullyQualifiedName.LastIndexOf('.');
        string raw = lastDot >= 0 ? fullyQualifiedName.Substring(lastDot + 1) : fullyQualifiedName;
        const string globalPrefix = "global::";
        if (raw.StartsWith(globalPrefix, System.StringComparison.Ordinal))
            raw = raw.Substring(globalPrefix.Length);
        return raw;
    }

    /// <summary>Strips a leading 'I' from an interface name (e.g., IMyService -> MyService).</summary>
    private static string StripLeadingI(string name)
    {
        if (name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
            return name.Substring(1);
        return name;
    }

    /// <summary>Converts a siteId to PascalCase (e.g., "tci" -> "Tci", "site-01" -> "Site01").</summary>
    private static string ToPascalCase(string siteId)
    {
        if (string.IsNullOrEmpty(siteId)) return siteId;
        var sb = new System.Text.StringBuilder(siteId.Length);
        bool capitalizeNext = true;
        foreach (char c in siteId)
        {
            if (!char.IsLetterOrDigit(c))
            {
                capitalizeNext = true;
                continue;
            }
            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }
        return sb.ToString();
    }
}
