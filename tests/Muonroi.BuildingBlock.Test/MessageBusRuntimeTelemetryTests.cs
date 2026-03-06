namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class MessageBusRuntimeTelemetryTests
{
    public sealed record TestMessage(string Value);

    [Fact]
    public void ResolveTransport_ReturnsExpectedSystem()
    {
        Assert.Equal("rabbitmq", MessageBusRuntimeTelemetry.ResolveTransport(new Uri("rabbitmq://localhost/x")));
        Assert.Equal("kafka", MessageBusRuntimeTelemetry.ResolveTransport(new Uri("kafka://localhost/topic")));
        Assert.Equal("https", MessageBusRuntimeTelemetry.ResolveTransport(new Uri("https://localhost/api")));
        Assert.Equal("unknown", MessageBusRuntimeTelemetry.ResolveTransport(null));
    }

    [Fact]
    public async Task PublishFilter_EmitsActivity_WithTenantTag()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-publish";

        try
        {
            List<Activity> stopped = [];
            using ActivityListener listener = new();
            listener.ShouldListenTo = source => source.Name == MessageBusRuntimeTelemetry.ActivitySourceName;
            listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
            listener.ActivityStopped = stopped.Add;
            ActivitySource.AddActivityListener(listener);

            EcsPublishLoggingFilter<TestMessage> filter = new(CreateMessageBusLicensedState());
            PublishContext<TestMessage> context = Substitute.For<PublishContext<TestMessage>>();
            context.DestinationAddress.Returns(new Uri("rabbitmq://localhost/exchange/test"));
            context.Headers.Returns(new DictionarySendHeaders());
            IPipe<PublishContext<TestMessage>> next = Substitute.For<IPipe<PublishContext<TestMessage>>>();
            next.Send(context).Returns(Task.CompletedTask);

            await filter.Send(context, next);

            Activity? publishActivity = stopped.LastOrDefault(activity =>
                string.Equals(activity.OperationName, "messagebus.publish", StringComparison.Ordinal));

            Assert.NotNull(publishActivity);
            Assert.Equal("publish", publishActivity!.GetTagItem("messaging.operation"));
            Assert.Equal("tenant-publish", publishActivity.GetTagItem("tenant.id"));
            Assert.Equal("rabbitmq", publishActivity.GetTagItem("messaging.system"));
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task SendFilter_EmitsActivity_WithTenantTag()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-send";

        try
        {
            List<Activity> stopped = [];
            using ActivityListener listener = new();
            listener.ShouldListenTo = source => source.Name == MessageBusRuntimeTelemetry.ActivitySourceName;
            listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
            listener.ActivityStopped = stopped.Add;
            ActivitySource.AddActivityListener(listener);

            EcsSendLoggingFilter<TestMessage> filter = new(CreateMessageBusLicensedState());
            SendContext<TestMessage> context = Substitute.For<SendContext<TestMessage>>();
            context.DestinationAddress.Returns(new Uri("rabbitmq://localhost/queue/test"));
            context.Headers.Returns(new DictionarySendHeaders());
            IPipe<SendContext<TestMessage>> next = Substitute.For<IPipe<SendContext<TestMessage>>>();
            next.Send(context).Returns(Task.CompletedTask);

            await filter.Send(context, next);

            Activity? sendActivity = stopped.LastOrDefault(activity =>
                string.Equals(activity.OperationName, "messagebus.send", StringComparison.Ordinal));

            Assert.NotNull(sendActivity);
            Assert.Equal("send", sendActivity!.GetTagItem("messaging.operation"));
            Assert.Equal("tenant-send", sendActivity.GetTagItem("tenant.id"));
            Assert.Equal("rabbitmq", sendActivity.GetTagItem("messaging.system"));
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task ConsumeFilter_Uses_InputAddress_ForTransport_When_DestinationMissing()
    {
        List<Activity> stopped = [];
        using ActivityListener listener = new();
        listener.ShouldListenTo = source => source.Name == MessageBusRuntimeTelemetry.ActivitySourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = stopped.Add;
        ActivitySource.AddActivityListener(listener);

        EcsConsumeLoggingFilter<TestMessage> filter = new(CreateMessageBusLicensedState());
        ConsumeContext<TestMessage> context = Substitute.For<ConsumeContext<TestMessage>>();
        context.DestinationAddress.Returns((Uri?)null);
        ReceiveContext receiveContext = Substitute.For<ReceiveContext>();
        receiveContext.InputAddress.Returns(new Uri("kafka://localhost/topic-a"));
        context.ReceiveContext.Returns(receiveContext);
        Headers headers = Substitute.For<Headers>();
        headers
            .TryGetHeader(CustomHeader.TenantId, out Arg.Any<object?>())
            .Returns(callInfo =>
            {
                callInfo[1] = "tenant-consume";
                return true;
            });
        context.Headers.Returns(headers);

        IPipe<ConsumeContext<TestMessage>> next = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();
        next.Send(context).Returns(Task.CompletedTask);

        await filter.Send(context, next);

        Activity? consumeActivity = stopped.LastOrDefault(activity =>
            string.Equals(activity.OperationName, "messagebus.consume", StringComparison.Ordinal));

        Assert.NotNull(consumeActivity);
        Assert.Equal("consume", consumeActivity!.GetTagItem("messaging.operation"));
        Assert.Equal("tenant-consume", consumeActivity.GetTagItem("tenant.id"));
        Assert.Equal("kafka", consumeActivity.GetTagItem("messaging.system"));
    }

    private static LicenseState CreateMessageBusLicensedState()
    {
        return new LicenseState
        {
            IsValid = true,
            Tier = LicenseTier.Licensed,
            Features = [FreeTierFeatures.Premium.MessageBus]
        };
    }
}
