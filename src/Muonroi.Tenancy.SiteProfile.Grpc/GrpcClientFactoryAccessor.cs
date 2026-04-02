using System.Collections.Concurrent;
using System.Reflection;
using Grpc.Core;
using GrpcClientFactory = global::Grpc.Net.ClientFactory.GrpcClientFactory;

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
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(registry);

        foreach (SiteGrpcClientDescriptor desc in registry.Descriptors)
        {
            string fullName = desc.ClientType.FullName!;
            var key = (fullName, desc.ServiceName);
            if (_invokers.ContainsKey(key)) continue;

            string[] names = [desc.ServiceName, fullName, desc.ClientType.Name];
            MethodInfo createMethod = typeof(GrpcClientFactory)
                .GetMethod(nameof(GrpcClientFactory.CreateClient))!
                .MakeGenericMethod(desc.ClientType);

            foreach (string name in names)
            {
                try
                {
                    object client = createMethod.Invoke(factory, [name])!;
                    // Extract CallInvoker from the gRPC client via reflection
                    // ClientBase has a protected property CallInvoker
                    var invokerProp = typeof(ClientBase).GetProperty(
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
        if (!_invokers.TryGetValue((fullName, serviceName), out CallInvoker? invoker))
            return null;

        // Create client instance: new TClient(CallInvoker)
        // All gRPC clients have a constructor that takes CallInvoker
        ConstructorInfo? ctor = clientType.GetConstructor([typeof(CallInvoker)]);
        return ctor?.Invoke([invoker]);
    }
}
