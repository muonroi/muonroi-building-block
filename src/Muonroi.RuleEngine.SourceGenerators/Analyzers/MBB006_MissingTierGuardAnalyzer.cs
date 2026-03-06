using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Diagnostics;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MBB006_MissingTierGuardAnalyzer : DiagnosticAnalyzer
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

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MBBDiagnosticDescriptors.MBB006MissingTierGuard);

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

        string methodName = MBBAnalyzerHelpers.GetInvokedMethodName(invocation);
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
