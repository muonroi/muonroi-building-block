namespace Muonroi.BuildingBlock.Test;

public class RabbitMqBusConfiguratorTests
{
    [Fact]
    public void Configure_Throws_When_Config_Missing()
    {
        RabbitMqBusConfigurator configurator = new();
        MessageBusConfigs cfgs = new()
        {
            BusType = BusType.RabbitMq,
            RabbitMq = null
        };
        Assert.Throws<InvalidDataException>(() =>
            configurator.Configure(Substitute.For<IBusRegistrationConfigurator>(), cfgs));
    }

    [Theory]
    [InlineData("localhost", "/", "u", "p")]
    [InlineData("other", "v", "user", "pass")]
    public void Configure_With_Valid_Config_Does_Not_Throw(string host, string vhost, string user, string pass)
    {
        RabbitMqBusConfigurator configurator = new();
        IBusRegistrationConfigurator bus = Substitute.For<IBusRegistrationConfigurator>();
        RabbitMqConfigs rabbit = new()
        {
            Host = host,
            VirtualHost = vhost,
            Username = user,
            Password = pass
        };
        MessageBusConfigs cfgs = new()
        {
            RabbitMq = rabbit
        };
        Exception ex = Record.Exception(() => configurator.Configure(bus, cfgs));
        Assert.Null(ex);
    }
}

public class RabbitMqConfigsTests
{
    [Fact]
    public void Host_Getter_Returns_Value_Or_Empty()
    {
        RabbitMqConfigs cfg = new()
        {
            Host = "h"
        };
        Assert.Equal("h", cfg.Host);
        cfg.Host = null!;
        Assert.Null(cfg.Host);
    }

    [Fact]
    public void VirtualHost_Getter_Returns_Value_Or_Default()
    {
        RabbitMqConfigs cfg = new();
        Assert.Equal("/", cfg.VirtualHost);
        cfg.VirtualHost = null!;
        Assert.Null(cfg.VirtualHost);
    }

    [Fact]
    public void Username_Getter_Returns_Value_Or_Null()
    {
        RabbitMqConfigs cfg = new()
        {
            Username = "u"
        };
        Assert.Equal("u", cfg.Username);
        cfg.Username = null!;
        Assert.Null(cfg.Username);
    }

    [Fact]
    public void Password_Getter_Returns_Value_Or_Null()
    {
        RabbitMqConfigs cfg = new()
        {
            Password = "p"
        };
        Assert.Equal("p", cfg.Password);
        cfg.Password = null!;
        Assert.Null(cfg.Password);
    }

    [Fact]
    public void MessageBusRuntimeConfigs_Has_Safe_Defaults()
    {
        MessageBusRuntimeConfigs cfg = new();

        Assert.True(cfg.RetryCount > 0);
        Assert.True(cfg.RetryIntervalMs > 0);
        Assert.True(cfg.PrefetchCount > 0);
        Assert.True(cfg.ConcurrentMessageLimit > 0);
        Assert.True(cfg.EnableInMemoryOutbox);
    }


    [Fact]
    public void Configure_Sets_EndpointNameFormatter()
    {
        RabbitMqBusConfigurator configurator = new();
        MessageBusConfigs messageBusConfigs = new()
        {
            RabbitMq = new RabbitMqConfigs
            {
                Host = "localhost"
            }
        };
        IBusRegistrationConfigurator busRegistrationConfigurator = Substitute.For<IBusRegistrationConfigurator>();

        configurator.Configure(busRegistrationConfigurator, messageBusConfigs);

        busRegistrationConfigurator.Received(1).SetKebabCaseEndpointNameFormatter();
    }
}
