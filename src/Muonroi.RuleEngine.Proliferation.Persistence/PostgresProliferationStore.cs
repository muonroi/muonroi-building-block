using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Muonroi.RuleEngine.Proliferation.Models;
using Muonroi.RuleEngine.Proliferation.Persistence.Entities;

namespace Muonroi.RuleEngine.Proliferation.Persistence;

public sealed class PostgresProliferationStore(ProliferationDbContext db) : IProliferationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task SaveScenariosAsync(IReadOnlyList<NeuronScenario> scenarios, CancellationToken ct = default)
    {
        foreach (NeuronScenario scenario in scenarios)
        {
            NeuronScenarioEntity entity = ToEntity(scenario);
            db.NeuronScenarios.Add(entity);

            db.RuleLineages.Add(new RuleLineageEntity
            {
                ScenarioId = scenario.Id,
                SeedRuleCode = scenario.SeedRuleCode,
                ParentScenarioId = scenario.ParentScenarioId,
                Depth = scenario.GenerationDepth,
                Reason = scenario.ProliferationReason,
                CreatedAt = scenario.CreatedAt
            });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveResultAsync(ScenarioResult result, CancellationToken ct = default)
    {
        ScenarioResultEntity entity = new()
        {
            ScenarioId = result.ScenarioId,
            IsSuccess = result.IsSuccess,
            MatchesExpectation = result.MatchesExpectation,
            ActualBehavior = result.ActualBehavior,
            OutputFactsJson = result.OutputFacts.HasValue
                ? result.OutputFacts.Value.GetRawText()
                : null,
            ErrorsJson = JsonSerializer.Serialize(result.Errors, JsonOptions),
            DurationMs = (long)result.Duration.TotalMilliseconds,
            ExecutedAt = result.ExecutedAt
        };

        ScenarioResultEntity? existing = await db.ScenarioResults.FindAsync([result.ScenarioId], ct);
        if (existing is not null)
        {
            db.Entry(existing).CurrentValues.SetValues(entity);
        }
        else
        {
            db.ScenarioResults.Add(entity);
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NeuronScenario>> GetPendingScenariosAsync(int limit = 10, CancellationToken ct = default)
    {
        List<NeuronScenarioEntity> entities = await db.NeuronScenarios
            .Where(e => e.Status == ScenarioStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<NeuronScenario>> GetScenariosBySeedAsync(string seedRuleCode, CancellationToken ct = default)
    {
        List<NeuronScenarioEntity> entities = await db.NeuronScenarios
            .Where(e => e.SeedRuleCode == seedRuleCode)
            .OrderBy(e => e.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList();
    }

    public async Task<ScenarioResult?> GetResultAsync(string scenarioId, CancellationToken ct = default)
    {
        ScenarioResultEntity? entity = await db.ScenarioResults
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ScenarioId == scenarioId, ct);

        if (entity is null)
            return null;

        JsonElement? outputFacts = null;
        if (!string.IsNullOrWhiteSpace(entity.OutputFactsJson))
        {
            using JsonDocument doc = JsonDocument.Parse(entity.OutputFactsJson);
            outputFacts = doc.RootElement.Clone();
        }

        string[] errors = [];
        if (!string.IsNullOrWhiteSpace(entity.ErrorsJson))
        {
            errors = JsonSerializer.Deserialize<string[]>(entity.ErrorsJson, JsonOptions) ?? [];
        }

        return new ScenarioResult
        {
            ScenarioId = entity.ScenarioId,
            IsSuccess = entity.IsSuccess,
            MatchesExpectation = entity.MatchesExpectation,
            ActualBehavior = entity.ActualBehavior,
            OutputFacts = outputFacts,
            Errors = errors,
            Duration = TimeSpan.FromMilliseconds(entity.DurationMs),
            ExecutedAt = entity.ExecutedAt
        };
    }

    public async Task UpdateStatusAsync(string scenarioId, ScenarioStatus status, CancellationToken ct = default)
    {
        await db.NeuronScenarios
            .Where(e => e.Id == scenarioId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, status), ct);
    }

    public async Task<IReadOnlyList<RuleLineage>> GetLineageAsync(string seedRuleCode, CancellationToken ct = default)
    {
        List<RuleLineageEntity> entities = await db.RuleLineages
            .Where(e => e.SeedRuleCode == seedRuleCode)
            .OrderBy(e => e.Depth)
            .ThenBy(e => e.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        return entities.Select(e => new RuleLineage
        {
            ScenarioId = e.ScenarioId,
            SeedRuleCode = e.SeedRuleCode,
            ParentScenarioId = e.ParentScenarioId,
            Depth = e.Depth,
            Reason = e.Reason,
            CreatedAt = e.CreatedAt
        }).ToList();
    }

    public async Task<ProliferationStats> GetStatsAsync(string? seedRuleCode = null, CancellationToken ct = default)
    {
        IQueryable<NeuronScenarioEntity> query = db.NeuronScenarios.AsNoTracking();
        if (seedRuleCode is not null)
            query = query.Where(e => e.SeedRuleCode == seedRuleCode);

        int total = await query.CountAsync(ct);
        int passed = await query.CountAsync(e => e.Status == ScenarioStatus.Passed, ct);
        int failed = await query.CountAsync(e => e.Status == ScenarioStatus.Failed, ct);
        int pending = await query.CountAsync(e => e.Status == ScenarioStatus.Pending, ct);
        int maxDepth = total > 0 ? await query.MaxAsync(e => e.GenerationDepth, ct) : 0;

        Dictionary<string, int> bySeed = await query
            .GroupBy(e => e.SeedRuleCode)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, ct);

        return new ProliferationStats
        {
            TotalScenarios = total,
            Passed = passed,
            Failed = failed,
            Pending = pending,
            MaxDepthReached = maxDepth,
            BySeedRule = bySeed
        };
    }

    private static NeuronScenarioEntity ToEntity(NeuronScenario scenario) => new()
    {
        Id = scenario.Id,
        SeedRuleCode = scenario.SeedRuleCode,
        ScenarioName = scenario.ScenarioName,
        Type = scenario.Type,
        Scope = scenario.Scope,
        ParentScenarioId = scenario.ParentScenarioId,
        GenerationDepth = scenario.GenerationDepth,
        ProliferationReason = scenario.ProliferationReason,
        InputFactsJson = scenario.InputFacts.ValueKind != System.Text.Json.JsonValueKind.Undefined
            ? scenario.InputFacts.GetRawText()
            : "{}",
        ExpectedBehavior = scenario.ExpectedBehavior,
        GeneratedRuleFlowGraph = scenario.GeneratedRuleFlowGraph,
        Status = scenario.Status,
        CreatedAt = scenario.CreatedAt,
        TenantId = scenario.TenantId
    };

    private static NeuronScenario ToModel(NeuronScenarioEntity entity)
    {
        JsonElement inputFacts = default;
        if (!string.IsNullOrWhiteSpace(entity.InputFactsJson))
        {
            using JsonDocument doc = JsonDocument.Parse(entity.InputFactsJson);
            inputFacts = doc.RootElement.Clone();
        }

        return new NeuronScenario
        {
            Id = entity.Id,
            SeedRuleCode = entity.SeedRuleCode,
            ScenarioName = entity.ScenarioName,
            Type = entity.Type,
            Scope = entity.Scope,
            ParentScenarioId = entity.ParentScenarioId,
            GenerationDepth = entity.GenerationDepth,
            ProliferationReason = entity.ProliferationReason,
            InputFacts = inputFacts,
            ExpectedBehavior = entity.ExpectedBehavior,
            GeneratedRuleFlowGraph = entity.GeneratedRuleFlowGraph,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            TenantId = entity.TenantId
        };
    }
}
