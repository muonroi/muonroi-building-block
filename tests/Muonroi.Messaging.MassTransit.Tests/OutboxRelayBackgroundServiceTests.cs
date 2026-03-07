using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Logging.Abstractions;
using Muonroi.Messaging.Abstractions.Events;
using Muonroi.Messaging.MassTransit.Messaging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Muonroi.Messaging.MassTransit.Tests;

public class OutboxRelayBackgroundServiceTests
{
    public class SampleEvent
    {
        public string Data { get; set; } = string.Empty;
    }

    [Fact]
    public async Task RelayPendingAsync_Publishes_Pending_Events()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();
        IEventOutboxStore store = Substitute.For<IEventOutboxStore>();
        IMJsonSerializeService jsonService = Substitute.For<IMJsonSerializeService>();
        IPublishEndpoint publishEndpoint = Substitute.For<IPublishEndpoint>();

        services.AddSingleton(store);
        services.AddSingleton(jsonService);
        services.AddSingleton(publishEndpoint);
        IServiceProvider provider = services.BuildServiceProvider();

        IOptions<MessageBusConfigs> options = Substitute.For<IOptions<MessageBusConfigs>>();
        options.Value.Returns(new MessageBusConfigs());

        OutboxRelayBackgroundService service = new(provider, options, Substitute.For<IMLog<OutboxRelayBackgroundService>>());

        SampleEvent evt = new() { Data = "test" };
        EventOutbox outbox1 = new()
        {
            Id = 1,
            Status = EventOutboxStatus.Pending,
            EventType = typeof(SampleEvent).AssemblyQualifiedName,
            EventContent = "{\"Data\":\"test\"}"
        };

        store.EventOutboxes.Returns(new[] { outbox1 }.AsQueryable());
        jsonService.Deserialize<SampleEvent>(outbox1.EventContent).Returns(evt);

        // NSubstitute with Reflection invoke can be tricky, so we test if the status is updated.
        // IPublishEndpoint.Publish<T> will be invoked. 
        // Act
        await service.RelayPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(EventOutboxStatus.Published, outbox1.Status);
        await store.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RelayPendingAsync_Marks_Failed_When_Type_Missing()
    {
        // Arrange
        IServiceCollection services = new ServiceCollection();
        IEventOutboxStore store = Substitute.For<IEventOutboxStore>();
        IMJsonSerializeService jsonService = Substitute.For<IMJsonSerializeService>();
        IPublishEndpoint publishEndpoint = Substitute.For<IPublishEndpoint>();

        services.AddSingleton(store);
        services.AddSingleton(jsonService);
        services.AddSingleton(publishEndpoint);
        IServiceProvider provider = services.BuildServiceProvider();

        IOptions<MessageBusConfigs> options = Substitute.For<IOptions<MessageBusConfigs>>();
        options.Value.Returns(new MessageBusConfigs());

        OutboxRelayBackgroundService service = new(provider, options, Substitute.For<IMLog<OutboxRelayBackgroundService>>());

        EventOutbox outbox1 = new()
        {
            Id = 1,
            Status = EventOutboxStatus.Pending,
            EventType = "UnknownNamespace.UnknownType, UnknownAssembly",
            EventContent = "{}"
        };

        store.EventOutboxes.Returns(new[] { outbox1 }.AsQueryable());

        // Act
        await service.RelayPendingAsync(CancellationToken.None);

        // Assert
        Assert.Equal(EventOutboxStatus.Failed, outbox1.Status);
        Assert.Contains("Cannot resolve type", outbox1.ErrorMessage);
        await store.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
