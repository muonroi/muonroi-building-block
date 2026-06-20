using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Muonroi.CodeStandards.Diagnostics;

namespace Muonroi.CodeStandards.Analyzers;

/// <summary>
/// MSTD0003: All logging must go through the ecosystem logger <c>IMLog</c>/<c>IMLog&lt;T&gt;</c>
/// (Muonroi.Logging.Abstractions), inside Muonroi.* non-test namespaces. The following ad-hoc
/// logging sinks are forbidden:
/// <list type="bullet">
///   <item><description><c>Console.Write</c>/<c>Console.WriteLine</c> and <c>Console.Error</c>/<c>Console.Out</c> writes</description></item>
///   <item><description><c>System.Diagnostics.Debug.*</c> / <c>System.Diagnostics.Trace.*</c></description></item>
///   <item><description>static <c>Serilog.Log.*</c></description></item>
///   <item><description>raw <c>ILogger</c>/<c>ILogger&lt;T&gt;</c> <c>Log*</c> calls (where the receiver is NOT an IMLog)</description></item>
/// </list>
/// The Muonroi.Logging* implementation projects (which wrap the raw ILogger) and test
/// assemblies are exempt. Calling <c>LogInformation</c>/etc. on an IMLog receiver is allowed
/// because it still routes through the MLog pipeline.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class Mstd0003_LoggingViaMLogAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(MstdDiagnosticDescriptors.Mstd0003LoggingViaMLog);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (MstdAnalyzerHelpers.IsTestAssembly(context.Compilation))
        {
            return;
        }

        var invocation = (InvocationExpressionSyntax)context.Node;

        string ns = MstdAnalyzerHelpers.GetNamespace(invocation);
        if (!MstdAnalyzerHelpers.IsMuonroiNamespace(ns)
            || MstdAnalyzerHelpers.IsLoggingInfrastructureNamespace(ns))
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
        {
            return;
        }

        string? sink = ClassifyForbiddenSink(invocation, method, context.SemanticModel);
        if (sink is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            MstdDiagnosticDescriptors.Mstd0003LoggingViaMLog,
            invocation.GetLocation(),
            sink,
            ns));
    }

    private static string? ClassifyForbiddenSink(
        InvocationExpressionSyntax invocation, IMethodSymbol method, SemanticModel model)
    {
        INamedTypeSymbol? containingType = method.ContainingType;
        string containingFullName = containingType?.ToDisplayString() ?? string.Empty;
        string methodName = method.Name;

        // 1. System.Console.Write / WriteLine
        if (containingFullName == "System.Console"
            && (methodName == "Write" || methodName == "WriteLine"))
        {
            return "Console." + methodName;
        }

        // 2. System.Diagnostics.Debug.* / System.Diagnostics.Trace.*
        if (containingFullName == "System.Diagnostics.Debug"
            || containingFullName == "System.Diagnostics.Trace")
        {
            return containingType!.Name + "." + methodName;
        }

        // 3. Static Serilog.Log.*
        if (containingFullName == "Serilog.Log")
        {
            return "Serilog.Log." + methodName;
        }

        // 4. Console.Error.* / Console.Out.* (TextWriter writes on the console streams)
        if ((methodName == "Write" || methodName == "WriteLine")
            && invocation.Expression is MemberAccessExpressionSyntax writeAccess
            && IsConsoleStream(writeAccess.Expression, model))
        {
            return "Console.Error/Out." + methodName;
        }

        // 5. Raw ILogger Log* call on a non-IMLog receiver
        if (IsMicrosoftLoggingMethod(method)
            && IsLogWriteName(methodName))
        {
            ITypeSymbol? receiver = GetReceiverType(invocation, model);
            if (receiver is not null
                && MstdAnalyzerHelpers.ImplementsILogger(receiver)
                && !MstdAnalyzerHelpers.IsIMLog(receiver))
            {
                return "ILogger." + methodName;
            }
        }

        return null;
    }

    private static bool IsConsoleStream(ExpressionSyntax expr, SemanticModel model)
    {
        if (model.GetSymbolInfo(expr).Symbol is IPropertySymbol prop)
        {
            return (prop.Name == "Error" || prop.Name == "Out")
                && prop.ContainingType?.ToDisplayString() == "System.Console";
        }

        return false;
    }

    private static bool IsMicrosoftLoggingMethod(IMethodSymbol method)
    {
        // Extension methods live on Microsoft.Extensions.Logging.LoggerExtensions; the core
        // Log/BeginScope members live on Microsoft.Extensions.Logging.ILogger itself.
        INamedTypeSymbol? containingType = method.ContainingType;
        if (containingType is null)
        {
            return false;
        }

        if (containingType.ContainingNamespace?.ToDisplayString() != "Microsoft.Extensions.Logging")
        {
            return false;
        }

        return containingType.Name == "LoggerExtensions"
            || containingType.Name == "ILogger"
            || MstdAnalyzerHelpers.ImplementsILogger(containingType);
    }

    private static bool IsLogWriteName(string name)
    {
        // Log, LogInformation, LogError, LogWarning, LogDebug, LogTrace, LogCritical.
        return name == "Log"
            || (name.StartsWith("Log", StringComparison.Ordinal) && name.Length > 3);
    }

    private static ITypeSymbol? GetReceiverType(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
            ? model.GetTypeInfo(memberAccess.Expression).Type
            : null;
    }
}
