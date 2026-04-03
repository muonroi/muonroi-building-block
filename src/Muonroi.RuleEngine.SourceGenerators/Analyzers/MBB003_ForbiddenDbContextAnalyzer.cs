using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Diagnostics;
using System.Collections.Immutable;

namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

/// <summary>
/// Analyzer for the MBB003 diagnostic.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mbb003_ForbiddenDbContextAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MBBDiagnosticDescriptors.MBB003ForbiddenDbContextInheritance];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol || typeSymbol.TypeKind != TypeKind.Class)
        {
            return;
        }

        if (typeSymbol.Name == "MDbContext")
        {
            return;
        }

        if (!InheritsFrom(typeSymbol, "Microsoft.EntityFrameworkCore.DbContext"))
        {
            return;
        }

        if (InheritsFrom(typeSymbol, "Muonroi.Data.EntityFrameworkCore.Entity.MDbContext"))
        {
            return;
        }

        string ns = typeSymbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        if (MbbAnalyzerHelpers.IsInfrastructureNamespace(ns))
        {
            return;
        }

        Location location = typeSymbol.Locations.Length > 0 ? typeSymbol.Locations[0] : Location.None;
        context.ReportDiagnostic(Diagnostic.Create(
            MBBDiagnosticDescriptors.MBB003ForbiddenDbContextInheritance,
            location,
            typeSymbol.Name));
    }

    private static bool InheritsFrom(INamedTypeSymbol typeSymbol, string baseTypeMetadataName)
    {
        INamedTypeSymbol? current = typeSymbol;
        while (current is not null)
        {
            if (string.Equals(current.ToDisplayString(), baseTypeMetadataName, StringComparison.Ordinal))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
