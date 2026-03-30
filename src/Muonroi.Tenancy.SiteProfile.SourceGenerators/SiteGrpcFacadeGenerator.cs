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
        IncrementalValuesProvider<FacadeModel?> candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsInterfaceWithAttributes(s),
                transform: static (ctx, _) => GetFacadeModel(ctx))
            .Where(static m => m is not null);

        IncrementalValueProvider<ImmutableArray<FacadeModel?>> collected = candidates.Collect();

        context.RegisterSourceOutput(collected, static (spc, models) => Execute(models, spc));
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
        List<RpcMethodModel> filteredShared = sharedMethods
            .Where(m => !extendNames.Contains(m.MethodName))
            .ToList();

        // Track collisions for diagnostic comment
        List<string> collisions = sharedMethods
            .Where(m => extendNames.Contains(m.MethodName))
            .Select(m => m.MethodName)
            .ToList();

        return new FacadeModel(
            interfaceName,
            ns,
            facadeClassName,
            filteredShared,
            extendMethods,
            sharedClientSymbol.ToDisplayString(),
            extendClientSymbols.Select(s => s.ToDisplayString()).ToList(),
            collisions);
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

        AppendRpcSignatures(sb, model.SharedMethods, "// Shared gRPC client methods");
        AppendRpcSignatures(sb, model.ExtendMethods, "// Per-site extend client methods");

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

        // Fields
        sb.AppendLine($"    private readonly {model.SharedClientFullName} _shared;");
        for (int i = 0; i < model.ExtendClientFullNames.Count; i++)
        {
            sb.AppendLine($"    private readonly {model.ExtendClientFullNames[i]} _extend{i};");
        }
        sb.AppendLine();

        // Constructor — takes GrpcChannel
        sb.AppendLine("    /// <summary>Creates the facade by instantiating all inner gRPC clients from the supplied channel.</summary>");
        sb.AppendLine("    public " + model.FacadeClassName + "(global::Grpc.Core.ChannelBase channel)");
        sb.AppendLine("    {");
        sb.AppendLine($"        _shared = new {model.SharedClientFullName}(channel);");
        for (int i = 0; i < model.ExtendClientFullNames.Count; i++)
        {
            sb.AppendLine($"        _extend{i} = new {model.ExtendClientFullNames[i]}(channel);");
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

    private sealed class FacadeModel
    {
        public string InterfaceName { get; }
        public string Namespace { get; }
        public string FacadeClassName { get; }
        public IReadOnlyList<RpcMethodModel> SharedMethods { get; }
        public IReadOnlyList<RpcMethodModel> ExtendMethods { get; }
        public string SharedClientFullName { get; }
        public IReadOnlyList<string> ExtendClientFullNames { get; }
        public IReadOnlyList<string> Collisions { get; }

        public FacadeModel(
            string interfaceName,
            string ns,
            string facadeClassName,
            IReadOnlyList<RpcMethodModel> sharedMethods,
            IReadOnlyList<RpcMethodModel> extendMethods,
            string sharedClientFullName,
            IReadOnlyList<string> extendClientFullNames,
            IReadOnlyList<string> collisions)
        {
            InterfaceName = interfaceName;
            Namespace = ns;
            FacadeClassName = facadeClassName;
            SharedMethods = sharedMethods;
            ExtendMethods = extendMethods;
            SharedClientFullName = sharedClientFullName;
            ExtendClientFullNames = extendClientFullNames;
            Collisions = collisions;
        }
    }

    private sealed class RpcMethodModel
    {
        public string MethodName { get; }
        public string RequestType { get; }
        public string ResponseType { get; }
        public string ReturnTypeKind { get; }
        public string SourceClient { get; }

        public RpcMethodModel(
            string methodName,
            string requestType,
            string responseType,
            string returnTypeKind,
            string sourceClient)
        {
            MethodName = methodName;
            RequestType = requestType;
            ResponseType = responseType;
            ReturnTypeKind = returnTypeKind;
            SourceClient = sourceClient;
        }
    }
}
