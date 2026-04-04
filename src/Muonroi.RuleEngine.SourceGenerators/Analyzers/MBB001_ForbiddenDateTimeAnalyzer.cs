using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Diagnostics;
using System.Collections.Immutable;

namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

/// <summary>
/// Analyzer for the MBB001 diagnostic.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mbb001_ForbiddenDateTimeAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MBBDiagnosticDescriptors.MBB001ForbiddenDateTimeNow];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        string memberName = memberAccess.Name.Identifier.ValueText;
        if (memberName is not ("Now" or "UtcNow"))
        {
            return;
        }

        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(memberAccess, context.CancellationToken).Symbol;
        if (symbol is not IPropertySymbol propertySymbol ||
            propertySymbol.ContainingType.ToDisplayString() != "System.DateTime")
        {
            return;
        }

        string ns = MbbAnalyzerHelpers.GetNamespace(memberAccess);
        if (MbbAnalyzerHelpers.IsWrapperOrInfrastructureNamespace(ns))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MBBDiagnosticDescriptors.MBB001ForbiddenDateTimeNow,
            memberAccess.GetLocation(),
            $"DateTime.{memberName}",
            string.IsNullOrWhiteSpace(ns) ? "<global>" : ns));
    }
}
