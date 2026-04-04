using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Muonroi.RuleEngine.SourceGenerators.Analyzers;

internal static class MbbAnalyzerHelpers
{
    public static string GetNamespace(SyntaxNode node)
    {
        BaseNamespaceDeclarationSyntax? ns = node.AncestorsAndSelf()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        return ns?.Name.ToString() ?? string.Empty;
    }

    public static bool IsInfrastructureNamespace(string ns)
    {
        return ns.IndexOf(".Infrastructure.", StringComparison.Ordinal) >= 0 ||
               ns.EndsWith(".Infrastructure", StringComparison.Ordinal);
    }

    public static bool IsWrapperOrInfrastructureNamespace(string ns)
    {
        return ns.IndexOf(".Wrappers", StringComparison.Ordinal) >= 0 || IsInfrastructureNamespace(ns);
    }

    public static bool IsContextNamespace(string ns)
    {
        return ns.Equals("Muonroi.Core.Abstractions.Context", StringComparison.Ordinal) ||
               ns.StartsWith("Muonroi.Core.Abstractions.Context.", StringComparison.Ordinal);
    }

    public static bool IsSerilogContextAllowedNamespace(string ns)
    {
        return ns.Equals("Muonroi.Observability", StringComparison.Ordinal) ||
               ns.StartsWith("Muonroi.Observability.", StringComparison.Ordinal) ||
               ns.Equals("Muonroi.Logging", StringComparison.Ordinal) ||
               ns.StartsWith("Muonroi.Logging.", StringComparison.Ordinal);
    }

    public static string GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => string.Empty
        };
    }
}
