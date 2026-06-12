using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Muonroi.Tenancy.SiteProfile.SourceGenerators;

/// <summary>
/// Incremental source generator that scans partial interfaces decorated with
/// [GenerateSiteGrpcFacade(SharedClient = typeof(X), ExtendClients = new[] { typeof(Y) })]
/// and emits:
/// <list type="number">
///   <item>A partial interface completion with all Async RPC method signatures.</item>
///   <item>A concrete <c>{InterfaceName}Facade</c> class that delegates each RPC to the
///       correct inner client (shared or per-site extend).</item>
/// </list>
/// </summary>
[Generator]
public sealed class SiteGrpcFacadeGenerator : IIncrementalGenerator
{
    private const string AttributeShortName = "GenerateSiteGrpcFacade";

    // RPC call return type short names (Grpc.Core)
    private static readonly string[] RpcReturnTypeNames = new[]
    {
        "AsyncUnaryCall",
        "AsyncServerStreamingCall",
        "AsyncClientStreamingCall",
        "AsyncDuplexStreamingCall"
    };

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<(FacadeModel? Model, string? Diag)> candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsInterfaceWithAttributes(s),
                transform: static (ctx, _) => GetFacadeModelWithDiag(ctx))
            .Where(static m => m.Model is not null || m.Diag is not null);

        IncrementalValueProvider<ImmutableArray<(FacadeModel? Model, string? Diag)>> collected = candidates.Collect();

        context.RegisterSourceOutput(collected, static (spc, models) =>
        {
            // Emit diagnostics as a generated file for debugging
            var diagLines = new List<string>();
            var validModels = ImmutableArray.CreateBuilder<FacadeModel?>();
            foreach (var (model, diag) in models)
            {
                if (diag is not null) diagLines.Add(diag);
                if (model is not null) validModels.Add(model);
            }
            if (diagLines.Count > 0)
            {
                spc.AddSource("__SiteGrpcFacadeGenerator.Diag.g.cs",
                    "// SiteGrpcFacadeGenerator diagnostics:\n" +
                    string.Join("\n", diagLines.Select(d => "// " + d)));
            }
            Execute(validModels.ToImmutable(), spc);
        });
    }

    private static (FacadeModel? Model, string? Diag) GetFacadeModelWithDiag(GeneratorSyntaxContext context)
    {
        var ifaceDecl = (InterfaceDeclarationSyntax)context.Node;
        string ifaceName = ifaceDecl.Identifier.Text;

        if (context.SemanticModel.GetDeclaredSymbol(ifaceDecl) is not INamedTypeSymbol ifaceSymbol)
            return (null, $"[{ifaceName}] Could not get declared symbol");

        AttributeData? attr = FindAttribute(ifaceSymbol, AttributeShortName);
        if (attr is null)
        {
            // Check all attributes for debugging
            var attrNames = string.Join(", ", ifaceSymbol.GetAttributes().Select(a =>
                a.AttributeClass?.ToDisplayString() ?? "null"));
            return (null, $"[{ifaceName}] No {AttributeShortName} attribute found. Has: [{attrNames}]");
        }

        INamedTypeSymbol? sharedClientSymbol = null;
        List<INamedTypeSymbol> extendClientSymbols = new List<INamedTypeSymbol>();

        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg.Key == "SharedClient")
                sharedClientSymbol = namedArg.Value.Value as INamedTypeSymbol;
            else if (namedArg.Key == "ExtendClients" && !namedArg.Value.IsNull && namedArg.Value.Kind == TypedConstantKind.Array)
            {
                foreach (TypedConstant item in namedArg.Value.Values)
                {
                    if (item.Value is INamedTypeSymbol extSym)
                        extendClientSymbols.Add(extSym);
                }
            }
        }

        if (sharedClientSymbol is null)
            return (null, $"[{ifaceName}] SharedClient is null. Attr class: {attr.AttributeClass?.ToDisplayString()}. Named args: {string.Join(", ", attr.NamedArguments.Select(a => $"{a.Key}={a.Value}"))}");

        // Call original logic for successful case
        return (GetFacadeModel(context), null);
    }

    // -----------------------------------------------------------------------
    // Predicate — fast SyntaxNode filter
    // -----------------------------------------------------------------------

    private static bool IsInterfaceWithAttributes(SyntaxNode node)
        => node is InterfaceDeclarationSyntax iface
           && iface.AttributeLists.Count > 0
           && iface.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

    // -----------------------------------------------------------------------
    // Transform — semantic analysis
    // -----------------------------------------------------------------------

    private static FacadeModel? GetFacadeModel(GeneratorSyntaxContext context)
    {
        var ifaceDecl = (InterfaceDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(ifaceDecl) is not INamedTypeSymbol ifaceSymbol)
            return null;

        // Find [GenerateSiteGrpcFacade] attribute
        AttributeData? attr = FindAttribute(ifaceSymbol, AttributeShortName);
        if (attr is null)
            return null;

        // Extract SharedClient type from named argument
        INamedTypeSymbol? sharedClientSymbol = null;
        List<INamedTypeSymbol> extendClientSymbols = new List<INamedTypeSymbol>();

        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg.Key == "SharedClient")
            {
                sharedClientSymbol = namedArg.Value.Value as INamedTypeSymbol;
            }
            else if (namedArg.Key == "ExtendClients")
            {
                // ExtendClients is Type[] — stored as array TypedConstant
                if (!namedArg.Value.IsNull && namedArg.Value.Kind == TypedConstantKind.Array)
                {
                    foreach (TypedConstant item in namedArg.Value.Values)
                    {
                        if (item.Value is INamedTypeSymbol extSym)
                            extendClientSymbols.Add(extSym);
                    }
                }
            }
        }

        if (sharedClientSymbol is null)
            return null;

        string ns = GetNamespace(ifaceSymbol);
        string interfaceName = ifaceSymbol.Name; // e.g., "ITciFcdClient"
        string facadeClassName = BuildFacadeClassName(interfaceName); // e.g., "TciFcdClientFacade"

        // Detect base facade interfaces — collect method names already declared by parent
        // so the emitted partial interface skips them (avoids CS0108 hiding warnings).
        HashSet<string> inheritedMethodNames = CollectInheritedFacadeMethodNames(ifaceSymbol);

        // Extract RPC methods from shared client
        List<RpcMethodModel> sharedMethods = ExtractRpcMethods(sharedClientSymbol, "shared");

        // Extract RPC methods from extend clients
        // Build a set of method names from all extend clients for collision detection
        List<RpcMethodModel> extendMethods = new List<RpcMethodModel>();
        for (int i = 0; i < extendClientSymbols.Count; i++)
        {
            List<RpcMethodModel> methods = ExtractRpcMethods(extendClientSymbols[i], $"extend:{i}");
            extendMethods.AddRange(methods);
        }

        // Collision resolution: extend wins — remove shared methods that have same name as extend
        HashSet<string> extendNames = new HashSet<string>(extendMethods.Select(m => m.MethodName));
        List<RpcMethodModel> filteredShared = [.. sharedMethods.Where(m => !extendNames.Contains(m.MethodName))];

        // Track collisions for diagnostic comment
        List<string> collisions = [.. sharedMethods
            .Where(m => extendNames.Contains(m.MethodName))
            .Select(m => m.MethodName)];

        // Split shared methods into inherited (skip in interface) vs own (emit in interface)
        // The facade impl still generates ALL methods — only the interface skips inherited ones.
        List<RpcMethodModel> ownSharedMethods = [.. filteredShared.Where(m => !inheritedMethodNames.Contains(m.MethodName))];
        List<RpcMethodModel> ownExtendMethods = [.. extendMethods.Where(m => !inheritedMethodNames.Contains(m.MethodName))];

        return new FacadeModel(
            interfaceName,
            ns,
            facadeClassName,
            filteredShared,
            extendMethods,
            ownSharedMethods,
            ownExtendMethods,
            sharedClientSymbol.ToDisplayString(),
            extendClientSymbols.Select(s => s.ToDisplayString()).ToList(),
            collisions);
    }

    // -----------------------------------------------------------------------
    // Base facade interface detection
    // -----------------------------------------------------------------------

    /// <summary>
    /// Walks the interface's base interface list. For each base that is also decorated with
    /// [GenerateSiteGrpcFacade], computes the RPC methods it would emit and returns their names.
    /// This allows the derived interface to skip re-declaring those methods (avoiding CS0108).
    /// </summary>
    private static HashSet<string> CollectInheritedFacadeMethodNames(INamedTypeSymbol ifaceSymbol)
    {
        var inherited = new HashSet<string>();

        foreach (INamedTypeSymbol baseIface in ifaceSymbol.Interfaces)
        {
            AttributeData? baseAttr = FindAttribute(baseIface, AttributeShortName);
            if (baseAttr is null)
                continue;

            // Extract SharedClient from base's attribute
            INamedTypeSymbol? baseShared = null;
            List<INamedTypeSymbol> baseExtends = new List<INamedTypeSymbol>();

            foreach (var namedArg in baseAttr.NamedArguments)
            {
                if (namedArg.Key == "SharedClient")
                    baseShared = namedArg.Value.Value as INamedTypeSymbol;
                else if (namedArg.Key == "ExtendClients" && !namedArg.Value.IsNull && namedArg.Value.Kind == TypedConstantKind.Array)
                {
                    foreach (TypedConstant item in namedArg.Value.Values)
                    {
                        if (item.Value is INamedTypeSymbol extSym)
                            baseExtends.Add(extSym);
                    }
                }
            }

            if (baseShared is not null)
            {
                foreach (RpcMethodModel m in ExtractRpcMethods(baseShared, "inherited"))
                    inherited.Add(m.MethodName);
            }

            foreach (INamedTypeSymbol ext in baseExtends)
            {
                foreach (RpcMethodModel m in ExtractRpcMethods(ext, "inherited"))
                    inherited.Add(m.MethodName);
            }

            // Recurse: base interface may itself inherit from another facade interface
            foreach (string name in CollectInheritedFacadeMethodNames(baseIface))
                inherited.Add(name);
        }

        return inherited;
    }

    // -----------------------------------------------------------------------
    // RPC method extraction
    // -----------------------------------------------------------------------

    private static List<RpcMethodModel> ExtractRpcMethods(INamedTypeSymbol clientSymbol, string sourceClient)
    {
        var methods = new List<RpcMethodModel>();

        foreach (ISymbol member in clientSymbol.GetMembers())
        {
            if (member is not IMethodSymbol method)
                continue;

            // Only public instance methods
            if (method.DeclaredAccessibility != Accessibility.Public)
                continue;
            if (method.IsStatic)
                continue;

            // Must end with "Async"
            if (!method.Name.EndsWith("Async"))
                continue;

            // Skip object-level methods
            if (IsObjectMethod(method.Name))
                continue;

            // Skip property accessors
            if (method.MethodKind == MethodKind.PropertyGet || method.MethodKind == MethodKind.PropertySet)
                continue;

            // Validate return type is one of the gRPC call types
            string? responseType = ExtractResponseType(method.ReturnType);
            if (responseType is null)
                continue;

            // Extract request parameter type (first parameter)
            string? requestType = null;
            if (method.Parameters.Length > 0)
            {
                requestType = method.Parameters[0].Type.ToDisplayString();
            }

            if (requestType is null)
                continue;

            // Avoid duplicate method names (keep first occurrence per source)
            if (methods.Any(m => m.MethodName == method.Name))
                continue;

            methods.Add(new RpcMethodModel(
                method.Name,
                requestType,
                responseType,
                GetReturnTypeKind(method.ReturnType),
                sourceClient));
        }

        // Also check base types (gRPC generated clients inherit from ClientBase)
        if (clientSymbol.BaseType is not null
            && clientSymbol.BaseType.SpecialType != SpecialType.System_Object)
        {
            List<RpcMethodModel> baseMethods = ExtractRpcMethods(clientSymbol.BaseType, sourceClient);
            foreach (RpcMethodModel bm in baseMethods)
            {
                if (!methods.Any(m => m.MethodName == bm.MethodName))
                    methods.Add(bm);
            }
        }

        return methods;
    }

    private static string? ExtractResponseType(ITypeSymbol returnType)
    {
        if (returnType is not INamedTypeSymbol namedReturn)
            return null;

        string typeName = namedReturn.Name;
        if (!RpcReturnTypeNames.Any(n => typeName == n))
            return null;

        // Generic type argument = TResponse
        if (namedReturn.TypeArguments.Length == 1)
            return namedReturn.TypeArguments[0].ToDisplayString();

        // AsyncClientStreamingCall<TRequest, TResponse> has 2 type args
        if (namedReturn.TypeArguments.Length == 2)
            return namedReturn.TypeArguments[1].ToDisplayString();

        return null;
    }

    private static string GetReturnTypeKind(ITypeSymbol returnType)
    {
        if (returnType is INamedTypeSymbol named)
            return named.Name;
        return "AsyncUnaryCall";
    }

    private static bool IsObjectMethod(string name)
    {
        return name is "Dispose" or "ToString" or "GetHashCode" or "Equals"
               or "GetType" or "MemberwiseClone" or "Finalize";
    }

    // -----------------------------------------------------------------------
    // Code emission
    // -----------------------------------------------------------------------

    private static void Execute(ImmutableArray<FacadeModel?> models, SourceProductionContext context)
    {
        if (models.IsDefaultOrEmpty)
            return;

        foreach (FacadeModel? model in models)
        {
            if (model is null) continue;

            // Emit 1: partial interface completion
            string ifaceSource = EmitInterface(model);
            context.AddSource($"{model.InterfaceName}.Facade.g.cs", ifaceSource);

            // Emit 2: facade implementation class
            string implSource = EmitFacadeImpl(model);
            context.AddSource($"{model.FacadeClassName}.g.cs", implSource);
        }
    }

    private static string EmitInterface(FacadeModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated by Muonroi.Tenancy.SiteProfile.SourceGenerators — SiteGrpcFacadeGenerator.");
        sb.AppendLine("// Do not edit manually.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"public partial interface {model.InterfaceName}");
        sb.AppendLine("{");

        // Emit only methods NOT already declared in base facade interfaces.
        // Inherited methods are available via interface inheritance — no re-declaration needed.
        AppendRpcSignatures(sb, model.OwnSharedMethods, "// Shared gRPC client methods");
        AppendRpcSignatures(sb, model.OwnExtendMethods, "// Per-site extend client methods");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void AppendRpcSignatures(StringBuilder sb, IReadOnlyList<RpcMethodModel> methods, string sectionComment)
    {
        if (methods.Count == 0) return;

        sb.AppendLine($"    {sectionComment}");
        foreach (RpcMethodModel m in methods)
        {
            // Interface uses Task<TResponse> for cleaner consumer API (unwrapped from AsyncUnaryCall)
            sb.AppendLine($"    global::System.Threading.Tasks.Task<{m.ResponseType}> {m.MethodName}({m.RequestType} request, global::System.Threading.CancellationToken cancellationToken = default);");
        }
        sb.AppendLine();
    }

    private static string EmitFacadeImpl(FacadeModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated by Muonroi.Tenancy.SiteProfile.SourceGenerators — SiteGrpcFacadeGenerator.");
        sb.AppendLine("// Do not edit manually.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Grpc.Core;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine($"namespace {model.Namespace};");
            sb.AppendLine();
        }

        sb.AppendLine($"/// <summary>");
        sb.AppendLine($"/// Generated facade that unifies shared + per-site gRPC clients behind <see cref=\"{model.InterfaceName}\"/>.");
        sb.AppendLine($"/// Delegates each RPC to the correct inner client.");
        sb.AppendLine($"/// </summary>");
        sb.AppendLine($"public sealed class {model.FacadeClassName} : {model.InterfaceName}");
        sb.AppendLine("{");

        // Fields — only gRPC clients, no logging dependency
        sb.AppendLine($"    private readonly {model.SharedClientFullName} _shared;");
        for (int i = 0; i < model.ExtendClientFullNames.Count; i++)
        {
            sb.AppendLine($"    private readonly {model.ExtendClientFullNames[i]} _extend{i};");
        }
        sb.AppendLine();

        // Constructor — only ClientBase params (factory resolves these via GrpcClientFactoryAccessor)
        sb.AppendLine("    /// <summary>Creates the facade from pre-resolved gRPC client instances.</summary>");
        sb.Append($"    public {model.FacadeClassName}({model.SharedClientFullName} shared");
        for (int i = 0; i < model.ExtendClientFullNames.Count; i++)
        {
            sb.Append($", {model.ExtendClientFullNames[i]} extend{i}");
        }
        sb.AppendLine(")");
        sb.AppendLine("    {");
        sb.AppendLine($"        _shared = shared;");
        for (int i = 0; i < model.ExtendClientFullNames.Count; i++)
        {
            sb.AppendLine($"        _extend{i} = extend{i};");
        }
        sb.AppendLine("    }");
        sb.AppendLine();

        // Collision comments
        if (model.Collisions.Count > 0)
        {
            sb.AppendLine("    // Per-site override semantics: the following methods exist in both shared and extend clients.");
            sb.AppendLine("    // The extend version takes precedence.");
            foreach (string collision in model.Collisions)
            {
                sb.AppendLine($"    // Collision: {collision} — extend wins.");
            }
            sb.AppendLine();
        }

        // Shared method implementations
        if (model.SharedMethods.Count > 0)
        {
            sb.AppendLine("    // ----- Shared client delegations -----");
            foreach (RpcMethodModel m in model.SharedMethods)
            {
                EmitMethodImpl(sb, m, "_shared");
            }
        }

        // Extend method implementations
        if (model.ExtendMethods.Count > 0)
        {
            sb.AppendLine("    // ----- Extend client delegations -----");
            foreach (RpcMethodModel m in model.ExtendMethods)
            {
                // Determine which _extend field to use
                string field = m.SourceClient.StartsWith("extend:") && int.TryParse(m.SourceClient.Substring("extend:".Length), out int idx)
                    ? $"_extend{idx}"
                    : "_shared";
                EmitMethodImpl(sb, m, field);
            }
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitMethodImpl(StringBuilder sb, RpcMethodModel m, string field)
    {
        sb.AppendLine($"    /// <inheritdoc/>");
        sb.AppendLine($"    public async global::System.Threading.Tasks.Task<{m.ResponseType}> {m.MethodName}({m.RequestType} request, global::System.Threading.CancellationToken cancellationToken = default)");
        sb.AppendLine("    {");

        // For AsyncUnaryCall — await the call directly
        if (m.ReturnTypeKind == "AsyncUnaryCall")
        {
            sb.AppendLine($"        return await {field}.{m.MethodName}(request, cancellationToken: cancellationToken);");
        }
        else
        {
            // For streaming calls, still delegate — consumer handles the stream
            sb.AppendLine($"        return await {field}.{m.MethodName}(request, cancellationToken: cancellationToken);");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static AttributeData? FindAttribute(INamedTypeSymbol symbol, string shortName)
    {
        foreach (AttributeData attr in symbol.GetAttributes())
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

    /// <summary>
    /// Converts interface name to facade class name.
    /// Convention: strip leading 'I' if present, then append 'Facade'.
    /// E.g., "ITciFcdClient" => "TciFcdClientFacade".
    /// </summary>
    private static string BuildFacadeClassName(string interfaceName)
    {
        string baseName = interfaceName.StartsWith("I") && interfaceName.Length > 1
            ? interfaceName.Substring(1)
            : interfaceName;
        return baseName + "Facade";
    }

    // -----------------------------------------------------------------------
    // Model
    // -----------------------------------------------------------------------

    private sealed class FacadeModel(
        string interfaceName,
        string ns,
        string facadeClassName,
        IReadOnlyList<RpcMethodModel> sharedMethods,
        IReadOnlyList<RpcMethodModel> extendMethods,
        IReadOnlyList<RpcMethodModel> ownSharedMethods,
        IReadOnlyList<RpcMethodModel> ownExtendMethods,
        string sharedClientFullName,
        IReadOnlyList<string> extendClientFullNames,
        IReadOnlyList<string> collisions)
    {
        public string InterfaceName { get; } = interfaceName;
        public string Namespace { get; } = ns;
        public string FacadeClassName { get; } = facadeClassName;
        /// <summary>All shared methods — used by facade impl (must implement all).</summary>
        public IReadOnlyList<RpcMethodModel> SharedMethods { get; } = sharedMethods;
        /// <summary>All extend methods — used by facade impl (must implement all).</summary>
        public IReadOnlyList<RpcMethodModel> ExtendMethods { get; } = extendMethods;
        /// <summary>Shared methods NOT inherited from base facade interfaces — emitted in interface only.</summary>
        public IReadOnlyList<RpcMethodModel> OwnSharedMethods { get; } = ownSharedMethods;
        /// <summary>Extend methods NOT inherited from base facade interfaces — emitted in interface only.</summary>
        public IReadOnlyList<RpcMethodModel> OwnExtendMethods { get; } = ownExtendMethods;
        public string SharedClientFullName { get; } = sharedClientFullName;
        public IReadOnlyList<string> ExtendClientFullNames { get; } = extendClientFullNames;
        public IReadOnlyList<string> Collisions { get; } = collisions;
    }

    private sealed class RpcMethodModel(
        string methodName,
        string requestType,
        string responseType,
        string returnTypeKind,
        string sourceClient)
    {
        public string MethodName { get; } = methodName;
        public string RequestType { get; } = requestType;
        public string ResponseType { get; } = responseType;
        public string ReturnTypeKind { get; } = returnTypeKind;
        public string SourceClient { get; } = sourceClient;
    }
}
