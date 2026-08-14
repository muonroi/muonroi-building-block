namespace Muonroi.Auth.Tests;

public sealed class TokenStoreAbstractionTests
{
    [Fact]
    public void DefaultTokenRevocationStore_RegistersInMemory_WhenNoneProvided()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<IMDateTimeService>(new FakeDateTimeService());

        // Act
        services.AddDefaultTokenRevocationStore();
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        ITokenRevocationStore store = provider.GetRequiredService<ITokenRevocationStore>();
        store.Should().BeOfType<TokenRevocationStore>();
    }

    [Fact]
    public void DefaultTokenRevocationStore_PreservesCustom_WhenAlreadyRegistered()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<IMDateTimeService>(new FakeDateTimeService());
        services.AddSingleton<ITokenRevocationStore>(new RecordingTokenRevocationStore());

        // Act — TryAdd should NOT overwrite
        services.AddDefaultTokenRevocationStore();
        ServiceProvider provider = services.BuildServiceProvider();

        // Assert
        ITokenRevocationStore store = provider.GetRequiredService<ITokenRevocationStore>();
        store.Should().BeOfType<RecordingTokenRevocationStore>();
    }

    [Fact]
    public void InMemoryTokenRevocationStore_RevokeAndCheck_WorksCorrectly()
    {
        // Arrange
        FakeDateTimeService dateTime = new() { CurrentTime = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc) };
        TokenRevocationStore store = new(dateTime);
        DateTime futureExpiry = dateTime.CurrentTime.AddHours(1);

        // Act — revoke token
        store.Revoke("jti-test", futureExpiry);

        // Assert — token is revoked
        store.IsRevoked("jti-test").Should().BeTrue();

        // Act — advance time past expiry
        dateTime.CurrentTime = dateTime.CurrentTime.AddHours(2);

        // Assert — token auto-cleaned, no longer revoked
        store.IsRevoked("jti-test").Should().BeFalse();
    }

    [Fact]
    public void CustomTokenRevocationStore_InterceptsAllCalls()
    {
        // Arrange
        RecordingTokenRevocationStore recorder = new();
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IMDateTimeService>(new FakeDateTimeService());
        services.AddSingleton<ITokenRevocationStore>(recorder);

        // Act — AddDefaultTokenRevocationStore should NOT overwrite custom registration
        services.AddDefaultTokenRevocationStore();
        ServiceProvider provider = services.BuildServiceProvider();
        ITokenRevocationStore store = provider.GetRequiredService<ITokenRevocationStore>();

        store.Revoke("jti-intercept", DateTime.UtcNow.AddHours(1));
        bool revoked = store.IsRevoked("jti-intercept");

        // Assert
        store.Should().BeSameAs(recorder);
        recorder.RevokedJtis.Should().Contain("jti-intercept");
        recorder.CheckedJtis.Should().Contain("jti-intercept");
        revoked.Should().BeTrue();
    }

    #region Test Doubles

    private sealed class FakeDateTimeService : IMDateTimeService
    {
        public DateTime CurrentTime { get; set; } = new(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        public DateTime Now() => CurrentTime;
        public DateTime UtcNow() => CurrentTime;
        public DateTime Today() => CurrentTime.Date;
        public DateTime UtcToday() => CurrentTime.Date;
        public double NowTs() => new DateTimeOffset(CurrentTime).ToUnixTimeSeconds();
        public double UtcNowTs() => new DateTimeOffset(CurrentTime).ToUnixTimeSeconds();
    }

    private sealed class RecordingTokenRevocationStore : ITokenRevocationStore
    {
        public List<string> RevokedJtis { get; } = [];
        public List<string> CheckedJtis { get; } = [];

        public void Revoke(string jti, DateTime expires)
        {
            RevokedJtis.Add(jti);
        }

        public bool IsRevoked(string jti)
        {
            CheckedJtis.Add(jti);
            return RevokedJtis.Contains(jti);
        }
    }

    #endregion
}
