using Microsoft.AspNetCore.Mvc;
using Muonroi.RuleEngine.CEP.Abstractions;
using Muonroi.RuleEngine.CEP.Builder;

namespace Muonroi.RuleEngine.CEP.Controllers;

/// <summary>
/// CEP configuration API endpoints.
/// </summary>
/// <param name="repository">CEP configuration repository.</param>
[ApiController]
[Route("api/v1/rule-engine/cep")]
public sealed class CepController(ICepConfigRepository repository) : ControllerBase
{
    /// <summary>
    /// Lists CEP configurations.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CepConfigDto>>> List(CancellationToken cancellationToken)
    {
        IReadOnlyList<CepConfig> configs = await repository.ListAsync(cancellationToken);
        return Ok(configs.Select(Map).ToArray());
    }

    /// <summary>
    /// Gets a CEP configuration by id.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<CepConfigDto>> Get(string id, CancellationToken cancellationToken)
    {
        CepConfig? config = await repository.GetAsync(id, cancellationToken);
        if (config is null)
        {
            return NotFound();
        }

        return Ok(Map(config));
    }

    /// <summary>
    /// Creates or updates a CEP configuration.
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<CepConfigDto>> Save(
        string id,
        [FromBody] CepConfigDto config,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.Name))
        {
            return BadRequest(new { message = "Config name is required." });
        }

        if (!Enum.TryParse(config.WindowType, true, out WindowType windowType))
        {
            return BadRequest(new { message = $"Unsupported windowType '{config.WindowType}'." });
        }

        if (config.WindowSizeSeconds <= 0)
        {
            return BadRequest(new { message = "WindowSizeSeconds must be greater than zero." });
        }

        if (config.TimeToLiveSeconds <= 0)
        {
            return BadRequest(new { message = "TimeToLiveSeconds must be greater than zero." });
        }

        if (config.TimeToLiveSeconds < config.WindowSizeSeconds)
        {
            return BadRequest(new { message = "TimeToLiveSeconds must be greater than or equal to WindowSizeSeconds." });
        }

        CepConfig persisted = await repository.SaveAsync(
            new CepConfig
            {
                Id = id,
                TenantId = config.TenantId ?? "_global",
                Name = config.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(config.Description) ? null : config.Description.Trim(),
                WindowType = windowType,
                WindowSize = TimeSpan.FromSeconds(config.WindowSizeSeconds),
                TimeToLive = TimeSpan.FromSeconds(config.TimeToLiveSeconds),
                CorrelationKey = string.IsNullOrWhiteSpace(config.CorrelationKey) ? "default" : config.CorrelationKey.Trim(),
                Metadata = new Dictionary<string, string>(config.Metadata, StringComparer.OrdinalIgnoreCase),
                CreatedAtUtc = config.CreatedAtUtc,
                UpdatedAtUtc = config.UpdatedAtUtc
            },
            cancellationToken);

        return Ok(Map(persisted));
    }

    /// <summary>
    /// Deletes a CEP configuration.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        bool removed = await repository.DeleteAsync(id, cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    /// <summary>
    /// Simulates CEP window processing for a configuration.
    /// </summary>
    [HttpPost("{id}/simulate")]
    public async Task<ActionResult<CepSimulationResponse>> Simulate(
        string id,
        [FromBody] CepSimulationRequest request,
        CancellationToken cancellationToken)
    {
        CepConfig? config = await repository.GetAsync(id, cancellationToken);
        if (config is null)
        {
            return NotFound();
        }

        CepWindow<Dictionary<string, object?>> window = CepWindowBuilder
            .For<Dictionary<string, object?>>(config)
            .CorrelateBy(payload => ResolvePayloadKey(config.CorrelationKey, payload))
            .Build();

        List<CepSimulationWindowResult> windows = [];
        foreach (CepSimulationEvent evt in request.Events.OrderBy(x => x.TimestampUtc))
        {
            string effectiveKey = ResolveEventKey(config, evt);
            DateTime timestampUtc = evt.TimestampUtc.Kind == DateTimeKind.Utc
                ? evt.TimestampUtc
                : evt.TimestampUtc.ToUniversalTime();
            IReadOnlyList<CepEvent<Dictionary<string, object?>>> eventsInWindow = window.Add(
                effectiveKey,
                evt.Payload,
                timestampUtc);

            windows.Add(new CepSimulationWindowResult
            {
                Key = effectiveKey,
                TimestampUtc = timestampUtc,
                Count = eventsInWindow.Count,
                WindowTimestampsUtc = eventsInWindow.Select(x => x.Timestamp).ToArray()
            });
        }

        return Ok(new CepSimulationResponse
        {
            Id = config.Id,
            Name = config.Name,
            TenantId = config.TenantId,
            CorrelationKey = config.CorrelationKey,
            ProcessedEvents = request.Events.Count,
            Windows = windows
        });
    }

    private static CepConfigDto Map(CepConfig config)
    {
        return new CepConfigDto
        {
            Id = config.Id,
            TenantId = config.TenantId,
            Name = config.Name,
            Description = config.Description,
            WindowType = config.WindowType.ToString(),
            WindowSizeSeconds = (int)Math.Round(config.WindowSize.TotalSeconds),
            TimeToLiveSeconds = (int)Math.Round(config.TimeToLive.TotalSeconds),
            CorrelationKey = config.CorrelationKey,
            Metadata = new Dictionary<string, string>(config.Metadata, StringComparer.OrdinalIgnoreCase),
            CreatedAtUtc = config.CreatedAtUtc,
            UpdatedAtUtc = config.UpdatedAtUtc
        };
    }

    private static string ResolveEventKey(CepConfig config, CepSimulationEvent evt)
    {
        if (!string.IsNullOrWhiteSpace(evt.Key))
        {
            return evt.Key.Trim();
        }

        return ResolvePayloadKey(config.CorrelationKey, evt.Payload);
    }

    private static string ResolvePayloadKey(string correlationKey, IReadOnlyDictionary<string, object?> payload)
    {
        if (!string.IsNullOrWhiteSpace(correlationKey) &&
            payload.TryGetValue(correlationKey, out object? value) &&
            value is not null)
        {
            string text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        return "default";
    }
}
