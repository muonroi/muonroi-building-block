using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Context;
using Muonroi.RuleEngine.CEP.Controllers;
using Muonroi.RuleEngine.CEP.Repositories;

namespace Muonroi.RuleEngine.CEP.Tests;

public class CepControllerTests
{
    [Fact]
    public async Task SaveAndList_UsesRepositoryBackedState()
    {
        CepController controller = CreateController("tenant-a");

        ActionResult<CepConfigDto> save = await controller.Save(
            "fraud",
            new CepConfigDto
            {
                Name = "Fraud",
                WindowType = "Sliding",
                WindowSizeSeconds = 30,
                TimeToLiveSeconds = 60,
                CorrelationKey = "cardId"
            },
            CancellationToken.None);

        ActionResult<IReadOnlyList<CepConfigDto>> list = await controller.List(CancellationToken.None);

        CepConfigDto saved = Assert.IsType<CepConfigDto>(Assert.IsType<OkObjectResult>(save.Result).Value);
        IReadOnlyList<CepConfigDto> items =
            Assert.IsAssignableFrom<IReadOnlyList<CepConfigDto>>(Assert.IsType<OkObjectResult>(list.Result).Value);

        Assert.Equal("fraud", saved.Id);
        Assert.Single(items);
        Assert.Equal("tenant-a", items[0].TenantId);
    }

    [Fact]
    public async Task Save_ReturnsBadRequest_ForInvalidWindowType()
    {
        CepController controller = CreateController("tenant-a");

        ActionResult<CepConfigDto> result = await controller.Save(
            "fraud",
            new CepConfigDto
            {
                Name = "Fraud",
                WindowType = "Unknown",
                WindowSizeSeconds = 30,
                TimeToLiveSeconds = 60
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Simulate_ReturnsWindowCounts()
    {
        CepController controller = CreateController("tenant-a");
        await controller.Save(
            "fraud",
            new CepConfigDto
            {
                Name = "Fraud",
                WindowType = "Sliding",
                WindowSizeSeconds = 30,
                TimeToLiveSeconds = 60,
                CorrelationKey = "cardId"
            },
            CancellationToken.None);

        ActionResult<CepSimulationResponse> result = await controller.Simulate(
            "fraud",
            new CepSimulationRequest
            {
                Events =
                [
                    new CepSimulationEvent
                    {
                        TimestampUtc = new DateTime(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc),
                        Payload = new Dictionary<string, object?> { ["cardId"] = "card-01", ["amount"] = 120m }
                    },
                    new CepSimulationEvent
                    {
                        TimestampUtc = new DateTime(2026, 3, 9, 10, 0, 10, DateTimeKind.Utc),
                        Payload = new Dictionary<string, object?> { ["cardId"] = "card-01", ["amount"] = 140m }
                    }
                ]
            },
            CancellationToken.None);

        CepSimulationResponse response = Assert.IsType<CepSimulationResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

        Assert.Equal(2, response.ProcessedEvents);
        Assert.Equal(1, response.Windows[0].Count);
        Assert.Equal(2, response.Windows[1].Count);
        Assert.All(response.Windows, item => Assert.Equal("card-01", item.Key));
    }

    [Fact]
    public async Task Delete_RemovesConfig()
    {
        CepController controller = CreateController("tenant-a");
        await controller.Save(
            "fraud",
            new CepConfigDto
            {
                Name = "Fraud",
                WindowType = "Sliding",
                WindowSizeSeconds = 30,
                TimeToLiveSeconds = 60
            },
            CancellationToken.None);

        IActionResult delete = await controller.Delete("fraud", CancellationToken.None);
        ActionResult<CepConfigDto> get = await controller.Get("fraud", CancellationToken.None);

        Assert.IsType<NoContentResult>(delete);
        Assert.IsType<NotFoundResult>(get.Result);
    }

    private static CepController CreateController(string tenantId)
    {
        SystemExecutionContextAccessor accessor = new();
        accessor.Set(new SystemExecutionContext(tenantId, null, null, "corr", null, null, false, [], "test"));
        InMemoryCepConfigRepository repository = new(
            new StubDateTimeService(new DateTime(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc)),
            accessor);
        return new CepController(repository);
    }
}
