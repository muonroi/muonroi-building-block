namespace Muonroi.RuleEngine.SourceGenerators.Authoring;

internal static class RuleAuthoringManifestExtractor
{
    public static AuthoringManifestDefinition Build(Compilation compilation, IEnumerable<MethodDeclarationSyntax> methods)
    {
        List<AuthoringRuleDefinition> rules = [];
        foreach (MethodDeclarationSyntax method in methods.Distinct())
        {
            SemanticModel model = compilation.GetSemanticModel(method.SyntaxTree);
            IMethodSymbol? methodSymbol = model.GetDeclaredSymbol(method) as IMethodSymbol;
            if (methodSymbol is null)
            {
                continue;
            }

            AttributeData? extractAttribute = methodSymbol.GetAttributes()
                .FirstOrDefault(attribute => attribute.AttributeClass?.Name.IndexOf("ExtractAsRule", StringComparison.Ordinal) >= 0);
            if (extractAttribute is null)
            {
                continue;
            }

            (string code, int order, IReadOnlyList<string> dependsOn) = ParseRuleAttribute(extractAttribute, methodSymbol.Name);
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            IParameterSymbol? contextParameter = methodSymbol.Parameters
                .FirstOrDefault(parameter => !IsFactBag(parameter.Type) && !IsCancellationToken(parameter.Type));
            AuthoringContextOverride contextOverride = ReadContextOverride(methodSymbol);
            Dictionary<string, AuthoringFactOverride> factOverrides = ReadFactOverrides(methodSymbol);
            CatalogMetadataDefinition catalog = ReadCatalogMetadata(methodSymbol);

            rules.Add(new AuthoringRuleDefinition(
                code,
                order,
                [.. dependsOn],
                contextParameter?.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                contextOverride.Title,
                contextOverride.Description,
                FactSchemaWalker.Walk(contextParameter?.Type, contextOverride.Description),
                ExtractConsumedFacts(method, model, factOverrides),
                ExtractProducedFacts(method, model, factOverrides),
                catalog.DisplayName ?? code,
                catalog.Category,
                catalog.Icon,
                catalog.Tags,
                catalog.Description,
                catalog.IsPaletteVisible ?? true));
        }

        return new AuthoringManifestDefinition(
            compilation.AssemblyName ?? "Unknown.Assembly",
            compilation.Assembly.Identity.Version?.ToString() ?? "1.0.0.0",
            [.. rules.OrderBy(rule => rule.Order).ThenBy(rule => rule.Code, StringComparer.OrdinalIgnoreCase)]);
    }

