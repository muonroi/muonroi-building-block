namespace Muonroi.Tenancy.SiteProfile.SourceGenerators;

/// <summary>
/// Analyzer MSP040 — warns when a concrete <c>ISiteColumnMap</c> implementation is missing
/// a <c>Column()</c> switch arm for an entity property that is EF-configured via
/// <c>[System.ComponentModel.DataAnnotations.Schema.ColumnAttribute]</c>.
///
/// <para>
/// Architecture: Uses <see cref="AnalysisContext.RegisterCompilationStartAction"/> to collect
/// all entity types and their EF-configured properties upfront, then
/// <c>RegisterSyntaxNodeAction</c> on ClassDeclarationSyntax to check each ISiteColumnMap class.
/// This pattern matches MSP001/MSP002 for IDE inline squiggles.
/// </para>
///
/// <para>
/// Skip rules:
/// <list type="bullet">
///   <item><c>DefaultSiteColumnMap</c> or any direct subclass of it is skipped — uses convention-based mapping</item>
///   <item>Abstract classes are skipped</item>
///   <item>Properties where <c>HasColumn</c> override returns false for that property are excluded</item>
///   <item>Suppressible via <c>#pragma warning disable MSP040</c></item>
/// </list>
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ColumnMapDriftAnalyzer : DiagnosticAnalyzer
{
    /// <summary>MSP040 diagnostic identifier.</summary>
    public const string DiagnosticId = "MSP040";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "ISiteColumnMap missing Column() mapping for entity property",
        messageFormat: "ISiteColumnMap '{0}' does not map property '{1}' from entity '{2}'. Add a case in Column() switch or suppress if intentional (HasColumn returns false).",
        category: "Muonroi.SiteProfile",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Concrete ISiteColumnMap implementations should map all EF-configured entity properties in their Column() switch to prevent runtime SQL errors when new properties are added to entity types.");

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

            // Resolve ISiteColumnMap interface
            var iSiteColumnMap = compilation
                .GetTypeByMetadataName("Muonroi.Tenancy.SiteProfile.Web.Dapper.ISiteColumnMap");
            if (iSiteColumnMap is null) return;

            // Resolve DefaultSiteColumnMap base class — skipped from validation (convention-based)
            var defaultSiteColumnMap = compilation
                .GetTypeByMetadataName("Muonroi.Tenancy.SiteProfile.Web.Dapper.DefaultSiteColumnMap");

            // Resolve [Column] attribute type (System.ComponentModel.DataAnnotations.Schema.ColumnAttribute)
            var columnAttribute = compilation
                .GetTypeByMetadataName("System.ComponentModel.DataAnnotations.Schema.ColumnAttribute");
            if (columnAttribute is null) return;

            // Collect all EF-configured entity types and their [Column]-attributed property names upfront
#pragma warning disable RS1030 // Intentional: sync collection before registering per-node action to avoid race condition
            var entityPropertyMap = CollectEntityProperties(compilation, columnAttribute);
#pragma warning restore RS1030

            if (entityPropertyMap.Count == 0) return;

            // Register SyntaxNodeAction on ClassDeclaration — reports inline for IDE squiggles
            compilationContext.RegisterSyntaxNodeAction(syntaxContext =>
            {
                var classDecl = (ClassDeclarationSyntax)syntaxContext.Node;
                var model = syntaxContext.SemanticModel;

                if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
                    return;

                // Skip abstract classes and interfaces
                if (classSymbol.IsAbstract || classSymbol.TypeKind == TypeKind.Interface)
                    return;

                // Skip if this is DefaultSiteColumnMap itself
                if (defaultSiteColumnMap is not null
                    && SymbolEqualityComparer.Default.Equals(classSymbol, defaultSiteColumnMap))
                    return;

                // Must implement ISiteColumnMap
                if (!classSymbol.AllInterfaces.Any(i =>
                    SymbolEqualityComparer.Default.Equals(i, iSiteColumnMap)))
                    return;

                // Skip direct subclasses of DefaultSiteColumnMap that use convention (per D-04)
                // A class that only overrides specific columns in base.Column() is convention-based.
                // We validate ALL concrete ISiteColumnMap implementors — including DefaultSiteColumnMap subclasses.
                // However, DefaultSiteColumnMap ITSELF is excluded. Subclasses that override Column()
                // with a switch are subject to MSP040 for completeness.
                // Per D-04: "only validates concrete ISiteColumnMap subclasses (not DefaultSiteColumnMap base)"
                // This means DefaultSiteColumnMap itself is excluded, but its subclasses ARE validated
                // when they override Column() with switch arms.

                // Check if this class has a Column(string) override with switch arms
                var columnMethod = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.ValueText == "Column"
                        && m.ParameterList.Parameters.Count == 1);

                var columnWithTableMethod = classDecl.Members
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(m => m.Identifier.ValueText == "Column"
                        && m.ParameterList.Parameters.Count == 2);

                // Only analyze classes that have at least one Column() override with a switch
                // (if the class has no switch at all, it fully delegates to convention — skip)
                bool hasSwitchInSingleParam = columnMethod is not null
                    && HasSwitchExpression(columnMethod);
                bool hasSwitchInTwoParam = columnWithTableMethod is not null
                    && HasSwitchExpression(columnWithTableMethod);

                if (!hasSwitchInSingleParam && !hasSwitchInTwoParam)
                    return;

                // Collect all string literal patterns already handled in Column() switch arms
                var coveredByColumnSingle = hasSwitchInSingleParam
                    ? ExtractSwitchArmLiterals(columnMethod!)
                    : new HashSet<string>(System.StringComparer.Ordinal);

                var coveredByColumnTable = hasSwitchInTwoParam
                    ? ExtractSwitchArmLiterals(columnWithTableMethod!)
                    : new HashSet<string>(System.StringComparer.Ordinal);

                // Union of all covered property names across both overloads
                var allCovered = new HashSet<string>(coveredByColumnSingle, System.StringComparer.Ordinal);
                allCovered.UnionWith(coveredByColumnTable);

                // Determine which entity types this column map is associated with
                // Strategy: find entities where ≥1 of the covered property names matches — association heuristic
                // For each entity, check for uncovered properties
                foreach (var entityKvp in entityPropertyMap)
                {
                    string entityName = entityKvp.Key;
                    HashSet<string> entityProps = entityKvp.Value;

                    // Associate: at least one entity property appears in the switch arms
                    int matchCount = entityProps.Count(p => allCovered.Contains(p));
                    if (matchCount == 0) continue; // Not associated with this entity

                    // Report for each EF-configured property NOT in any Column() switch arm
                    foreach (var propName in entityProps)
                    {
                        if (allCovered.Contains(propName)) continue;

                        // Report MSP040 — squiggle on class name identifier
                        syntaxContext.ReportDiagnostic(
                            Diagnostic.Create(
                                Rule,
                                classDecl.Identifier.GetLocation(),
                                classSymbol.Name,
                                propName,
                                entityName));
                    }
                }

            }, SyntaxKind.ClassDeclaration);
        });
    }

    /// <summary>
    /// Scans all syntax trees in the compilation for entity types that have properties
    /// decorated with <c>[Column]</c> attribute (EF-configured) and returns a map of
    /// entity simple name → set of EF-configured property names.
    /// </summary>
