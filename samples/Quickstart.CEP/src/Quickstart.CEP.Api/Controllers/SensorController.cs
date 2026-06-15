using Microsoft.AspNetCore.Mvc;
using Muonroi.RuleEngine.CEP;
using Muonroi.RuleEngine.CEP.Abstractions;
using Muonroi.RuleEngine.CEP.Builder;
using Quickstart.CEP.Api.Models;
using Quickstart.CEP.Api.Services;

namespace Quickstart.CEP.Api.Controllers;

/// <summary>
/// Demonstrates all features of Muonroi.RuleEngine.CEP:
///
///   1. POST /api/sensors/events            — add a SensorReading to the service-level windows
///   2. GET  /api/sensors/windows           — list all persisted CepConfig entries
///   3. POST /api/sensors/windows           — create a new CepConfig via CepWindowBuilder
///   4. GET  /api/sensors/windows/{id}      — get a specific CepConfig
///   5. DELETE /api/sensors/windows/{id}    — delete a CepConfig
///   6. POST /api/sensors/windows/{id}/evaluate — push an event and get window results
///   7. GET  /api/sensors/alert-demo        — full anomaly-detection walkthrough
/// </summary>
[ApiController]
[Route("api/sensors")]
public class SensorController(
    TemperatureAlertService alertService,
    ICepConfigRepository configRepository) : ControllerBase
{
    // =========================================================================
    // 1. POST /api/sensors/events
    //    Submit a SensorReading to the service-level sliding + tumbling windows.
    //    Returns the current window snapshots for that device.
    // =========================================================================
    /// <summary>
    /// Submits a sensor reading and returns the current sliding- and
    /// tumbling-window snapshots for the device.
    /// </summary>
    [HttpPost("events")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult SubmitEvent([FromBody] SensorReading reading)
    {
        if (string.IsNullOrWhiteSpace(reading.DeviceId))
        {
            return BadRequest(new { error = "DeviceId is required." });
        }

        WindowSummary summary = alertService.RecordReading(reading);

        return Ok(new
        {
            message = "Reading recorded in both windows.",
            reading,
            windows = new
            {
                sliding = new
                {
                    config = new
                    {
                        alertService.SlidingConfig.Id,
                        alertService.SlidingConfig.Name,
                        alertService.SlidingConfig.WindowType,
                        windowSizeMinutes = alertService.SlidingConfig.WindowSize.TotalMinutes,
                        alertService.SlidingConfig.CorrelationKey,
                        alertService.SlidingConfig.TenantId,
                        alertService.SlidingConfig.Metadata
                    },
                    eventCount = summary.SlidingWindowEvents,
                    averageValue = Math.Round(summary.SlidingWindowAvg, 2)
                },
                tumbling = new
                {
                    config = new
                    {
                        alertService.TumblingConfig.Id,
                        alertService.TumblingConfig.Name,
                        alertService.TumblingConfig.WindowType,
                        windowSizeHours = alertService.TumblingConfig.WindowSize.TotalHours,
                        alertService.TumblingConfig.CorrelationKey,
                        alertService.TumblingConfig.TenantId,
                        alertService.TumblingConfig.Metadata
                    },
                    eventCount = summary.TumblingWindowEvents,
                    averageValue = Math.Round(summary.TumblingWindowAvg, 2)
                }
            }
        });
    }

    // =========================================================================
    // 2. GET /api/sensors/windows
    //    List all CepConfig entries visible in the current execution context.
    //    Uses ICepConfigRepository.ListAsync().
    // =========================================================================
    /// <summary>
    /// Lists all persisted CEP window configurations from the repository.
    /// </summary>
    [HttpGet("windows")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListWindows(CancellationToken token)
    {
        IReadOnlyList<CepConfig> configs = await configRepository.ListAsync(token);

        return Ok(new
        {
            count = configs.Count,
            windows = configs.Select(c => new
            {
                c.Id,
                c.Name,
                c.TenantId,
                c.Description,
                c.WindowType,
                windowSizeSeconds = (int)c.WindowSize.TotalSeconds,
                timeToLiveSeconds = (int)c.TimeToLive.TotalSeconds,
                c.CorrelationKey,
                c.Metadata,
                c.CreatedAtUtc,
                c.UpdatedAtUtc
            })
        });
    }

    // =========================================================================
    // 3. POST /api/sensors/windows
    //    Create and persist a new CepConfig using CepWindowBuilder.
    //    Accepts a request body specifying the window name, type, and size.
    // =========================================================================
    /// <summary>
    /// Creates and persists a new CEP window configuration.
    /// Demonstrates both <c>Sliding</c> and <c>Tumbling</c> builder paths.
    /// </summary>
    [HttpPost("windows")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateWindow(
        [FromBody] CreateWindowRequest request,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Name is required." });
        }

        if (request.WindowSizeSeconds <= 0)
        {
            return BadRequest(new { error = "WindowSizeSeconds must be greater than zero." });
        }

        // Build a CepConfig using the fluent builder.
        // CepWindowBuilder.Named(name) → CepConfigBuilder
        CepConfigBuilder builder = CepWindowBuilder
            .Named(request.Name)
            .ForTenant(request.TenantId)
            .Describe(request.Description)
            .CorrelateBy(request.CorrelationKey ?? "default");

        // Apply the requested windowing strategy.
        TimeSpan windowSize = TimeSpan.FromSeconds(request.WindowSizeSeconds);

        _ = request.WindowType?.ToLowerInvariant() == "tumbling"
            ? builder.Tumbling(windowSize)
            : builder.Sliding(windowSize);

        if (request.TimeToLiveSeconds > 0)
        {
            builder.KeepEventsFor(TimeSpan.FromSeconds(request.TimeToLiveSeconds));
        }

        foreach (KeyValuePair<string, string> kv in request.Metadata)
        {
            builder.WithMetadata(kv.Key, kv.Value);
        }

        CepConfig config = builder.Build(observedAtUtc: DateTime.UtcNow);

        // Check for a duplicate before saving.
        CepConfig? existing = await configRepository.GetAsync(config.Id, token);
        if (existing is not null)
        {
            return Conflict(new { error = $"A config with id '{config.Id}' already exists." });
        }

        // ICepConfigRepository.SaveAsync upserts the config.
        CepConfig saved = await configRepository.SaveAsync(config, token);

        return CreatedAtAction(
            nameof(GetWindow),
            new { id = saved.Id },
            new
            {
                saved.Id,
                saved.Name,
                saved.TenantId,
                saved.Description,
                saved.WindowType,
                windowSizeSeconds = (int)saved.WindowSize.TotalSeconds,
                timeToLiveSeconds = (int)saved.TimeToLive.TotalSeconds,
                saved.CorrelationKey,
                saved.Metadata,
                saved.CreatedAtUtc
            });
    }

    // =========================================================================
    // 4. GET /api/sensors/windows/{id}
    //    Retrieve a specific CepConfig by its identifier.
    //    Uses ICepConfigRepository.GetAsync().
    // =========================================================================
    /// <summary>
    /// Returns a single CEP window configuration by its identifier.
    /// </summary>
    [HttpGet("windows/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWindow(string id, CancellationToken token)
    {
        CepConfig? config = await configRepository.GetAsync(id, token);

        if (config is null)
        {
            return NotFound(new { error = $"Config '{id}' not found." });
        }

        return Ok(new
        {
            config.Id,
            config.Name,
            config.TenantId,
            config.Description,
            config.WindowType,
            windowSizeSeconds = (int)config.WindowSize.TotalSeconds,
            timeToLiveSeconds = (int)config.TimeToLive.TotalSeconds,
            config.CorrelationKey,
            config.Metadata,
            config.CreatedAtUtc,
            config.UpdatedAtUtc
        });
    }

    // =========================================================================
    // 5. DELETE /api/sensors/windows/{id}
    //    Remove a persisted CepConfig.
    //    Uses ICepConfigRepository.DeleteAsync().
    // =========================================================================
    /// <summary>
    /// Deletes a CEP window configuration by its identifier.
    /// </summary>
    [HttpDelete("windows/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWindow(string id, CancellationToken token)
    {
        bool deleted = await configRepository.DeleteAsync(id, token);

        if (!deleted)
        {
            return NotFound(new { error = $"Config '{id}' not found." });
        }

        return Ok(new { message = $"Config '{id}' deleted successfully." });
    }

    // =========================================================================
    // 6. POST /api/sensors/windows/{id}/evaluate
    //    Push a SensorReading into an ad-hoc CepEngine bound to the given config
    //    and return the window results immediately.
    // =========================================================================
    /// <summary>
    /// Pushes a sensor reading into a CEP window defined by the stored config
    /// and returns the events currently inside that window.
    /// </summary>
    [HttpPost("windows/{id}/evaluate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EvaluateWindow(
        string id,
        [FromBody] SensorReading reading,
        CancellationToken token)
    {
        CepConfig? config = await configRepository.GetAsync(id, token);

        if (config is null)
        {
            return NotFound(new { error = $"Config '{id}' not found." });
        }

        if (string.IsNullOrWhiteSpace(reading.DeviceId))
        {
            return BadRequest(new { error = "DeviceId is required." });
        }

        // Build a typed runtime window from the persisted config.
        // CepWindowBuilder.For<TPayload>(config) → CepWindowRuntimeBuilder<TPayload>
        // .CorrelateBy(Func<TPayload, string>) → CepWindowRuntimeBuilder<TPayload>
        // .Build() → CepWindow<TPayload>
        CepWindow<SensorReading> window = CepWindowBuilder
            .For<SensorReading>(config)
            .CorrelateBy(r => r.DeviceId)
            .Build();

        // CepWindow<T>.Add(payload, timestamp) → IReadOnlyList<CepEvent<T>>
        IReadOnlyList<CepEvent<SensorReading>> events = window.Add(reading, reading.Timestamp);

        return Ok(new
        {
            configId = config.Id,
            configName = config.Name,
            windowType = config.WindowType.ToString(),
            windowSizeSeconds = (int)config.WindowSize.TotalSeconds,
            correlationKey = config.CorrelationKey,
            inputReading = reading,
            windowResult = new
            {
                eventCount = events.Count,
                averageValue = events.Count > 0
                    ? Math.Round(events.Average(e => e.Value.Value), 2)
                    : 0,
                events = events.Select(e => new
                {
                    e.Key,
                    e.Timestamp,
                    e.Value.Metric,
                    e.Value.Value,
                    e.Value.DeviceId
                })
            }
        });
    }

    // =========================================================================
    // 7. GET /api/sensors/alert-demo
    //    End-to-end anomaly detection demo using a local CepEngine<SensorReading>.
    //    Generates 10 synthetic readings, some normal, some anomalous, then
    //    returns any alerts that fired.
    // =========================================================================
    /// <summary>
    /// Runs a self-contained anomaly-detection demonstration:
    /// <list type="bullet">
    ///   <item>Creates 10 synthetic sensor readings spread over 4 minutes.</item>
    ///   <item>Normal readings fall between 20–45 °C; anomalous readings spike to 95–100 °C.</item>
    ///   <item>An alert fires when ≥ 3 events in the 5-minute sliding window have an average > 80.</item>
    ///   <item>Returns every alert together with a full trace of all readings processed.</item>
    /// </list>
    /// </summary>
    [HttpGet("alert-demo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult AlertDemo(
        [FromQuery] double threshold = 80.0,
        [FromQuery] int minEvents = 3)
    {
        // Build synthetic sensor data covering two devices.
        // device-A: mostly normal with a spike cluster around T+2 min.
        // device-B: steady normal readings throughout.
        DateTime baseTime = DateTime.UtcNow.AddMinutes(-4);

        SensorReading[] readings =
        [
            // device-A — normal range
            new("device-A", "temperature", 22.5, baseTime.AddSeconds(0)),
            new("device-A", "temperature", 25.1, baseTime.AddSeconds(30)),
            // device-A — anomalous spike cluster (three consecutive high readings)
            new("device-A", "temperature", 92.0, baseTime.AddSeconds(60)),
            new("device-A", "temperature", 96.5, baseTime.AddSeconds(90)),
            new("device-A", "temperature", 98.3, baseTime.AddSeconds(120)),
            // device-A — returns to normal
            new("device-A", "temperature", 30.2, baseTime.AddSeconds(180)),
            new("device-A", "temperature", 28.8, baseTime.AddSeconds(240)),
            // device-B — always normal
            new("device-B", "temperature", 21.0, baseTime.AddSeconds(0)),
            new("device-B", "temperature", 23.4, baseTime.AddSeconds(60)),
            new("device-B", "temperature", 24.0, baseTime.AddSeconds(120))
        ];

        IReadOnlyList<AlertEvent> alerts =
            alertService.DetectAnomalies(readings, threshold, minEvents);

        return Ok(new
        {
            description =
                "Anomaly detection demo: a 5-minute sliding window raises an alert when " +
                $"≥ {minEvents} events have an average value > {threshold}.",
            parameters = new { threshold, minEvents },
            inputReadings = readings.Select(r => new
            {
                r.DeviceId,
                r.Metric,
                r.Value,
                timestampOffsetSeconds = (r.Timestamp - baseTime).TotalSeconds
            }),
            alertsFired = alerts.Count,
            alerts = alerts.Select(a => new
            {
                a.DeviceId,
                a.Rule,
                a.AverageValue,
                a.EventCount,
                a.WindowEnd
            }),
            notes = new[]
            {
                "device-A fires an alert because its three consecutive high readings (92, 96.5, 98.3) " +
                "are all within the 5-minute sliding window and their average (~95.6) exceeds the threshold.",
                "device-B never fires because its readings stay well below the threshold.",
                "CepEngine<T>.AddEvent returns only the events inside the active window at the moment " +
                "each reading is processed, so the window shrinks automatically as old events expire."
            }
        });
    }
}

// ---------------------------------------------------------------------------
// Request DTO for POST /api/sensors/windows
// ---------------------------------------------------------------------------
/// <summary>
/// Payload for creating a new CEP window configuration.
/// </summary>
public sealed record CreateWindowRequest
{
    /// <summary>Human-readable name for the window (becomes part of the auto-generated Id).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Optional tenant that owns the config. Defaults to <c>_global</c>.</summary>
    public string? TenantId { get; init; }

    /// <summary>Optional free-form description.</summary>
    public string? Description { get; init; }

    /// <summary>Windowing strategy: <c>"Sliding"</c> (default) or <c>"Tumbling"</c>.</summary>
    public string? WindowType { get; init; } = "Sliding";

    /// <summary>Active window size in seconds. Must be greater than zero.</summary>
    public int WindowSizeSeconds { get; init; } = 300;

    /// <summary>
    /// How long events are retained beyond the window. Must be ≥ WindowSizeSeconds.
    /// Pass 0 to let the builder default to the window size.
    /// </summary>
    public int TimeToLiveSeconds { get; init; }

    /// <summary>Logical field used to correlate events. Defaults to <c>"default"</c>.</summary>
    public string? CorrelationKey { get; init; } = "default";

    /// <summary>Arbitrary metadata for downstream consumers.</summary>
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}
