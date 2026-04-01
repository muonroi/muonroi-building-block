using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators;

/// <summary>
/// Incremental source generator that discovers partial ISiteProfile classes decorated with
/// [GenerateSiteProfile("siteId", typeof(DbContext))] and emits a partial RegisterServices()
/// method with DbContext registration, [SiteProfileBehavior] Apply() calls, and a
/// partial void RegisterAdditionalServices() extensibility hook.
///
/// Complements SiteProfileRegistrationGenerator (which emits AddGeneratedSiteProfiles + SiteIds).
/// This generator emits per-class partial implementation files.
/// </summary>
[Generator]
public sealed class SiteProfileScaffoldingGenerator : IIncrementalGenerator
{
    private const string GenerateSiteProfileAttributeName = "GenerateSiteProfile";
    private const string SiteProfileBehaviorAttributeName = "SiteProfileBehavior";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Scan for class declarations that have at least one attribute
        IncrementalValuesProvider<ScaffoldingModel?> candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsClassWithAttributes(s),
                transform: static (ctx, _) => GetScaffoldingModel(ctx))
            .Where(static m => m is not null);

        IncrementalValueProvider<ImmutableArray<ScaffoldingModel?>> collected = candidates.Collect();

        context.RegisterSourceOutput(collected, static (spc, models) => Execute(models, spc));
    }

    private static bool IsClassWithAttributes(SyntaxNode node)
        => node is ClassDeclarationSyntax cls && cls.AttributeLists.Count > 0;

    private static ScaffoldingModel? GetScaffoldingModel(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return null;

        // Find [GenerateSiteProfile] attribute
        AttributeData? generateAttr = FindAttribute(classSymbol, GenerateSiteProfileAttributeName);
        if (generateAttr is null)
            return null;

        // Extract siteId (arg 0) and dbContextType (arg 1)
        if (generateAttr.ConstructorArguments.Length < 2)
            return null;

        string? siteId = generateAttr.ConstructorArguments[0].Value as string;
        if (siteId is null)
            return null;

        if (generateAttr.ConstructorArguments[1].Value is not INamedTypeSymbol dbContextSymbol)
            return null;

        // Check if class is partial
        bool isPartial = classDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

        // Extract namespace
        string namespaceName = GetNamespace(classSymbol);

        // Extract SkipDbContextRegistration named argument (default false)
        bool skipDbContext = false;
        foreach (var namedArg in generateAttr.NamedArguments)
        {
            if (namedArg.Key == "SkipDbContextRegistration" && namedArg.Value.Value is bool val)
            {
                skipDbContext = val;
                break;
            }
        }

        // Collect [SiteProfileBehavior] attributes (AllowMultiple = true)
        List<string> behaviorTypeNames = new List<string>();
        foreach (var attr in classSymbol.GetAttributes())
        {
            if (attr.AttributeClass is null) continue;
            if (attr.AttributeClass.Name == SiteProfileBehaviorAttributeName ||
                attr.AttributeClass.Name == SiteProfileBehaviorAttributeName + "Attribute")
            {
                if (attr.ConstructorArguments.Length >= 1 &&
                    attr.ConstructorArguments[0].Value is INamedTypeSymbol behaviorSymbol)
                {
                    behaviorTypeNames.Add(behaviorSymbol.ToDisplayString());
                }
            }
        }

        return new ScaffoldingModel(
            className: classSymbol.Name,
            namespaceName: namespaceName,
            siteId: siteId,
            dbContextTypeName: dbContextSymbol.ToDisplayString(),
            behaviorTypeNames: behaviorTypeNames,
            isPartial: isPartial,
            skipDbContextRegistration: skipDbContext);
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

    private static void Execute(ImmutableArray<ScaffoldingModel?> models, SourceProductionContext context)
    {
        if (models.IsDefaultOrEmpty)
            return;

        foreach (var model in models)
        {
            if (model is null) continue;

            // Emit diagnostic if not partial
            if (!model.IsPartial)
            {
                var descriptor = new DiagnosticDescriptor(
                    id: "MSP010",
                    title: "ISiteProfile class should be partial for [GenerateSiteProfile]",
                    messageFormat: "Class '{0}' is decorated with [GenerateSiteProfile] but is not partial. Add the 'partial' keyword to enable source generation.",
                    category: "Muonroi.Tenancy.SiteProfile",
                    DiagnosticSeverity.Info,
                    isEnabledByDefault: true);

                context.ReportDiagnostic(
                    Diagnostic.Create(descriptor, Location.None, model.ClassName));
                // Still emit the file even if not partial — helps during refactoring
            }

            string source = EmitScaffoldedRegisterServices(model);
            string hintName = $"{model.ClassName}.RegisterServices.g.cs";
            context.AddSource(hintName, source);
        }
    }

    private static string EmitScaffoldedRegisterServices(ScaffoldingModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated by Muonroi.Tenancy.SiteProfile.SourceGenerators — SiteProfileScaffoldingGenerator.");
        sb.AppendLine("// Do not edit manually. Add custom registrations via partial void RegisterAdditionalServices().");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using Microsoft.Extensions.Configuration;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Muonroi.Tenancy.SiteProfile;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(model.NamespaceName))
        {
            sb.AppendLine($"namespace {model.NamespaceName};");
            sb.AppendLine();
        }

        sb.AppendLine($"public partial class {model.ClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <inheritdoc/>");
        sb.AppendLine("    public void RegisterServices(IServiceCollection services, IConfiguration configuration)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var log = services.BuildServiceProvider().GetService<Muonroi.Logging.Abstractions.IMLogFactory>()?.CreateLogger(\"Muonroi.SiteProfile.AOT.{model.ClassName}\");");
        sb.AppendLine($"        log?.Info(\"[SiteProfile-AOT] {model.ClassName}.RegisterServices — begin (site: {EscapeStringLiteral(model.SiteId)})\");");

        // DbContext registration (skipped when SkipDbContextRegistration = true)
        if (!model.SkipDbContextRegistration)
        {
            sb.AppendLine($"        // DbContext registration for site \"{model.SiteId}\"");
            sb.AppendLine($"        // Ecosystem: registers DbContextOptions<T> only (no non-generic) — prevents Autofac conflict");
            sb.AppendLine($"        // with EFCoreStoreDbContext<TenantInfo>. Safe for multiple site DbContexts in the same container.");
            sb.AppendLine($"        Muonroi.Tenancy.SiteProfile.Web.SiteProfileDbContextExtensions.AddSiteDbContext<{model.DbContextTypeName}>(services);");
            sb.AppendLine($"        log?.Info(\"[SiteProfile-AOT] {model.ClassName} — registered DbContext: {{DbContextType}}\", \"{EscapeStringLiteral(model.DbContextTypeName)}\");");
        }
        else
        {
            sb.AppendLine($"        // DbContext registration SKIPPED for site \"{model.SiteId}\" (SkipDbContextRegistration = true)");
            sb.AppendLine($"        // Consumer registers DbContext via its own infrastructure (e.g., AddInternalInfrastructure)");
            sb.AppendLine($"        log?.Debug(\"[SiteProfile-AOT] {model.ClassName} — DbContext registration skipped (SkipDbContextRegistration=true)\");");
        }

        // Behavior Apply() calls
        if (model.BehaviorTypeNames.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("        // Behavior composition (per [SiteProfileBehavior] attributes)");
            foreach (var behaviorTypeName in model.BehaviorTypeNames)
            {
                sb.AppendLine($"        new {behaviorTypeName}().Apply(services, configuration, \"{EscapeStringLiteral(model.SiteId)}\");");
                sb.AppendLine($"        log?.Info(\"[SiteProfile-AOT] {model.ClassName} — applied behavior: {{BehaviorType}}\", \"{EscapeStringLiteral(behaviorTypeName)}\");");
            }
        }

        // Consumer extensibility hook
        sb.AppendLine();
        sb.AppendLine("        // Consumer extensibility: implement partial void RegisterAdditionalServices() in a separate partial file");
        sb.AppendLine("        RegisterAdditionalServices(services, configuration);");
        sb.AppendLine($"        log?.Info(\"[SiteProfile-AOT] {model.ClassName}.RegisterServices — complete (site: {EscapeStringLiteral(model.SiteId)})\");");
        sb.AppendLine("    }");
        sb.AppendLine();

        // Partial extensibility method
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Override point for consumer-specific DI registrations beyond generated scaffolding.");
        sb.AppendLine("    /// Implement this in a separate partial file — not in the generated file.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"services\">The service collection.</param>");
        sb.AppendLine("    /// <param name=\"configuration\">Application configuration.</param>");
        sb.AppendLine("    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration);");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string EscapeStringLiteral(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // -----------------------------------------------------------------------
    // Model
    // -----------------------------------------------------------------------

    private sealed class ScaffoldingModel
    {
        public string ClassName { get; }
        public string NamespaceName { get; }
        public string SiteId { get; }
        public string DbContextTypeName { get; }
        public List<string> BehaviorTypeNames { get; }
        public bool IsPartial { get; }
        public bool SkipDbContextRegistration { get; }

        public ScaffoldingModel(
            string className,
            string namespaceName,
            string siteId,
            string dbContextTypeName,
            List<string> behaviorTypeNames,
            bool isPartial,
            bool skipDbContextRegistration = false)
        {
            ClassName = className;
            NamespaceName = namespaceName;
            SiteId = siteId;
            DbContextTypeName = dbContextTypeName;
            BehaviorTypeNames = behaviorTypeNames;
            IsPartial = isPartial;
            SkipDbContextRegistration = skipDbContextRegistration;
        }
    }
}
