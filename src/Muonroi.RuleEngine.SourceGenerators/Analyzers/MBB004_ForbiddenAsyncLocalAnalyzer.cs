using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Diagnostics;
using System.Collections.Immutable;

namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MBB004_ForbiddenAsyncLocalAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MBBDiagnosticDescriptors.MBB004ForbiddenAsyncLocal);

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

        string ns = MBBAnalyzerHelpers.GetNamespace(objectCreation);
        if (MBBAnalyzerHelpers.IsContextNamespace(ns))
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

        string ns = MBBAnalyzerHelpers.GetNamespace(declaration);
        if (MBBAnalyzerHelpers.IsContextNamespace(ns))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MBBDiagnosticDescriptors.MBB004ForbiddenAsyncLocal,
            declaration.GetLocation(),
            string.IsNullOrWhiteSpace(ns) ? "<global>" : ns));
    }
}
