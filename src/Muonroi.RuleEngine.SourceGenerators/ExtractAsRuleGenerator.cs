using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Muonroi.RuleEngine.SourceGenerators.Authoring;
using Muonroi.RuleEngine.SourceGenerators.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Models;
using Muonroi.RuleEngine.SourceGenerators.SourceWriters;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Muonroi.RuleEngine.SourceGenerators;

/// <summary>
/// Incremental generator that extracts annotated methods into rules.
/// </summary>
[Generator]
public sealed class ExtractAsRuleGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<MethodDeclarationSyntax> methodDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => IsSyntaxTargetForGeneration(syntaxNode),
                transform: static (generatorContext, _) => GetSemanticTargetForGeneration(generatorContext))
            .Where(static method => method is not null)!;

        IncrementalValueProvider<(Compilation, ImmutableArray<MethodDeclarationSyntax>)> compilationAndMethods =
            context.CompilationProvider.Combine(methodDeclarations.Collect());

        IncrementalValueProvider<bool> diagnosticsOnlyProvider = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) =>
            {
                if (provider.GlobalOptions.TryGetValue("build_property.MuonroiRuleGenDiagnosticsOnly", out string? raw) &&
                    bool.TryParse(raw, out bool enabled))
                {
                    return enabled;
                }

                return false;
            });

        context.RegisterSourceOutput(
            compilationAndMethods.Combine(diagnosticsOnlyProvider),
            static (sourceProductionContext, source) => Execute(source.Left.Item1, source.Left.Item2, source.Right, sourceProductionContext));
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    {
        return node is MethodDeclarationSyntax methodDeclaration && methodDeclaration.AttributeLists.Count > 0;
    }

    private static MethodDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        MethodDeclarationSyntax methodDeclaration = (MethodDeclarationSyntax)context.Node;
        foreach (AttributeListSyntax attributeList in methodDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attributeSyntax in attributeList.Attributes)
            {
                if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is IMethodSymbol attributeSymbol)
                {
                    string name = attributeSymbol.ContainingType.ToDisplayString();
                    if (name.IndexOf("MExtractAsRule", StringComparison.Ordinal) >= 0 || name.IndexOf("ExtractAsRule", StringComparison.Ordinal) >= 0)
                    {
                        return methodDeclaration;
                    }
                }
            }
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<MethodDeclarationSyntax> methods, bool diagnosticsOnly, SourceProductionContext context)
    {
        if (methods.IsDefaultOrEmpty)
        {
            return;
        }

        IEnumerable<MethodDeclarationSyntax> distinctMethods = methods.Distinct();
        List<ExtractedRuleDefinition> definitions = [];
        Dictionary<ExtractedRuleDefinition, Location> definitionLocations = [];

        foreach (MethodDeclarationSyntax method in distinctMethods)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            SemanticModel model = compilation.GetSemanticModel(method.SyntaxTree);
            IMethodSymbol? methodSymbol = model.GetDeclaredSymbol(method);
            if (methodSymbol is null)
            {
                continue;
            }

            AttributeData? attribute = methodSymbol.GetAttributes()
                .FirstOrDefault(candidate => candidate.AttributeClass?.Name?.IndexOf("ExtractAsRule", StringComparison.Ordinal) >= 0);
            if (attribute is null)
            {
                continue;
            }

            string ruleCode = attribute.ConstructorArguments.Length > 0
                ? attribute.ConstructorArguments[0].Value?.ToString() ?? methodSymbol.Name
                : methodSymbol.Name;
            int order = 0;
            string hookPoint = "BeforeRule";
            List<string> dependsOn = [];
            string? feelExpression = null;
            bool useFactBagAware = false;

            foreach (KeyValuePair<string, TypedConstant> argument in attribute.NamedArguments)
            {
                if (string.Equals(argument.Key, "Order", StringComparison.Ordinal))
                {
                    order = (int)(argument.Value.Value ?? 0);
                }
                else if (string.Equals(argument.Key, "HookPoint", StringComparison.Ordinal))
                {
                    hookPoint = GetHookPointName(argument.Value);
                }
                else if (string.Equals(argument.Key, "DependsOn", StringComparison.Ordinal))
                {
                    dependsOn.AddRange(GetArrayValues(argument.Value));
                }
                else if (string.Equals(argument.Key, "Expression", StringComparison.Ordinal))
                {
                    feelExpression = argument.Value.Value?.ToString();
                }
                else if (string.Equals(argument.Key, "UseFactBagAware", StringComparison.Ordinal))
                {
                    useFactBagAware = (bool)(argument.Value.Value ?? false);
                }
            }

            if (!string.IsNullOrWhiteSpace(feelExpression) && !FeelExpressionSyntaxValidator.TryValidate(feelExpression!, out string error))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    RuleGenDiagnostics.InvalidFeelExpression,
                    method.GetLocation(),
                    ruleCode,
                    feelExpression,
                    error));
                continue;
            }

            INamedTypeSymbol classSymbol = methodSymbol.ContainingType;
            string? sourceNamespace = classSymbol.ContainingNamespace.IsGlobalNamespace ? null : classSymbol.ContainingNamespace.ToDisplayString();
            List<ParameterModel> parameters = [.. methodSymbol.Parameters.Select(parameter =>
            {
                string? defaultValueExpression = null;
                if (parameter.HasExplicitDefaultValue)
                {
                    // For struct types (e.g. CancellationToken), ExplicitDefaultValue is null even for `= default`.
                    // Emit "default" instead of "null" to avoid CS1750.
                    if (parameter.ExplicitDefaultValue is null && parameter.Type.IsValueType)
                    {
                        defaultValueExpression = "default";
                    }
                    else
                    {
                        defaultValueExpression = parameter.ExplicitDefaultValue?.ToString() ?? "null";
                    }
                }
                return new ParameterModel(
                    parameter.Name,
                    parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    parameter.HasExplicitDefaultValue,
                    defaultValueExpression);
            })];
            string contextType = parameters.FirstOrDefault(parameter => !IsFactBag(parameter.TypeName) && !IsCancellationToken(parameter.TypeName))?.TypeName ?? "object";
            List<ServiceDependency> dependencies = ExtractDependencies(method, classSymbol, model, context);
            List<HelperMethodDefinition> helpers = ExtractHelperMethods(method, classSymbol, compilation, context);
            string[] customAttributes = [.. methodSymbol.GetAttributes()
                .Where(candidate => !(candidate.AttributeClass?.Name?.IndexOf("ExtractAsRule", StringComparison.Ordinal) >= 0))
                .Where(candidate => !string.Equals(candidate.AttributeClass?.ToDisplayString(), "Muonroi.RuleEngine.Abstractions.Authoring.MRuleContextDescriptionAttribute", StringComparison.Ordinal))
                .Where(candidate => !string.Equals(candidate.AttributeClass?.ToDisplayString(), "Muonroi.RuleEngine.Abstractions.Authoring.MRuleFactDescriptionAttribute", StringComparison.Ordinal))
                .Where(candidate => !string.Equals(candidate.AttributeClass?.Name, "MRuleCatalogEntryAttribute", StringComparison.Ordinal))
                .Select(candidate => $"[{candidate.AttributeClass?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}]")];
            string[] usings = [.. method.SyntaxTree.GetCompilationUnitRoot().Usings
                .Where(usingDirective => usingDirective.Name is not null)
                .Select(usingDirective => usingDirective.Name!.ToString())];

            ExtractedRuleDefinition definition = new(
                ruleCode,
                methodSymbol.Name,
                classSymbol.Name,
                sourceNamespace,
                sourceNamespace ?? "Generated.Rules",
                contextType,
                methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                order,
                hookPoint,
                dependsOn,
                usings,
                customAttributes,
                parameters,
                dependencies,
                helpers,
                null,
                method.Body?.ToFullString().Trim(),
                method.ExpressionBody?.Expression.ToString(),
                feelExpression,
                methodSymbol.IsAsync,
                method.SyntaxTree.FilePath,
                method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                useFactBagAware);

            definitions.Add(definition);
            definitionLocations[definition] = method.GetLocation();
        }

        RuleAuthoringAnalyzer.Analyze(compilation, distinctMethods, context);

        HashSet<string> duplicateCodes = new(StringComparer.OrdinalIgnoreCase);
        foreach (IGrouping<string, ExtractedRuleDefinition> group in definitions.GroupBy(definition => definition.Code))
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            duplicateCodes.Add(group.Key);
            foreach (ExtractedRuleDefinition item in group)
            {
                Location diagnosticLocation = definitionLocations.TryGetValue(item, out Location? location)
                    ? location
                    : Location.None;
                context.ReportDiagnostic(Diagnostic.Create(RuleGenDiagnostics.DuplicateRuleCode, diagnosticLocation, item.Code, item.ClassName));
            }
        }

        if (!diagnosticsOnly)
        {
            foreach (ExtractedRuleDefinition definition in definitions)
            {
                if (duplicateCodes.Contains(definition.Code))
                {
                    continue;
                }

                string source = GeneratedRuleSourceWriter.Render(definition);
                context.AddSource($"{ToIdentifier(definition.Code)}Rule.g.cs", source);
            }
        }

    }

    private static string GetHookPointName(TypedConstant constant)
    {
        return constant.Value?.ToString() ?? "BeforeRule";
    }

    private static IEnumerable<string> GetArrayValues(TypedConstant constant)
    {
        if (constant.Kind != TypedConstantKind.Array)
        {
            return Enumerable.Empty<string>();
        }

        return constant.Values.Select(value => value.Value?.ToString() ?? string.Empty).Where(value => !string.IsNullOrEmpty(value));
    }

    private static List<ServiceDependency> ExtractDependencies(MethodDeclarationSyntax method, INamedTypeSymbol classSymbol, SemanticModel model, SourceProductionContext context)
    {
        HashSet<string> usedIdentifiers = new(method.DescendantNodes().OfType<IdentifierNameSyntax>().Select(identifier => identifier.Identifier.ValueText));
        List<ServiceDependency> dependencies = [];
        foreach (ISymbol member in classSymbol.GetMembers())
        {
            if (member is not IFieldSymbol field || !usedIdentifiers.Contains(field.Name))
            {
                continue;
            }

            if (field.Type.TypeKind != TypeKind.Interface)
            {
                Location diagnosticLocation = field.Locations.FirstOrDefault() ?? Location.None;
                context.ReportDiagnostic(Diagnostic.Create(
                    RuleGenDiagnostics.NonInterfaceDependency,
                    diagnosticLocation,
                    field.Name,
                    field.Type.Name));
            }

            string typeName = field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            string parameterName = field.Name.TrimStart('_');
            if (parameterName.Length > 0)
            {
                parameterName = char.ToLowerInvariant(parameterName[0]) + parameterName.Substring(1);
            }
            else
            {
                parameterName = "dependency";
            }

            dependencies.Add(new ServiceDependency(typeName, field.Name, parameterName));
        }

        return dependencies;
    }

    private static List<HelperMethodDefinition> ExtractHelperMethods(MethodDeclarationSyntax mainMethod, INamedTypeSymbol classSymbol, Compilation compilation, SourceProductionContext context)
    {
        HashSet<string> invokedPrivateMethods = [];
        foreach (InvocationExpressionSyntax invocation in mainMethod.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (modelFor(mainMethod, compilation).GetSymbolInfo(invocation).Symbol is IMethodSymbol symbol &&
                SymbolEqualityComparer.Default.Equals(symbol.ContainingType, classSymbol) &&
                symbol.DeclaredAccessibility == Accessibility.Private)
            {
                invokedPrivateMethods.Add(symbol.Name);
            }
        }

        if (invokedPrivateMethods.Count == 0)
        {
            return [];
        }

        List<HelperMethodDefinition> helpers = [];
        foreach (SyntaxReference reference in classSymbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not ClassDeclarationSyntax classNode)
            {
                continue;
            }

            SemanticModel classModel = compilation.GetSemanticModel(classNode.SyntaxTree);
            foreach (MethodDeclarationSyntax helperMethod in classNode.Members.OfType<MethodDeclarationSyntax>())
            {
                if (!invokedPrivateMethods.Contains(helperMethod.Identifier.ValueText))
                {
                    continue;
                }

                if (classModel.GetDeclaredSymbol(helperMethod) is not IMethodSymbol helperSymbol)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        RuleGenDiagnostics.HelperMethodExtractionFailed,
                        helperMethod.GetLocation(),
                        helperMethod.Identifier.ValueText));
                    continue;
                }

                helpers.Add(new HelperMethodDefinition(
                    helperMethod.Identifier.ValueText,
                    helperSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    helperSymbol.Parameters.Select(parameter => new ParameterModel(
                        parameter.Name,
                        parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        parameter.HasExplicitDefaultValue,
                        parameter.HasExplicitDefaultValue ? (parameter.ExplicitDefaultValue?.ToString() ?? "null") : null)).ToList(),
                    helperMethod.Body?.ToFullString().Trim(),
                    helperMethod.ExpressionBody?.Expression.ToString(),
                    helperSymbol.IsAsync));
            }
        }

        return helpers;

        static SemanticModel modelFor(MethodDeclarationSyntax method, Compilation currentCompilation)
        {
            return currentCompilation.GetSemanticModel(method.SyntaxTree);
        }
    }

    private static bool IsFactBag(string typeName) => typeName.IndexOf("FactBag", StringComparison.Ordinal) >= 0;

    private static bool IsCancellationToken(string typeName) => typeName.IndexOf("CancellationToken", StringComparison.Ordinal) >= 0;

    private static string ToIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "GeneratedRule";
        }

        char[] chars = [.. value.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_')];
        string normalized = new(chars);
        if (!char.IsLetter(normalized[0]) && normalized[0] != '_')
        {
            normalized = "R_" + normalized;
        }

        return normalized;
    }
}
