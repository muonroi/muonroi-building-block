namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

/// <summary>
/// MBB010: Detects public methods that have non-nullable reference-type parameters
/// without any null guard in the method body.
///
/// Purpose: Enforce guard-clause discipline at compile time. Every public API surface
/// must validate reference-type parameters via MGuard.NotNull() or equivalent pattern.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mbb010_MissingGuardAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MBBDiagnosticDescriptors.MBB010MissingGuardForParameter);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax method)
        {
            return;
        }

        // Only check public methods (per D-09a: skip private/internal/protected)
        if (!method.Modifiers.Any(SyntaxKind.PublicKeyword))
        {
            return;
        }

        // Skip test assemblies (per D-09e)
        string assemblyName = context.Compilation.AssemblyName ?? string.Empty;
        if (assemblyName.IndexOf(".Tests", System.StringComparison.Ordinal) >= 0)
        {
            return;
        }

        string methodName = method.Identifier.ValueText;

        // Get method body text for guard detection
        string bodyText = method.Body?.ToFullString()
            ?? method.ExpressionBody?.ToFullString()
            ?? string.Empty;

        foreach (ParameterSyntax param in method.ParameterList.Parameters)
        {
            // Skip params with default null value (per D-09d)
            if (param.Default?.Value is LiteralExpressionSyntax lit
                && lit.IsKind(SyntaxKind.NullLiteralExpression))
            {
                continue;
            }

            // Get the semantic symbol for the parameter
            IParameterSymbol? paramSymbol = context.SemanticModel.GetDeclaredSymbol(param);
            if (paramSymbol is null)
            {
                continue;
            }

            // Skip value types (per D-09c): int, bool, struct, etc.
            if (paramSymbol.Type.IsValueType)
            {
                continue;
            }

            // Skip nullable reference types T? (per D-09b)
            // NullableAnnotation.None (oblivious context) is treated as non-nullable — fire warning
            if (paramSymbol.NullableAnnotation == NullableAnnotation.Annotated)
            {
                continue;
            }

            string paramName = paramSymbol.Name;

            // Skip if any null guard already exists in the method body
            if (HasNullGuard(bodyText, paramName))
            {
                continue;
            }

            // Report MBB010: param is unguarded
            context.ReportDiagnostic(Diagnostic.Create(
                MBBDiagnosticDescriptors.MBB010MissingGuardForParameter,
                param.GetLocation(),
                paramName,
                methodName));
        }
    }

    /// <summary>
    /// Detects whether the method body already contains a null guard for the given parameter.
    /// Recognizes: MGuard.NotNull, MGuard.NotEmpty, ArgumentNullException.ThrowIfNull,
    /// if (param == null), if (param is null), param ?? throw.
    /// </summary>
    private static bool HasNullGuard(string bodyText, string paramName)
    {
        return bodyText.IndexOf($"MGuard.NotNull({paramName}", System.StringComparison.Ordinal) >= 0
            || bodyText.IndexOf($"MGuard.NotEmpty({paramName}", System.StringComparison.Ordinal) >= 0
            || bodyText.IndexOf($"ArgumentNullException.ThrowIfNull({paramName}", System.StringComparison.Ordinal) >= 0
            || bodyText.IndexOf($"if ({paramName} == null)", System.StringComparison.Ordinal) >= 0
            || bodyText.IndexOf($"if ({paramName} is null)", System.StringComparison.Ordinal) >= 0
            || bodyText.IndexOf($"{paramName} ?? throw", System.StringComparison.Ordinal) >= 0;
    }
}
