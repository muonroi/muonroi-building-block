using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators;

/// <summary>
/// Incremental source generator that discovers all ISiteProfile implementations
/// and emits:
///   1. AddGeneratedSiteProfiles() extension method — AOT-safe, no reflection
///   2. SiteIds static class with const string per profile — compile-time safety
///   3. SiteDbContextTypeRegistry.g.cs with GetAllSiteDbContextTypes() — AOT-safe migration runner support
///
/// Generated output replaces reflection-based AddMultiSiteProfiles(Assembly[]) with
/// explicit new TProfile() instantiation for NativeAOT compatibility.
/// </summary>
[Generator]
public sealed class SiteProfileRegistrationGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // --- Pipeline 1: ISiteProfile scan (existing) ---
        // Scan for non-abstract, non-interface classes implementing ISiteProfile
        IncrementalValuesProvider<INamedTypeSymbol> profileClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax,
                transform: static (ctx, _) => GetSiteProfileSymbol(ctx))
            .Where(static s => s is not null)!;

        IncrementalValueProvider<ImmutableArray<INamedTypeSymbol>> collectedProfiles =
            profileClasses.Collect();

        context.RegisterSourceOutput(collectedProfiles, static (spc, profiles) => Execute(profiles, spc));

        // --- Pipeline 2: [GenerateSiteProfile] DbContext type discovery (new) ---
        // Scan for classes with attributes, extract DbContext type from [GenerateSiteProfile] arg[1]
        IncrementalValuesProvider<INamedTypeSymbol> dbContextTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is ClassDeclarationSyntax cls && cls.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetDbContextTypeFromGenerateSiteProfile(ctx))
            .Where(static s => s is not null)!;

        IncrementalValueProvider<ImmutableArray<INamedTypeSymbol>> collectedDbContextTypes =
            dbContextTypes.Collect();

        context.RegisterSourceOutput(collectedDbContextTypes,
            static (spc, types) => EmitSiteDbContextTypeRegistry(types, spc));

        // --- Pipeline 3: [SiteGrpcService] gRPC service type discovery ---
        // Scans BOTH local source code AND referenced assemblies for [SiteGrpcService].
        // This is critical for Host projects that reference site assemblies (e.g., Sites.TCI.dll)
        // where [SiteGrpcService] lives in a compiled dependency, not in local source.

        // 3a. Local source scan (catches [SiteGrpcService] in current compilation)
        IncrementalValuesProvider<(INamedTypeSymbol Symbol, string SiteId, string? Reason)> localGrpcServices =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => s is ClassDeclarationSyntax cls && cls.AttributeLists.Count > 0,
                    transform: static (ctx, _) => GetSiteGrpcServiceInfo(ctx))
                .Where(static s => s.Symbol is not null)!;

        // 3b. Referenced assembly scan (catches [SiteGrpcService] in compiled site projects)
        IncrementalValueProvider<ImmutableArray<(INamedTypeSymbol Symbol, string SiteId, string? Reason)>> referencedGrpcServices =
            context.CompilationProvider.Select(static (compilation, ct) =>
            {
                var results = ImmutableArray.CreateBuilder<(INamedTypeSymbol Symbol, string SiteId, string? Reason)>();

                INamedTypeSymbol? attrType = compilation.GetTypeByMetadataName("Muonroi.Tenancy.SiteProfile.Grpc.SiteGrpcServiceAttribute");
                if (attrType is null) return results.ToImmutable();

                string grpcAssemblyName = attrType.ContainingAssembly.Name;

                foreach (var reference in compilation.References)
                {
                    ct.ThrowIfCancellationRequested();
                    if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assemblySymbol)
                        continue;

                    if (string.Equals(assemblySymbol.Name, grpcAssemblyName, StringComparison.Ordinal))
                        continue;

                    bool referencesGrpc = false;
                    foreach (var module in assemblySymbol.Modules)
                    {
                        foreach (var refAsm in module.ReferencedAssemblySymbols)
                        {
                            if (string.Equals(refAsm.Name, grpcAssemblyName, StringComparison.Ordinal))
                            {
                                referencesGrpc = true;
                                break;
                            }
                        }
                        if (referencesGrpc) break;
                    }
                    if (!referencesGrpc) continue;

                    ScanNamespaceForSiteGrpcService(assemblySymbol.GlobalNamespace, attrType, results, ct);
                }

                return results.ToImmutable();
            });

        // 3c. Merge local + referenced into single collection
        IncrementalValueProvider<ImmutableArray<(INamedTypeSymbol Symbol, string SiteId, string? Reason)>> allGrpcServices =
            localGrpcServices.Collect().Combine(referencedGrpcServices)
                .Select(static (pair, _) =>
                {
                    var builder = ImmutableArray.CreateBuilder<(INamedTypeSymbol, string, string?)>();
                    builder.AddRange(pair.Left);
                    builder.AddRange(pair.Right);
                    return builder.ToImmutable();
                });

        context.RegisterSourceOutput(allGrpcServices,
            static (spc, services) => EmitSiteGrpcServiceRegistry(services, spc));

        // --- Pipeline 4: [SiteEntityMap] entity hierarchy type discovery ---
        IncrementalValuesProvider<(string SiteId, string CoreTypeName, string SiteTypeName, string TableName)?> entityMaps =
            context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => s is ClassDeclarationSyntax cls && cls.AttributeLists.Count > 0,
                    transform: static (ctx, _) => GetEntityMapsFromClass(ctx))
                .SelectMany(static (list, _) => list);

        IncrementalValueProvider<ImmutableArray<(string SiteId, string CoreTypeName, string SiteTypeName, string TableName)>> collectedEntityMaps =
            entityMaps.Where(static x => x is not null).Select(static (x, _) => x!.Value).Collect();

        context.RegisterSourceOutput(collectedEntityMaps,
            static (spc, maps) => EmitSiteEntityTypeRegistry(maps, spc));
    }

    private static INamedTypeSymbol? GetSiteProfileSymbol(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol symbol)
            return null;

        // Skip abstract classes and interfaces
        if (symbol.IsAbstract || symbol.TypeKind == TypeKind.Interface)
            return null;

        // Match by interface name "ISiteProfile" — same pattern as RuleRegistrationGenerator
        if (symbol.AllInterfaces.Any(i => i.Name == "ISiteProfile"))
            return symbol;

        return null;
    }

    private static void Execute(ImmutableArray<INamedTypeSymbol> profiles, SourceProductionContext context)
    {
        // Emit SiteIds even when empty — consumers get a valid (empty) class to reference
        EmitSiteIds(profiles, context);

        if (profiles.IsDefaultOrEmpty)
        {
            EmitEmptyRegistrationExtensions(context);
            return;
        }

        var distinctProfiles = profiles
            .Distinct(SymbolEqualityComparer.Default)
            .Cast<INamedTypeSymbol>()
            .ToList();

        EmitRegistrationExtensions(distinctProfiles, context);
    }

    // -----------------------------------------------------------------------
    // Generator 1: SiteProfileRegistrationExtensions.g.cs
    // -----------------------------------------------------------------------

    private static void EmitRegistrationExtensions(
        List<INamedTypeSymbol> profiles,
        SourceProductionContext context)
    {
        string profileInstantiations = string.Join("\n",
            profiles.Select(p => $"        new {p.ToDisplayString()}(),"));

        string source = $@"// <auto-generated />
#nullable enable
using Muonroi.Tenancy.SiteProfile;
using Muonroi.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Muonroi.Tenancy.SiteProfile.Generated;

/// <summary>
/// AOT-safe manifest of all discovered ISiteProfile implementations.
/// Each profile is instantiated via new() — no Activator.CreateInstance.
/// </summary>
internal static class SiteProfileManifest
{{
    public static ISiteProfile[] CreateAll() => new ISiteProfile[]
    {{
{profileInstantiations}
    }};
}}

/// <summary>
/// AOT-safe alternative to AddMultiSiteProfiles — delegates to AddMultiSiteProfilesCore
/// which reuses ALL existing ecosystem logic (tracker, validator, strict mode, error handling).
/// </summary>
internal static class SiteProfileRegistrationExtensions
{{
    public static IServiceCollection AddGeneratedSiteProfiles(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<IServiceProvider, string?> siteCodeAccessor,
        IMLog? diagnosticLog = null)
        => services.AddMultiSiteProfilesCore(
            configuration, siteCodeAccessor, SiteProfileManifest.CreateAll(), diagnosticLog);
}}
";
        context.AddSource("SiteProfileRegistrationExtensions.g.cs", source);
    }

    private static void EmitEmptyRegistrationExtensions(SourceProductionContext context)
    {
        const string source = @"// <auto-generated />
// No ISiteProfile implementations found in this compilation.
#nullable enable
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Tenancy.SiteProfile;

namespace Muonroi.Tenancy.SiteProfile.Generated;

internal static class SiteProfileRegistrationExtensions
{
    /// <summary>No ISiteProfile implementations found — AddGeneratedSiteProfiles is a no-op.</summary>
    public static IServiceCollection AddGeneratedSiteProfiles(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<IServiceProvider, string?> siteCodeAccessor)
        => services;
}
";
        context.AddSource("SiteProfileRegistrationExtensions.g.cs", source);
    }

    // -----------------------------------------------------------------------
    // Generator 2: SiteIds.g.cs
    // -----------------------------------------------------------------------

    private static void EmitSiteIds(
        ImmutableArray<INamedTypeSymbol> profiles,
        SourceProductionContext context)
    {
        string constants = "";
        if (!profiles.IsDefaultOrEmpty)
        {
            constants = string.Join("\n", profiles
                .Distinct(SymbolEqualityComparer.Default)
                .Cast<INamedTypeSymbol>()
                .Select(p => (Profile: p, SiteId: ExtractSiteIdValue(p)))
                .Where(x => x.SiteId is not null)
                .Select(x =>
                    $"    /// <summary>SiteId constant for {x.Profile.Name} — value: \"{x.SiteId}\"</summary>\n" +
                    $"    internal const string {SanitizeIdentifier(x.SiteId!)} = \"{EscapeStringLiteral(x.SiteId!)}\";"));
        }

        string source = $@"// <auto-generated />
// Compile-time SiteId constants — use instead of string literals.

namespace Muonroi.Tenancy.SiteProfile.Generated;

/// <summary>
/// Compile-time SiteId constants — typo becomes compile error per MSP001 analyzer.
/// </summary>
internal static class SiteIds
{{
{constants}
}}
";
        context.AddSource("SiteIds.g.cs", source);
    }

    /// <summary>
    /// Extracts the compile-time SiteId string value from a property declaration.
    /// Handles arrow expression (=> "VALUE") and auto-property initializer (= "VALUE").
    /// </summary>
    private static string? ExtractSiteIdValue(INamedTypeSymbol profileSymbol)
    {
        var siteIdProp = profileSymbol.GetMembers("SiteId")
            .OfType<IPropertySymbol>()
            .FirstOrDefault();
        if (siteIdProp is null) return null;

        foreach (var syntaxRef in siteIdProp.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax();

            if (syntax is PropertyDeclarationSyntax propDecl)
            {
                // Arrow expression: string SiteId => "VALUE"
                if (propDecl.ExpressionBody?.Expression is LiteralExpressionSyntax arrowLiteral)
                    return arrowLiteral.Token.ValueText;

                // Auto-property with initializer: string SiteId { get; } = "VALUE"
                if (propDecl.Initializer?.Value is LiteralExpressionSyntax initLiteral)
                    return initLiteral.Token.ValueText;

                // Getter with return: string SiteId { get { return "VALUE"; } }
                var getter = propDecl.AccessorList?.Accessors
                    .FirstOrDefault(a => a.Keyword.ValueText == "get");
                if (getter?.Body?.Statements.FirstOrDefault() is ReturnStatementSyntax ret
                    && ret.Expression is LiteralExpressionSyntax retLiteral)
                    return retLiteral.Token.ValueText;
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a SiteId string value to a valid C# identifier (uppercase, replace non-alphanumeric with underscore).
    /// </summary>
    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrEmpty(value)) return "_EMPTY";

        var sb = new StringBuilder(value.Length);
        foreach (char c in value.ToUpperInvariant())
        {
            sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        }

        // Prefix with underscore if starts with digit
        if (char.IsDigit(sb[0]))
            sb.Insert(0, '_');

        return sb.ToString();
    }

    /// <summary>
    /// Escapes special characters in a string literal for C# source generation.
    /// </summary>
    private static string EscapeStringLiteral(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // -----------------------------------------------------------------------
    // Pipeline 2 helpers: [GenerateSiteProfile] DbContext type discovery
    // -----------------------------------------------------------------------

    /// <summary>
    /// Extracts the DbContext type symbol from [GenerateSiteProfile("siteId", typeof(DbContext))].
    /// Returns null if class has no [GenerateSiteProfile] attribute or arg[1] is not a type.
    /// </summary>
    private static INamedTypeSymbol? GetDbContextTypeFromGenerateSiteProfile(GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return null;

        // Find [GenerateSiteProfile] or [GenerateSiteProfileAttribute]
        foreach (var attr in classSymbol.GetAttributes())
        {
            if (attr.AttributeClass is null) continue;
            string name = attr.AttributeClass.Name;
            if (name != "GenerateSiteProfile" && name != "GenerateSiteProfileAttribute")
                continue;

            // Must have at least 2 constructor args: siteId (string) + dbContextType (Type)
            if (attr.ConstructorArguments.Length < 2)
                return null;

            if (attr.ConstructorArguments[1].Value is INamedTypeSymbol dbContextSymbol)
                return dbContextSymbol;
        }

        return null;
    }

    // -----------------------------------------------------------------------
    // Pipeline 3 helpers: [SiteGrpcService] gRPC service type discovery
    // -----------------------------------------------------------------------

    /// <summary>
    /// Recursively scans a namespace for types with [SiteGrpcService] attribute.
    /// Used by Pipeline 3b to discover services in referenced assemblies.
    /// </summary>
    private static void ScanNamespaceForSiteGrpcService(
        INamespaceSymbol ns,
        INamedTypeSymbol attrType,
        ImmutableArray<(INamedTypeSymbol Symbol, string SiteId, string? Reason)>.Builder results,
        System.Threading.CancellationToken ct)
    {
        foreach (var member in ns.GetMembers())
        {
            ct.ThrowIfCancellationRequested();

            if (member is INamespaceSymbol childNs)
            {
                ScanNamespaceForSiteGrpcService(childNs, attrType, results, ct);
            }
            else if (member is INamedTypeSymbol typeSymbol && typeSymbol.TypeKind == TypeKind.Class && !typeSymbol.IsAbstract)
            {
                foreach (var attr in typeSymbol.GetAttributes())
                {
                    if (attr.AttributeClass is null) continue;
                    if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrType)) continue;

                    if (attr.ConstructorArguments.Length < 1) continue;
                    if (attr.ConstructorArguments[0].Value is not string siteId) continue;

                    string? reason = null;
                    foreach (var namedArg in attr.NamedArguments)
                    {
                        if (namedArg.Key == "Reason" && namedArg.Value.Value is string r)
                            reason = r;
                    }

                    results.Add((typeSymbol, siteId, reason));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Extracts (symbol, siteId, reason?) from a class with [SiteGrpcService("siteId")] attribute.
    /// Returns default tuple with null Symbol when attribute is absent.
    /// </summary>
    private static (INamedTypeSymbol? Symbol, string SiteId, string? Reason) GetSiteGrpcServiceInfo(
        GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return default;

        foreach (var attr in classSymbol.GetAttributes())
        {
            if (attr.AttributeClass is null) continue;
            string name = attr.AttributeClass.Name;
            if (name != "SiteGrpcService" && name != "SiteGrpcServiceAttribute")
                continue;

            // ConstructorArguments[0] = siteId (string, required)
            if (attr.ConstructorArguments.Length < 1)
                continue;
            if (attr.ConstructorArguments[0].Value is not string siteId)
                continue;

            // Named argument Reason (optional)
            string? reason = null;
            foreach (var namedArg in attr.NamedArguments)
            {
                if (namedArg.Key == "Reason" && namedArg.Value.Value is string r)
                    reason = r;
            }

            return (classSymbol, siteId, reason);
        }

        return default;
    }

    // -----------------------------------------------------------------------
    // Generator 4: SiteGrpcServiceRegistry.g.cs
    // -----------------------------------------------------------------------

    /// <summary>
    /// Emits SiteGrpcServiceRegistry.g.cs with:
    ///   - SiteGrpcServiceDescriptor record (SiteId, ServiceType, Reason)
    ///   - SiteGrpcServiceRegistry static class with GetAllSiteGrpcServices()
    /// AOT-safe — no reflection at runtime.
    /// </summary>
    private static void EmitSiteGrpcServiceRegistry(
        ImmutableArray<(INamedTypeSymbol Symbol, string SiteId, string? Reason)> services,
        SourceProductionContext context)
    {
        string arrayBody;
        if (services.IsDefaultOrEmpty)
        {
            arrayBody = "        => System.Array.Empty<GeneratedSiteGrpcServiceDescriptor>();";
        }
        else
        {
            var distinctServices = services
                .Where(s => s.Symbol is not null)
                .GroupBy(s => s.Symbol.ToDisplayString(), StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            if (distinctServices.Count == 0)
            {
                arrayBody = "        => System.Array.Empty<GeneratedSiteGrpcServiceDescriptor>();";
            }
            else
            {
                string entries = string.Join("\n", distinctServices.Select(x =>
                {
                    string fullName = x.Symbol.ToDisplayString();
                    string sid = EscapeStringLiteral(x.SiteId);
                    return x.Reason is not null
                        ? $"            new GeneratedSiteGrpcServiceDescriptor(\"{sid}\", typeof({fullName}), \"{EscapeStringLiteral(x.Reason)}\"),"
                        : $"            new GeneratedSiteGrpcServiceDescriptor(\"{sid}\", typeof({fullName})),";
                }));
                arrayBody = $@"        => new GeneratedSiteGrpcServiceDescriptor[]
        {{
{entries}
        }};";
            }
        }

        string source = $@"// <auto-generated />
#nullable enable
using System;
using System.Collections.Generic;

namespace Muonroi.Tenancy.SiteProfile.Generated;

internal sealed record GeneratedSiteGrpcServiceDescriptor(
    string SiteId, System.Type ServiceType, string? Reason = null);

/// <summary>
/// Registry of all gRPC service types from [SiteGrpcService]. AOT-safe.
/// </summary>
internal static class SiteGrpcServiceRegistry
{{
    public static IReadOnlyList<GeneratedSiteGrpcServiceDescriptor> GetAllSiteGrpcServices()
{arrayBody}
}}
";
        context.AddSource("SiteGrpcServiceRegistry.g.cs", source);
    }

    // -----------------------------------------------------------------------
    // Pipeline 4 helpers: [SiteEntityMap] entity hierarchy type discovery
    // -----------------------------------------------------------------------

    /// <summary>
    /// Extracts all (SiteId, CoreTypeName, SiteTypeName, TableName) tuples from a class
    /// that has both [GenerateSiteProfile] (for siteId) and [SiteEntityMap] attributes.
    /// Returns empty when class has no [GenerateSiteProfile] or no [SiteEntityMap].
    /// </summary>
    private static ImmutableArray<(string SiteId, string CoreTypeName, string SiteTypeName, string TableName)?> GetEntityMapsFromClass(
        GeneratorSyntaxContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return ImmutableArray<(string, string, string, string)?>.Empty;

        // Find siteId from [GenerateSiteProfile]
        string? siteId = null;
        foreach (var attr in classSymbol.GetAttributes())
        {
            if (attr.AttributeClass is null) continue;
            string name = attr.AttributeClass.Name;
            if (name != "GenerateSiteProfile" && name != "GenerateSiteProfileAttribute") continue;
            if (attr.ConstructorArguments.Length >= 1 && attr.ConstructorArguments[0].Value is string sid)
                siteId = sid;
            break;
        }
        if (siteId is null)
            return ImmutableArray<(string, string, string, string)?>.Empty;

        // Collect [SiteEntityMap] attributes
        var builder = ImmutableArray.CreateBuilder<(string, string, string, string)?>();
        foreach (var attr in classSymbol.GetAttributes())
        {
            if (attr.AttributeClass is null) continue;
            string name = attr.AttributeClass.Name;
            if (name != "SiteEntityMap" && name != "SiteEntityMapAttribute") continue;
            if (attr.ConstructorArguments.Length < 3) continue;
            if (attr.ConstructorArguments[0].Value is not INamedTypeSymbol coreSymbol) continue;
            if (attr.ConstructorArguments[1].Value is not INamedTypeSymbol siteSymbol) continue;
            if (attr.ConstructorArguments[2].Value is not string tableName) continue;
            builder.Add((siteId, coreSymbol.ToDisplayString(), siteSymbol.ToDisplayString(), tableName));
        }
        return builder.ToImmutable();
    }

    // -----------------------------------------------------------------------
    // Generator 5: SiteEntityTypeRegistry.g.cs
    // -----------------------------------------------------------------------

    /// <summary>
    /// Emits SiteEntityTypeRegistry.g.cs with:
    ///   - GeneratedSiteEntityDescriptor record (SiteId, CoreType, SiteType, TableName)
    ///   - SiteEntityTypeRegistry static class with GetAllSiteEntityTypes()
    /// AOT-safe — no reflection at runtime.
    /// </summary>
    private static void EmitSiteEntityTypeRegistry(
        ImmutableArray<(string SiteId, string CoreTypeName, string SiteTypeName, string TableName)> maps,
        SourceProductionContext context)
    {
        string arrayBody;
        if (maps.IsDefaultOrEmpty)
        {
            arrayBody = "        => System.Array.Empty<GeneratedSiteEntityDescriptor>();";
        }
        else
        {
            var distinctMaps = maps
                .GroupBy(m => $"{m.SiteId}|{m.CoreTypeName}|{m.SiteTypeName}", StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();

            if (distinctMaps.Count == 0)
            {
                arrayBody = "        => System.Array.Empty<GeneratedSiteEntityDescriptor>();";
            }
            else
            {
                string entries = string.Join("\n", distinctMaps.Select(m =>
                    $"            new GeneratedSiteEntityDescriptor(\"{EscapeStringLiteral(m.SiteId)}\", typeof({m.CoreTypeName}), typeof({m.SiteTypeName}), \"{EscapeStringLiteral(m.TableName)}\"),"));
                arrayBody = $@"        => new GeneratedSiteEntityDescriptor[]
        {{
{entries}
        }};";
            }
        }

        string source = $@"// <auto-generated />
#nullable enable
using System;
using System.Collections.Generic;

namespace Muonroi.Tenancy.SiteProfile.Generated;

/// <summary>
/// Descriptor for a site entity hierarchy mapping from [SiteEntityMap].
/// </summary>
internal sealed record GeneratedSiteEntityDescriptor(
    string SiteId, System.Type CoreType, System.Type SiteType, string TableName);

/// <summary>
/// Registry of all entity hierarchy mappings from [SiteEntityMap]. AOT-safe.
/// </summary>
internal static class SiteEntityTypeRegistry
{{
    /// <summary>
    /// Returns all entity type pairs declared via [SiteEntityMap] across all SiteProfiles.
    /// </summary>
    public static IReadOnlyList<GeneratedSiteEntityDescriptor> GetAllSiteEntityTypes()
{arrayBody}
}}
";
        context.AddSource("SiteEntityTypeRegistry.g.cs", source);
    }

    // -----------------------------------------------------------------------
    // Generator 3: SiteDbContextTypeRegistry.g.cs
    // -----------------------------------------------------------------------

    /// <summary>
    /// Emits SiteDbContextTypeRegistry.g.cs with GetAllSiteDbContextTypes() method.
    /// Returns all DbContext types discovered from [GenerateSiteProfile] attributes.
    /// AOT-safe — no reflection. Used by SiteMigrationRunner at startup.
    /// </summary>
    private static void EmitSiteDbContextTypeRegistry(
        ImmutableArray<INamedTypeSymbol> dbContextTypes,
        SourceProductionContext context)
    {
        string arrayBody;
        if (dbContextTypes.IsDefaultOrEmpty)
        {
            arrayBody = "        => System.Array.Empty<System.Type>();";
        }
        else
        {
            var distinctTypes = dbContextTypes
                .Distinct(SymbolEqualityComparer.Default)
                .Cast<INamedTypeSymbol>()
                .ToList();

            if (distinctTypes.Count == 0)
            {
                arrayBody = "        => System.Array.Empty<System.Type>();";
            }
            else
            {
                string entries = string.Join("\n",
                    distinctTypes.Select(t => $"            typeof({t.ToDisplayString()}),"));
                arrayBody = $@"        => new System.Type[]
        {{
{entries}
        }};";
            }
        }

        string source = $@"// <auto-generated />
#nullable enable
using System;
using System.Collections.Generic;

namespace Muonroi.Tenancy.SiteProfile.Generated;

/// <summary>
/// Registry of all site DbContext types from [GenerateSiteProfile]. AOT-safe.
/// </summary>
internal static class SiteDbContextTypeRegistry
{{
    public static IReadOnlyList<System.Type> GetAllSiteDbContextTypes()
{arrayBody}
}}
";
        context.AddSource("SiteDbContextTypeRegistry.g.cs", source);
    }
}
