namespace TestProject.Service.IntegrationTests;

/// <summary>
/// SRVC-03: Verifies that AddSiteCommandHandler keyed dispatch resolves the correct
/// per-site MSiteCommandHandler implementation based on the active site context.
/// All assertions use captured log output — no private state inspection.
/// </summary>
public sealed class CommandHandlerDispatchTests
{
    /// <summary>
    /// Mutable holder so lambdas can close over a changeable site ID.
    /// </summary>
    private sealed class SiteHolder { public string SiteId { get; set; } = SiteIds.DEFAULT; }

    /// <summary>
    /// Minimal ISiteConfiguration stub — returns default values.
    /// Handlers only need the constructor to succeed; no site config is exercised in these tests.
    /// </summary>
    private sealed class FakeSiteConfiguration : ISiteConfiguration
    {
        public T? GetValue<T>(string key) => default;
        public T GetValue<T>(string key, T defaultValue) => defaultValue;
        public IConfigurationSection GetSection(string key)
            => new ConfigurationBuilder().Build().GetSection(key);
        public IEnumerable<IConfigurationSection> GetChildren() => [];
    }

    /// <summary>
    /// Builds a ServiceProvider configured for handler dispatch testing.
    /// The holder.SiteId is read inside lambdas so callers can switch context per scope.
    /// </summary>
    private static (ServiceProvider provider, LogCapture logCapture) BuildServiceProvider(SiteHolder holder)
    {
        var logCapture = new LogCapture();
        var services = new ServiceCollection();

        services.AddLogging(b => b.AddProvider(logCapture));

        // ISiteConfiguration stub — handlers need it in the constructor
        services.AddScoped<ISiteConfiguration>(_ => new FakeSiteConfiguration());

        // ISiteProfileResolver reads from holder so site context can be switched between scopes
        services.AddScoped<ISiteProfileResolver>(_ => new FakeSiteProfileResolver(holder.SiteId));

        // Register keyed per-site handlers
        services.AddKeyedScoped<IRequestHandler<CreateOrderCommand, CreateOrderResponse>,
            AlphaCreateOrderHandler>(SiteIds.ALPHA);
        services.AddKeyedScoped<IRequestHandler<CreateOrderCommand, CreateOrderResponse>,
            BravoCreateOrderHandler>(SiteIds.BRAVO);

        // Register the site-resolved dispatch factory (resolves by resolver.Current.SiteId key)
        services.AddSiteCommandHandler<CreateOrderCommand, CreateOrderResponse>();

        return (services.BuildServiceProvider(validateScopes: true), logCapture);
    }

