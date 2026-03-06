using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Muonroi.RuleGen.Models;

namespace Muonroi.RuleGen.Services;

internal sealed class RoslynRuleExtractor
{
    public static async Task<IReadOnlyList<ExtractedRuleDefinition>> ExtractAsync(
        IReadOnlyList<string> sourceFiles,
        string outputNamespace,
        string? contextOverride,
        bool useParallel,
        CancellationToken cancellationToken)
    {
        if (sourceFiles.Count == 0)
        {
            return [];
        }

        IEnumerable<SyntaxTree> syntaxTrees = await Task.WhenAll(sourceFiles.Select(async file =>
            CSharpSyntaxTree.ParseText(await File.ReadAllTextAsync(file, cancellationToken), path: file, cancellationToken: cancellationToken)));

        CSharpCompilation compilation = CSharpCompilation.Create(
            "MuonroiRuleAnalysis",
            syntaxTrees: syntaxTrees,
            references: GetMetadataReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        List<ExtractedRuleDefinition> rules = [];
        object gate = new();

        if (useParallel)
        {
            await Parallel.ForEachAsync(syntaxTrees, cancellationToken, (tree, ct) =>
            {
                SemanticModel model = compilation.GetSemanticModel(tree);
                IReadOnlyList<ExtractedRuleDefinition> extracted = ExtractFromSource(tree, model, outputNamespace, contextOverride);
                lock (gate)
                {
                    rules.AddRange(extracted);
                }
                return ValueTask.CompletedTask;
            });
        }
        else
        {
            foreach (SyntaxTree tree in syntaxTrees)
            {
                cancellationToken.ThrowIfCancellationRequested();
                SemanticModel model = compilation.GetSemanticModel(tree);
                rules.AddRange(ExtractFromSource(tree, model, outputNamespace, contextOverride));
            }
        }

        return rules
            .OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.MethodName, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        return
        [
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
            MetadataReference.CreateFromFile(AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "System.Runtime").Location)
        ];
    }

