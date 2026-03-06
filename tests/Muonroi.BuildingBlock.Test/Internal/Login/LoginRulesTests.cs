using Microsoft.Extensions.Logging.Abstractions;
using Muonroi.Auth.BearerToken.Signers;
using Muonroi.RuleEngine.Abstractions;

namespace Muonroi.BuildingBlock.Test.Internal.Login;

[Collection("NonParallel")]
public sealed class LoginRulesTests : IDisposable
{
    private readonly DateTime _baseTime = new(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
    private readonly TestClockProvider _clock;

    public LoginRulesTests()
    {
        _clock = new TestClockProvider(_baseTime);
        Clock.Provider = _clock;
    }

    public void Dispose()
    {
        Clock.Provider = ClockProviders.Unspecified;
    }

    [Fact]
    public async Task ValidateLoginInfoRule_Fails_When_Credentials_Missing()
    {
        await using FakeDbContext dbContext = CreateDbContext();
        LoginContext<LoginRuleTestRight, FakeDbContext> context = CreateContext(dbContext, string.Empty, string.Empty);
        ValidateLoginInfoRule<LoginRuleTestRight, FakeDbContext> rule = new();

        RuleResult result = await rule.EvaluateAsync(context, new FactBag(), CancellationToken.None);
        Assert.True(result.IsSuccess);

        await rule.ExecuteAsync(context, CancellationToken.None);

        Assert.False(context.Result.IsOk);
        Assert.Contains(context.Result.ErrorMessages,
            error => error.ErrorMessage == nameof(SystemEnum.InvalidLoginInfo));
    }

    [Fact]
    public async Task LoadUserRule_Loads_User_When_Present()
    {
        await using FakeDbContext dbContext = CreateDbContext();
        MUser user = await SeedUserAsync(dbContext);
        LoginContext<LoginRuleTestRight, FakeDbContext> context = CreateContext(dbContext);

        LoadUserRule<LoginRuleTestRight, FakeDbContext> rule = new();
        RuleResult result = await rule.EvaluateAsync(context, new FactBag(), CancellationToken.None);
        Assert.True(result.IsSuccess);

        await rule.ExecuteAsync(context, CancellationToken.None);

        Assert.True(context.Result.IsOk);
        Assert.NotNull(context.User);
        Assert.Equal(user.Id, context.User!.Id);
    }

    [Fact]
    public async Task LoadUserRule_Adds_Error_When_User_Not_Found()
    {
        await using FakeDbContext dbContext = CreateDbContext();
        LoginContext<LoginRuleTestRight, FakeDbContext> context = CreateContext(dbContext);

        LoadUserRule<LoginRuleTestRight, FakeDbContext> rule = new();
        await rule.ExecuteAsync(context, CancellationToken.None);

        Assert.False(context.Result.IsOk);
        Assert.Contains(context.Result.ErrorMessages,
            error => error.ErrorCode == nameof(SystemEnum.InvalidCredentials));
    }

    [Fact]
    public async Task CheckAccountLockRule_Adds_Error_When_Account_Locked()
    {
        await using FakeDbContext dbContext = CreateDbContext();
        MUser user = await SeedUserAsync(dbContext);
        MUserLoginAttempt attempt = new()
        {
            UserGuid = user.EntityId,
            AttemptTime = 3,
            LockTo = _baseTime.AddMinutes(5)
        };
        _ = await dbContext.MUserLoginAttempts.AddAsync(attempt);
        _ = await dbContext.SaveChangesAsync();

        LoginContext<LoginRuleTestRight, FakeDbContext> context = CreateContext(dbContext);
        context.User = user;

        CheckAccountLockRule<LoginRuleTestRight, FakeDbContext> rule = new();
        await rule.ExecuteAsync(context, CancellationToken.None);

        Assert.False(context.Result.IsOk);
        Assert.Contains(context.Result.ErrorMessages,
            error => error.ErrorMessage == attempt.LockTo.Subtract(_baseTime).ToString());
    }

    [Fact]
    public async Task VerifyPasswordRule_Records_Attempt_On_Failure()
    {
        await using FakeDbContext dbContext = CreateDbContext();
        MUser user = await SeedUserAsync(dbContext, "correct");
        LoginContext<LoginRuleTestRight, FakeDbContext> context = CreateContext(dbContext, password: "wrong");
        context.User = user;

        VerifyPasswordRule<LoginRuleTestRight, FakeDbContext> rule = new();
        await rule.ExecuteAsync(context, CancellationToken.None);

        Assert.False(context.Result.IsOk);
        MUserLoginAttempt attempt = await dbContext.MUserLoginAttempts.AsNoTracking().SingleAsync();
        Assert.Equal(user.EntityId, attempt.UserGuid);
        Assert.Equal(1, attempt.AttemptTime);
        Assert.Equal(DateTime.MinValue, attempt.LockTo);
    }

    [Fact]
    public async Task VerifyPasswordRule_Does_Not_Run_When_Result_Invalid()
    {
        await using FakeDbContext dbContext = CreateDbContext();
        MUser user = await SeedUserAsync(dbContext, "correct");
        LoginContext<LoginRuleTestRight, FakeDbContext> context = CreateContext(dbContext, password: "correct");
        context.User = user;
        context.Result.AddError("err");

        VerifyPasswordRule<LoginRuleTestRight, FakeDbContext> rule = new();
        await rule.ExecuteAsync(context, CancellationToken.None);

        Assert.True(context.Result.ErrorMessages.Count == 1);
        Assert.Empty(await dbContext.MUserLoginAttempts.ToListAsync());
    }

    [Fact]
    public async Task GenerateTokenRule_Creates_Response_And_Resets_Attempts()
    {
        await using FakeDbContext dbContext = CreateDbContext();
        MUser user = await SeedUserAsync(dbContext, "correct");
        MUserLoginAttempt attempt = new()
        {
            UserGuid = user.EntityId,
            AttemptTime = 3,
            LockTo = _baseTime.AddMinutes(5)
        };
        _ = await dbContext.MUserLoginAttempts.AddAsync(attempt);
        _ = await dbContext.SaveChangesAsync();

        IMultiLevelCacheService cache = Substitute.For<IMultiLevelCacheService>();
        LoginContext<LoginRuleTestRight, FakeDbContext> context = CreateContext(dbContext, password: "correct", cacheService: cache);
        context.User = user;
        context.LoginAttempt = await dbContext.MUserLoginAttempts.FirstAsync();

        GenerateTokenRule<LoginRuleTestRight, FakeDbContext> rule = new();
        await rule.ExecuteAsync(context, CancellationToken.None);

        Assert.True(context.Result.IsOk);
        Assert.NotNull(context.Result.Result);
        Assert.False(string.IsNullOrWhiteSpace(context.Result.Result!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(context.Result.Result.RefreshToken));

        MUserLoginAttempt updated = await dbContext.MUserLoginAttempts.AsNoTracking().SingleAsync();
        Assert.Equal(0, updated.AttemptTime);
        Assert.Equal(DateTime.MinValue, updated.LockTo);

        int refreshCount = await dbContext.RefreshTokens.CountAsync();
        Assert.Equal(1, refreshCount);

        await cache.Received().SetAsync(Arg.Is<string>(key => key.StartsWith("token_validity:")),
            Arg.Any<MRefreshToken>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void LoginRules_Order_And_Dependencies_Are_Consistent()
    {
        IRule<LoginContext<LoginRuleTestRight, FakeDbContext>>[] rules =
        [
            new ValidateLoginInfoRule<LoginRuleTestRight, FakeDbContext>(),
            new LoadUserRule<LoginRuleTestRight, FakeDbContext>(),
            new CheckAccountLockRule<LoginRuleTestRight, FakeDbContext>(),
            new VerifyPasswordRule<LoginRuleTestRight, FakeDbContext>(),
            new GenerateTokenRule<LoginRuleTestRight, FakeDbContext>()
        ];

        int[] expectedOrder = [1, 2, 3, 4, 5];
        string[][] expectedDependencies =
        [
            [],
            ["ValidateLoginInfo"],
            ["LoadUser"],
            ["CheckAccountLock"],
            ["VerifyPassword"]
        ];

        for (int i = 0; i < rules.Length; i++)
        {
            Assert.Equal(expectedOrder[i], rules[i].Order);
            Assert.Equal(expectedDependencies[i], rules[i].DependsOn);

            foreach (string dependency in rules[i].DependsOn)
            {
                IRule<LoginContext<LoginRuleTestRight, FakeDbContext>>? dependencyRule = rules.SingleOrDefault(r => r.Code == dependency);
                Assert.NotNull(dependencyRule);
                Assert.True(dependencyRule!.Order < rules[i].Order);
            }
        }
    }

    [Fact]
    public async Task VerifyPasswordRule_Locks_Account_With_Configured_Durations()
    {
        await using FakeDbContext dbContext = CreateDbContext();
        MUser user = await SeedUserAsync(dbContext, "correct");
        VerifyPasswordRule<LoginRuleTestRight, FakeDbContext> verify = new();

        Dictionary<int, TimeSpan?> expectations = new()
        {
            { 1, null },
            { 2, null },
            { 3, TimeSpan.FromMinutes(5) },
            { 4, TimeSpan.FromMinutes(10) },
            { 5, TimeSpan.FromMinutes(30) },
            { 6, null }
        };

        foreach ((int attemptNumber, TimeSpan? lockDuration) in expectations)
        {
            _clock.SetUtcNow(_baseTime.AddMinutes(attemptNumber));
            LoginContext<LoginRuleTestRight, FakeDbContext> context = CreateContext(dbContext, password: "wrong");
            context.User = user;
            context.LoginAttempt = await dbContext.MUserLoginAttempts.FirstOrDefaultAsync();
            await verify.ExecuteAsync(context, CancellationToken.None);

            MUserLoginAttempt attempt = await dbContext.MUserLoginAttempts.AsNoTracking().SingleAsync();
            Assert.Equal(attemptNumber, attempt.AttemptTime);

            if (attemptNumber == 6)
            {
                Assert.Equal(DateTime.MaxValue.Date, attempt.LockTo.Date);
            }
            else if (lockDuration is null)
            {
                Assert.Equal(DateTime.MinValue, attempt.LockTo);
            }
            else
            {
                Assert.Equal(_clock.UtcNow.Add(lockDuration.Value), attempt.LockTo, TimeSpan.FromSeconds(1));
            }

            dbContext.ChangeTracker.Clear();
            user = await dbContext.Users.FirstAsync();
        }
    }

    private static LoginContext<LoginRuleTestRight, FakeDbContext> CreateContext(
        FakeDbContext dbContext,
        string username = "user",
        string password = "correct",
        IMultiLevelCacheService? cacheService = null)
    {
        cacheService ??= Substitute.For<IMultiLevelCacheService>();
        cacheService
            .SetAsync(Arg.Any<string>(), Arg.Any<MRefreshToken>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        LoginRequestModel request = new()
        {
            Username = username,
            Password = password
        };

        MTokenInfo tokenInfo = new()
        {
            SymmetricSecretKey = "0123456789ABCDEF0123456789ABCDEF",
            Issuer = "issuer",
            Audience = "audience",
            ExpiryMinutes = 60,
            RefreshTokenTtl = 120,
            UseRsa = false
        };

        MAuthenticateTokenHelper<LoginRuleTestRight> helper = new(tokenInfo, new HmacTokenSigner(tokenInfo.SymmetricSecretKey),
            NullLogger<MAuthenticateTokenHelper<LoginRuleTestRight>>.Instance);

        return new LoginContext<LoginRuleTestRight, FakeDbContext>(dbContext, request, tokenInfo, helper, cacheService,
            "vi-VN");
    }

    private static async Task<MUser> SeedUserAsync(FakeDbContext dbContext, string password = "correct")
    {
        string hash = MPasswordHelper.HashPassword(password, out string? salt);
        MUser user = new()
        {
            UserName = "user",
            EmailAddress = "user@example.com",
            Name = "Test",
            Surname = "User",
            Password = hash,
            Salt = salt,
            CreatorUserId = Guid.NewGuid(),
            CreationTime = DateTime.UtcNow
        };

        _ = await dbContext.Users.AddAsync(user);
        _ = await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return await dbContext.Users.FirstAsync();
    }

    private static FakeDbContext CreateDbContext()
    {
        DbContextOptions<FakeDbContext> options = new DbContextOptionsBuilder<FakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        FakeDbContext context = new(options);
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class TestClockProvider(DateTime utcNow) : IClockProvider
    {
        public DateTime Now => UtcNow.ToLocalTime();

        public DateTime UtcNow { get; private set; } = utcNow;

        public DateTimeKind Kind => DateTimeKind.Utc;

        public bool SupportsMultipleTimezone => true;

        public DateTime Normalize(DateTime dateTime)
        {
            return dateTime;
        }

        public void SetUtcNow(DateTime utcNow)
        {
            UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        }
    }

    private enum LoginRuleTestRight
    {
        None = 0,
        Read = 1,
        Write = 2
    }
}
