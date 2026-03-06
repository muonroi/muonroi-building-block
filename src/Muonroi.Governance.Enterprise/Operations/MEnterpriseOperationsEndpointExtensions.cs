namespace Muonroi.Governance.Operations;

public static class MEnterpriseOperationsEndpointExtensions
{
    public static IEndpointRouteBuilder MapMEnterpriseOperationsEndpoints(
        this IEndpointRouteBuilder endpoints,
        string basePath = "/api/v1/enterprise-ops")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup(basePath).WithTags("Muonroi Enterprise Operations");

        group.MapPost("/upgrade/compatibility/check",
            (MUpgradeCompatibilityRequest request, IMUpgradeCompatibilityService service) =>
                Results.Ok(service.Evaluate(request)));

        group.MapPost("/upgrade/compatibility/check-files",
            (MUpgradeCompatibilityFileRequest request, IMUpgradeCompatibilityService service) =>
                Results.Ok(service.EvaluateFromFiles(request)));

        group.MapGet("/slo/presets",
            (IMEnterpriseSloPresetService service) => Results.Ok(service.GetPresetNames()));

        group.MapGet("/slo/presets/{presetName}",
            (string presetName, IMEnterpriseSloPresetService service) =>
            {
                try
                {
                    return Results.Ok(service.GetPreset(presetName));
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    return Results.BadRequest(new { Error = ex.Message });
                }
            });

        return endpoints;
    }
}
