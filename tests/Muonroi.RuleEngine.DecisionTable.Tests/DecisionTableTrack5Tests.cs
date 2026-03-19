using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Muonroi.Governance.License;
using Muonroi.RuleEngine.DecisionTable.Models;
using Muonroi.RuleEngine.DecisionTable.Validators;
using Muonroi.RuleEngine.DecisionTable.Web;
using DecisionTableModel = Muonroi.RuleEngine.DecisionTable.Models.DecisionTable;

namespace Muonroi.RuleEngine.DecisionTable.Tests;

public sealed class DecisionTableTrack5Tests
{
    [Fact]
    public async Task DecisionTableExecute_ShouldReturnMatchingOutputs()
    {
        await using WebApplication app = BuildApp();
        await app.StartAsync();
        HttpClient client = app.GetTestClient();

        DecisionTableModel created = await CreateTableAsync(client, BuildExecutionTable());
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/decision-tables/{created.Id}/execute",
            new
            {
                inputs = new
                {
                    amount = 1200,
                    region = "VN"
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.GetProperty("matched").GetBoolean());
        Assert.Equal("row-1", payload.RootElement.GetProperty("matchedRowIds")[0].GetString());
        Assert.Equal("vip", payload.RootElement.GetProperty("outputs")[0].GetProperty("outputs").GetProperty("segment").GetString());
    }

    [Fact]
    public async Task DecisionTableExecute_ShouldReturnEmptyWhenNoMatch()
    {
        await using WebApplication app = BuildApp();
        await app.StartAsync();
        HttpClient client = app.GetTestClient();

        DecisionTableModel created = await CreateTableAsync(client, BuildExecutionTable());
        HttpResponseMessage response = await client.PostAsJsonAsync(
            $"/api/v1/decision-tables/{created.Id}/execute",
            new
            {
                inputs = new
                {
                    amount = 10,
                    region = "EU"
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(payload.RootElement.GetProperty("matched").GetBoolean());
        Assert.Equal(0, payload.RootElement.GetProperty("outputs").GetArrayLength());
    }

    [Fact]
    public async Task DecisionTableFeelValidateExpression_ShouldValidateRequest()
    {
        await using WebApplication app = BuildApp();
        await app.StartAsync();
        HttpClient client = app.GetTestClient();

        HttpResponseMessage validResponse = await client.PostAsJsonAsync(
            "/api/v1/decision-tables/sample/feel/validate-expression",
            new
            {
                expression = "> 100",
                columnDataType = "number"
            });

        HttpResponseMessage invalidResponse = await client.PostAsJsonAsync(
            "/api/v1/decision-tables/sample/feel/validate-expression",
            new
            {
                expression = "><",
                columnDataType = "number"
            });

        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);

        using JsonDocument validPayload = JsonDocument.Parse(await validResponse.Content.ReadAsStringAsync());
        using JsonDocument invalidPayload = JsonDocument.Parse(await invalidResponse.Content.ReadAsStringAsync());
        Assert.True(validPayload.RootElement.GetProperty("isValid").GetBoolean());
        Assert.False(invalidPayload.RootElement.GetProperty("isValid").GetBoolean());
    }

    [Fact]
    public void DecisionTableDiffer_ShouldReportAddedRemovedAndModifiedRows()
    {
        DecisionTableModel from = BuildExecutionTable();
        from.Version = 1;

        DecisionTableModel to = BuildExecutionTable();
        to.Version = 2;
        to.Rows[0].OutputCells[0].Expression = "\"gold\"";
        to.Rows.RemoveAll(x => x.Id == "row-2");
        to.Rows.Add(new DecisionTableRow
        {
            Id = "row-3",
            Order = 3,
            InputCells =
            [
                new DecisionTableCell
                {
                    ColumnId = "input-amount",
                    Expression = "> 2000"
                },
                new DecisionTableCell
                {
                    ColumnId = "input-region",
                    Expression = "\"VN\""
                }
            ],
            OutputCells =
            [
                new DecisionTableCell
                {
                    ColumnId = "output-segment",
                    Expression = "\"premium\""
                }
            ]
        });

        DecisionTableDiff diff = new DecisionTableDiffer().Compute(from, to);

        Assert.True(diff.HasChanges);
        Assert.Contains(diff.RowDiffs, x => x.Kind == DiffKind.Modified && x.RowId == "row-1");
        Assert.Contains(diff.RowDiffs, x => x.Kind == DiffKind.Removed && x.RowId == "row-2");
        Assert.Contains(diff.RowDiffs, x => x.Kind == DiffKind.Added && x.RowId == "row-3");
    }

    [Fact]
    public void DecisionTableValidator_ShouldReportConflict_ForUniqueMultiColumnOverlap()
    {
        DecisionTableModel table = BuildConflictTable();

        ValidationResult result = new DecisionTableValidator().Validate(table);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("Conflict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DecisionTableValidator_ShouldNotReportConflict_WhenRowsDoNotOverlap()
    {
        DecisionTableModel table = BuildConflictTable();
        table.Rows[1].InputCells[1].Expression = "\"US\"";

        ValidationResult result = new DecisionTableValidator().Validate(table);

        Assert.DoesNotContain(result.Errors, x => x.Contains("Conflict", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DecisionTableValidator_ShouldWarnForUnreachableRow_InFirstHitPolicy()
    {
        DecisionTableModel table = new()
        {
            Name = "Redundancy",
            HitPolicy = HitPolicy.First,
            InputColumns =
            [
                new DecisionTableColumn
                {
                    Id = "input-amount",
                    Name = "amount",
                    Label = "Amount",
                    DataType = "number"
                }
            ],
            OutputColumns =
            [
                new DecisionTableColumn
                {
                    Id = "output-decision",
                    Name = "decision",
                    Label = "Decision",
                    DataType = "string"
                }
            ],
            Rows =
            [
                new DecisionTableRow
                {
                    Id = "row-1",
                    Order = 1,
                    InputCells = [new DecisionTableCell { ColumnId = "input-amount", Expression = "> 0" }],
                    OutputCells = [new DecisionTableCell { ColumnId = "output-decision", Expression = "\"allow\"" }]
                },
                new DecisionTableRow
                {
                    Id = "row-2",
                    Order = 2,
                    InputCells = [new DecisionTableCell { ColumnId = "input-amount", Expression = "> 1000" }],
                    OutputCells = [new DecisionTableCell { ColumnId = "output-decision", Expression = "\"review\"" }]
                }
            ]
        };

        ValidationResult result = new DecisionTableValidator().Validate(table);

        Assert.NotNull(result.Warnings);
        Assert.Contains(result.Warnings!, x => x.Contains("Unreachable row", StringComparison.OrdinalIgnoreCase));
    }

    private static WebApplication BuildApp()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development
        });

        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(CreateLicensedState());
        builder.Services.AddDecisionTableWeb();

        WebApplication app = builder.Build();
        app.MapControllers();
        return app;
    }

    private static LicenseState CreateLicensedState()
    {
        return new LicenseState
        {
            IsValid = true,
            Tier = LicenseTier.Licensed,
            Features = ["decision-table.web", FreeTierFeatures.Premium.RuleEngine],
            Payload = new LicensePayload
            {
                LicenseId = "tests-decision-table",
                AllowedFeatures = ["decision-table.web", FreeTierFeatures.Premium.RuleEngine]
            }
        };
    }

    private static async Task<DecisionTableModel> CreateTableAsync(HttpClient client, DecisionTableModel table)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/decision-tables", table);
        response.EnsureSuccessStatusCode();
        DecisionTableModel? created = await response.Content.ReadFromJsonAsync<DecisionTableModel>();
        Assert.NotNull(created);
        return created;
    }

    private static DecisionTableModel BuildExecutionTable()
    {
        return new DecisionTableModel
        {
            Name = "ExecutionTable",
            HitPolicy = HitPolicy.First,
            InputColumns =
            [
                new DecisionTableColumn
                {
                    Id = "input-amount",
                    Name = "amount",
                    Label = "Amount",
                    DataType = "number"
                },
                new DecisionTableColumn
                {
                    Id = "input-region",
                    Name = "region",
                    Label = "Region",
                    DataType = "string"
                }
            ],
            OutputColumns =
            [
                new DecisionTableColumn
                {
                    Id = "output-segment",
                    Name = "segment",
                    Label = "Segment",
                    DataType = "string"
                }
            ],
            Rows =
            [
                new DecisionTableRow
                {
                    Id = "row-1",
                    Order = 1,
                    InputCells =
                    [
                        new DecisionTableCell { ColumnId = "input-amount", Expression = ">= 1000" },
                        new DecisionTableCell { ColumnId = "input-region", Expression = "\"VN\"" }
                    ],
                    OutputCells =
                    [
                        new DecisionTableCell { ColumnId = "output-segment", Expression = "\"vip\"" }
                    ]
                },
                new DecisionTableRow
                {
                    Id = "row-2",
                    Order = 2,
                    InputCells =
                    [
                        new DecisionTableCell { ColumnId = "input-amount", Expression = ">= 500" },
                        new DecisionTableCell { ColumnId = "input-region", Expression = "\"US\"" }
                    ],
                    OutputCells =
                    [
                        new DecisionTableCell { ColumnId = "output-segment", Expression = "\"priority\"" }
                    ]
                }
            ]
        };
    }

    private static DecisionTableModel BuildConflictTable()
    {
        return new DecisionTableModel
        {
            Name = "ConflictTable",
            HitPolicy = HitPolicy.Unique,
            InputColumns =
            [
                new DecisionTableColumn
                {
                    Id = "input-amount",
                    Name = "amount",
                    Label = "Amount",
                    DataType = "number"
                },
                new DecisionTableColumn
                {
                    Id = "input-region",
                    Name = "region",
                    Label = "Region",
                    DataType = "string"
                }
            ],
            OutputColumns =
            [
                new DecisionTableColumn
                {
                    Id = "output-discount",
                    Name = "discount",
                    Label = "Discount",
                    DataType = "number"
                }
            ],
            Rows =
            [
                new DecisionTableRow
                {
                    Id = "row-1",
                    Order = 1,
                    InputCells =
                    [
                        new DecisionTableCell { ColumnId = "input-amount", Expression = "> 1000" },
                        new DecisionTableCell { ColumnId = "input-region", Expression = "\"VN\"" }
                    ],
                    OutputCells =
                    [
                        new DecisionTableCell { ColumnId = "output-discount", Expression = "0.1" }
                    ]
                },
                new DecisionTableRow
                {
                    Id = "row-2",
                    Order = 2,
                    InputCells =
                    [
                        new DecisionTableCell { ColumnId = "input-amount", Expression = "> 500" },
                        new DecisionTableCell { ColumnId = "input-region", Expression = "\"VN\"" }
                    ],
                    OutputCells =
                    [
                        new DecisionTableCell { ColumnId = "output-discount", Expression = "0.05" }
                    ]
                }
            ]
        };
    }
}
