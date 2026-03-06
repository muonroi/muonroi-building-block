using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.RuleEngine.SourceGenerators.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MBB005_AbstractionInfraDepAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] ForbiddenDependencyTokens =
    [
        "EntityFrameworkCore",
        "Hangfire",
        "Quartz",
        "MassTransit",
        "Serilog",
        "Confluent.Kafka",
        "RabbitMQ.Client"
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MBBDiagnosticDescriptors.MBB005AbstractionInfraDependency);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        string assemblyName = context.Compilation.AssemblyName ?? string.Empty;
        if (!assemblyName.EndsWith(".Abstractions", StringComparison.Ordinal))
        {
            return;
        }

        IEnumerable<string> references = context.Compilation.ReferencedAssemblyNames
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x))!;

        foreach (string reference in references)
        {
            if (reference.StartsWith("Microsoft", StringComparison.Ordinal) ||
                reference.StartsWith("System", StringComparison.Ordinal))
            {
                continue;
            }

            if (ForbiddenDependencyTokens.Any(token => reference.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MBBDiagnosticDescriptors.MBB005AbstractionInfraDependency,
                    Location.None,
                    assemblyName,
                    reference));
            }
        }
    }
}
