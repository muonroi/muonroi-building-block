using DecisionTableModel = Muonroi.RuleEngine.DecisionTable.Models.DecisionTable;

namespace Muonroi.RuleEngine.DecisionTable.Tests;

public sealed class InMemoryDecisionTableStoreTests
{
    [Fact]
    public async Task SaveAsync_PersistsClone_SortsRows_AndCreatesVersionAudit()
    {
        InMemoryDecisionTableStore store = new(new TestJsonSerializeService());
        DecisionTableModel table = CreateTable(
            id: "table-1",
            name: "Eligibility",
            tenantId: "tenant-a",
            hitPolicy: HitPolicy.First,
            description: "Eligibility rules",
            rowIds: ["row-b", "row-a"]);

        await store.SaveAsync(table, actor: "tester", reason: "initial");

        table.Name = "mutated";
        table.Rows[0].Order = 99;

        DecisionTableModel? saved = await store.GetByIdAsync("table-1");
        IReadOnlyList<DecisionTableVersionSnapshot> versions = await store.GetVersionHistoryAsync("table-1");
        IReadOnlyList<DecisionTableAuditEntry> audits = await store.GetAuditTrailAsync("table-1");

        Assert.NotNull(saved);
        Assert.Equal("Eligibility", saved.Name);
        Assert.Equal(1, saved.Version);
        Assert.Equal(["row-a", "row-b"], saved.Rows.Select(x => x.Id));
        Assert.Equal([1, 2], saved.Rows.Select(x => x.Order));
        Assert.Single(versions);
        Assert.Equal("create", versions[0].ChangeType);
        Assert.Equal("tester", versions[0].Actor);
        Assert.Single(audits);
        Assert.Equal("create", audits[0].Action);
        Assert.Contains("\"Name\":\"Eligibility\"", audits[0].PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueryAsync_AppliesFilters_Paging_AndSkipsDeletedByDefault()
    {
        InMemoryDecisionTableStore store = new(new TestJsonSerializeService());

        await store.SaveAsync(CreateTable("alpha-1", "Alpha One", "tenant-a", HitPolicy.First, "find me", ["a1"]));
        await store.SaveAsync(CreateTable("beta-1", "Beta One", "tenant-a", HitPolicy.Collect, "other", ["b1"]));
        await store.SaveAsync(CreateTable("alpha-2", "Alpha Two", "tenant-b", HitPolicy.First, "searchable", ["c1"]));
        await store.BulkDeleteAsync(["alpha-2"], actor: "tester", reason: "cleanup");

        DecisionTablePageResult filtered = await store.QueryAsync(new DecisionTableQuery
        {
            Search = "alpha",
            TenantId = "tenant-a",
            HitPolicy = HitPolicy.First,
            Page = 1,
            PageSize = 10
        });
        DecisionTablePageResult paged = await store.QueryAsync(new DecisionTableQuery
        {
            Page = 1,
            PageSize = 1
        });
        DecisionTablePageResult includingDeleted = await store.QueryAsync(new DecisionTableQuery
        {
            IncludeDeleted = true,
            Page = 1,
            PageSize = 10
        });

        Assert.Single(filtered.Items);
        Assert.Equal("alpha-1", filtered.Items[0].Id);
        Assert.Single(paged.Items);
        Assert.Equal(2, paged.Total);
        Assert.Equal(3, includingDeleted.Total);
    }

    [Fact]
    public async Task BulkUpsertAndDeleteAsync_RecordExpectedResults_AndDeleteHidesTable()
    {
        InMemoryDecisionTableStore store = new(new TestJsonSerializeService());
        DecisionTableModel first = CreateTable("bulk-1", "Bulk One", "tenant-a", HitPolicy.First, "desc", ["r1"]);
        DecisionTableModel second = CreateTable("bulk-2", "Bulk Two", "tenant-b", HitPolicy.First, "desc", ["r2"]);

        DecisionTableBulkResult upsert = await store.BulkUpsertAsync([first, second], actor: "batch", reason: "seed");
        DecisionTableBulkResult delete = await store.BulkDeleteAsync(["bulk-1", "", "missing"], actor: "batch", reason: "trim");
        IReadOnlyList<DecisionTableAuditEntry> audits = await store.GetAuditTrailAsync();

        Assert.Equal(2, upsert.ProcessedCount);
        Assert.Equal(["bulk-1", "bulk-2"], upsert.Ids);
        Assert.Equal(1, delete.ProcessedCount);
        Assert.Equal(["bulk-1"], delete.Ids);
        Assert.Null(await store.GetByIdAsync("bulk-1"));
        Assert.NotNull(await store.GetByIdAsync("bulk-2"));
        Assert.Contains(audits, x => x.Action == "bulk-upsert" && x.TableId is null);
        Assert.Contains(audits, x => x.Action == "bulk-delete" && x.TableId is null);
        Assert.Contains(audits, x => x.Action == "delete" && x.TableId == "bulk-1");
    }

    [Fact]
    public async Task ReorderRowsAsync_UpdatesOrder_Version_AndAudit()
    {
        InMemoryDecisionTableStore store = new(new TestJsonSerializeService());
        DecisionTableModel table = CreateTable("reorder-1", "Reorder", "tenant-a", HitPolicy.First, "desc", ["row-1", "row-2", "row-3"]);

        await store.SaveAsync(table, actor: "tester", reason: "initial");

        bool reordered = await store.ReorderRowsAsync("reorder-1", ["row-3", "row-1", "row-2"], actor: "tester", reason: "reprioritize");
        DecisionTableModel? saved = await store.GetByIdAsync("reorder-1");
        IReadOnlyList<DecisionTableVersionSnapshot> versions = await store.GetVersionHistoryAsync("reorder-1");
        IReadOnlyList<DecisionTableAuditEntry> audits = await store.GetAuditTrailAsync("reorder-1");

        Assert.True(reordered);
        Assert.NotNull(saved);
        Assert.Equal(2, saved.Version);
        Assert.Equal(["row-3", "row-1", "row-2"], saved.Rows.Select(x => x.Id));
        Assert.Equal([1, 2, 3], saved.Rows.Select(x => x.Order));
        Assert.Equal([2, 1], versions.Select(x => x.Version));
        Assert.Equal("reorder", versions[0].ChangeType);
        Assert.Contains(audits, x => x.Action == "reorder-rows");
    }

    [Fact]
    public async Task ReorderRowsAsync_ReturnsFalse_WhenTableMissingOrInputDoesNotMatchRows()
    {
        InMemoryDecisionTableStore store = new(new TestJsonSerializeService());
        await store.SaveAsync(CreateTable("guard-1", "Guard", "tenant-a", HitPolicy.First, "desc", ["row-1", "row-2"]));

        bool missingTable = await store.ReorderRowsAsync("missing", ["row-1"]);
        bool wrongCount = await store.ReorderRowsAsync("guard-1", ["row-1"]);
        bool wrongId = await store.ReorderRowsAsync("guard-1", ["row-1", "row-x"]);

        Assert.False(missingTable);
        Assert.False(wrongCount);
        Assert.False(wrongId);
    }

    private static DecisionTableModel CreateTable(
        string id,
        string name,
        string tenantId,
        HitPolicy hitPolicy,
        string description,
        IReadOnlyList<string> rowIds)
    {
        DecisionTableColumn input = new()
        {
            Id = "input-age",
            Name = "Age",
            Label = "Age",
            DataType = "number"
        };
        DecisionTableColumn output = new()
        {
            Id = "output-status",
            Name = "Status",
            Label = "Status",
            DataType = "string"
        };

        List<DecisionTableRow> rows = [.. rowIds
            .Select((rowId, index) => new DecisionTableRow
            {
                Id = rowId,
                Order = rowIds.Count - index,
                Description = $"Row {index + 1}",
                InputCells =
                [
                    new DecisionTableCell
                    {
                        ColumnId = input.Id,
                        Expression = $">= {18 + index}"
                    }
                ],
                OutputCells =
                [
                    new DecisionTableCell
                    {
                        ColumnId = output.Id,
                        Expression = $"Level{index + 1}"
                    }
                ]
            })];

        return new DecisionTableModel
        {
            Id = id,
            Name = name,
            Description = description,
            TenantId = tenantId,
            HitPolicy = hitPolicy,
            InputColumns = [input],
            OutputColumns = [output],
            Rows = rows,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
    }

    private sealed class TestJsonSerializeService : IMJsonSerializeService
    {
        public T? Deserialize<T>(string text) => JsonSerializer.Deserialize<T>(text);

        public string Serialize<T>(T obj) => JsonSerializer.Serialize(obj);
    }
}
