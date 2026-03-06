namespace Muonroi.BuildingBlock.Test;

public class MessageBusTenantIsolationTests
{
    public sealed record MessageEnvelope(string Value);
    private static readonly LicenseState MessageBusLicensed = new()
    {
        IsValid = true,
        Tier = LicenseTier.Licensed,
        Features = [FreeTierFeatures.Premium.MessageBus]
    };

    [Fact]
    public async Task TenantContextConsumeFilter_SetsTenantForMessage_AndClearsAfterSuccess()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "seed-tenant";

        try
        {
            TenantContextConsumeFilter<MessageEnvelope> filter = new();
            ConsumeContext<MessageEnvelope> context = Substitute.For<ConsumeContext<MessageEnvelope>>();
            Headers headers = Substitute.For<Headers>();
            headers
                .TryGetHeader(CustomHeader.TenantId, out Arg.Any<object?>())
                .Returns(callInfo =>
                {
                    callInfo[1] = "tenant-a";
                    return true;
                });
            context.Headers.Returns(headers);

            string? tenantInsidePipeline = null;
            IPipe<ConsumeContext<MessageEnvelope>> next = Substitute.For<IPipe<ConsumeContext<MessageEnvelope>>>();
            next.Send(context).Returns(_ =>
            {
                tenantInsidePipeline = TenantContext.CurrentTenantId;
                return Task.CompletedTask;
            });

            await filter.Send(context, next);

            Assert.Equal("tenant-a", tenantInsidePipeline);
            Assert.Equal("seed-tenant", TenantContext.CurrentTenantId);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task TenantContextConsumeFilter_ClearsTenantAfterException()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "seed-tenant";

        try
        {
            TenantContextConsumeFilter<MessageEnvelope> filter = new();
            ConsumeContext<MessageEnvelope> context = Substitute.For<ConsumeContext<MessageEnvelope>>();
            Headers headers = Substitute.For<Headers>();
            headers
                .TryGetHeader(CustomHeader.TenantId, out Arg.Any<object?>())
                .Returns(callInfo =>
                {
                    callInfo[1] = "tenant-b";
                    return true;
                });
            context.Headers.Returns(headers);

            IPipe<ConsumeContext<MessageEnvelope>> next = Substitute.For<IPipe<ConsumeContext<MessageEnvelope>>>();
            next.Send(context).Returns(_ => throw new InvalidOperationException("boom"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => filter.Send(context, next));
            Assert.Equal("seed-tenant", TenantContext.CurrentTenantId);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task EcsPublishLoggingFilter_PropagatesTenantHeader_FromRuntimeContext()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-pub";
        try
        {
            EcsPublishLoggingFilter<MessageEnvelope> filter = new(MessageBusLicensed);
            PublishContext<MessageEnvelope> context = Substitute.For<PublishContext<MessageEnvelope>>();
            DictionarySendHeaders headers = new();
            context.Headers.Returns(headers);
            IPipe<PublishContext<MessageEnvelope>> next = Substitute.For<IPipe<PublishContext<MessageEnvelope>>>();
            next.Send(context).Returns(Task.CompletedTask);

            await filter.Send(context, next);

            Assert.True(headers.TryGetHeader(CustomHeader.TenantId, out object? tenantHeader));
            Assert.Equal("tenant-pub", tenantHeader);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task EcsSendLoggingFilter_PropagatesTenantHeader_FromRuntimeContext()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-send";
        try
        {
            EcsSendLoggingFilter<MessageEnvelope> filter = new(MessageBusLicensed);
            SendContext<MessageEnvelope> context = Substitute.For<SendContext<MessageEnvelope>>();
            DictionarySendHeaders headers = new();
            context.Headers.Returns(headers);
            IPipe<SendContext<MessageEnvelope>> next = Substitute.For<IPipe<SendContext<MessageEnvelope>>>();
            next.Send(context).Returns(Task.CompletedTask);

            await filter.Send(context, next);

            Assert.True(headers.TryGetHeader(CustomHeader.TenantId, out object? tenantHeader));
            Assert.Equal("tenant-send", tenantHeader);
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }
}
