using MassTransit;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Messaging.MassTransit.Messaging;
using Muonroi.RuleEngine.Abstractions;
using Muonroi.Tenancy.Abstractions.Interfaces;
using NSubstitute;
using Xunit;

namespace Muonroi.Messaging.MassTransit.Tests;

public class TenantQuotaMessagingFilterTests
{
    public class TestMessage { }

    [Fact]
    public async Task Send_Throws_When_Quota_Exceeded()
    {
        // Arrange
        var tracker = Substitute.For<ITenantQuotaTracker>();
        var accessor = Substitute.For<ISystemExecutionContextAccessor>();
        var options = Substitute.For<IOptions<MessageBusConfigs>>();

        options.Value.Returns(new MessageBusConfigs { EnableQuotaEnforcement = true });
        
        string tenantId = "tenant-over-limit";
        accessor.Get().TenantId.Returns(tenantId);

        // Mock quota exceeded for minute
        tracker.CheckQuotaAsync(tenantId, QuotaType.MessagesPerMinute, 1).Returns(false);

        var filter = new TenantQuotaMessagingFilter<TestMessage>(tracker, accessor, options);
        var context = Substitute.For<PublishContext<TestMessage>>();
        context.Headers.Returns(Substitute.For<SendHeaders>());
        var next = Substitute.For<IPipe<PublishContext<TestMessage>>>();

        // Act & Assert
        QuotaExceededException ex = await Assert.ThrowsAsync<QuotaExceededException>(() => filter.Send(context, next));
        Assert.Contains("rate limit exceeded", ex.Message);
        
        await next.DidNotReceive().Send(Arg.Any<PublishContext<TestMessage>>());
    }

    [Fact]
    public async Task Send_Increments_Usage_When_Within_Quota()
    {
        // Arrange
        var tracker = Substitute.For<ITenantQuotaTracker>();
        var accessor = Substitute.For<ISystemExecutionContextAccessor>();
        var options = Substitute.For<IOptions<MessageBusConfigs>>();

        options.Value.Returns(new MessageBusConfigs { EnableQuotaEnforcement = true });
        
        string tenantId = "tenant-ok";
        accessor.Get().TenantId.Returns(tenantId);

        tracker.CheckQuotaAsync(tenantId, Arg.Any<QuotaType>(), 1).Returns(true);

        var filter = new TenantQuotaMessagingFilter<TestMessage>(tracker, accessor, options);
        var context = Substitute.For<PublishContext<TestMessage>>();
        context.Headers.Returns(Substitute.For<SendHeaders>());
        var next = Substitute.For<IPipe<PublishContext<TestMessage>>>();

        // Act
        await filter.Send(context, next);

        // Assert
        await tracker.Received(1).IncrementUsageAsync(tenantId, QuotaType.MessagesPerMinute, 1);
        await tracker.Received(1).IncrementUsageAsync(tenantId, QuotaType.MessagesPerDay, 1);
        await next.Received(1).Send(context);
    }
}
