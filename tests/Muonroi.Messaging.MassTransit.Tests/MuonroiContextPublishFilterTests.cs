using MassTransit;
using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Models.Common;
using Muonroi.Messaging.MassTransit.Messaging;
using NSubstitute;
using Xunit;
using Headers = MassTransit.Headers;

namespace Muonroi.Messaging.MassTransit.Tests;

public class MuonroiContextPublishFilterTests
{
    public class TestMessage { }

    [Fact]
    public async Task Send_SetsHeaders_And_MasksToken_When_Configured()
    {
        // Arrange
        ISystemExecutionContextAccessor accessor = Substitute.For<ISystemExecutionContextAccessor>();
        ITenantContextPolicy policy = Substitute.For<ITenantContextPolicy>();
        IOptions<MessageBusConfigs> options = Substitute.For<IOptions<MessageBusConfigs>>();

        options.Value.Returns(new MessageBusConfigs { MaskAccessTokenInHeaders = true });

        SystemExecutionContext rawContext = new(
            tenantId: "tenant-1",
            userId: "user-1",
            username: "admin",
            correlationId: "corr-1",
            accessToken: "secret-token",
            apiKey: null,
            isAuthenticated: true,
            permissions: [],
            sourceType: "test-source");

        accessor.Get().Returns(rawContext);
        policy.ResolveAndValidate(Arg.Any<ISystemExecutionContext>()).Returns(rawContext);

        MuonroiContextPublishFilter<TestMessage> filter = new(accessor, policy, options);

        PublishContext<TestMessage> context = Substitute.For<PublishContext<TestMessage>>();
        IPipe<PublishContext<TestMessage>> next = Substitute.For<IPipe<PublishContext<TestMessage>>>();
        
        Headers headers = Substitute.For<Headers>();
        context.Headers.Returns(Substitute.For<SendHeaders>());

        // Act
        await filter.Send(context, next);

        // Assert
        context.Headers.Received().Set(CustomHeader.TenantId, "tenant-1");
        context.Headers.Received().Set(ClaimConstants.UserIdentifier, "user-1");
        context.Headers.Received().Set(CustomHeader.CorrelationId, "corr-1");
        
        // Token must NOT be set directly
        context.Headers.DidNotReceive().Set(ClaimConstants.AccessToken, "secret-token");
        
        // Signature MUST be set
        context.Headers.Received().Set(Arg.Is<string>(s => s == "X-Muonroi-Identity-Sig"), Arg.Any<string>());

        await next.Received(1).Send(context);
    }
}
