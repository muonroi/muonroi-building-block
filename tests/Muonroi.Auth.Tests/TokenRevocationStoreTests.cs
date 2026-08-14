namespace Muonroi.Auth.Tests;

public class TokenRevocationStoreTests
{
    private sealed class FakeDateTimeService : IMDateTimeService
    {
        public DateTime CurrentTime { get; set; } = DateTime.UtcNow;
        public DateTime UtcNow() => CurrentTime;
        public DateTime Now() => CurrentTime;
        public DateTime Today() => CurrentTime.Date;
        public DateTime UtcToday() => CurrentTime.Date;
        public double NowTs() => new DateTimeOffset(CurrentTime).ToUnixTimeSeconds();
        public double UtcNowTs() => new DateTimeOffset(CurrentTime).ToUnixTimeSeconds();
    }

    [Fact]
    public void IsRevoked_UnrevokedToken_ReturnsFalse()
    {
        var dt = new FakeDateTimeService();
        var store = new TokenRevocationStore(dt);

        store.IsRevoked("jti-1").Should().BeFalse();
    }

    [Fact]
    public void Revoke_ThenIsRevoked_ReturnsTrue()
    {
        var dt = new FakeDateTimeService();
        var store = new TokenRevocationStore(dt);

        store.Revoke("jti-1", DateTime.UtcNow.AddHours(1));

        store.IsRevoked("jti-1").Should().BeTrue();
    }

    [Fact]
    public void IsRevoked_ExpiredRevocation_ReturnsFalseAndCleans()
    {
        var dt = new FakeDateTimeService { CurrentTime = DateTime.UtcNow };
        var store = new TokenRevocationStore(dt);
        store.Revoke("jti-1", DateTime.UtcNow.AddMinutes(1));

        // Advance time past expiry
        dt.CurrentTime = DateTime.UtcNow.AddMinutes(2);

        store.IsRevoked("jti-1").Should().BeFalse();
    }

    [Fact]
    public void Revoke_MultipleTokens_AllRevoked()
    {
        var dt = new FakeDateTimeService();
        var store = new TokenRevocationStore(dt);
        DateTime expiry = DateTime.UtcNow.AddHours(1);

        store.Revoke("jti-1", expiry);
        store.Revoke("jti-2", expiry);

        store.IsRevoked("jti-1").Should().BeTrue();
        store.IsRevoked("jti-2").Should().BeTrue();
        store.IsRevoked("jti-3").Should().BeFalse();
    }
}
