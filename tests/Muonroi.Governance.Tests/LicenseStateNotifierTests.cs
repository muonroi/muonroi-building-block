namespace Muonroi.Governance.Tests;

public class LicenseStateNotifierTests
{
    [Fact]
    public void LatestPayload_Is_Null_Initially()
    {
        LicenseStateNotifier notifier = new();

        notifier.LatestPayload.Should().BeNull();
    }

    [Fact]
    public void NotifyRefreshed_Updates_LatestPayload()
    {
        LicenseStateNotifier notifier = new();
        LicensePayload payload = new() { LicenseId = "LIC-001" };

        notifier.NotifyRefreshed(payload);

        notifier.LatestPayload.Should().BeSameAs(payload);
    }

    [Fact]
    public void NotifyRefreshed_Raises_Event()
    {
        LicenseStateNotifier notifier = new();
        LicensePayload? received = null;
        notifier.OnLicenseRefreshed += p => received = p;

        LicensePayload payload = new() { LicenseId = "LIC-002" };
        notifier.NotifyRefreshed(payload);

        received.Should().BeSameAs(payload);
    }

    [Fact]
    public void NotifyRefreshed_Multiple_Times_Keeps_Latest()
    {
        LicenseStateNotifier notifier = new();
        LicensePayload first = new() { LicenseId = "LIC-001" };
        LicensePayload second = new() { LicenseId = "LIC-002" };

        notifier.NotifyRefreshed(first);
        notifier.NotifyRefreshed(second);

        notifier.LatestPayload!.LicenseId.Should().Be("LIC-002");
    }
}
