namespace Muonroi.RuleGen.Services;

internal static class RuleTypeDiscoveryService
{
    public static IReadOnlyList<DiscoveredRuleType> Discover(string rulesDir)
    {
        if (!Directory.Exists(rulesDir))
        {
            throw new DirectoryNotFoundException($"Rules directory not found: {rulesDir}");
        }

        List<DiscoveredRuleType> list = [];
        foreach (string file in Directory.EnumerateFiles(rulesDir, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();

            foreach (ClassDeclarationSyntax classNode in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (classNode.BaseList is null)
                {
                    continue;
                }

                foreach (BaseTypeSyntax baseType in classNode.BaseList.Types)
                {
                    if (TryResolveRuleContext(baseType.Type, out string? contextType))
                    {
                        string className = classNode.Identifier.ValueText;
                        string? ns = classNode.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
                        string fullType = string.IsNullOrWhiteSpace(ns)
                            ? className
                            : $"{ns}.{className}";
                        list.Add(new DiscoveredRuleType(fullType, contextType!));
                        break;
                    }
                }
            }
        }

        return list
            .GroupBy(x => $"{x.ClassName}|{x.ContextType}", StringComparer.Ordinal)
            .Select(g => g.First())
            .ToArray();
    }

    private static bool TryResolveRuleContext(TypeSyntax type, out string? contextType)
    {
        contextType = null;

        if (type is GenericNameSyntax generic &&
            string.Equals(generic.Identifier.ValueText, "IRule", StringComparison.Ordinal) &&
            generic.TypeArgumentList.Arguments.Count == 1)
        {
            contextType = generic.TypeArgumentList.Arguments[0].ToString();
            return true;
        }

        if (type is QualifiedNameSyntax qualified &&
            qualified.Right is GenericNameSyntax qGeneric &&
            string.Equals(qGeneric.Identifier.ValueText, "IRule", StringComparison.Ordinal) &&
            qGeneric.TypeArgumentList.Arguments.Count == 1)
        {
            contextType = qGeneric.TypeArgumentList.Arguments[0].ToString();
            return true;
        }

        if (type is AliasQualifiedNameSyntax aliasQualified &&
            aliasQualified.Name is GenericNameSyntax aGeneric &&
            string.Equals(aGeneric.Identifier.ValueText, "IRule", StringComparison.Ordinal) &&
            aGeneric.TypeArgumentList.Arguments.Count == 1)
        {
            contextType = aGeneric.TypeArgumentList.Arguments[0].ToString();
            return true;
        }

        string raw = type.ToString();
        int start = raw.IndexOf("IRule<", StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        start += "IRule<".Length;
        int depth = 1;
        for (int i = start; i < raw.Length; i++)
        {
            char ch = raw[i];
            if (ch == '<')
            {
                depth++;
            }
            else if (ch == '>')
            {
                depth--;
                if (depth == 0)
                {
                    contextType = raw[start..i].Trim();
                    return !string.IsNullOrWhiteSpace(contextType);
                }
            }
        }

        return false;
    }
}
