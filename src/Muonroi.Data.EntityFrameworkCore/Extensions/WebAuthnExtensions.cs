namespace Muonroi.Data.EntityFrameworkCore.Extensions;

/// <summary>
/// DI extension for registering EF-backed WebAuthn credential store.
/// </summary>
public static class WebAuthnExtensions
{
    /// <summary>
    /// Registers <see cref="EfWebAuthnCredentialStore"/> as the <see cref="IWebAuthnCredentialStore"/> implementation.
    /// Call this after <c>AddWebAuthn</c> when using EF Core for credential persistence.
    /// </summary>
    public static IServiceCollection AddEfWebAuthnCredentialStore(this IServiceCollection services)
    {
        services.TryAddScoped<IWebAuthnCredentialStore, EfWebAuthnCredentialStore>();
        return services;
    }
}
