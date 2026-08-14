namespace Muonroi.RuleEngine.SourceGenerators;

/// <summary>
/// Emits the generated build-time rule catalog manifest provider used by the authoring registry.
/// </summary>
[Generator]
public sealed class RuleCatalogRegistrationGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<MethodDeclarationSyntax> methodDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (syntaxNode, _) => IsSyntaxTargetForGeneration(syntaxNode),
                transform: static (generatorContext, _) => GetSemanticTargetForGeneration(generatorContext))
            .Where(static method => method is not null)!;

        IncrementalValueProvider<(Compilation, ImmutableArray<MethodDeclarationSyntax>)> compilationAndMethods =
            context.CompilationProvider.Combine(methodDeclarations.Collect());

        context.RegisterSourceOutput(
            compilationAndMethods,
            static (sourceProductionContext, source) => Execute(source.Item1, source.Item2, sourceProductionContext));
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

    private static void Execute(Compilation compilation, ImmutableArray<MethodDeclarationSyntax> methods, SourceProductionContext context)
    {
        if (methods.IsDefaultOrEmpty)
        {
            return;
        }

        IEnumerable<MethodDeclarationSyntax> distinctMethods = methods.Distinct();
        AuthoringManifestDefinition manifest = RuleAuthoringManifestExtractor.Build(compilation, distinctMethods);
        string rootNamespace = manifest.Rules.Count > 0
            ? distinctMethods
                .Select(method => compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)?.ContainingType?.ContainingNamespace.ToDisplayString())
                .FirstOrDefault(sourceNamespace => !string.IsNullOrWhiteSpace(sourceNamespace))
                ?? ToIdentifier(compilation.AssemblyName ?? "MuonroiRuleAssembly")
            : ToIdentifier(compilation.AssemblyName ?? "MuonroiRuleAssembly");

        string manifestSource = RuleAuthoringManifestSourceWriter.Render(rootNamespace, manifest);
        context.AddSource("RuleCatalogRegistration.g.cs", manifestSource);
    }

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
