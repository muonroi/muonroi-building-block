namespace Muonroi.Governance.ControlPlane;

/// <summary>
/// Represents the MControl Plane Endpoint Extensions.
/// </summary>
public static class MControlPlaneEndpointExtensions
{
    /// <summary>
    /// Executes the Map MEnterprise Control Plane Endpoints operation.
    /// </summary>
    public static IEndpointRouteBuilder MapMEnterpriseControlPlaneEndpoints(
        this IEndpointRouteBuilder endpoints,
        string basePath = "/api/v1/control-plane")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup(basePath).WithTags("Muonroi Enterprise Control Plane");

        group.MapPost("/licenses/issue",
            (MIssueLicenseRequest request, IMEnterpriseControlPlaneService service) =>
                Execute(() => Results.Ok(service.IssueLicense(request))));

        group.MapPost("/licenses/revoke",
            (MRevokeLicenseRequest request, IMEnterpriseControlPlaneService service) =>
                Execute(() => Results.Ok(service.RevokeLicense(request))));

        group.MapPost("/licenses/tenants/assign",
            (MAssignTenantsRequest request, IMEnterpriseControlPlaneService service) =>
                Execute(() => Results.Ok(service.AssignTenants(request))));

        group.MapGet("/licenses/{licenseId}",
            (string licenseId, IMEnterpriseControlPlaneService service) =>
            {
                MControlPlaneLicenseRecord? result = service.GetLicense(licenseId);
                return result == null ? Results.NotFound() : Results.Ok(result);
            });

        group.MapPost("/policies/draft",
            (MCreatePolicyDraftRequest request, IMEnterpriseControlPlaneService service) =>
                Execute(() => Results.Ok(service.CreatePolicyDraft(request))));

        group.MapPost("/policies/approve",
            (MApprovePolicyBundleRequest request, IMEnterpriseControlPlaneService service) =>
                Execute(() => Results.Ok(service.ApprovePolicyBundle(request))));

        group.MapPost("/policies/activate",
            (MActivatePolicyBundleRequest request, IMEnterpriseControlPlaneService service) =>
                Execute(() => Results.Ok(service.ActivatePolicyBundle(request))));

        group.MapPost("/policies/rollback",
            (MRollbackPolicyBundleRequest request, IMEnterpriseControlPlaneService service) =>
                Execute(() => Results.Ok(service.RollbackPolicyBundle(request))));

        group.MapGet("/policies/{licenseId}",
            (string licenseId, IMEnterpriseControlPlaneService service) =>
                Results.Ok(service.GetPolicyBundles(licenseId)));

        group.MapGet("/policies/{licenseId}/active",
            (string licenseId, IMEnterpriseControlPlaneService service) =>
            {
                MControlPlanePolicyBundleRecord? active = service.GetActivePolicyBundle(licenseId);
                return active == null ? Results.NotFound() : Results.Ok(active);
            });

        group.MapGet("/audit",
            (int? take, IMEnterpriseControlPlaneService service) =>
                Results.Ok(service.GetAuditTrail(take.GetValueOrDefault(100))));

        return endpoints;
    }

    private static IResult Execute(Func<IResult> operation)
    {
        try
        {
            return operation();
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or InvalidOperationException)
        {
            return Results.BadRequest(new { Error = ex.Message });
        }
    }
}


