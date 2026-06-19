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
}
