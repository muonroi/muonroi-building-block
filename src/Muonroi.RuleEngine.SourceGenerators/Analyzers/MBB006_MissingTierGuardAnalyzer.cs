namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

/// <summary>
/// Analyzer for the MBB006 diagnostic.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mbb006_MissingTierGuardAnalyzer : DiagnosticAnalyzer
{
    private static readonly HashSet<string> GuardedRegistrationCalls =
    [
        "AddMassTransit",
        "AddGrpcServer",
        "AddRedis",
        "AddMessageBus",
        "AddRuleEngineStore",
        "AddObservability"
    ];

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MBBDiagnosticDescriptors.MBB006MissingTierGuard];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        string methodName = MbbAnalyzerHelpers.GetInvokedMethodName(invocation);
        if (!GuardedRegistrationCalls.Contains(methodName))
        {
            return;
        }

        MethodDeclarationSyntax? containingMethod = invocation.Ancestors()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault();
        if (containingMethod is null)
        {
            return;
        }

        bool hasTierGuard = containingMethod.Body?.ToString().Contains("EnsureFeatureOrThrow(") == true;
        if (hasTierGuard)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MBBDiagnosticDescriptors.MBB006MissingTierGuard,
            invocation.GetLocation(),
            containingMethod.Identifier.ValueText,
            methodName));
    }
}
