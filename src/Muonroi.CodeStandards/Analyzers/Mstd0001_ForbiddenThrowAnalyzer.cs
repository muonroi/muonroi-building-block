using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.CodeStandards.Diagnostics;

namespace Muonroi.CodeStandards.Analyzers;

/// <summary>
/// MSTD0001: Forbids <c>throw new X(...)</c> where <c>X</c> is not derived from MException,
/// inside Muonroi.* non-test namespaces. MGuard.* calls and direct MException-derived throws
/// are allowed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mstd0001_ForbiddenThrowAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MstdDiagnosticDescriptors.Mstd0001ForbiddenThrow);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ThrowStatement);
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ThrowExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        ObjectCreationExpressionSyntax? creation = context.Node switch
        {
            ThrowStatementSyntax throwStmt => throwStmt.Expression as ObjectCreationExpressionSyntax,
            ThrowExpressionSyntax throwExpr => throwExpr.Expression as ObjectCreationExpressionSyntax,
            _ => null
        };

        if (creation is null)
        {
            return;
        }

        if (MstdAnalyzerHelpers.IsTestAssembly(context.Compilation))
        {
            return;
        }

        string ns = MstdAnalyzerHelpers.GetNamespace(context.Node);
        if (!MstdAnalyzerHelpers.IsMuonroiNamespace(ns))
        {
            return;
        }

        ITypeSymbol? thrownType = context.SemanticModel.GetTypeInfo(creation).Type;
        if (thrownType is null)
        {
            return;
        }

        if (MstdAnalyzerHelpers.InheritsFromMException(thrownType))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MstdDiagnosticDescriptors.Mstd0001ForbiddenThrow,
            creation.GetLocation(),
            thrownType.Name,
            ns));
    }
}
