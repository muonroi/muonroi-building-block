namespace Muonroi.RuleEngine.Proliferation.Persistence;

/// <summary>
/// PostgreSQL-backed persistence for proliferation scenarios and results.
/// </summary>
/// <remarks>
/// Creates a PostgreSQL-backed proliferation store.
/// </remarks>
/// <param name="db">The proliferation DbContext.</param>
public sealed class PostgresProliferationStore(ProliferationDbContext db) : IProliferationStore
{
    private readonly ProliferationDbContext _db = db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc/>
    public async Task SaveScenariosAsync(IReadOnlyList<NeuronScenario> scenarios, CancellationToken ct = default)
    {
        foreach (NeuronScenario scenario in scenarios)
        {
            NeuronScenarioEntity entity = ToEntity(scenario);
            _db.NeuronScenarios.Add(entity);

            _db.RuleLineages.Add(new RuleLineageEntity
            {
                ScenarioId = scenario.Id,
                SeedRuleCode = scenario.SeedRuleCode,
                ParentScenarioId = scenario.ParentScenarioId,
                Depth = scenario.GenerationDepth,
                Reason = scenario.ProliferationReason,
                CreatedAt = scenario.CreatedAt
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
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

        ScenarioResultEntity? existing = await _db.ScenarioResults.FindAsync([result.ScenarioId], ct);
        if (existing is not null)
        {
            _db.Entry(existing).CurrentValues.SetValues(entity);
        }
        else
        {
            _db.ScenarioResults.Add(entity);
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NeuronScenario>> GetPendingScenariosAsync(int limit = 10, CancellationToken ct = default)
    {
        List<NeuronScenarioEntity> entities = await _db.NeuronScenarios
            .Where(e => e.Status == ScenarioStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NeuronScenario>> GetScenariosBySeedAsync(string seedRuleCode, CancellationToken ct = default)
    {
        List<NeuronScenarioEntity> entities = await _db.NeuronScenarios
            .Where(e => e.SeedRuleCode == seedRuleCode)
            .OrderBy(e => e.CreatedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ScenarioResult>> GetResultsByWorkflowAsync(string seedRuleCode, CancellationToken ct = default)
    {
        // Single query: join scenarios -> results filtered by workflow
        List<ScenarioResultEntity> entities = await _db.ScenarioResults
            .AsNoTracking()
            .Where(r => _db.NeuronScenarios
                .Any(s => s.Id == r.ScenarioId && s.SeedRuleCode == seedRuleCode))
            .ToListAsync(ct);

        return entities.Select(ToResultModel).ToList();
    }

    /// <inheritdoc/>
    public async Task<ScenarioResult?> GetResultAsync(string scenarioId, CancellationToken ct = default)
    {
        ScenarioResultEntity? entity = await _db.ScenarioResults
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ScenarioId == scenarioId, ct);

        return entity is null ? null : ToResultModel(entity);
    }

    /// <inheritdoc/>
    public async Task UpdateStatusAsync(string scenarioId, ScenarioStatus status, CancellationToken ct = default)
    {
        await _db.NeuronScenarios
            .Where(e => e.Id == scenarioId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, status), ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RuleLineage>> GetLineageAsync(string seedRuleCode, CancellationToken ct = default)
    {
        List<RuleLineageEntity> entities = await _db.RuleLineages
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

    /// <inheritdoc/>
    public async Task<ProliferationStats> GetStatsAsync(string? seedRuleCode = null, CancellationToken ct = default)
    {
        IQueryable<NeuronScenarioEntity> query = _db.NeuronScenarios.AsNoTracking();
        if (seedRuleCode is not null)
        {
            query = query.Where(e => e.SeedRuleCode == seedRuleCode);
        }

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
        InputFactsJson = scenario.InputFacts.ValueKind != JsonValueKind.Undefined
            ? scenario.InputFacts.GetRawText()
            : "{}",
        ExpectedBehavior = scenario.ExpectedBehavior,
        GeneratedRuleFlowGraph = scenario.GeneratedRuleFlowGraph,
        Status = scenario.Status,
        CreatedAt = scenario.CreatedAt,
        TenantId = scenario.TenantId
    };

    private static ScenarioResult ToResultModel(ScenarioResultEntity entity)
    {
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
