namespace Muonroi.AspNetCore.Extensions;

/// <summary>
/// Extension methods for registering the Muonroi causal chain propagation infrastructure.
/// </summary>
public static class CausalChainExtensions
{
    /// <summary>
    /// Registers <see cref="MCausalChainOptions"/> and <see cref="MCausalChainDelegatingHandler"/>
    /// into the DI container.
    ///
    /// Call this from <c>Program.cs</c> before <c>builder.Build()</c>:
    /// <code>
    /// services.AddMuonroiCausalChain(options =>
    /// {
    ///     options.ServiceName = "my-service";
    /// });
    /// </code>
    ///
    /// Then attach <see cref="MCausalChainDelegatingHandler"/> to any <see cref="System.Net.Http.HttpClient"/>
    /// that calls upstream Muonroi services:
    /// <code>
    /// services.AddHttpClient&lt;IMyServiceClient, MyServiceClient&gt;()
    ///         .AddHttpMessageHandler&lt;MCausalChainDelegatingHandler&gt;();
    /// </code>
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">Optional action to configure <see cref="MCausalChainOptions"/>.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddMuonroiCausalChain(
        this IServiceCollection services,
        Action<MCausalChainOptions>? configure = null)
    {
        MCausalChainOptions options = new();
        configure?.Invoke(options);
        services.AddSingleton(Options.Create(options));
        services.AddTransient<MCausalChainDelegatingHandler>();
        return services;
    }
}
