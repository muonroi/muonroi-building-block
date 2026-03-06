using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Muonroi.AspNetCore.Extensions;

public static class UiEnginePolicyNames
{
    public const string Propose = "ui-engine:changes:propose";
    public const string Apply = "ui-engine:changes:apply";
}

public sealed class UiEnginePolicyOptions
{
    public bool UseClaimRequirement { get; set; } = false;
    public string ClaimType { get; set; } = "permission";
    public string ProposeClaimValue { get; set; } = UiEnginePolicyNames.Propose;
    public string ApplyClaimValue { get; set; } = UiEnginePolicyNames.Apply;
}

public static class UiEnginePolicyExtensions
{
    public static IServiceCollection AddUiEngineChangePolicies(
        this IServiceCollection services,
        Action<UiEnginePolicyOptions>? configure = null)
    {
        UiEnginePolicyOptions options = new();
        configure?.Invoke(options);

        services.AddAuthorization(authOptions =>
        {
            authOptions.AddPolicy(UiEnginePolicyNames.Propose, policy =>
                BuildPolicy(policy, options.ClaimType, options.ProposeClaimValue, options.UseClaimRequirement));

            authOptions.AddPolicy(UiEnginePolicyNames.Apply, policy =>
                BuildPolicy(policy, options.ClaimType, options.ApplyClaimValue, options.UseClaimRequirement));
        });

        return services;
    }

    private static void BuildPolicy(
        AuthorizationPolicyBuilder builder,
        string claimType,
        string claimValue,
        bool useClaimRequirement)
    {
        builder.RequireAuthenticatedUser();
        if (useClaimRequirement)
        {
            builder.RequireClaim(claimType, claimValue);
        }
    }
}
