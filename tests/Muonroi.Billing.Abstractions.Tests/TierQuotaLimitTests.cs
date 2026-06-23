namespace Muonroi.Billing.Abstractions.Tests;

/// <summary>
/// Tests for tier-sourced <see cref="TenantQuota.MaxPdfRendersPerDay"/> presets (MON-04, D-04):
/// non-Enterprise tiers carry a finite, monotonically increasing daily cap; Enterprise is unlimited.
/// </summary>
public sealed class TierQuotaLimitTests
{
    [Fact]
    public void Free_has_a_finite_pdf_render_cap()
    {
        TenantQuotaPresets.Free.MaxPdfRendersPerDay.Should().BeLessThan(int.MaxValue);
        TenantQuotaPresets.Free.MaxPdfRendersPerDay.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Free_Starter_Professional_caps_are_finite_and_monotonically_increasing()
    {
        int free = TenantQuotaPresets.Free.MaxPdfRendersPerDay;
        int starter = TenantQuotaPresets.Starter.MaxPdfRendersPerDay;
        int professional = TenantQuotaPresets.Professional.MaxPdfRendersPerDay;

        free.Should().BeLessThan(int.MaxValue);
        starter.Should().BeLessThan(int.MaxValue);
        professional.Should().BeLessThan(int.MaxValue);

        free.Should().BeLessThan(starter);
        starter.Should().BeLessThan(professional);
    }

    [Fact]
    public void Enterprise_pdf_render_cap_remains_unlimited()
    {
        TenantQuotaPresets.Enterprise.MaxPdfRendersPerDay.Should().Be(int.MaxValue);
    }
}
