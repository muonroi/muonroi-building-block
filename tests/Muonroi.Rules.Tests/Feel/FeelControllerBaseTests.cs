using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Rules.Controllers;

namespace Muonroi.Rules.Tests.Feel;

public sealed class FeelControllerBaseTests
{
    [Fact]
    public void Evaluate_WithEmptyExpression_ReturnsBadRequest()
    {
        TestFeelController controller = CreateController();

        IActionResult result = controller.Evaluate(new FeelEvaluateRequest());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void Evaluate_WithContext_ReturnsSuccessfulResult()
    {
        TestFeelController controller = CreateController();

        IActionResult result = controller.Evaluate(new FeelEvaluateRequest
        {
            Expression = "amount > 10",
            Context = new Dictionary<string, JsonElement>
            {
                ["amount"] = JsonSerializer.SerializeToElement(12)
            }
        });

        JsonElement payload = ToJsonElement(result.Should().BeOfType<OkObjectResult>().Subject.Value);
        payload.GetProperty("success").GetBoolean().Should().BeTrue();
        payload.GetProperty("expression").GetString().Should().Be("amount > 10");
        payload.GetProperty("result").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public void Autocomplete_ReturnsKeywordAndContextSuggestions()
    {
        TestFeelController controller = CreateController();

        IActionResult result = controller.Autocomplete(new FeelAutocompleteRequest
        {
            PartialExpression = "a",
            DataType = "number",
            Context = new Dictionary<string, JsonElement>
            {
                ["amount"] = JsonSerializer.SerializeToElement(12)
            }
        });

        JsonElement payload = ToJsonElement(result.Should().BeOfType<OkObjectResult>().Subject.Value);
        payload.GetProperty("token").GetString().Should().Be("a");
        payload.GetProperty("suggestions").EnumerateArray().Select(x => x.GetString()).Should().Contain("amount");
        payload.GetProperty("suggestions").EnumerateArray().Select(x => x.GetString()).Should().Contain("and");
    }

    [Fact]
    public void Examples_ReturnsExampleGroups()
    {
        TestFeelController controller = CreateController();

        IActionResult result = controller.Examples();

        JsonElement payload = ToJsonElement(result.Should().BeOfType<OkObjectResult>().Subject.Value);
        payload.TryGetProperty("numeric", out _).Should().BeTrue();
        payload.TryGetProperty("stringOps", out _).Should().BeTrue();
        payload.TryGetProperty("listAndContext", out _).Should().BeTrue();
    }

    [Fact]
    public void ResolveExecutionContext_FallsBackToHeadersAndClaims()
    {
        ServiceCollection services = new();
        DefaultHttpContext httpContext = new()
        {
            RequestServices = services.BuildServiceProvider(),
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.Empty.ToString()),
                new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", "claim-user")
            ], "Test"))
        };
        httpContext.Request.Headers["X-Username"] = "header-user";
        httpContext.Request.Headers["X-Tenant-Id"] = "tenant-01";
        httpContext.Request.Headers["X-User-Id"] = "2e3af3bd-e557-4453-a88b-4eec03c8cf69";
        httpContext.Request.Headers["X-Actor"] = "system";
        httpContext.Request.Headers["X-Permissions"] = "Rule.Read,Rule.Write";

        TestFeelController controller = CreateController(httpContext);

        MControllerExecutionContext? context = controller.ResolveExecutionContextPublic();

        context.Should().NotBeNull();
        context!.Username.Should().Be("claim-user");
        context.TenantId.Should().Be("tenant-01");
        context.Actor.Should().Be("system");
        context.UserId.Should().Be(Guid.Empty);
        context.Permissions.Should().BeEquivalentTo(["Rule.Read", "Rule.Write"]);
    }

    [Fact]
    public void ResolveExecutionContext_UsesRegisteredResolver_WhenAvailable()
    {
        IMControllerExecutionContextResolver resolver = Substitute.For<IMControllerExecutionContextResolver>();
        MControllerExecutionContext expected = new()
        {
            Username = "resolved-user",
            TenantId = "tenant-02"
        };

        ServiceCollection services = new();
        services.AddSingleton(resolver);
        DefaultHttpContext httpContext = new()
        {
            RequestServices = services.BuildServiceProvider()
        };
        resolver.Resolve(httpContext).Returns(expected);

        TestFeelController controller = CreateController(httpContext);

        MControllerExecutionContext? context = controller.ResolveExecutionContextPublic();

        context.Should().BeSameAs(expected);
    }

    private static TestFeelController CreateController(HttpContext? httpContext = null)
    {
        TestFeelController controller = new();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext ?? new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().BuildServiceProvider()
            }
        };
        return controller;
    }

    private static JsonElement ToJsonElement(object? value)
    {
        return JsonSerializer.SerializeToElement(value, value?.GetType() ?? typeof(object));
    }

    private sealed class TestFeelController : FeelControllerBase
    {
        public MControllerExecutionContext? ResolveExecutionContextPublic()
        {
            return ResolveExecutionContext();
        }
    }
}
