using Muonroi.Governance.License;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.BuildingBlock.Test;

public class MGenericControllerRuleEngineLicenseTests
{
    private sealed class StubMediator : IMediator
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => Task.CompletedTask;
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => Task.FromResult(default(TResponse)!);
        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => Task.CompletedTask;
        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => Task.FromResult<object?>(null);
        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default) => AsyncEnumerable.Empty<TResponse>();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => AsyncEnumerable.Empty<object?>();
    }

    private sealed class DenyRuleEngineGuard : ILicenseGuard
    {
        private static readonly LicenseState State = LicenseState.CreateFree();
        public LicenseState Current => State;
        public LicenseTier Tier => State.Tier;
        public bool IsFreeMode => true;

        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
            string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName)
        {
            return !string.Equals(featureName, FreeTierFeatures.Premium.RuleEngine, StringComparison.OrdinalIgnoreCase);
        }

        public void EnsureFeature(string featureName)
        {
            if (!HasFeature(featureName))
                throw new InvalidOperationException("rule-engine feature not licensed");
        }

        public void RecordAction(LicenseActionContext context)
        {
        }

        public string GetChainToken() => "test";

        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
        {
            return decryptor("key", encryptedData);
        }
    }

    private sealed class ProductEntity : MEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class GenericRuleDbContext(DbContextOptions<GenericRuleDbContext> options)
        : MDbContext(options, new StubMediator())
    {
        public DbSet<ProductEntity> Products { get; set; } = null!;
    }

    private sealed class ProductController(
        GenericRuleDbContext db,
        ILicenseGuard guard,
        MTokenInfo tokenInfo,
        IConfiguration configuration) : MGenericController<ProductEntity, GenericRuleDbContext>(
            db,
            guard,
            tokenInfo,
            configuration)
    {
    }

    [Fact]
    public async Task Create_WhenRuleOrchestratorRegisteredAndRuleEngineNotLicensed_ShouldThrow()
    {
        DbContextOptions<GenericRuleDbContext> options = new DbContextOptionsBuilder<GenericRuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using GenericRuleDbContext db = new(options);

        ProductController controller = new(
            db,
            new DenyRuleEngineGuard(),
            new MTokenInfo { MultiTenantEnabled = false },
            new ConfigurationBuilder().AddInMemoryCollection().Build());

        ServiceProvider services = new ServiceCollection()
            .AddSingleton(new RuleOrchestrator<CrudContext<ProductEntity>>(
                [],
                [],
                NullLogger<RuleOrchestrator<CrudContext<ProductEntity>>>.Instance))
            .BuildServiceProvider();

        DefaultHttpContext httpContext = new()
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity([], "test"))
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        await Assert.ThrowsAsync<MInternalException>(() =>
        {
            ProductEntity entity = new()
            {
                Name = "p1"
            };
            return controller.Create(entity, CancellationToken.None);
        });
    }
}
