namespace Muonroi.AspNetCore.Tests.Permissions;

public class PermissionServiceExtendedTests
{
    private readonly TestDbContext _dbContext;
    private readonly MAuthenticateInfoContext _authContext;
    private readonly IMDateTimeService _dateTimeService;
    private readonly ILicenseGuard _licenseGuard;
    private readonly PermissionService<TestPerm, TestDbContext> _service;

    public PermissionServiceExtendedTests()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TestDbContext(options);
        _authContext = new MAuthenticateInfoContext(true)
        {
            Language = "en",
            CurrentUserGuid = Guid.NewGuid().ToString(),
            TokenValidityKey = "test-key"
        };
        _dateTimeService = new FakeDateTimeService();
        _licenseGuard = Substitute.For<ILicenseGuard>();
        
        _service = new PermissionService<TestPerm, TestDbContext>(
            _dbContext,
            _authContext,
            _dateTimeService,
            licenseGuard: _licenseGuard);
    }

    [Fact]
    public async Task LogoutAsync_ValidUser_RevokesToken()
    {
        var userGuid = Guid.Parse(_authContext.CurrentUserGuid);
        var token = new MRefreshToken
        {
            TokenValidityKey = _authContext.TokenValidityKey,
            CreatorUserId = userGuid,
            IsRevoked = false,
            IsDeleted = false
        };
        _dbContext.Set<MRefreshToken>().Add(token);
        await _dbContext.SaveChangesAsync();

        var result = await _service.LogoutAsync(CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.True(token.IsRevoked);
    }

    [Fact]
    public async Task LogoutAllAsync_ValidUser_RevokesAllTokens()
    {
        var userGuid = Guid.Parse(_authContext.CurrentUserGuid);
        var tokens = new[]
        {
            new MRefreshToken { CreatorUserId = userGuid, IsRevoked = false, TokenValidityKey = "1" },
            new MRefreshToken { CreatorUserId = userGuid, IsRevoked = false, TokenValidityKey = "2" }
        };
        _dbContext.Set<MRefreshToken>().AddRange(tokens);
        await _dbContext.SaveChangesAsync();

        var result = await _service.LogoutAllAsync(CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.All(tokens, t => Assert.True(t.IsRevoked));
    }

    [Fact]
    public async Task GetUsersAsync_ReturnsPagedResult()
    {
        _dbContext.Set<MUser>().AddRange(
            new MUser { UserName = "u1", EmailAddress = "u1@a.com", Name = "N1", Surname = "S1", Password = "p" },
            new MUser { UserName = "u2", EmailAddress = "u2@a.com", Name = "N2", Surname = "S2", Password = "p" }
        );
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetUsersAsync(1, 10, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.NotNull(result.Result);
        Assert.Equal(2, result.Result.RowCount);
        Assert.Equal(2, result.Result.Items.Count());
    }

    [Fact]
    public async Task GetUserAsync_ValidId_ReturnsUser()
    {
        var user = new MUser { UserName = "u1", EmailAddress = "u1@a.com", Name = "N1", Surname = "S1", Password = "p" };
        _dbContext.Set<MUser>().Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetUserAsync(user.EntityId, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.NotNull(result.Result);
        Assert.Equal(user.UserName, result.Result.UserName);
    }

    [Fact]
    public async Task GetUserAsync_InvalidId_ReturnsError()
    {
        var result = await _service.GetUserAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsOk);
    }

    [Fact]
    public async Task UpdateUserAsync_ValidRequest_UpdatesUser()
    {
        var user = new MUser { UserName = "u1", EmailAddress = "u1@a.com", Name = "N1", Surname = "S1", Password = "p" };
        _dbContext.Set<MUser>().Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new MUserModel
        {
            UserGuid = user.EntityId.ToString(),
            Name = "UpdatedName",
            Surname = "UpdatedSurname",
            Email = "updated@a.com"
        };

        var result = await _service.UpdateUserAsync(request, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.NotNull(result.Result);
        Assert.Equal("UpdatedName", result.Result.Name);
        Assert.Equal("updated@a.com", result.Result.EmailAddress);
    }

    [Fact]
    public async Task DeleteUserAsync_ValidId_SoftDeletesUser()
    {
        var user = new MUser { UserName = "u1", EmailAddress = "u1@a.com", Name = "N1", Surname = "S1", Password = "p" };
        _dbContext.Set<MUser>().Add(user);
        await _dbContext.SaveChangesAsync();

        var result = await _service.DeleteUserAsync(user.EntityId, CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.True(user.IsDeleted);
    }

    [Fact]
    public async Task GetUiEngineSchemaVersionAsync_ReturnsVersion()
    {
        var result = await _service.GetUiEngineSchemaVersionAsync(CancellationToken.None);

        Assert.True(result.IsOk);
        Assert.NotNull(result.Result);
    }

    [Fact]
    public async Task NotifyUiEngineSchemaChangeAsync_CallsNotifier()
    {
        var notifier = Substitute.For<IUiEngineSchemaNotifier>();
        var serviceWithNotifier = new PermissionService<TestPerm, TestDbContext>(
            _dbContext,
            _authContext,
            _dateTimeService,
            uiEngineSchemaNotifier: notifier,
            licenseGuard: _licenseGuard);

        var notification = new MUiEngineSchemaChangeNotification
        {
            SchemaHash = "new-hash",
            Source = "test"
        };

        var result = await serviceWithNotifier.NotifyUiEngineSchemaChangeAsync(notification, CancellationToken.None);

        Assert.True(result.IsOk);
        await notifier.Received(1).NotifySchemaChangedAsync(Arg.Any<MUiEngineSchemaVersion>(), Arg.Any<CancellationToken>());
    }
}
