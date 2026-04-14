using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Diagnostics;
using System.Collections.Immutable;

namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

/// <summary>
/// MBB009: Detects <c>throw new</c> constructs that use non-MException types
/// inside Muonroi.* namespaces.
///
/// Purpose: Prevent exception regression — all Muonroi code must use the MException
/// hierarchy for consistent, layer-tagged error handling.
///
/// Does NOT fire when:
/// (a) the thrown type inherits from MException,
/// (b) the containing namespace is not Muonroi.*,
/// (c) the code is inside a test project (assembly name contains ".Tests").
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mbb009_ForbiddenRawExceptionAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MBBDiagnosticDescriptors.MBB009ForbiddenRawException);

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
        // Extract the creation expression from either a ThrowStatement or ThrowExpression
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

        // Must be inside a Muonroi.* namespace
        string ns = MbbAnalyzerHelpers.GetNamespace(context.Node);
        if (!ns.StartsWith("Muonroi.", StringComparison.Ordinal))
        {
            return;
        }

        // Skip test assemblies (assembly name contains ".Tests")
        string assemblyName = context.Compilation.AssemblyName ?? string.Empty;
        if (assemblyName.IndexOf(".Tests", StringComparison.Ordinal) >= 0)
        {
            return;
        }

        // Resolve the thrown type using the semantic model
        ITypeSymbol? thrownType = context.SemanticModel.GetTypeInfo(creation).Type;
        if (thrownType is null)
        {
            return;
        }

        // If the type (or any ancestor) is MException from Muonroi.*, it is allowed
        if (InheritsFromMException(thrownType))
        {
            return;
        }

        // Report MBB009 with the raw type name and the containing namespace
        context.ReportDiagnostic(Diagnostic.Create(
            MBBDiagnosticDescriptors.MBB009ForbiddenRawException,
            creation.GetLocation(),
            thrownType.Name,
            ns));
    }

    /// <summary>
    /// Walks the <paramref name="type"/> base-type chain looking for a class named
    /// <c>MException</c> in a Muonroi.* namespace.
    /// </summary>
    private static bool InheritsFromMException(ITypeSymbol type)
    {
        ITypeSymbol? current = type.BaseType;
        while (current is not null)
        {
            if (current.Name == "MException" &&
                current.ContainingNamespace?.ToDisplayString()
                    .StartsWith("Muonroi.", StringComparison.Ordinal) == true)
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
