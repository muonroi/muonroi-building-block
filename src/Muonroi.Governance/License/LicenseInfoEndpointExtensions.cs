using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Muonroi.Governance.License;

/// <summary>
/// Extension methods to expose license info to frontend applications.
/// Consumer apps call app.MapMuonroiLicenseInfoEndpoint() to serve license JWT to their FE.
/// </summary>
public static class LicenseInfoEndpointExtensions
{
    /// <summary>
    /// Maps a GET endpoint that returns license tier info and the activation JWT
    /// for frontend license verification (MLicenseVerifier).
    ///
    /// Usage:
    /// <code>
    /// app.MapMuonroiLicenseInfoEndpoint(); // defaults to /api/v1/license/info
    /// </code>
    /// </summary>
    public static IEndpointRouteBuilder MapMuonroiLicenseInfoEndpoint(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/api/v1/license/info")
    {
        endpoints.MapGet(pattern, (HttpContext context) =>
        {
            LicenseState? state = context.RequestServices.GetService<LicenseState>();
            ILicenseStore? store = context.RequestServices.GetService<ILicenseStore>();

            string? activationJwt = store?.LoadActivationJwt();

            return Results.Ok(new LicenseInfoResponse
            {
                Tier = state?.TrustedTier.ToString() ?? "Free",
                IsValid = state?.IsValid ?? false,
                ActivationProof = activationJwt
            });
        })
        .WithTags("License")
        .AllowAnonymous();

        return endpoints;
    }
}

internal sealed class LicenseInfoResponse
{
    public string Tier { get; init; } = "Free";
    public bool IsValid { get; init; }

    /// <summary>
    /// RS256 JWT for frontend MLicenseVerifier initialization.
    /// Null if no activation JWT is available.
    /// </summary>
    public string? ActivationProof { get; init; }
}
