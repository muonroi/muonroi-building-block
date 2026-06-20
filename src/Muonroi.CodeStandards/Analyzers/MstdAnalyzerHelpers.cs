using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Muonroi.CodeStandards.Analyzers;

internal static class MstdAnalyzerHelpers
{
    public static string GetNamespace(SyntaxNode node)
    {
        BaseNamespaceDeclarationSyntax? ns = node.AncestorsAndSelf()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        return ns?.Name.ToString() ?? string.Empty;
    }

    public static bool IsMuonroiNamespace(string ns)
    {
        return ns.StartsWith("Muonroi.", StringComparison.Ordinal)
            || ns.Equals("Muonroi", StringComparison.Ordinal);
    }

    public static bool IsTestAssembly(Compilation compilation)
    {
        string assemblyName = compilation.AssemblyName ?? string.Empty;
        return assemblyName.IndexOf(".Tests", StringComparison.Ordinal) >= 0;
    }

    public static bool InheritsFromMException(ITypeSymbol type)
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

    /// <summary>
    /// The Muonroi.Logging / Muonroi.Logging.Abstractions projects legitimately wrap the raw
    /// Microsoft <c>ILogger</c>; logging-sink rules do not apply inside them.
    /// </summary>
    public static bool IsLoggingInfrastructureNamespace(string ns)
    {
        return ns.StartsWith("Muonroi.Logging", StringComparison.Ordinal);
    }

    /// <summary>Returns true when <paramref name="type"/> is or implements Microsoft.Extensions.Logging.ILogger.</summary>
    public static bool ImplementsILogger(ITypeSymbol type)
    {
        if (IsMicrosoftILogger(type))
        {
            return true;
        }

        foreach (INamedTypeSymbol iface in type.AllInterfaces)
        {
            if (IsMicrosoftILogger(iface))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns true when <paramref name="type"/> is or implements Muonroi.Logging.Abstractions.IMLog (incl. IMLog&lt;T&gt;).</summary>
    public static bool IsIMLog(ITypeSymbol type)
    {
        if (IsIMLogType(type))
        {
            return true;
        }

        foreach (INamedTypeSymbol iface in type.AllInterfaces)
        {
            if (IsIMLogType(iface))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMicrosoftILogger(ITypeSymbol type)
    {
        return type.Name == "ILogger"
            && type.ContainingNamespace?.ToDisplayString() == "Microsoft.Extensions.Logging";
    }

    private static bool IsIMLogType(ITypeSymbol type)
    {
        // IMLog and IMLog<T> share Name "IMLog" and the same namespace.
        return type.Name == "IMLog"
            && type.ContainingNamespace?.ToDisplayString() == "Muonroi.Logging.Abstractions";
    }
}
