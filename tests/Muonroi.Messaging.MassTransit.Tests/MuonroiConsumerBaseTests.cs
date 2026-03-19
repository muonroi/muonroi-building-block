using MassTransit;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Governance.License;
using Muonroi.Logging.Abstractions;
using Muonroi.Messaging.MassTransit.Messaging;
using NSubstitute;
using Xunit;

namespace Muonroi.Messaging.MassTransit.Tests;

public class MuonroiConsumerBaseTests
{
    public class TestMessage { }

    private class TestConsumer(
        ISystemExecutionContextAccessor contextAccessor,
        IMLog<TestMessage> log,
        ILicenseGuard? licenseGuard = null,
        bool throwError = false)
        : MuonroiConsumerBase<TestMessage>(contextAccessor, log, licenseGuard)
    {
        public bool HandlerCalled { get; private set; }
        public ISystemExecutionContext? PassedContext { get; private set; }

        protected override Task HandleAsync(ConsumeContext<TestMessage> context, ISystemExecutionContext executionContext, CancellationToken cancellationToken)
        {
            HandlerCalled = true;
            PassedContext = executionContext;
            if (throwError)
            {
                throw new InvalidOperationException("Business error");
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Consume_Calls_HandleAsync_With_Context()
    {
        // Arrange
        ISystemExecutionContextAccessor accessor = Substitute.For<ISystemExecutionContextAccessor>();
        ISystemExecutionContext mockContext = Substitute.For<ISystemExecutionContext>();
        accessor.Get().Returns(mockContext);

        TestConsumer consumer = new(accessor, Substitute.For<IMLog<TestMessage>>());
        ConsumeContext<TestMessage> consumeContext = Substitute.For<ConsumeContext<TestMessage>>();

        // Act
        await consumer.Consume(consumeContext);

        // Assert
        Assert.True(consumer.HandlerCalled);
        Assert.Same(mockContext, consumer.PassedContext);
    }

    [Fact]
    public async Task Consume_Enforces_LicenseGuard()
    {
        // Arrange
        ISystemExecutionContextAccessor accessor = Substitute.For<ISystemExecutionContextAccessor>();
        ILicenseGuard guard = Substitute.For<ILicenseGuard>();

        TestConsumer consumer = new(accessor, Substitute.For<IMLog<TestMessage>>(), guard);
        ConsumeContext<TestMessage> consumeContext = Substitute.For<ConsumeContext<TestMessage>>();

        // Act
        await consumer.Consume(consumeContext);

        // Assert
        guard.Received(1).EnsureFeature(FreeTierFeatures.Premium.MessageBus);
    }

    [Fact]
    public async Task Consume_When_HandlerThrows_LogsAndRethrows()
    {
        // Arrange
        ISystemExecutionContextAccessor accessor = Substitute.For<ISystemExecutionContextAccessor>();
        ISystemExecutionContext mockContext = Substitute.For<ISystemExecutionContext>();
        accessor.Get().Returns(mockContext);

        TestConsumer consumer = new(accessor, Substitute.For<IMLog<TestMessage>>(), null, throwError: true);
        ConsumeContext<TestMessage> consumeContext = Substitute.For<ConsumeContext<TestMessage>>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => consumer.Consume(consumeContext));
        Assert.True(consumer.HandlerCalled);
    }
}
