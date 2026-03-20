using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Diagnostics;
using System.Collections.Immutable;

namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

/// <summary>
/// Analyzer for the MBB004 diagnostic.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mbb004_ForbiddenAsyncLocalAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MBBDiagnosticDescriptors.MBB004ForbiddenAsyncLocal];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeObjectCreation, SyntaxKind.ObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeVariableDeclaration, SyntaxKind.VariableDeclaration);
    }

    private static void AnalyzeObjectCreation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not ObjectCreationExpressionSyntax objectCreation)
        {
            return;
        }

        ITypeSymbol? typeSymbol = context.SemanticModel.GetTypeInfo(objectCreation, context.CancellationToken).Type;
        if (typeSymbol?.OriginalDefinition.ToDisplayString() != "System.Threading.AsyncLocal<T>")
        {
            return;
        }

        string ns = MbbAnalyzerHelpers.GetNamespace(objectCreation);
        if (MbbAnalyzerHelpers.IsContextNamespace(ns))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MBBDiagnosticDescriptors.MBB004ForbiddenAsyncLocal,
            objectCreation.GetLocation(),
            string.IsNullOrWhiteSpace(ns) ? "<global>" : ns));
    }

    private static void AnalyzeVariableDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not VariableDeclarationSyntax declaration)
        {
            return;
        }

        ITypeSymbol? typeSymbol = context.SemanticModel.GetTypeInfo(declaration.Type, context.CancellationToken).Type;
        if (typeSymbol?.OriginalDefinition.ToDisplayString() != "System.Threading.AsyncLocal<T>")
        {
            return;
        }

        string ns = MbbAnalyzerHelpers.GetNamespace(declaration);
        if (MbbAnalyzerHelpers.IsContextNamespace(ns))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MBBDiagnosticDescriptors.MBB004ForbiddenAsyncLocal,
            declaration.GetLocation(),
            string.IsNullOrWhiteSpace(ns) ? "<global>" : ns));
    }
}
