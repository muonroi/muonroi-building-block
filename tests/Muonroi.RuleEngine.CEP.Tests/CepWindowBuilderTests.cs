using Muonroi.RuleEngine.CEP.Builder;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.RuleEngine.CEP.Tests;

public class CepWindowBuilderTests
{
    [Fact]
    public void ConfigBuilder_BuildsNormalizedConfig()
    {
        CepConfig config = CepWindowBuilder
            .Named("High Velocity Cards")
            .ForTenant("tenant-a")
            .Describe("Detects bursts per card.")
            .Sliding(TimeSpan.FromSeconds(30))
            .KeepEventsFor(TimeSpan.FromMinutes(5))
            .CorrelateBy("cardId")
            .WithMetadata("threshold", "3")
            .Build();

        Assert.Equal("high-velocity-cards", config.Id);
        Assert.Equal("tenant-a", config.TenantId);
        Assert.Equal(WindowType.Sliding, config.WindowType);
        Assert.Equal("3", config.Metadata["threshold"]);
    }

    [Fact]
    public void RuntimeBuilder_CreatesWindowFromPersistedConfig()
    {
        CepConfig config = CepWindowBuilder
            .Named("Fraud")
            .Sliding(TimeSpan.FromSeconds(20))
            .KeepEventsFor(TimeSpan.FromSeconds(60))
            .CorrelateBy("cardId")
            .Build("fraud-window");

        CepWindow<PaymentEvent> window = CepWindowBuilder
            .For<PaymentEvent>(config)
            .CorrelateBy(x => x.CardId)
            .Build();

        DateTime t0 = new(2026, 3, 9, 10, 0, 0, DateTimeKind.Utc);
        IReadOnlyList<CepEvent<PaymentEvent>> events = window.Add(new PaymentEvent("card-01", 50m), t0);

        Assert.Single(events);
        Assert.Equal("card-01", events[0].Key);
        Assert.Equal(config.Id, window.Config.Id);
    }

    [Fact]
    public void KeepEventsFor_RejectsTtlSmallerThanWindow()
    {
        CepConfigBuilder builder = CepWindowBuilder
            .Named("Fraud")
            .Sliding(TimeSpan.FromSeconds(30));

        MArgumentException ex = Assert.Throws<MArgumentException>(() => builder.KeepEventsFor(TimeSpan.FromSeconds(10)));

        Assert.Contains("Time to live", ex.Message);
    }

    private sealed record PaymentEvent(string CardId, decimal Amount);
}
