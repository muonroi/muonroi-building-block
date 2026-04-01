using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators;

/// <summary>
/// Incremental source generator that discovers partial site profile classes decorated with
/// [SiteProfileAlias("TARGET")] and emits a RegisterAliasServices() partial method that
/// forwards all keyed service registrations from the target site to the alias site.
///
/// Purpose: Eliminates boilerplate for identical sites (D-19..D-21). A site marked
/// [SiteProfileAlias("DEFAULT")] gets all DEFAULT's keyed services without writing any code.
/// Clean migration path: remove attribute and create dedicated classes when site needs to diverge.
///
/// Emits:
/// - MSP030 (Warning): [SiteProfileAlias] used with [SiteProfileBehavior] — behaviors may conflict
/// - MSP031 (Error): [SiteProfileAlias] requires [GenerateSiteProfile] on the same class
/// </summary>
[Generator]
public sealed class SiteProfileAliasGenerator : IIncrementalGenerator
{
    private const string AliasAttributeName = "SiteProfileAlias";
    private const string GenerateAttributeName = "GenerateSiteProfile";
    private const string BehaviorAttributeName = "SiteProfileBehavior";

    // -----------------------------------------------------------------------
    // Diagnostics
    // -----------------------------------------------------------------------

    private static readonly DiagnosticDescriptor Msp030BehaviorConflict = new DiagnosticDescriptor(
        id: "MSP030",
        title: "[SiteProfileAlias] used with [SiteProfileBehavior] — behaviors may not apply to aliased profiles",
        messageFormat: "Class '{0}' uses [SiteProfileAlias] together with [SiteProfileBehavior]. Behaviors are applied to the alias site's registration, not the target's. Consider removing [SiteProfileBehavior] or implementing site-specific behaviors instead of aliasing.",
        category: "Muonroi.Tenancy.SiteProfile",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Msp031MissingGenerateAttribute = new DiagnosticDescriptor(
        id: "MSP031",
        title: "[SiteProfileAlias] requires [GenerateSiteProfile] on the same class",
        messageFormat: "Class '{0}' is decorated with [SiteProfileAlias] but is missing [GenerateSiteProfile]. Add [GenerateSiteProfile(\"{1}\", typeof(DbContext))] to specify the alias site's identity.",
        category: "Muonroi.Tenancy.SiteProfile",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Scan for class declarations that have at least one attribute
        IncrementalValuesProvider<AliasModel?> candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsClassWithAttributes(s),
                transform: static (ctx, _) => GetAliasModel(ctx))
            .Where(static m => m is not null);

        IncrementalValueProvider<ImmutableArray<AliasModel?>> collected = candidates.Collect();

        context.RegisterSourceOutput(collected, static (spc, models) => Execute(models, spc));
    }

    private static bool IsClassWithAttributes(SyntaxNode node)
        => node is ClassDeclarationSyntax cls && cls.AttributeLists.Count > 0;

    private static AliasModel? GetAliasModel(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return null;

        // Only process classes with [SiteProfileAlias]
        AttributeData? aliasAttr = FindAttribute(classSymbol, AliasAttributeName);
        if (aliasAttr is null)
            return null;

        // Extract targetSiteId from [SiteProfileAlias]
        if (aliasAttr.ConstructorArguments.Length < 1)
            return null;

        string? targetSiteId = aliasAttr.ConstructorArguments[0].Value as string;
        if (targetSiteId is null)
            return null;

        // Check for [GenerateSiteProfile] to get the alias site's SiteId
        AttributeData? generateAttr = FindAttribute(classSymbol, GenerateAttributeName);
        string? aliasSiteId = null;
        if (generateAttr is not null && generateAttr.ConstructorArguments.Length >= 1)
        {
            aliasSiteId = generateAttr.ConstructorArguments[0].Value as string;
        }

        // Check for [SiteProfileBehavior] (for MSP030 warning)
        bool hasBehavior = classSymbol.GetAttributes().Any(attr =>
            attr.AttributeClass is not null &&
            (attr.AttributeClass.Name == BehaviorAttributeName ||
             attr.AttributeClass.Name == BehaviorAttributeName + "Attribute"));

        // Check if partial
        bool isPartial = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

        // Extract namespace
        string namespaceName = GetNamespace(classSymbol);

        return new AliasModel(
            className: classSymbol.Name,
            namespaceName: namespaceName,
            aliasSiteId: aliasSiteId,
            targetSiteId: targetSiteId,
            hasGenerateAttribute: generateAttr is not null,
            hasBehaviorAttribute: hasBehavior,
            isPartial: isPartial,
            location: classDecl.Identifier.GetLocation());
    }

