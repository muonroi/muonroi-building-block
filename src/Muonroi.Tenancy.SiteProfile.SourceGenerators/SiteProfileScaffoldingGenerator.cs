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
        string dbContextArg = model.SkipDbContextRegistration
            ? "null"
            : $"typeof({model.DbContextTypeName})";

        string behaviorArg = model.BehaviorTypeNames.Count == 0
            ? "null"
            : $"new System.Type[] {{ {string.Join(", ", model.BehaviorTypeNames.Select(b => $"typeof({b})"))} }}";

        string nsDecl = string.IsNullOrEmpty(model.NamespaceName)
            ? ""
            : $"namespace {model.NamespaceName};\n";

        return $@"// <auto-generated />
// Do not edit manually. Add custom registrations via partial void RegisterAdditionalServices().
#nullable enable
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Tenancy.SiteProfile;
using Muonroi.Tenancy.SiteProfile.Web;

{nsDecl}
public partial class {model.ClassName}
{{
    /// <inheritdoc/>
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {{
        SiteProfileBootstrap.RegisterSiteServices(
            ""{EscapeStringLiteral(model.SiteId)}"",
            {dbContextArg},
            {behaviorArg},
            services,
            configuration,
            skipDbContext: {(model.SkipDbContextRegistration ? "true" : "false")});
        RegisterAdditionalServices(services, configuration);
    }}

    /// <summary>
    /// Override point for consumer-specific DI registrations beyond generated scaffolding.
    /// </summary>
    partial void RegisterAdditionalServices(IServiceCollection services, IConfiguration configuration);
}}
";
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
