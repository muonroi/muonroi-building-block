using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Diagnostics;
using System.Collections.Immutable;

namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

/// <summary>
/// Analyzer for the MBB002 diagnostic.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mbb002_ForbiddenJsonSerializerAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MBBDiagnosticDescriptors.MBB002ForbiddenJsonSerializer];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation ||
            invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol methodSymbol ||
            methodSymbol.ContainingType.ToDisplayString() != "System.Text.Json.JsonSerializer")
        {
            return;
        }

        string ns = MbbAnalyzerHelpers.GetNamespace(invocation);
        if (ns.IndexOf(".Adapters.", StringComparison.Ordinal) >= 0 ||
            MbbAnalyzerHelpers.IsWrapperOrInfrastructureNamespace(ns))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MBBDiagnosticDescriptors.MBB002ForbiddenJsonSerializer,
            invocation.GetLocation(),
            methodSymbol.Name));
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
        if (symbol is null || symbol.ContainingType?.ToDisplayString() != "System.Text.Json.JsonSerializer")
        {
            return;
        }

        string ns = MbbAnalyzerHelpers.GetNamespace(memberAccess);
        if (ns.IndexOf(".Adapters.", StringComparison.Ordinal) >= 0 ||
            MbbAnalyzerHelpers.IsWrapperOrInfrastructureNamespace(ns))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MBBDiagnosticDescriptors.MBB002ForbiddenJsonSerializer,
            memberAccess.GetLocation(),
            symbol.Name));
    }
}
