using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Diagnostics;
using System;
using System.Collections.Immutable;

namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MBB002_ForbiddenJsonSerializerAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MBBDiagnosticDescriptors.MBB002ForbiddenJsonSerializer);

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

        string ns = MBBAnalyzerHelpers.GetNamespace(invocation);
        if (ns.IndexOf(".Adapters.", StringComparison.Ordinal) >= 0 ||
            MBBAnalyzerHelpers.IsWrapperOrInfrastructureNamespace(ns))
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

        string ns = MBBAnalyzerHelpers.GetNamespace(memberAccess);
        if (ns.IndexOf(".Adapters.", StringComparison.Ordinal) >= 0 ||
            MBBAnalyzerHelpers.IsWrapperOrInfrastructureNamespace(ns))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MBBDiagnosticDescriptors.MBB002ForbiddenJsonSerializer,
            memberAccess.GetLocation(),
            symbol.Name));
    }
}
