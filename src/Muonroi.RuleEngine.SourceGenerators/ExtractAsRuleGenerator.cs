using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Muonroi.RuleEngine.SourceGenerators.Authoring;
using Muonroi.RuleEngine.SourceGenerators.Models;
using Muonroi.RuleEngine.SourceGenerators.SourceWriters;
using Muonroi.RuleEngine.SourceGenerators.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;

namespace Muonroi.RuleEngine.SourceGenerators;

[Generator]
public sealed class ExtractAsRuleGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter: chá»‰ láº¥y methods cÃ³ [MExtractAsRule] attribute
        IncrementalValuesProvider<MethodDeclarationSyntax> methodDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)!;

        // 2. Combine vá»›i Compilation
        IncrementalValueProvider<(Compilation, ImmutableArray<MethodDeclarationSyntax>)> compilationAndMethods =
            context.CompilationProvider.Combine(methodDeclarations.Collect());

        // 3. Register source output
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

        // 3. Register source output
        context.RegisterSourceOutput(
            compilationAndMethods.Combine(diagnosticsOnlyProvider),
            static (spc, source) => Execute(source.Left.Item1, source.Left.Item2, source.Right, spc));
    }

    private static bool IsSyntaxTargetForGeneration(SyntaxNode node)
    {
        return node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0;
    }

    private static MethodDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        foreach (AttributeListSyntax attributeListSyntax in methodDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
            {
                if (context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol is IMethodSymbol attributeSymbol)
                {
                    string name = attributeSymbol.ContainingType.ToDisplayString();
                    if (name.Contains("MExtractAsRule") || name.Contains("ExtractAsRule"))
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
        if (methods.IsDefaultOrEmpty) return;

        IEnumerable<MethodDeclarationSyntax> distinctMethods = methods.Distinct();
        List<ExtractedRuleDefinition> definitions = new List<ExtractedRuleDefinition>();
        Dictionary<ExtractedRuleDefinition, Location> definitionLocations = new Dictionary<ExtractedRuleDefinition, Location>();

        foreach (MethodDeclarationSyntax method in distinctMethods)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            SemanticModel model = compilation.GetSemanticModel(method.SyntaxTree);
            IMethodSymbol? methodSymbol = model.GetDeclaredSymbol(method);
            if (methodSymbol == null) continue;

            AttributeData? attribute = methodSymbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name.Contains("ExtractAsRule") == true);

            if (attribute == null) continue;

            // Extract Rule Code
            string ruleCode = attribute.ConstructorArguments.Length > 0 
                ? attribute.ConstructorArguments[0].Value?.ToString() ?? methodSymbol.Name
                : methodSymbol.Name;

            // Extract named arguments
            int order = 0;
            string hookPoint = "BeforeRule";
            List<string> dependsOn = new List<string>();

            foreach (KeyValuePair<string, TypedConstant> arg in attribute.NamedArguments)
            {
                if (arg.Key == "Order") order = (int)(arg.Value.Value ?? 0);
                else if (arg.Key == "HookPoint") hookPoint = GetHookPointName(arg.Value);
                else if (arg.Key == "DependsOn") dependsOn.AddRange(GetArrayValues(arg.Value));
            }

            // Extract Class Info
            INamedTypeSymbol classSymbol = methodSymbol.ContainingType;
            string className = classSymbol.Name;
            string? ns = classSymbol.ContainingNamespace.IsGlobalNamespace ? null : classSymbol.ContainingNamespace.ToDisplayString();

            // Extract Parameters
            List<ParameterModel> parameters = methodSymbol.Parameters.Select(p => new ParameterModel(
                p.Name,
                p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                p.HasExplicitDefaultValue,
                p.HasExplicitDefaultValue ? (p.ExplicitDefaultValue?.ToString() ?? "null") : null)).ToList();

            // Resolve Context Type
            string contextType = parameters.FirstOrDefault(p => !IsFactBag(p.TypeName) && !IsCancellationToken(p.TypeName))?.TypeName ?? "object";

            // Dependencies
            List<ServiceDependency> dependencies = ExtractDependencies(method, classSymbol, model, context);

            // Helper Methods
            List<HelperMethodDefinition> helpers = ExtractHelperMethods(method, classSymbol, model);

            // Metadata
            string[] customAttributes = methodSymbol.GetAttributes()
                .Where(a => !a.AttributeClass?.Name.Contains("ExtractAsRule") == true)
                .Where(a => !string.Equals(a.AttributeClass?.ToDisplayString(), "Muonroi.RuleEngine.Abstractions.Authoring.MRuleContextDescriptionAttribute", StringComparison.Ordinal))
                .Where(a => !string.Equals(a.AttributeClass?.ToDisplayString(), "Muonroi.RuleEngine.Abstractions.Authoring.MRuleFactDescriptionAttribute", StringComparison.Ordinal))
                .Select(a => $"[{a.AttributeClass?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}]")
                .ToArray();

            string[] usings = method.SyntaxTree.GetCompilationUnitRoot().Usings
                .Where(u => u.Name != null)
                .Select(u => u.Name!.ToString())
                .ToArray();

            var definition = new ExtractedRuleDefinition(
                ruleCode,
                methodSymbol.Name,
                className,
                ns,
                ns ?? "Generated.Rules", // Default output namespace for SG
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
                null, // Doc comment extraction is expensive in SG, skipping or simple regex
                method.Body?.ToFullString().Trim(),
                method.ExpressionBody?.Expression.ToString(),
                methodSymbol.IsAsync,
                method.SyntaxTree.FilePath,
                method.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            );

            definitions.Add(definition);
            definitionLocations[definition] = method.GetLocation();
        }

        // Authoring diagnostics for large source classes
        RuleAuthoringAnalyzer.Analyze(compilation, distinctMethods, context);

        // Check for duplicates
        HashSet<string> duplicateCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var groups = definitions.GroupBy(d => d.Code);
        foreach (var group in groups)
        {
            if (group.Count() > 1)
            {
                duplicateCodes.Add(group.Key);
                foreach (var item in group)
                {
                    Location diagnosticLocation = definitionLocations.TryGetValue(item, out Location? loc)
                        ? loc
                        : Location.None;
                    context.ReportDiagnostic(Diagnostic.Create(RuleGenDiagnostics.DuplicateRuleCode, diagnosticLocation, item.Code, item.ClassName));
                }
            }
        }

        string rootNamespace = definitions
            .Select(definition => definition.SourceNamespace)
            .FirstOrDefault(sourceNamespace => !string.IsNullOrWhiteSpace(sourceNamespace))
            ?? ToIdentifier(compilation.AssemblyName ?? "MuonroiRuleAssembly");

        if (!diagnosticsOnly)
        {
            foreach (var def in definitions)
            {
                // Skip generation for duplicate codes to avoid duplicate hintName/source conflicts.
                if (duplicateCodes.Contains(def.Code))
                {
                    continue;
                }
                string source = GeneratedRuleSourceWriter.Render(def);
                context.AddSource($"{ToIdentifier(def.Code)}Rule.g.cs", source);
            }
        }

        AuthoringManifestDefinition manifest = RuleAuthoringManifestExtractor.Build(compilation, distinctMethods);
        string manifestSource = RuleAuthoringManifestSourceWriter.Render(rootNamespace, manifest);
        context.AddSource($"{ToIdentifier(compilation.AssemblyName ?? "RuleAssembly")}.RuleAuthoringManifestProvider.g.cs", manifestSource);
    }

    private static string GetHookPointName(TypedConstant constant)
    {
        if (constant.Value is int val)
        {
            // Simple mapping or assuming enum value string
            return "BeforeRule"; // Fallback
        }
        return constant.Value?.ToString() ?? "BeforeRule";
    }

    private static IEnumerable<string> GetArrayValues(TypedConstant constant)
    {
        if (constant.Kind == TypedConstantKind.Array)
        {
            return constant.Values.Select(v => v.Value?.ToString() ?? string.Empty).Where(s => !string.IsNullOrEmpty(s));
        }
        return Enumerable.Empty<string>();
    }

    private static List<ServiceDependency> ExtractDependencies(MethodDeclarationSyntax method, INamedTypeSymbol classSymbol, SemanticModel model, SourceProductionContext context)
    {
        HashSet<string> usedIdentifiers = new HashSet<string>(method.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Select(x => x.Identifier.ValueText));

        List<ServiceDependency> deps = new List<ServiceDependency>();
        foreach (ISymbol member in classSymbol.GetMembers())
        {
            if (member is IFieldSymbol field && usedIdentifiers.Contains(field.Name))
            {
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
                string paramName = field.Name.TrimStart('_');
                if (paramName.Length > 0) paramName = char.ToLower(paramName[0]) + paramName.Substring(1);
                else paramName = "dependency";

                deps.Add(new ServiceDependency(typeName, field.Name, paramName));
            }
        }
        return deps;
    }

    private static List<HelperMethodDefinition> ExtractHelperMethods(MethodDeclarationSyntax mainMethod, INamedTypeSymbol classSymbol, SemanticModel model)
    {
        HashSet<string> invokedPrivateMethods = new HashSet<string>(mainMethod.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(i => model.GetSymbolInfo(i).Symbol as IMethodSymbol)
            .Where(s => s != null && SymbolEqualityComparer.Default.Equals(s.ContainingType, classSymbol) && s.DeclaredAccessibility == Accessibility.Private)
            .Select(s => s!.Name));

        if (invokedPrivateMethods.Count == 0) return new List<HelperMethodDefinition>();

        List<HelperMethodDefinition> helpers = new List<HelperMethodDefinition>();
        // Note: Finding the syntax for private methods in the same compilation
        foreach (SyntaxReference reference in classSymbol.DeclaringSyntaxReferences)
        {
            SyntaxNode classNode = reference.GetSyntax();
            foreach (MethodDeclarationSyntax method in classNode.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (invokedPrivateMethods.Contains(method.Identifier.ValueText))
                {
                    IMethodSymbol? helperSymbol = model.GetDeclaredSymbol(method);
                    if (helperSymbol == null) continue;

                    helpers.Add(new HelperMethodDefinition(
                        method.Identifier.ValueText,
                        helperSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        helperSymbol.Parameters.Select(p => new ParameterModel(p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), p.HasExplicitDefaultValue, null)).ToList(),
                        method.Body?.ToFullString().Trim(),
                        method.ExpressionBody?.Expression.ToString(),
                        helperSymbol.IsAsync));
                }
            }
        }

        return helpers;
    }

    private static bool IsFactBag(string typeName) => typeName.Contains("FactBag");
    private static bool IsCancellationToken(string typeName) => typeName.Contains("CancellationToken");

    private static string ToIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "GeneratedRule";
        char[] chars = value.Select(ch => char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_').ToArray();
        string normalized = new string(chars);
        if (!char.IsLetter(normalized[0]) && normalized[0] != '_') normalized = $"R_{normalized}";
        return normalized;
    }
}

