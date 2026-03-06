using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Muonroi.AspNetCore.Filters;

namespace Muonroi.BuildingBlock.Test.External.Controller.ActionFilters;

public class FeatureFlagFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_DisabledFeature_Sets404AndSkipsNext()
    {
        // Arrange
        const string featureName = "SomeFeature";
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"Features:{featureName}"] = "false"
            })
            .Build();

        FeatureFlagFilter filter = new(featureName, configuration);
        (ActionExecutingContext context, Func<bool> wasNextCalled, ActionExecutionDelegate nextDelegate) = CreateContext();

        // Act
        await filter.OnActionExecutionAsync(context, nextDelegate);

        // Assert
        StatusCodeResult statusResult = Assert.IsType<StatusCodeResult>(context.Result);
        Assert.Equal(StatusCodes.Status404NotFound, statusResult.StatusCode);
        Assert.False(wasNextCalled());
    }

    [Fact]
    public async Task OnActionExecutionAsync_EnabledFeature_InvokesNext()
    {
        // Arrange
        const string featureName = "EnabledFeature";
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"Features:{featureName}"] = "true"
            })
            .Build();

        FeatureFlagFilter filter = new(featureName, configuration);
        (ActionExecutingContext context, Func<bool> wasNextCalled, ActionExecutionDelegate nextDelegate) = CreateContext();

        // Act
        await filter.OnActionExecutionAsync(context, nextDelegate);

        // Assert
        Assert.Null(context.Result);
        Assert.True(wasNextCalled());
    }

    [Fact]
    public void FeatureFlagAttribute_ConfiguresFilterWithFeatureArgument()
    {
        const string featureName = "TargetFeature";

        FeatureFlagAttribute attribute = new(featureName);

        Assert.Equal(typeof(FeatureFlagFilter), attribute.ImplementationType);
        object argument = Assert.Single(attribute.Arguments!);
        Assert.Equal(featureName, argument);
    }

    private static (ActionExecutingContext Context, Func<bool> WasNextCalled, ActionExecutionDelegate Next)
        CreateContext()
    {
        DefaultHttpContext httpContext = new();
        ActionContext actionContext = new(httpContext, new RouteData(), new ActionDescriptor());
        List<IFilterMetadata> filters = [];
        Dictionary<string, object?> actionArguments = [];
        object controller = new();
        bool nextCalled = false;
        ActionExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(
                new ActionExecutedContext(actionContext, filters, controller));
        };

        ActionExecutingContext executingContext = new(actionContext, filters, actionArguments, controller);
        return (executingContext, () => nextCalled, next);
    }
}
