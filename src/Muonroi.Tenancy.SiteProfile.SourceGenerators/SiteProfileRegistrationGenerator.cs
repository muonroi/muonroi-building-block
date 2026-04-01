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
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// This file was generated by Muonroi.Tenancy.SiteProfile.SourceGenerators.");
        sb.AppendLine("// Do not edit manually.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using Microsoft.Extensions.Configuration;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Muonroi.Tenancy.SiteProfile;");
        sb.AppendLine("using Muonroi.Logging.Abstractions;");
        sb.AppendLine();
        sb.AppendLine("namespace Muonroi.Tenancy.SiteProfile.Generated;");
        sb.AppendLine();
        sb.AppendLine("internal static class SiteProfileRegistrationExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// AOT-safe alternative to AddMultiSiteProfiles with Assembly[] params.");
        sb.AppendLine("    /// Registers all discovered ISiteProfile implementations without reflection.");
        sb.AppendLine("    /// Generated by Muonroi.Tenancy.SiteProfile.SourceGenerators.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"services\">The service collection.</param>");
        sb.AppendLine("    /// <param name=\"configuration\">Application configuration.</param>");
        sb.AppendLine("    /// <param name=\"siteCodeAccessor\">Per-request site code resolution delegate.</param>");
        sb.AppendLine("    /// <param name=\"logFactory\">Optional IMLogFactory for structured logging to centralized systems (Elasticsearch, etc.).</param>");
        sb.AppendLine("    public static IServiceCollection AddGeneratedSiteProfiles(");
        sb.AppendLine("        this IServiceCollection services,");
        sb.AppendLine("        IConfiguration configuration,");
        sb.AppendLine("        Func<IServiceProvider, string?> siteCodeAccessor,");
        sb.AppendLine("        IMLogFactory? logFactory = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(siteCodeAccessor);");
        sb.AppendLine();
        sb.AppendLine("        var log = logFactory?.CreateLogger(\"Muonroi.SiteProfile.AOT\");");
        sb.AppendLine("        var profiles = new Dictionary<string, ISiteProfile>(StringComparer.OrdinalIgnoreCase);");
        sb.AppendLine();
        sb.AppendLine("        log?.Info(\"[SiteProfile-AOT] AddGeneratedSiteProfiles — begin registration\");");
        sb.AppendLine();
        sb.AppendLine("        // Explicit instantiation — no Activator.CreateInstance, AOT-safe");

        for (int i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];
            string fullName = profile.ToDisplayString();
            sb.AppendLine($"        var profile{i} = new {fullName}();");
            sb.AppendLine($"        profiles[profile{i}.SiteId] = profile{i};");
            sb.AppendLine($"        log?.Info(\"[SiteProfile-AOT] Instantiated {{SiteId}} ({{ProfileType}})\", profile{i}.SiteId, typeof({fullName}).Name);");
            sb.AppendLine($"        profile{i}.RegisterServices(services, configuration);");
            sb.AppendLine($"        log?.Info(\"[SiteProfile-AOT] Registered services for {{SiteId}}\", profile{i}.SiteId);");
            sb.AppendLine();
        }

        sb.AppendLine("        // Register all profiles as singletons (for diagnostics / enumeration)");
        sb.AppendLine("        foreach (var p in profiles.Values)");
        sb.AppendLine("            services.AddSingleton<ISiteProfile>(p);");
        sb.AppendLine();
        sb.AppendLine("        log?.Info(\"[SiteProfile-AOT] Registered {Count} profile(s) as singletons: [{SiteIds}]\", profiles.Count, string.Join(\", \", profiles.Keys));");
        sb.AppendLine();
        sb.AppendLine("        // Register ISiteProfileResolver — per-request, resolves correct profile by SiteCode");
        sb.AppendLine("        services.AddScoped<ISiteProfileResolver>(sp =>");
        sb.AppendLine("        {");
        sb.AppendLine("            var reqLog = sp.GetService<IMLogFactory>()?.CreateLogger(\"Muonroi.SiteProfile.AOT.Resolver\");");
        sb.AppendLine("            var siteCode = siteCodeAccessor(sp) ?? \"default\";");
        sb.AppendLine("            if (profiles.TryGetValue(siteCode, out var match))");
        sb.AppendLine("            {");
        sb.AppendLine("                reqLog?.Debug(\"[SiteProfile-AOT] Resolved site '{SiteCode}' → {ResolvedSiteId}\", siteCode, match.SiteId);");
        sb.AppendLine("                return new SiteProfileResolver(match);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            // Fallback: try \"default\" profile");
        sb.AppendLine("            if (profiles.TryGetValue(\"default\", out var fallback))");
        sb.AppendLine("            {");
        sb.AppendLine("                reqLog?.Warn(\"[SiteProfile-AOT] Site '{SiteCode}' not found, falling back to 'default'\", siteCode);");
        sb.AppendLine("                return new SiteProfileResolver(fallback);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reqLog?.Error(null, \"[SiteProfile-AOT] No ISiteProfile for site '{SiteCode}'. Available: [{AvailableSites}]\", siteCode, string.Join(\", \", profiles.Keys));");
        sb.AppendLine("            throw new InvalidOperationException(");
        sb.AppendLine("                $\"No ISiteProfile for site '{siteCode}'. \" +");
        sb.AppendLine("                $\"Available: [{string.Join(\", \", profiles.Keys)}]\");");
        sb.AppendLine("        });");
        sb.AppendLine();
        sb.AppendLine("        log?.Info(\"[SiteProfile-AOT] AddGeneratedSiteProfiles — registration complete\");");
        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("SiteProfileRegistrationExtensions.g.cs", sb.ToString());
    }

    private static void EmitEmptyRegistrationExtensions(SourceProductionContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// No ISiteProfile implementations found in this compilation.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using Microsoft.Extensions.Configuration;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Muonroi.Tenancy.SiteProfile;");
        sb.AppendLine();
        sb.AppendLine("namespace Muonroi.Tenancy.SiteProfile.Generated;");
        sb.AppendLine();
        sb.AppendLine("internal static class SiteProfileRegistrationExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>No ISiteProfile implementations found — AddGeneratedSiteProfiles is a no-op.</summary>");
        sb.AppendLine("    public static IServiceCollection AddGeneratedSiteProfiles(");
        sb.AppendLine("        this IServiceCollection services,");
        sb.AppendLine("        IConfiguration configuration,");
        sb.AppendLine("        Func<IServiceProvider, string?> siteCodeAccessor)");
        sb.AppendLine("        => services;");
        sb.AppendLine("}");

        context.AddSource("SiteProfileRegistrationExtensions.g.cs", sb.ToString());
    }

    // -----------------------------------------------------------------------
    // Generator 2: SiteIds.g.cs
    // -----------------------------------------------------------------------

    private static void EmitSiteIds(
        ImmutableArray<INamedTypeSymbol> profiles,
        SourceProductionContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Compile-time SiteId constants — use instead of string literals.");
        sb.AppendLine("// Generated by Muonroi.Tenancy.SiteProfile.SourceGenerators.");
        sb.AppendLine();
        sb.AppendLine("namespace Muonroi.Tenancy.SiteProfile.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Compile-time SiteId constants — use instead of string literals.");
        sb.AppendLine("/// Typo becomes compile error per MSP001 analyzer.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static class SiteIds");
        sb.AppendLine("{");

        if (!profiles.IsDefaultOrEmpty)
        {
            var distinctProfiles = profiles
                .Distinct(SymbolEqualityComparer.Default)
                .Cast<INamedTypeSymbol>();

            foreach (var profile in distinctProfiles)
            {
                string? siteIdValue = ExtractSiteIdValue(profile);
                if (siteIdValue is null) continue;

                // Constant name: sanitize value to valid C# identifier (uppercase)
                string constantName = SanitizeIdentifier(siteIdValue);
                sb.AppendLine($"    /// <summary>SiteId constant for {profile.Name} — value: \"{siteIdValue}\"</summary>");
                sb.AppendLine($"    internal const string {constantName} = \"{EscapeStringLiteral(siteIdValue)}\";");
            }
        }

        sb.AppendLine("}");

        context.AddSource("SiteIds.g.cs", sb.ToString());
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
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated by Muonroi.Tenancy.SiteProfile.SourceGenerators — SiteProfileRegistrationGenerator.");
        sb.AppendLine("// Do not edit manually.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace Muonroi.Tenancy.SiteProfile.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Descriptor for a site-specific gRPC service. Generated inline to avoid");
        sb.AppendLine("/// external assembly reference issues in source generator context.");
        sb.AppendLine("/// Compatible with GeneratedSiteGrpcServiceDescriptor.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal sealed record GeneratedSiteGrpcServiceDescriptor(");
        sb.AppendLine("    string SiteId, System.Type ServiceType, string? Reason = null);");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Registry of all gRPC service types discovered from [SiteGrpcService] attributes.");
        sb.AppendLine("/// AOT-safe — no reflection. Used by MapSiteGrpcServices() at startup.");
        sb.AppendLine("/// Generated by Muonroi.Tenancy.SiteProfile.SourceGenerators.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static class SiteGrpcServiceRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Returns all site-specific gRPC services discovered from [SiteGrpcService] attributes.");
        sb.AppendLine("    /// AOT-safe — no runtime reflection. Call from MapSiteGrpcServices() at startup.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static System.Collections.Generic.IReadOnlyList<GeneratedSiteGrpcServiceDescriptor> GetAllSiteGrpcServices()");

        if (services.IsDefaultOrEmpty)
        {
            sb.AppendLine("        => System.Array.Empty<GeneratedSiteGrpcServiceDescriptor>();");
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
                sb.AppendLine("        => System.Array.Empty<GeneratedSiteGrpcServiceDescriptor>();");
            }
            else
            {
                sb.AppendLine("        => new GeneratedSiteGrpcServiceDescriptor[]");
                sb.AppendLine("        {");
                foreach (var (symbol, siteId, reason) in distinctServices)
                {
                    string fullName = symbol.ToDisplayString();
                    string siteIdLiteral = EscapeStringLiteral(siteId);
                    if (reason is not null)
                    {
                        string reasonLiteral = EscapeStringLiteral(reason);
                        sb.AppendLine($"            new GeneratedSiteGrpcServiceDescriptor(\"{siteIdLiteral}\", typeof({fullName}), \"{reasonLiteral}\"),");
                    }
                    else
                    {
                        sb.AppendLine($"            new GeneratedSiteGrpcServiceDescriptor(\"{siteIdLiteral}\", typeof({fullName})),");
                    }
                }
                sb.AppendLine("        };");
            }
        }

        sb.AppendLine("}");

        context.AddSource("SiteGrpcServiceRegistry.g.cs", sb.ToString());
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
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated by Muonroi.Tenancy.SiteProfile.SourceGenerators — SiteProfileRegistrationGenerator.");
        sb.AppendLine("// Do not edit manually.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine("namespace Muonroi.Tenancy.SiteProfile.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Registry of all site DbContext types discovered from [GenerateSiteProfile] attributes.");
        sb.AppendLine("/// AOT-safe — no reflection. Used by SiteMigrationRunner at startup.");
        sb.AppendLine("/// Generated by Muonroi.Tenancy.SiteProfile.SourceGenerators.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("internal static class SiteDbContextTypeRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Returns all site DbContext types discovered from [GenerateSiteProfile] attributes.");
        sb.AppendLine("    /// AOT-safe — no reflection. Used by SiteMigrationRunner at startup.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static IReadOnlyList<System.Type> GetAllSiteDbContextTypes()");

        if (dbContextTypes.IsDefaultOrEmpty)
        {
            // No [GenerateSiteProfile] attributes found — emit empty array
            sb.AppendLine("        => System.Array.Empty<System.Type>();");
        }
        else
        {
            // Deduplicate by full type name to avoid duplicates when multiple profiles reference the same DbContext
            var distinctTypes = dbContextTypes
                .Distinct(SymbolEqualityComparer.Default)
                .Cast<INamedTypeSymbol>()
                .ToList();

            if (distinctTypes.Count == 0)
            {
                sb.AppendLine("        => System.Array.Empty<System.Type>();");
            }
            else
            {
                sb.AppendLine("        => new System.Type[]");
                sb.AppendLine("        {");
                foreach (var dbContextType in distinctTypes)
                {
                    string fullName = dbContextType.ToDisplayString();
                    sb.AppendLine($"            typeof({fullName}),");
                }
                sb.AppendLine("        };");
            }
        }

        sb.AppendLine("}");

        context.AddSource("SiteDbContextTypeRegistry.g.cs", sb.ToString());
    }
}