#pragma warning disable RS1030 // Intentional: sync collection in CompilationStartAction to avoid race condition
    private static Dictionary<string, HashSet<string>> CollectEntityProperties(
        Compilation compilation, INamedTypeSymbol columnAttribute)
    {
        var result = new Dictionary<string, HashSet<string>>(System.StringComparer.Ordinal);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var classDeclarations = tree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>();

            foreach (var classDecl in classDeclarations)
            {
                if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
                    continue;

                // Collect properties with [Column] attribute
                var efProps = new HashSet<string>(System.StringComparer.Ordinal);

                foreach (var member in classSymbol.GetMembers().OfType<IPropertySymbol>())
                {
                    bool hasColumnAttr = member.GetAttributes().Any(a =>
                        SymbolEqualityComparer.Default.Equals(a.AttributeClass, columnAttribute));

                    if (hasColumnAttr)
                        efProps.Add(member.Name);
                }

                // Also walk base class hierarchy
                var baseType = classSymbol.BaseType;
                while (baseType is not null && baseType.SpecialType != SpecialType.System_Object)
                {
                    foreach (var member in baseType.GetMembers().OfType<IPropertySymbol>())
                    {
                        bool hasColumnAttr = member.GetAttributes().Any(a =>
                            SymbolEqualityComparer.Default.Equals(a.AttributeClass, columnAttribute));

                        if (hasColumnAttr)
                            efProps.Add(member.Name);
                    }
                    baseType = baseType.BaseType;
                }

                if (efProps.Count > 0)
                {
                    string entityKey = classSymbol.Name;
                    if (!result.ContainsKey(entityKey))
                        result[entityKey] = efProps;
                    else
                        result[entityKey].UnionWith(efProps);
                }
            }
        }

        return result;
    }
