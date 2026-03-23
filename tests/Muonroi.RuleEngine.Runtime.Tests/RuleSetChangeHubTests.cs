using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Muonroi.Core.Abstractions.Constants;
using Muonroi.RuleEngine.Runtime.Web.Hubs;
using NSubstitute;
using System.Security.Claims;
using Xunit;

namespace Muonroi.RuleEngine.Runtime.Tests;

public sealed class RuleSetChangeHubTests
{
    [Fact]
    public void BuildTenantGroup_ShouldNormalizeTenantId()
    {
        RuleSetChangeHub.BuildTenantGroup(" Tenant-A ").Should().Be("tenant:tenant-a");
    }

    [Fact]
    public async Task JoinTenantGroup_ShouldAddConnectionToGroup_WhenTenantMatchesClaim()
    {
        RuleSetChangeHub hub = new()
        {
            Context = CreateContext(
                "conn-1",
                new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimConstants.TenantId, "tenant-a")
                ], "test"))),
            Groups = Substitute.For<IGroupManager>()
        };

        await hub.JoinTenantGroup(" tenant-a ");

        await hub.Groups.Received(1).AddToGroupAsync("conn-1", "tenant:tenant-a", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LeaveTenantGroup_ShouldAllowAdminRole()
    {
        RuleSetChangeHub hub = new()
        {
            Context = CreateContext(
                "conn-2",
                new ClaimsPrincipal(new ClaimsIdentity([], "test", ClaimTypes.Name, ClaimTypes.Role)
                {
                    Label = "admin"
                })),
            Groups = Substitute.For<IGroupManager>()
        };
        hub.Context.User!.AddIdentity(new ClaimsIdentity([new Claim(ClaimTypes.Role, "cp.admin")], "role"));

        await hub.LeaveTenantGroup("Tenant-B");

        await hub.Groups.Received(1).RemoveFromGroupAsync("conn-2", "tenant:tenant-b", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task JoinTenantGroup_ShouldThrow_WhenUserIsUnauthorized()
    {
        RuleSetChangeHub hub = new()
        {
            Context = CreateContext("conn-3", new ClaimsPrincipal(new ClaimsIdentity())),
            Groups = Substitute.For<IGroupManager>()
        };

        Func<Task> act = () => hub.JoinTenantGroup("tenant-a");

        await act.Should().ThrowAsync<HubException>()
            .WithMessage("*Not authorized*");
    }

    private static HubCallerContext CreateContext(string connectionId, ClaimsPrincipal user)
    {
        HubCallerContext context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(connectionId);
        context.User.Returns(user);
        return context;
    }
}