    private static AttributeData? FindAttribute(INamedTypeSymbol symbol, string shortName)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass is null) continue;
            string name = attr.AttributeClass.Name;
            if (name == shortName || name == shortName + "Attribute")
                return attr;
        }
        return null;
    }

    private static string GetNamespace(INamedTypeSymbol symbol)
    {
        if (symbol.ContainingNamespace is null || symbol.ContainingNamespace.IsGlobalNamespace)
            return string.Empty;
        return symbol.ContainingNamespace.ToDisplayString();
    }

    private static void Execute(ImmutableArray<AliasModel?> models, SourceProductionContext context)
    {
        if (models.IsDefaultOrEmpty)
            return;

        foreach (var model in models)
        {
            if (model is null) continue;

            // MSP031: [SiteProfileAlias] without [GenerateSiteProfile]
            if (!model.HasGenerateAttribute)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Msp031MissingGenerateAttribute, model.Location,
                        model.ClassName, model.TargetSiteId));
                continue; // Cannot generate without knowing the alias SiteId
            }

            // MSP030: [SiteProfileAlias] + [SiteProfileBehavior] warning
            if (model.HasBehaviorAttribute)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Msp030BehaviorConflict, model.Location,
                        model.ClassName));
                // Continue generating — warning only, not blocking
            }

            string source = EmitAliasRegistration(model);
            string hintName = $"{model.ClassName}.RegisterAliasServices.g.cs";
            context.AddSource(hintName, source);
        }
    }

    private static string EmitAliasRegistration(AliasModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated by Muonroi.Tenancy.SiteProfile.SourceGenerators — SiteProfileAliasGenerator.");
        sb.AppendLine($"// Site \"{model.AliasSiteId}\" aliases all keyed services from \"{model.TargetSiteId}\".");
        sb.AppendLine("// Migration path: remove [SiteProfileAlias], create dedicated service classes.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(model.NamespaceName))
        {
            sb.AppendLine($"namespace {model.NamespaceName};");
            sb.AppendLine();
        }

        sb.AppendLine($"public partial class {model.ClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Auto-generated: registers keyed service aliases for site \"{model.AliasSiteId}\"");
        sb.AppendLine($"    /// pointing to \"{model.TargetSiteId}\"'s implementations.");
        sb.AppendLine("    /// Called from RegisterServices() — do not invoke manually.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"services\">The service collection.</param>");
        sb.AppendLine($"    partial void RegisterAliasServices(IServiceCollection services)");
        sb.AppendLine("    {");
        sb.AppendLine($"        System.Diagnostics.Trace.WriteLine(\"[SiteProfile-AOT] {model.ClassName}.RegisterAliasServices — aliasing '{EscapeStringLiteral(model.AliasSiteId ?? model.ClassName)}' → '{EscapeStringLiteral(model.TargetSiteId)}'\", \"Muonroi.SiteProfile.AOT\");");
        sb.AppendLine($"        // Alias site \"{EscapeStringLiteral(model.AliasSiteId ?? model.ClassName)}\" forwards all keyed service");
        sb.AppendLine($"        // requests to target site \"{EscapeStringLiteral(model.TargetSiteId)}\".");
        sb.AppendLine("        //");
        sb.AppendLine("        // Pattern: AddKeyedScoped<IService>(aliasSiteId, (sp, _) => sp.GetRequiredKeyedService<IService>(targetSiteId))");
        sb.AppendLine("        //");
        sb.AppendLine("        // The concrete service types to alias are discovered at runtime by inspecting");
        sb.AppendLine("        // the keyed service registrations made by the target site profile.");
        sb.AppendLine("        // Use the AliasKeyedServicesFrom extension method for runtime discovery:");
        sb.AppendLine($"        //   services.AliasKeyedServicesFrom(\"{EscapeStringLiteral(model.TargetSiteId)}\", \"{EscapeStringLiteral(model.AliasSiteId ?? model.ClassName)}\");");
        sb.AppendLine("        //");
        sb.AppendLine("        // Example direct usage (if you know the interface types):");
        sb.AppendLine($"        //   services.AddKeyedScoped<IOrderService>(\"{EscapeStringLiteral(model.AliasSiteId ?? model.ClassName)}\",");
        sb.AppendLine($"        //       (sp, _) => sp.GetRequiredKeyedService<IOrderService>(\"{EscapeStringLiteral(model.TargetSiteId)}\"));");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Override point: implement in a separate partial file to add alias-specific registrations.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine($"    partial void RegisterAliasServices(IServiceCollection services);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EscapeStringLiteral(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // -----------------------------------------------------------------------
    // Model
    // -----------------------------------------------------------------------

    private sealed class AliasModel
    {
        public string ClassName { get; }
        public string NamespaceName { get; }
        public string? AliasSiteId { get; }
        public string TargetSiteId { get; }
        public bool HasGenerateAttribute { get; }
        public bool HasBehaviorAttribute { get; }
        public bool IsPartial { get; }
        public Location Location { get; }

        public AliasModel(
            string className,
            string namespaceName,
            string? aliasSiteId,
            string targetSiteId,
            bool hasGenerateAttribute,
            bool hasBehaviorAttribute,
            bool isPartial,
            Location location)
        {
            ClassName = className;
            NamespaceName = namespaceName;
            AliasSiteId = aliasSiteId;
            TargetSiteId = targetSiteId;
            HasGenerateAttribute = hasGenerateAttribute;
            HasBehaviorAttribute = hasBehaviorAttribute;
            IsPartial = isPartial;
            Location = location;
        }
    }
}