    [Fact]
    public async Task AlphaSite_DispatchesCreateOrderCommand_ResolvesAlphaHandler()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = SiteIds.ALPHA };
        var (provider, logCapture) = BuildServiceProvider(holder);
        var logger = provider.GetRequiredService<ILogger<CommandHandlerDispatchTests>>();

        using var scope = provider.CreateScope();

        // Act
        var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<CreateOrderCommand, CreateOrderResponse>>();
        logger.LogInformation("Handler resolved: {HandlerType} for site {SiteId}", handler.GetType().Name, holder.SiteId);

        var response = await handler.Handle(new CreateOrderCommand("order-1", "alpha test"), CancellationToken.None);
        logger.LogInformation("Handler result: {Message} handlerType={HandlerType}", response.Message, response.HandlerType);

        // Assert — log chain verification
        logCapture.HasMessage("AlphaCreateOrderHandler").Should().BeTrue(
            "AlphaCreateOrderHandler type name must appear in captured logs");
        logCapture.HasMessage("ALPHA-Handled").Should().BeTrue(
            "ALPHA-Handled marker must appear in the response message log");

        // Also assert response content directly
        response.Success.Should().BeTrue();
        response.HandlerType.Should().Be(nameof(AlphaCreateOrderHandler));
        response.Message.Should().Contain("ALPHA-Handled");
    }

    [Fact]
    public async Task BravoSite_DispatchesCreateOrderCommand_ResolvesBravoHandler()
    {
        // Arrange
        var holder = new SiteHolder { SiteId = SiteIds.BRAVO };
        var (provider, logCapture) = BuildServiceProvider(holder);
        var logger = provider.GetRequiredService<ILogger<CommandHandlerDispatchTests>>();

        using var scope = provider.CreateScope();

        // Act
        var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<CreateOrderCommand, CreateOrderResponse>>();
        logger.LogInformation("Handler resolved: {HandlerType} for site {SiteId}", handler.GetType().Name, holder.SiteId);

        var response = await handler.Handle(new CreateOrderCommand("order-2", "bravo test"), CancellationToken.None);
        logger.LogInformation("Handler result: {Message} handlerType={HandlerType}", response.Message, response.HandlerType);

        // Assert — log chain verification
        logCapture.HasMessage("BravoCreateOrderHandler").Should().BeTrue(
            "BravoCreateOrderHandler type name must appear in captured logs");
        logCapture.HasMessage("BRAVO-Handled").Should().BeTrue(
            "BRAVO-Handled marker must appear in the response message log");

        // Also assert response content directly
        response.Success.Should().BeTrue();
        response.HandlerType.Should().Be(nameof(BravoCreateOrderHandler));
        response.Message.Should().Contain("BRAVO-Handled");
    }

    [Fact]
    public async Task SwitchingSiteContext_DispatchesDifferentHandlers()
    {
        // Arrange — single root provider, switch holder.SiteId per scope
        var holder = new SiteHolder { SiteId = SiteIds.ALPHA };
        var (provider, logCapture) = BuildServiceProvider(holder);
        var logger = provider.GetRequiredService<ILogger<CommandHandlerDispatchTests>>();

        // --- Alpha scope ---
        holder.SiteId = SiteIds.ALPHA;
        CreateOrderResponse alphaResponse;
        using (var scope = provider.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<CreateOrderCommand, CreateOrderResponse>>();
            logger.LogInformation("Handler resolved: {HandlerType} for site {SiteId}", handler.GetType().Name, holder.SiteId);
            alphaResponse = await handler.Handle(new CreateOrderCommand("order-alpha", "switching test"), CancellationToken.None);
            logger.LogInformation("Handler result: {Message} handlerType={HandlerType}", alphaResponse.Message, alphaResponse.HandlerType);
        }

        // Clear log capture before Bravo scope
        logCapture.Clear();

        // --- Bravo scope ---
        holder.SiteId = SiteIds.BRAVO;
        CreateOrderResponse bravoResponse;
        using (var scope = provider.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<IRequestHandler<CreateOrderCommand, CreateOrderResponse>>();
            logger.LogInformation("Handler resolved: {HandlerType} for site {SiteId}", handler.GetType().Name, holder.SiteId);
            bravoResponse = await handler.Handle(new CreateOrderCommand("order-bravo", "switching test"), CancellationToken.None);
            logger.LogInformation("Handler result: {Message} handlerType={HandlerType}", bravoResponse.Message, bravoResponse.HandlerType);
        }

        // Assert — Alpha scope produced Alpha handler
        alphaResponse.HandlerType.Should().Be(nameof(AlphaCreateOrderHandler),
            "switching to ALPHA context must dispatch to AlphaCreateOrderHandler");
        alphaResponse.Message.Should().Contain("ALPHA-Handled");

        // Assert — Bravo scope produced Bravo handler
        bravoResponse.HandlerType.Should().Be(nameof(BravoCreateOrderHandler),
            "switching to BRAVO context must dispatch to BravoCreateOrderHandler");
        bravoResponse.Message.Should().Contain("BRAVO-Handled");

        // Bravo log chain verification (log cleared before Bravo scope)
        logCapture.HasMessage("BravoCreateOrderHandler").Should().BeTrue(
            "BravoCreateOrderHandler type name must appear in logs after switching to BRAVO site");
        logCapture.HasMessage("BRAVO-Handled").Should().BeTrue(
            "BRAVO-Handled marker must appear in logs after switching to BRAVO site");

        // Ensure handlers are different types
        alphaResponse.HandlerType.Should().NotBe(bravoResponse.HandlerType,
            "different site contexts must resolve different handler types");
    }
}
