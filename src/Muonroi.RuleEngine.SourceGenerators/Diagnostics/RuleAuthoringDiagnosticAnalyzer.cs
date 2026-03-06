using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Linq;

namespace Muonroi.RuleEngine.SourceGenerators.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RuleAuthoringDiagnosticAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        RuleGenDiagnostics.MissingDependencyReference,
        RuleGenDiagnostics.OrderWithoutDependsOn,
        RuleGenDiagnostics.FactConsumptionWithoutDependency,
        RuleGenDiagnostics.NullableAssignedToNonNullableString,
        RuleGenDiagnostics.FactGuardThrowsInvalidOperation);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationAction(static compilationContext =>
        {
            MethodDeclarationSyntax[] methods = compilationContext.Compilation.SyntaxTrees
                .SelectMany(tree => tree.GetRoot(compilationContext.CancellationToken)
                    .DescendantNodes()
                    .OfType<MethodDeclarationSyntax>())
                .Where(m => m.AttributeLists.Count > 0)
                .ToArray();

            RuleAuthoringAnalyzer.Analyze(
                compilationContext.Compilation,
                methods,
                compilationContext.ReportDiagnostic);
        });
    }
}
