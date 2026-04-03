using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Governance.Compliance;

/// <summary>
/// Represents the MCompliance Endpoint Extensions.
/// </summary>
public static class MComplianceEndpointExtensions
{
    /// <summary>
    /// Executes the Map MCompliance Endpoints operation.
    /// </summary>
    public static IEndpointRouteBuilder MapMComplianceEndpoints(
        this IEndpointRouteBuilder endpoints,
        string basePath = "/api/v1/compliance")
    {
        MGuard.NotNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup(basePath).WithTags("Muonroi Enterprise Compliance");

        group.MapPost("/export/run", async (IMComplianceExportService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ExportAsync(cancellationToken)));

        group.MapGet("/export/records",
            async (
                DateTimeOffset? startUtc,
                DateTimeOffset? endUtc,
                string? tenantId,
                MComplianceExportSource? source,
                int? maxRecords,
                IMComplianceExportService service,
                CancellationToken cancellationToken) =>
            {
                MComplianceExportQuery query = new()
                {
                    StartUtc = startUtc,
                    EndUtc = endUtc,
                    TenantId = tenantId,
                    Source = source,
                    MaxRecords = maxRecords
                };
                IReadOnlyList<MComplianceExportRecord> records = await service.GetExportRecordsAsync(query, cancellationToken);
                return Results.Ok(records);
            });

        group.MapGet("/verify",
            async (
                DateTimeOffset? startUtc,
                DateTimeOffset? endUtc,
                string? tenantId,
                MComplianceExportSource? source,
                IMComplianceExportService service,
                CancellationToken cancellationToken) =>
            {
                MComplianceVerificationRequest request = new()
                {
                    StartUtc = startUtc,
                    EndUtc = endUtc,
                    TenantId = tenantId,
                    Source = source
                };
                return Results.Ok(await service.VerifyAsync(request, cancellationToken));
            });

        group.MapPost("/evidence-packs/generate",
            async (MComplianceEvidencePackRequest request, IMComplianceEvidencePackService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GenerateAsync(request, cancellationToken)));

        group.MapPost("/retention/prune", async (IMComplianceExportService service, CancellationToken cancellationToken) =>
        {
            int deleted = await service.PruneEvidencePacksAsync(cancellationToken);
            return Results.Ok(new { deleted });
        });

        return endpoints;
    }
}
