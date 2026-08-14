namespace Muonroi.AspNetCore.Tests.Services;

public sealed class UiEngineSchemaVersionServiceTests
{
    [Fact]
    public async Task BuildUiEngineSchemaVersionAsync_Builds_Deterministic_Hash_From_Active_Permissions()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using TestDbContext dbContext = new(options);
        dbContext.Set<MPermission>().AddRange(
            new MPermission
            {
                Name = "orders.read",
                UiKey = "orders.read",
                Type = PermissionType.Action,
                ParentUiKey = "orders",
                IsGranted = true,
                Order = 2
            },
            new MPermission
            {
                Name = "orders",
                UiKey = "orders",
                Type = PermissionType.Menu,
                IsGranted = true,
                Order = 1
            },
            new MPermission
            {
                Name = "deleted.permission",
                UiKey = "deleted.permission",
                Type = PermissionType.Action,
                IsDeleted = true,
                IsGranted = true,
                Order = 99
            });
        await dbContext.SaveChangesAsync();

        IMDateTimeService dateTimeService = Substitute.For<IMDateTimeService>();
        DateTime fixedUtc = new(2026, 3, 23, 17, 45, 0, DateTimeKind.Utc);
        dateTimeService.UtcNow().Returns(fixedUtc);
        FakeJsonSerializeService serializer = new();
        UiEngineSchemaVersionService<TestDbContext> service = new(dbContext, dateTimeService, serializer);

        MUiEngineSchemaVersion result = await service.BuildUiEngineSchemaVersionAsync();

        string expectedPayload = serializer.Serialize(new
        {
            runtimeSchemaVersion = MUiEngineManifest.MSchemaVersionV2,
            permissions = new object[]
            {
                new
                {
                    UiKey = "orders",
                    Name = "orders",
                    Type = PermissionType.Menu,
                    ParentUiKey = (string?)null,
                    IsGranted = true,
                    Order = 1
                },
                new
                {
                    UiKey = "orders.read",
                    Name = "orders.read",
                    Type = PermissionType.Action,
                    ParentUiKey = "orders",
                    IsGranted = true,
                    Order = 2
                }
            }
        });
        string expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(expectedPayload))).ToLowerInvariant();

        result.Version.Should().Be(MUiEngineManifest.MSchemaVersionV2);
        result.SchemaHash.Should().Be(expectedHash);
        result.OpenApiHash.Should().Be(expectedHash);
        result.GeneratedAtUtc.Should().Be(fixedUtc);
    }

    [Fact]
    public async Task BuildUiEngineSchemaVersionAsync_With_No_Permissions_Still_Returns_Stable_Result()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using TestDbContext dbContext = new(options);
        IMDateTimeService dateTimeService = Substitute.For<IMDateTimeService>();
        dateTimeService.UtcNow().Returns(new DateTime(2026, 3, 23, 18, 0, 0, DateTimeKind.Utc));
        FakeJsonSerializeService serializer = new();
        UiEngineSchemaVersionService<TestDbContext> service = new(dbContext, dateTimeService, serializer);

        MUiEngineSchemaVersion first = await service.BuildUiEngineSchemaVersionAsync();
        MUiEngineSchemaVersion second = await service.BuildUiEngineSchemaVersionAsync();

        first.SchemaHash.Should().Be(second.SchemaHash);
        first.OpenApiHash.Should().Be(second.OpenApiHash);
        first.Version.Should().Be(MUiEngineManifest.MSchemaVersionV2);
    }
}
