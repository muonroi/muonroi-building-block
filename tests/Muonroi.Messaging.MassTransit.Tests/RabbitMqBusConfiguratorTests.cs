namespace Muonroi.Messaging.MassTransit.Tests;

public class RabbitMqBusConfiguratorTests
{
    [Fact]
    public void Configure_Throws_When_RabbitMq_Config_Missing()
    {
        RabbitMqBusConfigurator configurator = new();
        MessageBusConfigs configs = new()
        {
            RabbitMq = null
        };

        MInternalException exception = Assert.Throws<MInternalException>(() => configurator.Configure(Substitute.For<IBusRegistrationConfigurator>(), configs));
        Assert.Equal("RabbitMQ configuration missing", exception.Message);
    }

    [Fact]
    public void Configure_With_Valid_Config_And_No_Prefix_Does_Not_Throw()
    {
        RabbitMqBusConfigurator configurator = new();
        IBusRegistrationConfigurator busRegistrationConfigurator = Substitute.For<IBusRegistrationConfigurator>();

        MessageBusConfigs configs = new()
        {
            RabbitMq = new RabbitMqConfigs
            {
                Host = "localhost",
                VirtualHost = "/",
                Username = "guest",
                Password = "guest"
            },
            Runtime = new MessageBusRuntimeConfigs
            {
                EndpointPrefix = string.Empty
            }
        };

        Exception? ex = Record.Exception(() => configurator.Configure(busRegistrationConfigurator, configs));

        Assert.Null(ex);
    }

    [Fact]
    public void Configure_With_EndpointPrefix_Does_Not_Throw()
    {
        RabbitMqBusConfigurator configurator = new();
        IBusRegistrationConfigurator busRegistrationConfigurator = Substitute.For<IBusRegistrationConfigurator>();

        MessageBusConfigs configs = new()
        {
            RabbitMq = new RabbitMqConfigs
            {
                Host = "localhost",
                VirtualHost = "/",
                Username = "guest",
                Password = "guest"
            },
            Runtime = new MessageBusRuntimeConfigs
            {
                EndpointPrefix = "core"
            }
        };

        Exception? ex = Record.Exception(() => configurator.Configure(busRegistrationConfigurator, configs));

        Assert.Null(ex);
    }
}

public class RabbitMqConfigsTests
{
    [Fact]
    public void RabbitMqConfigs_Has_Expected_Defaults()
    {
        RabbitMqConfigs configs = new();

        Assert.Equal(string.Empty, configs.Host);
        Assert.Equal("/", configs.VirtualHost);
        Assert.Equal(string.Empty, configs.Username);
        Assert.Equal(string.Empty, configs.Password);
        Assert.Equal(5672, configs.Port);
        Assert.False(configs.UseSsl);
        Assert.Equal(string.Empty, configs.SslServerName);
        Assert.Equal(30, configs.HeartbeatSeconds);
        Assert.True(configs.PublisherConfirmation);
    }

    [Fact]
    public void MessageBusRuntimeConfigs_Has_Expected_Defaults()
    {
        MessageBusRuntimeConfigs configs = new();

        Assert.Equal(3, configs.RetryCount);
        Assert.Equal(500, configs.RetryIntervalMs);
        Assert.Equal(32, configs.PrefetchCount);
        Assert.Equal(16, configs.ConcurrentMessageLimit);
        Assert.True(configs.EnableInMemoryOutbox);
        Assert.Equal(string.Empty, configs.EndpointPrefix);
    }
}
