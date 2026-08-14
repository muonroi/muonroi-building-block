namespace Muonroi.Diagnostics.Generator;

/// <summary>
/// Source generator that emits trace wrapper methods for attributed methods.
/// </summary>
[Generator]
public sealed class TraceableGenerator : IIncrementalGenerator
{
    /// <summary>Registers syntax transforms and source output for tracing.</summary>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.SyntaxProvider
            .CreateSyntaxProvider(
                (node, _) => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0,
                (ctx, _) => GetMethodTarget(ctx))
            .Where(m => m != null);

        context.RegisterSourceOutput(provider.Collect(), Execute!);
    }

    private static MethodDeclarationSyntax? GetMethodTarget(GeneratorSyntaxContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;
        foreach (var attrList in method.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                if (context.SemanticModel.GetSymbolInfo(attr).Symbol is IMethodSymbol symbol &&
                    symbol.ContainingType.ToDisplayString() == "Muonroi.Core.Abstractions.Diagnostics.MTraceableAttribute")
                {
                    return method;
                }
            }
        }
        return null;
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<MethodDeclarationSyntax> methods)
    {
        if (methods.IsDefaultOrEmpty) return;

        foreach (var group in methods.GroupBy(m => m.Parent as ClassDeclarationSyntax))
        {
            var classDecl = group.Key;
            if (classDecl == null) continue;

            var sb = new StringBuilder();
            var namespaceName = GetNamespace(classDecl);
            
            sb.AppendLine("using System;");
            sb.AppendLine("using Muonroi.Core.Abstractions.Diagnostics;");
            sb.AppendLine($"namespace {namespaceName};");
            sb.AppendLine($"partial class {classDecl.Identifier.Text}");
            sb.AppendLine("{");

            foreach (var method in group)
            {
                GenerateInstrumentedMethod(sb, method);
            }

            sb.AppendLine("}");
            context.AddSource($"{classDecl.Identifier.Text}_Traces.g.cs", sb.ToString());
        }
    }

    private static void GenerateInstrumentedMethod(StringBuilder sb, MethodDeclarationSyntax method)
    {
        // For simplicity in this version, we wrap the original call.
        // In a full implementation, we'd use the SyntaxRewriter to inject into the body.
        var methodName = method.Identifier.Text;
        sb.AppendLine($"    public void {methodName}_TraceWrapper()");
        sb.AppendLine("    {");
        sb.AppendLine($"        using var scope = Muonroi.Core.Abstractions.Context.MTraceContextHolder.Current.Value?.BeginNode(\"{methodName}\", MTraceNodeType.Custom);");
        sb.AppendLine($"        {methodName}();");
        sb.AppendLine("    }");
    }

    private static string GetNamespace(SyntaxNode node)
    {
        var ns = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        return ns?.Name.ToString() ?? "Global";
    }
}
