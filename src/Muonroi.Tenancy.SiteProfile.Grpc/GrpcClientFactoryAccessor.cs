namespace Muonroi.Tenancy.SiteProfile.Grpc;

/// <summary>
/// Pre-resolves gRPC channels at startup (when GrpcClientFactory works) and creates client
/// instances on demand with the correct Type from the requesting assembly. Works around two
/// Autofac issues: (1) GrpcClientFactory's IOptionsMonitor is empty in child scopes,
/// (2) proto types generated in multiple assemblies are different CLR Types despite same FullName.
///
/// <para>
/// Initialized by <see cref="SiteGrpcExtensions.InitializeSiteGrpcClients"/>.
/// Consumers MUST call <c>app.InitializeSiteGrpcClients()</c> in Program.cs after <c>builder.Build()</c>.
/// </para>
/// </summary>
[SuppressMessage("Muonroi.CodeStandards", "MSTD0002", Justification = "Reflection-based MethodInfo.FullName lookup is guaranteed non-null by prior registry population.")]
public sealed class GrpcClientFactoryAccessor
{
    // Cache: (clientType.FullName, serviceName) → CallInvoker
    // Using FullName instead of Type to handle cross-assembly proto types
    private readonly ConcurrentDictionary<(string fullName, string serviceName), CallInvoker> _invokers = new();

    /// <summary>Whether <see cref="Initialize"/> has been called.</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Pre-resolves gRPC clients from the registry and caches their CallInvokers.
    /// Called once at startup from <see cref="SiteGrpcExtensions.InitializeSiteGrpcClients"/>.
    /// </summary>
    internal void Initialize(GrpcClientFactory factory, SiteGrpcClientRegistry registry)
    {
        MGuard.NotNull(factory);
        MGuard.NotNull(registry);

        foreach (SiteGrpcClientDescriptor desc in registry.Descriptors)
        {
            string fullName = desc.ClientType.FullName!;
            (string fullName, string ServiceName) key = (fullName, desc.ServiceName);
            if (_invokers.ContainsKey(key))
            {
                continue;
            }

            string[] names = [desc.ServiceName, fullName, desc.ClientType.Name];
            MethodInfo createMethod = typeof(GrpcClientFactory)
                .GetMethod(nameof(GrpcClientFactory.CreateClient))!
                .MakeGenericMethod(desc.ClientType);

            foreach (string name in names)
            {
                try
                {
                    object client = createMethod.Invoke(factory, [name])!;
                    PropertyInfo? invokerProp = typeof(ClientBase).GetProperty(
                        "CallInvoker", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (invokerProp?.GetValue(client) is CallInvoker invoker)
                    {
                        _invokers.TryAdd(key, invoker);
                    }
                    break;
                }
                catch (TargetInvocationException)
                {
                    // Name not registered — try next
                }
            }
        }

        IsInitialized = true;
    }

    /// <summary>
    /// Creates a gRPC client of the exact <paramref name="clientType"/> using a cached CallInvoker.
    /// This handles the cross-assembly proto type issue by creating the client with the caller's Type.
    /// </summary>
    internal object? CreateClient(Type clientType, string serviceName)
    {
        string fullName = clientType.FullName!;

        // Primary lookup: exact (fullName, serviceName) match
        if (_invokers.TryGetValue((fullName, serviceName), out CallInvoker? invoker))
        {
            ConstructorInfo? ctor = clientType.GetConstructor([typeof(CallInvoker)]);
            return ctor?.Invoke([invoker]);
        }

        // Fallback: type match across any service name
        // Handles facade constructors that take both shared + site-specific gRPC clients
        KeyValuePair<(string fullName, string serviceName), CallInvoker> fallback = _invokers.FirstOrDefault(kv => kv.Key.fullName == fullName);
        if (fallback.Value is not null)
        {
            ConstructorInfo? ctor = clientType.GetConstructor([typeof(CallInvoker)]);
            return ctor?.Invoke([fallback.Value]);
        }

        return null;
    }
}
