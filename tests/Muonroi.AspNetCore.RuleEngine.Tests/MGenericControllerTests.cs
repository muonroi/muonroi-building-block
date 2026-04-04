namespace Muonroi.AspNetCore.RuleEngine.Tests;

public class MGenericControllerTests
{
    public class TestEntity : MEntity, ITenantScoped
    {
        public string Name { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
    }

    public class TestDbContext(DbContextOptions<TestDbContext> options) : MDbContext(options)
    {
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TestEntity>().HasKey(x => x.Id);
        }
    }

    public class TestGenericController(
        TestDbContext dbContext,
        ILicenseGuard licenseGuard,
        MTokenInfo tokenInfo,
        IConfiguration configuration,
        IMDateTimeService dateTimeService)
        : MGenericController<TestEntity, TestDbContext>(dbContext, licenseGuard, tokenInfo, configuration, dateTimeService)
    {
    }

    private readonly TestDbContext _dbContext;
    private readonly ILicenseGuard _licenseGuard = Substitute.For<ILicenseGuard>();
    private readonly MTokenInfo _tokenInfo = new();
    private readonly IConfiguration _configuration;
    private readonly IMDateTimeService _dateTimeService = Substitute.For<IMDateTimeService>();
    private readonly TestGenericController _controller;
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();

    public MGenericControllerTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TestDbContext(options);

        _dateTimeService.UtcNow().Returns(DateTime.UtcNow);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MultiTenantConfigs:Enabled"] = "true",
                ["MultiTenantConfigs:RequireTenantClaimForAuthenticatedUser"] = "true"
            })
            .Build();

        _controller = new TestGenericController(
            _dbContext,
            _licenseGuard,
            _tokenInfo,
            _configuration,
            _dateTimeService);

        var httpContext = new DefaultHttpContext();
        httpContext.RequestServices = _serviceProvider;
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task Get_ShouldReturnOk_WhenUserHasPermission()
    {
        // Arrange
        var entities = new List<TestEntity>
        {
            new() { EntityId = Guid.NewGuid(), Name = "Test 1", TenantId = "tenant-1" },
            new() { EntityId = Guid.NewGuid(), Name = "Test 2", TenantId = "tenant-1" }
        };
        await _dbContext.TestEntities.AddRangeAsync(entities);
        await _dbContext.SaveChangesAsync();

        TenantContext.CurrentTenantId = "tenant-1";
        _tokenInfo.MultiTenantEnabled = true;

        // Act
        var result = await _controller.Get();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var response = (MResponse<object>)okResult.Value!;
        response.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_ShouldReturnOk_WhenEntityExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity = new TestEntity { EntityId = id, Name = "Test 1", TenantId = "tenant-1" };
        await _dbContext.TestEntities.AddAsync(entity);
        await _dbContext.SaveChangesAsync();

        TenantContext.CurrentTenantId = "tenant-1";
        _tokenInfo.MultiTenantEnabled = true;

        // Act
        var result = await _controller.GetById(id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var response = (MResponse<TestEntity>)okResult.Value!;
        response.Result!.EntityId.Should().Be(id);
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenEntityDoesNotExist()
    {
        // Arrange
        TenantContext.CurrentTenantId = "tenant-1";
        _tokenInfo.MultiTenantEnabled = true;

        // Act
        var result = await _controller.GetById(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnOk_AndCallRules()
    {
        // Arrange
        var entity = new TestEntity { Name = "New Entity" };
        TenantContext.CurrentTenantId = "tenant-1";
        _tokenInfo.MultiTenantEnabled = true;

        var rule = Substitute.For<IRule<CrudContext<TestEntity>>>();
        rule.HookPoint.Returns(HookPoint.BeforeRule);
        rule.EvaluateAsync(Arg.Any<CrudContext<TestEntity>>(), Arg.Any<FactBag>(), Arg.Any<CancellationToken>())
            .Returns(RuleResult.Passed());

        var ruleOrchestrator = new RuleOrchestrator<CrudContext<TestEntity>>([rule], []);
        _serviceProvider.GetService(typeof(RuleOrchestrator<CrudContext<TestEntity>>)).Returns(ruleOrchestrator);

        // Act
        var result = await _controller.Create(entity, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        await rule.Received().ExecuteAsync(Arg.Any<CrudContext<TestEntity>>(), Arg.Any<CancellationToken>());
        
        var createdEntity = await _dbContext.TestEntities.FirstOrDefaultAsync(x => x.Name == "New Entity");
        createdEntity.Should().NotBeNull();
        createdEntity!.TenantId.Should().Be("tenant-1");
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenRuleCancelsOperation()
    {
        // Arrange
        var entity = new TestEntity { Name = "Invalid" };
        TenantContext.CurrentTenantId = "tenant-1";

        var rule = Substitute.For<IRule<CrudContext<TestEntity>>>();
        rule.HookPoint.Returns(HookPoint.BeforeRule);
        rule.EvaluateAsync(Arg.Any<CrudContext<TestEntity>>(), Arg.Any<FactBag>(), Arg.Any<CancellationToken>())
            .Returns(RuleResult.Passed());
        rule.When(x => x.ExecuteAsync(Arg.Any<CrudContext<TestEntity>>(), Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var context = call.Arg<CrudContext<TestEntity>>();
                context.CancelOperation = true;
                context.CancellationReason = "Invalid name";
            });

        var ruleOrchestrator = new RuleOrchestrator<CrudContext<TestEntity>>([rule], []);
        _serviceProvider.GetService(typeof(RuleOrchestrator<CrudContext<TestEntity>>)).Returns(ruleOrchestrator);

        // Act
        var result = await _controller.Create(entity, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)result;
        var response = (MResponse<object>)badRequest.Value!;
        response.Error!.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Update_ShouldReturnOk_AndApplyChanges()
    {
        // Arrange
        var id = Guid.NewGuid();
        var existing = new TestEntity { EntityId = id, Name = "Original", TenantId = "tenant-1" };
        await _dbContext.TestEntities.AddAsync(existing);
        await _dbContext.SaveChangesAsync();

        var updated = new TestEntity { Name = "Updated" };
        TenantContext.CurrentTenantId = "tenant-1";
        _tokenInfo.MultiTenantEnabled = true;

        // Act
        var result = await _controller.Update(id, updated, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var saved = await _dbContext.TestEntities.FirstOrDefaultAsync(x => x.EntityId == id);
        saved!.Name.Should().Be("Updated");
    }

    [Fact]
    public async Task Delete_ShouldSoftDelete_WhenEntityExists()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entity = new TestEntity { EntityId = id, Name = "To Delete", TenantId = "tenant-1" };
        await _dbContext.TestEntities.AddAsync(entity);
        await _dbContext.SaveChangesAsync();

        TenantContext.CurrentTenantId = "tenant-1";
        _tokenInfo.MultiTenantEnabled = true;

        // Act
        var result = await _controller.Delete(id, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var saved = await _dbContext.TestEntities.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.EntityId == id);
        saved!.IsDeleted.Should().BeTrue();
    }
}
