using Muonroi.AspNetCore.Middleware;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Tenancy.Abstractions;
using System.Diagnostics;
using System.Reflection;

namespace Muonroi.AspNetCore.Tests;

/// <summary>
/// Tests that MExceptionMiddleware writes the muonroi-causal-chain response header
/// when MCausalChainOptions are registered, and skips it gracefully when not registered.
/// </summary>
public class MExceptionMiddlewareChainTests
{
    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = string.Empty;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestDomainException : MException
    {
        public TestDomainException(string message, string errorCode = "CHAIN-TEST-001")
            : base(errorCode, message, MExceptionCategory.Domain, 500)
        {
            SourcePackage = "Muonroi.Tests";
            CallerMethod = "TestMethod";
        }
    }

    /// <summary>
    /// Invokes HandleExceptionAsync via reflection, passing the optional causalChainOptions.
    /// Mirrors the pattern in MExceptionMiddlewareTests.InvokeHandle.
    /// Note: HandleExceptionAsync takes MCausalChainOptions? (unwrapped), not IOptions.
    /// </summary>
    private static Task InvokeHandle(
        MExceptionMiddleware middleware,
        HttpContext context,
        Exception? exception,
        IMLog<MExceptionMiddleware>? logger = null,
        IMJsonSerializeService? serializeService = null,
        MAuthenticateInfoContext? authContext = null,
        IHostEnvironment? environment = null,
        ITenantContext? tenantContext = null,
        IOptions<MCausalChainOptions>? causalChainOptions = null)
    {
        MethodInfo methodInfo = typeof(MExceptionMiddleware)
            .GetMethod("HandleExceptionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // HandleExceptionAsync takes MCausalChainOptions? (not IOptions<>) — unwrap here
        MCausalChainOptions? options = causalChainOptions?.Value;

        return (Task)methodInfo.Invoke(middleware, [
            context,
            exception!,
            logger ?? Substitute.For<IMLog<MExceptionMiddleware>>(),
            serializeService ?? new MJsonSerializeService(),
            authContext ?? new MAuthenticateInfoContext(false),
            environment ?? new FakeEnvironment(),
            tenantContext,
            options
        ])!;
    }

    private static IOptions<MCausalChainOptions> CreateOptions(
        string serviceName = "test-service",
        bool includeMessage = false) =>
        Options.Create(new MCausalChainOptions
        {
            ServiceName = serviceName,
            PropagateInHeaders = true,
            MaxChainDepth = 10,
            IncludeMessageInChain = includeMessage
        });

    [Fact]
    public async Task Middleware_WritesCausalChainHeader_WhenMExceptionAndOptionsRegistered()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        MExceptionMiddleware middleware = new(_ => Task.CompletedTask);

        TestDomainException ex = new("something failed", "CHAIN-001");
        IOptions<MCausalChainOptions> opts = CreateOptions("my-service");

        // Act
        await InvokeHandle(middleware, context, ex, causalChainOptions: opts);

        // Assert — response header was written
        Assert.True(context.Response.Headers.ContainsKey("muonroi-causal-chain"),
            "Expected muonroi-causal-chain response header to be set.");

        string headerValue = context.Response.Headers["muonroi-causal-chain"].ToString();
        Assert.False(string.IsNullOrEmpty(headerValue));

        // Deserialize and verify chain content
        MCausalChain? chain = MCausalChain.TryDeserialize(headerValue);
        Assert.NotNull(chain);
        Assert.Equal("my-service", chain!.OriginService);
        Assert.Equal("CHAIN-001", chain.OriginErrorCode);
    }

    [Fact]
    public async Task Middleware_IncludesUpstreamCause_WhenMExceptionHasCausalChain()
    {
        // Arrange — build an upstream chain in Activity baggage so MException picks it up
        MCausalChain upstreamChain = new()
        {
            OriginService = "upstream-svc",
            OriginErrorCode = "UP-001",
            OriginLayer = MExceptionLayer.Application,
            Timestamp = DateTimeOffset.UtcNow
        };
        string baggageValue = MCausalChain.Serialize(upstreamChain);

        using Activity activity = new Activity("test").Start();
        activity.SetBaggage("muonroi.causal", baggageValue);

        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        MExceptionMiddleware middleware = new(_ => Task.CompletedTask);

        // The exception reads CausalChain from Activity.Current baggage automatically
        TestDomainException ex = new("downstream failure", "DOWN-001");
        IOptions<MCausalChainOptions> opts = CreateOptions("downstream-svc");

        // Act
        await InvokeHandle(middleware, context, ex, causalChainOptions: opts);

        // Assert
        string headerValue = context.Response.Headers["muonroi-causal-chain"].ToString();
        MCausalChain? chain = MCausalChain.TryDeserialize(headerValue);

        Assert.NotNull(chain);
        Assert.Equal("downstream-svc", chain!.OriginService);
        Assert.NotNull(chain.Cause);
        Assert.Equal("upstream-svc", chain.Cause!.OriginService);
        Assert.Equal("UP-001", chain.Cause.OriginErrorCode);
    }

    [Fact]
    public async Task Middleware_SkipsCausalChainHeader_WhenOptionsNotRegistered()
    {
        // Arrange — pass null for causalChainOptions (simulates no AddMuonroiCausalChain call)
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        MExceptionMiddleware middleware = new(_ => Task.CompletedTask);

        TestDomainException ex = new("oops");

        // Act — pass null causalChainOptions, should NOT throw and NOT write header
        await InvokeHandle(middleware, context, ex, causalChainOptions: null);

        // Assert
        Assert.False(context.Response.Headers.ContainsKey("muonroi-causal-chain"),
            "muonroi-causal-chain header should NOT be written when options are not registered.");
    }

    [Fact]
    public async Task Middleware_OmitsMessage_WhenIncludeMessageIsFalse()
    {
        // Arrange — IncludeMessageInChain defaults to false
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        MExceptionMiddleware middleware = new(_ => Task.CompletedTask);

        TestDomainException ex = new("sensitive error message");
        IOptions<MCausalChainOptions> opts = CreateOptions(includeMessage: false);

        // Act
        await InvokeHandle(middleware, context, ex, causalChainOptions: opts);

        // Assert — message should NOT be in chain
        string headerValue = context.Response.Headers["muonroi-causal-chain"].ToString();
        MCausalChain? chain = MCausalChain.TryDeserialize(headerValue);
        Assert.NotNull(chain);
        Assert.Null(chain!.OriginMessage);
    }

    [Fact]
    public async Task Middleware_IncludesMessage_WhenIncludeMessageIsTrue()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        MExceptionMiddleware middleware = new(_ => Task.CompletedTask);

        TestDomainException ex = new("error detail here");
        IOptions<MCausalChainOptions> opts = CreateOptions(includeMessage: true);

        // Act
        await InvokeHandle(middleware, context, ex, causalChainOptions: opts);

        // Assert — message should be present in chain
        string headerValue = context.Response.Headers["muonroi-causal-chain"].ToString();
        MCausalChain? chain = MCausalChain.TryDeserialize(headerValue);
        Assert.NotNull(chain);
        Assert.Equal("error detail here", chain!.OriginMessage);
    }
}
