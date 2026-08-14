namespace Muonroi.CodeStandards.Analyzers;

/// <summary>
/// MSTD0004: Forbids manually throwing MInternalException, MConfigurationException, 
/// MArgumentException, or MNotFoundException directly using throw new.
/// These should be thrown via the MGuard utility to ensure consistent handling.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mstd0004_DirectMGuardBypassAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> ForbiddenTypes = ImmutableHashSet.Create(
        "MInternalException",
        "MConfigurationException",
        "MArgumentException",
        "MNotFoundException"
    );

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MstdDiagnosticDescriptors.Mstd0004DirectMGuardBypass);

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

        // Allow MGuard to throw these exceptions internally!
        if (ns == "Muonroi.Core.Abstractions.Guards")
        {
            return;
        }

        ITypeSymbol? thrownType = context.SemanticModel.GetTypeInfo(creation).Type;
        if (thrownType is null)
        {
            return;
        }

        if (ForbiddenTypes.Contains(thrownType.Name))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                MstdDiagnosticDescriptors.Mstd0004DirectMGuardBypass,
                creation.GetLocation(),
                thrownType.Name));
        }
    }
}
