namespace Muonroi.AspNetCore.Tests.ActionFilters;

public class FeatureFlagFilterTests
{
    private static ActionExecutingContext CreateActionContext()
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
    }

    [Fact]
    public async Task OnActionExecutionAsync_FeatureEnabled_CallsNext()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Features:NewDashboard"] = "true" })
            .Build();
        var filter = new FeatureFlagFilter("NewDashboard", config);
        bool nextCalled = false;
        var context = CreateActionContext();

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor), [], new object()));
        });

        nextCalled.Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task OnActionExecutionAsync_FeatureDisabled_Returns404()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Features:NewDashboard"] = "false" })
            .Build();
        var filter = new FeatureFlagFilter("NewDashboard", config);
        bool nextCalled = false;
        var context = CreateActionContext();

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return Task.FromResult(new ActionExecutedContext(
                new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor), [], new object()));
        });

        nextCalled.Should().BeFalse();
        context.Result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task OnActionExecutionAsync_FeatureNotConfigured_Returns404()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var filter = new FeatureFlagFilter("MissingFeature", config);
        var context = CreateActionContext();

        await filter.OnActionExecutionAsync(context, () =>
            Task.FromResult(new ActionExecutedContext(
                new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor), [], new object())));

        context.Result.Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}

public class FeatureFlagAttributeTests
{
    [Fact]
    public void Constructor_SetsFilterType()
    {
        var attr = new FeatureFlagAttribute("TestFeature");

        attr.Should().BeAssignableTo<TypeFilterAttribute>();
        attr.Arguments.Should().ContainSingle().Which.Should().Be("TestFeature");
    }
}
