using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.RuleEngine.Runtime.Tracing;

/// <summary>
/// Endpoint mappings for rule debugger controls and trace queries.
/// </summary>
public static class RuleTracingEndpoints
{
    /// <summary>Maps rule tracing endpoints onto the route builder.</summary>
    public static IEndpointRouteBuilder MapRuleTracingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        MGuard.NotNull(endpoints);

        RouteGroupBuilder group = endpoints.MapGroup("/muonroi/rule-debugger");

        group.MapPost(
            "/{tenantId}/enable",
            async Task<IResult> (
                string tenantId,
                EnableRuleDebuggerRequest? body,
                [FromServices] IRuleDebuggerModeService service,
                CancellationToken ct) =>
            {
                int durationMinutes = body?.DurationMinutes ?? 30;
                if (durationMinutes <= 0)
                {
                    durationMinutes = 30;
                }

                await service.EnableAsync(tenantId, TimeSpan.FromMinutes(durationMinutes), ct);
                return Results.Ok(new
                {
                    TenantId = tenantId,
                    Enabled = true,
                    DurationMinutes = durationMinutes
                });
            });

        group.MapPost(
            "/{tenantId}/disable",
            async Task<IResult> (string tenantId, [FromServices] IRuleDebuggerModeService service, CancellationToken ct) =>
            {
                await service.DisableAsync(tenantId, ct);
                return Results.Ok(new { TenantId = tenantId, Enabled = false });
            });

        group.MapGet(
            "/{tenantId}/traces",
            async Task<IResult> (
                string tenantId,
                string? correlationId,
                DateTimeOffset? from,
                [FromServices] IRuleTraceStore store,
                CancellationToken ct) =>
            {
                IReadOnlyList<RuleTraceEntry> entries = await store.QueryAsync(tenantId, correlationId, from, ct);
                return Results.Ok(entries);
            });

        return endpoints;
    }

    /// <summary>Request payload used to enable rule debugging.</summary>
    public sealed class EnableRuleDebuggerRequest
    {
        /// <summary>Duration in minutes to keep debugging enabled.</summary>
        public int DurationMinutes { get; init; } = 30;
    }
}
