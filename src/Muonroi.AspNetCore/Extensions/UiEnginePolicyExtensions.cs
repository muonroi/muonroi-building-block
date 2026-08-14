namespace Muonroi.AspNetCore.Extensions;

/// <inheritdoc />
public static class UiEnginePolicyNames
{
/// <inheritdoc />
    public const string Propose = "ui-engine:changes:propose";
/// <inheritdoc />
    public const string Apply = "ui-engine:changes:apply";
}

/// <inheritdoc />
public sealed class UiEnginePolicyOptions
{
/// <inheritdoc />
    public bool UseClaimRequirement { get; set; } = false;
/// <inheritdoc />
    public string ClaimType { get; set; } = "permission";
/// <inheritdoc />
    public string ProposeClaimValue { get; set; } = UiEnginePolicyNames.Propose;
/// <inheritdoc />
    public string ApplyClaimValue { get; set; } = UiEnginePolicyNames.Apply;
}

/// <inheritdoc />
public static class UiEnginePolicyExtensions
{
/// <inheritdoc />
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
