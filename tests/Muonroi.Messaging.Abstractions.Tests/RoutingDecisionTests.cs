using Muonroi.Messaging.Abstractions.Contracts;

namespace Muonroi.Messaging.Abstractions.Tests;

/// <summary>
/// Verifies the routing decision contracts introduced for Track 8.
/// </summary>
public class RoutingDecisionTests
{
    /// <summary>
    /// Ensures that the pass-through decision does not redirect or reject.
    /// </summary>
    [Fact]
    public void PassThrough_Has_No_Target_And_Does_Not_Reject()
    {
        IRoutingDecision decision = RoutingDecision.PassThrough;

        decision.TargetAddress.Should().BeNull();
        decision.Reject.Should().BeFalse();
        decision.Reason.Should().BeNull();
    }

    /// <summary>
    /// Ensures that redirect decisions carry the configured target address.
    /// </summary>
    [Fact]
    public void RedirectTo_Sets_Target_Address()
    {
        IRoutingDecision decision = RoutingDecision.RedirectTo("rabbitmq://host/orders", "tenant override");

        decision.TargetAddress.Should().Be("rabbitmq://host/orders");
        decision.Reject.Should().BeFalse();
        decision.Reason.Should().Be("tenant override");
    }

    [Fact]
    public void RedirectTo_Without_Reason_Should_Leave_Reason_Null()
    {
        IRoutingDecision decision = RoutingDecision.RedirectTo("queue://alt");

        decision.TargetAddress.Should().Be("queue://alt");
        decision.Reason.Should().BeNull();
    }

    /// <summary>
    /// Ensures that dead-letter decisions set the reject flag.
    /// </summary>
    [Fact]
    public void DeadLetter_Sets_Reject_Flag()
    {
        IRoutingDecision decision = RoutingDecision.DeadLetter("policy rejected");

        decision.TargetAddress.Should().BeNull();
        decision.Reject.Should().BeTrue();
        decision.Reason.Should().Be("policy rejected");
    }

    [Fact]
    public void RoutingDecision_Record_Equality_Works()
    {
        RoutingDecision a = new(TargetAddress: "x");
        RoutingDecision b = new(TargetAddress: "x");

        a.Should().Be(b);
    }

    [Fact]
    public void RoutingDecision_Record_Inequality_Works()
    {
        RoutingDecision a = new(TargetAddress: "x");
        RoutingDecision b = new(TargetAddress: "y");

        a.Should().NotBe(b);
    }

    /// <summary>
    /// Ensures that the router contract exposes the expected members.
    /// </summary>
    [Fact]
    public async Task IMessageRouter_Contract_Exposes_Order_Code_And_RouteAsync()
    {
        SampleRouter router = new();
        SampleRoutingContext context = new();

        router.Order.Should().Be(10);
        router.Code.Should().Be("SAMPLE");

        IRoutingDecision decision = await router.RouteAsync(new SampleMessage(), context);

        decision.Should().BeSameAs(RoutingDecision.PassThrough);
    }

    /// <summary>
    /// Sample message used for contract verification.
    /// </summary>
    private sealed class SampleMessage;

    /// <summary>
    /// Sample router used for contract verification.
    /// </summary>
    private sealed class SampleRouter : IMessageRouter<SampleMessage>
    {
        /// <inheritdoc />
        public int Order => 10;

        /// <inheritdoc />
        public string Code => "SAMPLE";

        /// <inheritdoc />
        public Task<IRoutingDecision> RouteAsync(SampleMessage message, IRoutingContext context, CancellationToken ct = default)
        {
            return Task.FromResult(RoutingDecision.PassThrough);
        }
    }

    /// <summary>
    /// Sample routing context used for contract verification.
    /// </summary>
    private sealed class SampleRoutingContext : IRoutingContext
    {
        /// <inheritdoc />
        public string TenantId => "tenant-a";

        /// <inheritdoc />
        public string CorrelationId => "corr-1";

        /// <inheritdoc />
        public string MessageType => typeof(SampleMessage).FullName ?? typeof(SampleMessage).Name;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> Headers { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
