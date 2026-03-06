using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Muonroi.RuleEngine.DecisionTable.Models;
using Muonroi.RuleEngine.DecisionTable.Web;
using DecisionTableModel = Muonroi.RuleEngine.DecisionTable.Models.DecisionTable;

namespace Muonroi.RuleEngine.DecisionTable.Tests;

public sealed class DecisionTableApiIntegrationTests
{
    [Fact]
    public async Task DecisionTableApis_ShouldCreateValidateAndExport()
    {
        await using WebApplication app = BuildApp();
        await app.StartAsync();
        HttpClient client = app.GetTestClient();

        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/v1/decision-tables", BuildTable());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using JsonDocument createDoc = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        string? id = createDoc.RootElement.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(id));

        HttpResponseMessage validateResponse = await client.PostAsync($"/api/v1/decision-tables/{id}/validate", null);
        validateResponse.EnsureSuccessStatusCode();
        string validateJson = await validateResponse.Content.ReadAsStringAsync();
        Assert.Contains("\"isValid\":true", validateJson, StringComparison.OrdinalIgnoreCase);

        HttpResponseMessage exportJsonResponse = await client.PostAsync($"/api/v1/decision-tables/{id}/export/json", null);
        exportJsonResponse.EnsureSuccessStatusCode();
        string workflowJson = await exportJsonResponse.Content.ReadAsStringAsync();
        Assert.Contains("WorkflowName", workflowJson, StringComparison.OrdinalIgnoreCase);

        HttpResponseMessage exportDmnResponse = await client.PostAsync($"/api/v1/decision-tables/{id}/export/dmn", null);
        exportDmnResponse.EnsureSuccessStatusCode();
        string dmnXml = await exportDmnResponse.Content.ReadAsStringAsync();
        Assert.Contains("<definitions", dmnXml, StringComparison.OrdinalIgnoreCase);
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

    private static DecisionTableModel BuildTable()
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
            Name = "ApiTable",
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