    private static IReadOnlyList<ExtractedRuleDefinition> ExtractFromSource(
        SyntaxTree tree,
        SemanticModel model,
        string outputNamespace,
        string? contextOverride)
    {
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

        string[] usings = [.. root.Usings
            .Select(x => x.ToString().Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)];

        List<ExtractedRuleDefinition> rules = [];
        foreach (ClassDeclarationSyntax classNode in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            string className = classNode.Identifier.ValueText;
            if (string.IsNullOrWhiteSpace(className))
            {
                continue;
            }

            string? ns = classNode.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
            Dictionary<string, string> fieldMap = BuildFieldMap(classNode);
            IEnumerable<MethodDeclarationSyntax> methods = classNode.Members.OfType<MethodDeclarationSyntax>();

            foreach (MethodDeclarationSyntax method in methods)
            {
                AttributeSyntax? extractAttr = FindExtractAttribute(method);
                if (extractAttr is null)
                {
                    continue;
                }

                (string Code, int Order, string HookPoint, IReadOnlyList<string> DependsOn) = ParseExtractAttribute(extractAttr, model);
                if (string.IsNullOrWhiteSpace(Code))
                {
                    continue;
                }

                ParameterModel[] parameters = [.. method.ParameterList.Parameters.Select(MapParameter)];

                string? contextType = contextOverride;
                if (string.IsNullOrWhiteSpace(contextType))
                {
                    ParameterModel? contextParam = parameters.FirstOrDefault(p =>
                        !IsFactBagType(p.TypeName) &&
                        !IsCancellationTokenType(p.TypeName));
                    contextType = contextParam?.TypeName ?? "object";
                }

                string[] customAttributes = [.. method.AttributeLists
                    .SelectMany(x => x.Attributes)
                    .Where(a => !IsExtractAttribute(a))
                    .Select(a => $"[{a}]")];

                IReadOnlyList<HelperMethodDefinition> helperMethods = ExtractHelperMethods(method, classNode, model);
                IReadOnlyList<ServiceDependency> usedDependencies = ExtractDependencies(
                    method,
                    classNode,
                    fieldMap,
                    model,
                    helperMethods.Select(x => x.MethodName).ToArray());

                int line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

                rules.Add(new ExtractedRuleDefinition(
                    Code,
                    method.Identifier.ValueText,
                    className,
                    ns,
                    outputNamespace,
                    contextType,
                    method.ReturnType.ToString(),
                    Order,
                    HookPoint,
                    DependsOn,
                    usings,
                    customAttributes,
                    parameters,
                    usedDependencies,
                    helperMethods,
                    ExtractXmlComment(method),
                    method.Body?.ToFullString().Trim(),
                    method.ExpressionBody?.Expression.ToString(),
                    method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)),
                    tree.FilePath,
                    line));
            }
        }

        return rules;
    }

    private static IReadOnlyList<HelperMethodDefinition> ExtractHelperMethods(
        MethodDeclarationSyntax mainMethod,
        ClassDeclarationSyntax classNode,
        SemanticModel model)
    {
        Dictionary<string, MethodDeclarationSyntax> privateMethods = classNode.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(helper =>
                !string.Equals(helper.Identifier.ValueText, mainMethod.Identifier.ValueText, StringComparison.Ordinal) &&
                FindExtractAttribute(helper) is null)
            .ToDictionary(helper => helper.Identifier.ValueText, helper => helper, StringComparer.Ordinal);

        HashSet<string> reachable = new(StringComparer.Ordinal);
        Queue<string> pending = new(GetInvokedMethodNames(mainMethod).Where(privateMethods.ContainsKey));

        List<HelperMethodDefinition> helpers = [];
        while (pending.Count > 0)
        {
            string helperName = pending.Dequeue();
            if (!reachable.Add(helperName))
            {
                continue;
            }

            if (!privateMethods.TryGetValue(helperName, out MethodDeclarationSyntax? helperMethod))
            {
                continue;
            }

            helpers.Add(new HelperMethodDefinition(
                helperMethod.Identifier.ValueText,
                helperMethod.ReturnType.ToString(),
                [.. helperMethod.ParameterList.Parameters.Select(MapParameter)],
                helperMethod.Body?.ToFullString().Trim(),
                helperMethod.ExpressionBody?.Expression.ToString(),
                helperMethod.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)),
                helperMethod.Modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword))));

            foreach (string nested in GetInvokedMethodNames(helperMethod).Where(privateMethods.ContainsKey))
            {
                if (!reachable.Contains(nested))
                {
                    pending.Enqueue(nested);
                }
            }
        }

        return helpers;
    }

    private static IEnumerable<string> GetInvokedMethodNames(MethodDeclarationSyntax method)
    {
        foreach (InvocationExpressionSyntax invocation in method.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string? name = invocation.Expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(name))
            {
                yield return name!;
            }
        }
    }

    private static Dictionary<string, string> BuildFieldMap(ClassDeclarationSyntax classNode)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);

        foreach (FieldDeclarationSyntax field in classNode.Members.OfType<FieldDeclarationSyntax>())
        {
            string typeName = field.Declaration.Type.ToString();
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                string name = variable.Identifier.ValueText;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    map[name] = typeName;
                }
            }
        }

        if (classNode.ParameterList is not null)
        {
            foreach (ParameterSyntax parameter in classNode.ParameterList.Parameters)
            {
                if (parameter.Type is null)
                {
                    continue;
                }

                string name = parameter.Identifier.ValueText;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    map[name] = parameter.Type.ToString();
                }
            }
        }

        return map;
    }

    private static IReadOnlyList<ServiceDependency> ExtractDependencies(
        MethodDeclarationSyntax method,
        ClassDeclarationSyntax classNode,
        IReadOnlyDictionary<string, string> fieldMap,
        SemanticModel model,
        IReadOnlyCollection<string> helperMethodNames)
    {
        IEnumerable<IdentifierNameSyntax> usedNodes = method.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .ToArray();

        IEnumerable<IdentifierNameSyntax> helperNodes = classNode.Members
            .OfType<MethodDeclarationSyntax>()
            .Where(helper =>
                helperMethodNames.Contains(helper.Identifier.ValueText, StringComparer.Ordinal))
            .SelectMany(helper => helper.DescendantNodes().OfType<IdentifierNameSyntax>());

        HashSet<string> usedIdentifiers = usedNodes
            .Concat(helperNodes)
            .Select(x => x.Identifier.ValueText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        List<ServiceDependency> deps = [];
        foreach (KeyValuePair<string, string> pair in fieldMap)
        {
            if (!usedIdentifiers.Contains(pair.Key))
            {
                continue;
            }

            IdentifierNameSyntax? fieldNode = method.DescendantNodes().OfType<IdentifierNameSyntax>().FirstOrDefault(x => x.Identifier.ValueText == pair.Key);
            string typeName = pair.Value;
            if (fieldNode is not null)
            {
                ISymbol? symbol = model.GetSymbolInfo(fieldNode).Symbol;
                if (symbol is IFieldSymbol fieldSymbol)
                {
                    typeName = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                }
            }

            string parameterName = ToCamelCase(pair.Key.TrimStart('_'));
            deps.Add(new ServiceDependency(typeName, pair.Key, parameterName));
        }

        return deps;
    }

    private static ParameterModel MapParameter(ParameterSyntax parameter)
    {
        return new ParameterModel(
            parameter.Identifier.ValueText,
            parameter.Type?.ToString() ?? "object",
            parameter.Default is not null,
            parameter.Default?.Value.ToString());
    }

    private static bool IsExtractAttribute(AttributeSyntax attribute)
    {
        string name = attribute.Name.ToString();
        return name.EndsWith("MExtractAsRule", StringComparison.Ordinal) ||
               name.EndsWith("MExtractAsRuleAttribute", StringComparison.Ordinal) ||
               name.EndsWith("ExtractAsRule", StringComparison.Ordinal) ||
               name.EndsWith("ExtractAsRuleAttribute", StringComparison.Ordinal);
    }

    private static AttributeSyntax? FindExtractAttribute(MethodDeclarationSyntax method)
    {
        return method.AttributeLists
            .SelectMany(x => x.Attributes)
            .FirstOrDefault(IsExtractAttribute);
    }

    private static (string Code, int Order, string HookPoint, IReadOnlyList<string> DependsOn) ParseExtractAttribute(AttributeSyntax attribute, SemanticModel model)
    {
        string code = string.Empty;
        int order = 0;
        string hookPoint = "BeforeRule";
        List<string> dependsOn = [];

        if (attribute.ArgumentList is null)
        {
            return (code, order, hookPoint, dependsOn);
        }

        foreach (AttributeArgumentSyntax argument in attribute.ArgumentList.Arguments)
        {
            if (argument.NameEquals is null && string.IsNullOrWhiteSpace(code))
            {
                if (TryResolveExpression(argument.Expression, model, out string? codeValue))
                {
                    code = codeValue;
                }

                continue;
            }

            string? property = argument.NameEquals?.Name.Identifier.ValueText;
            if (string.IsNullOrWhiteSpace(property))
            {
                continue;
            }

            if (string.Equals(property, "Order", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(argument.Expression.ToString(), out int parsedOrder))
            {
                order = parsedOrder;
                continue;
            }

            if (string.Equals(property, "HookPoint", StringComparison.OrdinalIgnoreCase))
            {
                hookPoint = argument.Expression.ToString().Split('.').LastOrDefault() ?? "BeforeRule";
                continue;
            }

            if (string.Equals(property, "DependsOn", StringComparison.OrdinalIgnoreCase))
            {
                dependsOn.AddRange(ParseStringCollection(argument.Expression, model));
            }
        }

        return (code, order, hookPoint, dependsOn);
    }

    private static IEnumerable<string> ParseStringCollection(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is ArrayCreationExpressionSyntax arrayCreation)
        {
            if (arrayCreation.Initializer is not null)
            {
                foreach (ExpressionSyntax item in arrayCreation.Initializer.Expressions)
                {
                    if (TryResolveExpression(item, model, out string? value))
                    {
                        yield return value;
                    }
                }
            }

            yield break;
        }

        if (expression is ImplicitArrayCreationExpressionSyntax implicitArray)
        {
            foreach (ExpressionSyntax item in implicitArray.Initializer.Expressions)
            {
                if (TryResolveExpression(item, model, out string? value))
                {
                    yield return value;
                }
            }

            yield break;
        }

        if (expression is InitializerExpressionSyntax init)
        {
            foreach (ExpressionSyntax item in init.Expressions)
            {
                if (TryResolveExpression(item, model, out string? value))
                {
                    yield return value;
                }
            }

            yield break;
        }

        if (expression is CollectionExpressionSyntax collectionExpression)
        {
            foreach (CollectionElementSyntax element in collectionExpression.Elements)
            {
                if (element is not ExpressionElementSyntax expressionElement)
                {
                    continue;
                }

                if (TryResolveExpression(expressionElement.Expression, model, out string? value))
                {
                    yield return value;
                }
            }

            yield break;
        }

        if (TryResolveExpression(expression, model, out string? single))
        {
            yield return single;
        }
    }

    private static bool TryResolveExpression(ExpressionSyntax expression, SemanticModel model, out string value)
    {
        Optional<object?> constantValue = model.GetConstantValue(expression);
        if (constantValue.HasValue && constantValue.Value is string strValue)
        {
            value = strValue;
            return true;
        }

        if (expression is InvocationExpressionSyntax invocation && invocation.Expression is IdentifierNameSyntax id && id.Identifier.ValueText == "nameof")
        {
            ArgumentSyntax? arg = invocation.ArgumentList.Arguments.FirstOrDefault();
            if (arg is not null)
            {
                value = arg.Expression.ToString().Split('.').Last();
                return true;
            }
        }

        if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            value = literal.Token.ValueText;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static string? ExtractXmlComment(MethodDeclarationSyntax method)
    {
        SyntaxTrivia xmlTrivia = method.GetLeadingTrivia()
            .FirstOrDefault(t => t.GetStructure() is DocumentationCommentTriviaSyntax);

        return xmlTrivia.ToString().Trim();
    }

    private static bool IsFactBagType(string typeName)
    {
        string normalized = typeName.Replace(" ", string.Empty);
        return string.Equals(normalized, "FactBag", StringComparison.Ordinal) ||
               string.Equals(normalized, "Muonroi.RuleEngine.Abstractions.FactBag", StringComparison.Ordinal);
    }

    private static bool IsCancellationTokenType(string typeName)
    {
        string normalized = typeName.Replace(" ", string.Empty);
        return string.Equals(normalized, "CancellationToken", StringComparison.Ordinal) ||
               string.Equals(normalized, "System.Threading.CancellationToken", StringComparison.Ordinal);
    }

    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "dependency";
        }

        return char.ToLowerInvariant(input[0]) + input[1..];
    }
}
