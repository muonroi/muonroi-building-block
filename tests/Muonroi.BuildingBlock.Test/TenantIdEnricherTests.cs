namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class TenantIdEnricherTests
{
    [Fact]
    public void Enrich_Adds_TenantId_When_Present()
    {
        TenantContext.CurrentTenantId = "t";
        LogEvent evt = new(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            new MessageTemplateParser().Parse("test"), []);
        TenantIdEnricher enricher = new();
        enricher.Enrich(evt, new DummyPropertyFactory());
        Assert.True(evt.Properties.ContainsKey("TenantId"));
        ScalarValue val = (ScalarValue)evt.Properties["TenantId"];
        Assert.Equal("t", val.Value);
    }


    [Fact]
    public void Enrich_With_Null_Event_Throws()
    {
        TenantIdEnricher enricher = new();
        Assert.ThrowsAny<Exception>(() => enricher.Enrich(null!, new DummyPropertyFactory()));
    }

    [Fact]
    public void Enrich_Without_TenantId_Does_Not_Add()
    {
        TenantContext.CurrentTenantId = string.Empty;
        LogEvent evt = new(DateTimeOffset.UtcNow, LogEventLevel.Information, null,
            new MessageTemplateParser().Parse("test"), []);
        TenantIdEnricher enricher = new();
        enricher.Enrich(evt, new DummyPropertyFactory());
        Assert.False(evt.Properties.ContainsKey("TenantId"));
    }
}
