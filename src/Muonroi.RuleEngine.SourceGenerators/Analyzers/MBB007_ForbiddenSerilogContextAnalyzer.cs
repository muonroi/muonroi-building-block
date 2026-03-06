using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Diagnostics;
using System;
using System.Collections.Immutable;

namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MBB007_ForbiddenSerilogContextAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MBBDiagnosticDescriptors.MBB007ForbiddenSerilogContext);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, Microsoft.CodeAnalysis.CSharp.SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (!string.Equals(memberAccess.Name.Identifier.ValueText, "PushProperty", StringComparison.Ordinal))
        {
            return;
        }

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is not IMethodSymbol methodSymbol)
        {
            return;
        }

        string containingType = methodSymbol.ContainingType.ToDisplayString();
        if (!string.Equals(containingType, "Serilog.Context.LogContext", StringComparison.Ordinal))
        {
            return;
        }

        string ns = MBBAnalyzerHelpers.GetNamespace(invocation);
        if (MBBAnalyzerHelpers.IsSerilogContextAllowedNamespace(ns))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MBBDiagnosticDescriptors.MBB007ForbiddenSerilogContext,
            invocation.GetLocation()));
    }
}
