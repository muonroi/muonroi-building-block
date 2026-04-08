using Microsoft.Extensions.Hosting;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Experience.Runtime.Evolution;

/// <summary>
/// Opt-in background service that runs <see cref="ExperienceEvolutionOrchestrator.RunEvolutionAsync"/>
/// once per week (next Sunday midnight UTC). Enabled only when
/// <see cref="EvolutionOptions.EnableBackgroundService"/> is <c>true</c>.
/// </summary>
public sealed class EvolutionBackgroundService(
    ExperienceEvolutionOrchestrator orchestrator,
    EvolutionOptions options,
    IMLog<EvolutionBackgroundService>? log = null) : BackgroundService
{
    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.EnableBackgroundService)
        {
            log?.Debug("EvolutionBackgroundService is disabled via EvolutionOptions.EnableBackgroundService=false — exiting");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay = ComputeDelayUntilNextSunday();
            log?.Debug("Next evolution run in {Hours:F1}h", delay.TotalHours);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                EvolutionReport report = await orchestrator.RunEvolutionAsync(stoppingToken);
                log?.Info("Evolution complete:\n{Report}", report.ToString());
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                log?.Warn("Evolution run failed — {Error}", ex.Message);
            }
        }
    }

    /// <summary>
    /// Computes the time remaining until next Sunday midnight UTC.
    /// Returns at least 1 minute to avoid busy-looping if called exactly on Sunday midnight.
    /// </summary>
    private static TimeSpan ComputeDelayUntilNextSunday()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        int daysUntilSunday = ((int)DayOfWeek.Sunday - (int)now.DayOfWeek + 7) % 7;

        // If today is already Sunday, wait until next week
        if (daysUntilSunday == 0) daysUntilSunday = 7;

        DateTimeOffset nextSunday = now.Date.AddDays(daysUntilSunday);
        TimeSpan delay = nextSunday - now;

        // Minimum 1 minute to prevent tight loops
        return delay < TimeSpan.FromMinutes(1) ? TimeSpan.FromDays(7) : delay;
    }
}
