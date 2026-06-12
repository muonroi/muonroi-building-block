using MassTransit;

namespace Muonroi.BuildingBlock.Test;

public class KafkaIntegrationTests
{
    [Fact]
    public void Configure_Throws_When_Kafka_Config_Missing()
    {
        KafkaBusConfigurator configurator = new();
        MessageBusConfigs configs = new()
        {
            BusType = BusType.Kafka,
            Kafka = null
        };
        Assert.Throws<InvalidDataException>(() => configurator.Configure(null!, configs));
    }

    [Fact]
    public void AddMessageBus_Registers_KafkaRider()
    {
        Dictionary<string, string?> configValues = new()
        {
            [$"{MessageBusConfigs.SectionName}:BusType"] = "Kafka",
            [$"{MessageBusConfigs.SectionName}:Kafka:Host"] = "localhost:9092",
            [$"{MessageBusConfigs.SectionName}:Kafka:Topic"] = "topic",
            [$"{MessageBusConfigs.SectionName}:Kafka:GroupId"] = "group"
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        ServiceCollection services = [];

        services.AddMessageBus(configuration);

        bool hasKafkaRider = services.Any(sd => sd.ServiceType == typeof(IKafkaRider));
        Assert.True(hasKafkaRider);
    }

    [Fact]
    public void AddMessageBus_Registers_Bus_Control()
    {
        Dictionary<string, string?> configValues = new()
        {
            [$"{MessageBusConfigs.SectionName}:BusType"] = "Kafka",
            [$"{MessageBusConfigs.SectionName}:Kafka:Host"] = "localhost:9092",
            [$"{MessageBusConfigs.SectionName}:Kafka:Topic"] = "topic",
            [$"{MessageBusConfigs.SectionName}:Kafka:GroupId"] = "group"
        };

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        ServiceCollection services = [];

        services.AddMessageBus(configuration);

        bool hasBusControl = services.Any(descriptor => descriptor.ServiceType == typeof(IBusControl));

        Assert.True(hasBusControl);
    }

    [Fact]
    public void KafkaConfigs_Has_Defaults()
    {
        KafkaConfigs cfg = new();

        Assert.Equal("consumer-group", cfg.GroupId);
        Assert.Equal("Muonroi.Messaging", cfg.ClientId);
        Assert.True(cfg.EnableAutoCommit);
    }
}