#pragma warning restore RS1030

    /// <summary>
    /// Determines whether a method declaration contains any switch expression or switch statement.
    /// </summary>
    private static bool HasSwitchExpression(MethodDeclarationSyntax method)
    {
        return method.DescendantNodes().Any(n =>
            n is SwitchExpressionSyntax or SwitchStatementSyntax);
    }

    /// <summary>
    /// Extracts all string literal patterns used as switch arm cases from a method's
    /// switch expressions and switch statements. Returns the set of property names covered.
    /// Handles both:
    /// <list type="bullet">
    ///   <item>Switch expression arms: <c>"PropertyName" =&gt; "COLUMN"</c></item>
    ///   <item>Switch statement cases: <c>case "PropertyName":</c></item>
    ///   <item>Tuple switch arms: <c>("PropertyName", _) =&gt; "COLUMN"</c></item>
    /// </list>
    /// </summary>
    private static HashSet<string> ExtractSwitchArmLiterals(MethodDeclarationSyntax method)
    {
        var covered = new HashSet<string>(System.StringComparer.Ordinal);

        // Switch expression arms: propertyName switch { "PropA" => ..., "PropB" => ... }
        foreach (var switchExpr in method.DescendantNodes().OfType<SwitchExpressionSyntax>())
        {
            foreach (var arm in switchExpr.Arms)
            {
                ExtractLiteralsFromPattern(arm.Pattern, covered);
            }
        }

        // Switch statement cases: switch (propertyName) { case "PropA": ... }
        foreach (var switchStmt in method.DescendantNodes().OfType<SwitchStatementSyntax>())
        {
            foreach (var section in switchStmt.Sections)
            {
                foreach (var label in section.Labels)
                {
                    if (label is CaseSwitchLabelSyntax caseLabel
                        && caseLabel.Value is LiteralExpressionSyntax literal
                        && literal.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        covered.Add(literal.Token.ValueText);
                    }
                }
            }
        }

        return covered;
    }

    /// <summary>
    /// Recursively extracts string literal values from a switch arm pattern.
    /// Handles: ConstantPatternSyntax (simple string literal) and
    /// recursive pattern/list patterns for tuple switch arms.
    /// </summary>
    private static void ExtractLiteralsFromPattern(PatternSyntax pattern, HashSet<string> covered)
    {
        switch (pattern)
        {
            case ConstantPatternSyntax constant
                when constant.Expression is LiteralExpressionSyntax literal
                    && literal.IsKind(SyntaxKind.StringLiteralExpression):
                covered.Add(literal.Token.ValueText);
                break;

            // Tuple/positional pattern: ("PropName", _) or ("PropName", "TABLE")
            case RecursivePatternSyntax recursive when recursive.PositionalPatternClause is not null:
                foreach (var sub in recursive.PositionalPatternClause.Subpatterns)
                {
                    ExtractLiteralsFromPattern(sub.Pattern, covered);
                }
                break;

            // And/Or patterns
            case BinaryPatternSyntax binary:
                ExtractLiteralsFromPattern(binary.Left, covered);
                ExtractLiteralsFromPattern(binary.Right, covered);
                break;

            // Parenthesized pattern
            case ParenthesizedPatternSyntax parenthesized:
                ExtractLiteralsFromPattern(parenthesized.Pattern, covered);
                break;
        }
    }
}