    private static List<AuthoringFactDefinition> ExtractConsumedFacts(
        MethodDeclarationSyntax method,
        SemanticModel model,
        IReadOnlyDictionary<string, AuthoringFactOverride> overrides)
    {
        Dictionary<string, AuthoringFactDefinition> facts = new(StringComparer.Ordinal);

        foreach (InvocationExpressionSyntax invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                memberAccess.Expression is not IdentifierNameSyntax objectName ||
                !string.Equals(objectName.Identifier.ValueText, "facts", StringComparison.Ordinal))
            {
                continue;
            }

            string operation = memberAccess.Name.Identifier.ValueText;
            if (!string.Equals(operation, "Get", StringComparison.Ordinal) &&
                !string.Equals(operation, "TryGet", StringComparison.Ordinal))
            {
                continue;
            }

            if (invocation.ArgumentList.Arguments.Count == 0 ||
                !TryResolveStringValue(invocation.ArgumentList.Arguments[0].Expression, model, out string key))
            {
                continue;
            }

            ITypeSymbol? typeSymbol = null;
            if (memberAccess.Name is GenericNameSyntax genericName &&
                genericName.TypeArgumentList.Arguments.Count > 0)
            {
                typeSymbol = model.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]).Type;
            }

            AddOrUpdateFact(facts, key, typeSymbol, overrides, preferExistingType: true);
        }

        return [.. facts.Values.OrderBy(fact => fact.Key, StringComparer.Ordinal)];
    }

    private static List<AuthoringFactDefinition> ExtractProducedFacts(
        MethodDeclarationSyntax method,
        SemanticModel model,
        IReadOnlyDictionary<string, AuthoringFactOverride> overrides)
    {
        Dictionary<string, AuthoringFactDefinition> facts = new(StringComparer.Ordinal);

        foreach (InvocationExpressionSyntax invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess ||
                memberAccess.Expression is not IdentifierNameSyntax objectName ||
                !string.Equals(objectName.Identifier.ValueText, "facts", StringComparison.Ordinal) ||
                !string.Equals(memberAccess.Name.Identifier.ValueText, "Set", StringComparison.Ordinal) ||
                invocation.ArgumentList.Arguments.Count < 2 ||
                !TryResolveStringValue(invocation.ArgumentList.Arguments[0].Expression, model, out string key))
            {
                continue;
            }

            ITypeSymbol? typeSymbol = null;
            if (memberAccess.Name is GenericNameSyntax genericName &&
                genericName.TypeArgumentList.Arguments.Count > 0)
            {
                typeSymbol = model.GetTypeInfo(genericName.TypeArgumentList.Arguments[0]).Type;
            }

            if (typeSymbol is null)
            {
                typeSymbol = model.GetTypeInfo(invocation.ArgumentList.Arguments[1].Expression).Type;
            }

            AddOrUpdateFact(facts, key, typeSymbol, overrides, preferExistingType: false);
        }

        foreach (AssignmentExpressionSyntax assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is not ElementAccessExpressionSyntax elementAccess ||
                elementAccess.Expression is not IdentifierNameSyntax objectName ||
                !string.Equals(objectName.Identifier.ValueText, "facts", StringComparison.Ordinal) ||
                elementAccess.ArgumentList.Arguments.Count == 0 ||
                !TryResolveStringValue(elementAccess.ArgumentList.Arguments[0].Expression, model, out string key))
            {
                continue;
            }

            AddOrUpdateFact(facts, key, model.GetTypeInfo(assignment.Right).Type, overrides, preferExistingType: false);
        }

        return [.. facts.Values.OrderBy(fact => fact.Key, StringComparer.Ordinal)];
    }

    private static void AddOrUpdateFact(
        IDictionary<string, AuthoringFactDefinition> facts,
        string key,
        ITypeSymbol? typeSymbol,
        IReadOnlyDictionary<string, AuthoringFactOverride> overrides,
        bool preferExistingType)
    {
        overrides.TryGetValue(key, out AuthoringFactOverride? metadataOverride);
        AuthoringFactDefinition next = new(
            key,
            metadataOverride?.Label ?? key,
            typeSymbol?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            metadataOverride?.Description,
            metadataOverride?.Example,
            FactSchemaWalker.Walk(typeSymbol, metadataOverride?.Description));

        if (!facts.TryGetValue(key, out AuthoringFactDefinition? existing))
        {
            facts[key] = next;
            return;
        }

        facts[key] = existing with
        {
            Label = metadataOverride?.Label ?? existing.Label ?? next.Label,
            ClrTypeName = preferExistingType ? existing.ClrTypeName ?? next.ClrTypeName : next.ClrTypeName ?? existing.ClrTypeName,
            Description = metadataOverride?.Description ?? existing.Description ?? next.Description,
            Example = metadataOverride?.Example ?? existing.Example ?? next.Example,
            Schema = preferExistingType ? existing.Schema ?? next.Schema : next.Schema ?? existing.Schema
        };
    }

    private static (string Code, int Order, IReadOnlyList<string> DependsOn) ParseRuleAttribute(AttributeData attribute, string fallbackName)
    {
        string code = fallbackName;
        int order = 0;
        List<string> dependsOn = [];

        if (attribute.ConstructorArguments.Length > 0)
        {
            code = attribute.ConstructorArguments[0].Value?.ToString() ?? fallbackName;
        }

        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, "Order", StringComparison.OrdinalIgnoreCase))
            {
                order = (int)(argument.Value.Value ?? 0);
            }
            else if (string.Equals(argument.Key, "DependsOn", StringComparison.OrdinalIgnoreCase) &&
                     argument.Value.Kind == TypedConstantKind.Array)
            {
                foreach (TypedConstant value in argument.Value.Values)
                {
                    string? item = value.Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(item))
                    {
                        dependsOn.Add(item!);
                    }
                }
            }
        }

        return (code, order, dependsOn);
    }

    private static AuthoringContextOverride ReadContextOverride(IMethodSymbol methodSymbol)
    {
        AttributeData? attribute = methodSymbol.GetAttributes()
            .FirstOrDefault(item => string.Equals(item.AttributeClass?.Name, "MRuleContextDescriptionAttribute", StringComparison.Ordinal));
        if (attribute is null)
        {
            return new AuthoringContextOverride(null, null);
        }

        string? title = null;
        string? description = null;
        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, "Title", StringComparison.Ordinal))
            {
                title = argument.Value.Value?.ToString();
            }
            else if (string.Equals(argument.Key, "Description", StringComparison.Ordinal))
            {
                description = argument.Value.Value?.ToString();
            }
        }

        return new AuthoringContextOverride(title, description);
    }

    private static Dictionary<string, AuthoringFactOverride> ReadFactOverrides(IMethodSymbol methodSymbol)
    {
        Dictionary<string, AuthoringFactOverride> overrides = new(StringComparer.Ordinal);
        foreach (AttributeData attribute in methodSymbol.GetAttributes()
                     .Where(item => string.Equals(item.AttributeClass?.Name, "MRuleFactDescriptionAttribute", StringComparison.Ordinal)))
        {
            string key = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value?.ToString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            string? label = null;
            string? description = null;
            string? example = null;
            foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
            {
                if (string.Equals(argument.Key, "Label", StringComparison.Ordinal))
                {
                    label = argument.Value.Value?.ToString();
                }
                else if (string.Equals(argument.Key, "Description", StringComparison.Ordinal))
                {
                    description = argument.Value.Value?.ToString();
                }
                else if (string.Equals(argument.Key, "Example", StringComparison.Ordinal))
                {
                    example = argument.Value.Value?.ToString();
                }
            }

            overrides[key] = new AuthoringFactOverride(key, label, description, example);
        }

        return overrides;
    }

    private static CatalogMetadataDefinition ReadCatalogMetadata(IMethodSymbol methodSymbol)
    {
        AttributeData? attribute = methodSymbol.GetAttributes()
            .FirstOrDefault(item => string.Equals(item.AttributeClass?.Name, "MRuleCatalogEntryAttribute", StringComparison.Ordinal))
            ?? methodSymbol.ContainingType.GetAttributes()
                .FirstOrDefault(item => string.Equals(item.AttributeClass?.Name, "MRuleCatalogEntryAttribute", StringComparison.Ordinal));
        if (attribute is null)
        {
            return new CatalogMetadataDefinition(null, null, null, [], null, null);
        }

        string? displayName = null;
        string? category = null;
        string? icon = null;
        List<string> tags = [];
        string? description = null;
        bool? isPaletteVisible = null;

        foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
        {
            if (string.Equals(argument.Key, "DisplayName", StringComparison.Ordinal))
            {
                displayName = argument.Value.Value?.ToString();
            }
            else if (string.Equals(argument.Key, "Category", StringComparison.Ordinal))
            {
                category = argument.Value.Value?.ToString();
            }
            else if (string.Equals(argument.Key, "Icon", StringComparison.Ordinal))
            {
                icon = argument.Value.Value?.ToString();
            }
            else if (string.Equals(argument.Key, "Tags", StringComparison.Ordinal) && argument.Value.Kind == TypedConstantKind.Array)
            {
                foreach (TypedConstant value in argument.Value.Values)
                {
                    string? tag = value.Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(tag))
                    {
                        tags.Add(tag!);
                    }
                }
            }
            else if (string.Equals(argument.Key, "Description", StringComparison.Ordinal))
            {
                description = argument.Value.Value?.ToString();
            }
            else if (string.Equals(argument.Key, "IsPaletteVisible", StringComparison.Ordinal) && argument.Value.Value is bool visible)
            {
                isPaletteVisible = visible;
            }
        }

        return new CatalogMetadataDefinition(displayName, category, icon, tags, description, isPaletteVisible);
    }

    private static bool TryResolveStringValue(ExpressionSyntax expression, SemanticModel model, out string value)
    {
        Optional<object?> constantValue = model.GetConstantValue(expression);
        if (constantValue.HasValue && constantValue.Value is string stringValue)
        {
            value = stringValue;
            return true;
        }

        if (expression is LiteralExpressionSyntax literal &&
            literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
        {
            value = literal.Token.ValueText;
            return true;
        }

        ISymbol? symbol = model.GetSymbolInfo(expression).Symbol;
        if (symbol is IFieldSymbol fieldSymbol && fieldSymbol.IsConst && fieldSymbol.ConstantValue is string constString)
        {
            value = constString;
            return true;
        }

        if (expression is InvocationExpressionSyntax invocation &&
            invocation.Expression is IdentifierNameSyntax identifier &&
            string.Equals(identifier.Identifier.ValueText, "nameof", StringComparison.Ordinal) &&
            invocation.ArgumentList.Arguments.Count > 0)
        {
            value = invocation.ArgumentList.Arguments[0].Expression.ToString().Split('.').Last();
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool IsFactBag(ITypeSymbol typeSymbol)
    {
        return typeSymbol.Name.IndexOf("FactBag", StringComparison.Ordinal) >= 0;
    }

    private static bool IsCancellationToken(ITypeSymbol typeSymbol)
    {
        return typeSymbol.Name.IndexOf("CancellationToken", StringComparison.Ordinal) >= 0;
    }
}
