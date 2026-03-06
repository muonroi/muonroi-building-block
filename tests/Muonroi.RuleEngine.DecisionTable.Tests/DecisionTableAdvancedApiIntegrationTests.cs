using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Muonroi.RuleEngine.DecisionTable.Models;
using Muonroi.RuleEngine.DecisionTable.Web;
using DecisionTableModel = Muonroi.RuleEngine.DecisionTable.Models.DecisionTable;

namespace Muonroi.RuleEngine.DecisionTable.Tests;

public sealed class DecisionTableAdvancedApiIntegrationTests
{
    [Fact]
    public async Task DecisionTableApis_ShouldSupport_Search_Bulk_Reorder_Audit_And_Versions()
    {
        await using WebApplication app = BuildApp();
        await app.StartAsync();
        HttpClient client = app.GetTestClient();

        DecisionTableModel created = await CreateTableAsync(client, BuildTable("RiskScore", "tenant-a"));

        HttpResponseMessage listResponse = await client.GetAsync("/api/v1/decision-tables?search=Risk&tenantId=tenant-a&hitPolicy=FIRST");
        listResponse.EnsureSuccessStatusCode();
        DecisionTablePageResult? listResult = await listResponse.Content.ReadFromJsonAsync<DecisionTablePageResult>();
        Assert.NotNull(listResult);
        Assert.True(listResult.Items.Count >= 1);
        Assert.Contains(listResult.Items, x => x.Id == created.Id);

        string[] reversedRowIds = [.. created.Rows.OrderByDescending(x => x.Order).Select(x => x.Id)];
        HttpResponseMessage reorderResponse = await client.PostAsJsonAsync($"/api/v1/decision-tables/{created.Id}/rows/reorder", new
        {
            rowIds = reversedRowIds,
            actor = "integration-test",
            reason = "drag-drop"
        });
        reorderResponse.EnsureSuccessStatusCode();

        DecisionTableModel? reordered = await reorderResponse.Content.ReadFromJsonAsync<DecisionTableModel>();
        Assert.NotNull(reordered);
        Assert.Equal(reversedRowIds[0], reordered.Rows.OrderBy(x => x.Order).First().Id);

        HttpResponseMessage versionsResponse = await client.GetAsync($"/api/v1/decision-tables/{created.Id}/versions");
        versionsResponse.EnsureSuccessStatusCode();
        List<DecisionTableVersionSnapshot>? versions = await versionsResponse.Content.ReadFromJsonAsync<List<DecisionTableVersionSnapshot>>();
        Assert.NotNull(versions);
        Assert.True(versions.Count >= 2);

        HttpResponseMessage auditResponse = await client.GetAsync($"/api/v1/decision-tables/{created.Id}/audit");
        auditResponse.EnsureSuccessStatusCode();
        List<DecisionTableAuditEntry>? auditEntries = await auditResponse.Content.ReadFromJsonAsync<List<DecisionTableAuditEntry>>();
        Assert.NotNull(auditEntries);
        Assert.Contains(auditEntries, x => x.Action == "create");
        Assert.Contains(auditEntries, x => x.Action == "reorder-rows");

        DecisionTableModel bulkTableA = BuildTable("BulkA", "tenant-a");
        DecisionTableModel bulkTableB = BuildTable("BulkB", "tenant-b");
        HttpResponseMessage bulkUpsertResponse = await client.PostAsJsonAsync("/api/v1/decision-tables/bulk/upsert", new
        {
            tables = new[] { bulkTableA, bulkTableB },
            actor = "integration-test",
            reason = "bulk-seed"
        });
        bulkUpsertResponse.EnsureSuccessStatusCode();
        DecisionTableBulkResult? bulkResult = await bulkUpsertResponse.Content.ReadFromJsonAsync<DecisionTableBulkResult>();
        Assert.NotNull(bulkResult);
        Assert.Equal(2, bulkResult.ProcessedCount);

        HttpResponseMessage bulkDeleteResponse = await client.PostAsJsonAsync("/api/v1/decision-tables/bulk/delete", new
        {
            ids = new[] { created.Id },
            actor = "integration-test",
            reason = "cleanup"
        });
        bulkDeleteResponse.EnsureSuccessStatusCode();

        HttpResponseMessage deletedGetResponse = await client.GetAsync($"/api/v1/decision-tables/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deletedGetResponse.StatusCode);

        HttpResponseMessage includeDeletedResponse = await client.GetAsync("/api/v1/decision-tables?includeDeleted=true&search=RiskScore");
        includeDeletedResponse.EnsureSuccessStatusCode();
        DecisionTablePageResult? includeDeletedList = await includeDeletedResponse.Content.ReadFromJsonAsync<DecisionTablePageResult>();
        Assert.NotNull(includeDeletedList);
        Assert.Contains(includeDeletedList.Items, x => x.Id == created.Id);
    }

    private static WebApplication BuildApp()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseTestServer();
        builder.Services.AddDecisionTableWeb();

        WebApplication app = builder.Build();
        app.MapControllers();
        return app;
    }

    private static async Task<DecisionTableModel> CreateTableAsync(HttpClient client, DecisionTableModel table)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/decision-tables", table);
        response.EnsureSuccessStatusCode();
        DecisionTableModel? created = await response.Content.ReadFromJsonAsync<DecisionTableModel>();
        Assert.NotNull(created);
        return created;
    }

    private static DecisionTableModel BuildTable(string name, string tenantId)
    {
        DecisionTableColumn input = new()
        {
            Name = "Age",
            Label = "Age",
            DataType = "number"
        };
        DecisionTableColumn output = new()
        {
            Name = "CanApprove",
            Label = "CanApprove",
            DataType = "boolean"
        };

        DecisionTableModel table = new()
        {
            Name = name,
            TenantId = tenantId,
            HitPolicy = HitPolicy.First,
            InputColumns = [input],
            OutputColumns = [output],
            Rows = [
                new DecisionTableRow
                {
                    Order = 1,
                    InputCells = [new DecisionTableCell { ColumnId = input.Id, Expression = ">= 18" }],
                    OutputCells = [new DecisionTableCell { ColumnId = output.Id, Expression = "true" }]
                },
                new DecisionTableRow
                {
                    Order = 2,
                    InputCells = [new DecisionTableCell { ColumnId = input.Id, Expression = "< 18" }],
                    OutputCells = [new DecisionTableCell { ColumnId = output.Id, Expression = "false" }]
                }
            ]
        };
        return table;
    }
}
