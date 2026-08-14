namespace Muonroi.Governance.Enterprise.Tests;

public class EnterpriseGovernanceServiceExtensionsTests
{
    [Fact]
    public void AddMEnterpriseGovernance_ShouldRegisterCoreEnterpriseServices()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration();

        services.AddMEnterpriseGovernance(configuration);

        Assert.Contains(services, x => x.ServiceType == typeof(PolicyEnforcer));
        Assert.Contains(services, x => x.ServiceType == typeof(LicenseActivator));
        Assert.Contains(services, x => x.ServiceType == typeof(ChainSubmitter));
        Assert.Contains(services, x => x.ServiceType == typeof(IMComplianceExportService) && x.ImplementationType == typeof(MComplianceExportService));
        Assert.Contains(services, x => x.ServiceType == typeof(IMComplianceEvidencePackService) && x.ImplementationType == typeof(MComplianceEvidencePackService));
        Assert.Contains(services, x => x.ServiceType == typeof(IMUpgradeCompatibilityService) && x.ImplementationType == typeof(MUpgradeCompatibilityService));
        Assert.Contains(services, x => x.ServiceType == typeof(IMEnterpriseSloPresetService) && x.ImplementationType == typeof(MEnterpriseSloPresetService));
    }

    [Fact]
    public void AddMEnterpriseGovernance_WhenOnlineValidationAndComplianceEnabled_ShouldRegisterHostedServices()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{LicenseConfigs.SectionName}:EnableServerValidation"] = "true",
            [$"{LicenseConfigs.SectionName}:Mode"] = nameof(LicenseMode.Online),
            [$"{LicenseConfigs.SectionName}:Online:Endpoint"] = "https://license.muonroi.com",
            [$"{LicenseConfigs.SectionName}:Online:EnableHeartbeat"] = "true",
            [$"{LicenseConfigs.SectionName}:Compliance:Enabled"] = "true",
            [$"{LicenseConfigs.SectionName}:Compliance:EnableBackgroundExport"] = "true"
        });

        services.AddMEnterpriseGovernance(configuration);

        List<Type> hostedServices = [.. services
            .Where(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType != null)
            .Select(x => x.ImplementationType!)];

        Assert.Contains(typeof(LicenseActivationHostedService), hostedServices);
        Assert.Contains(typeof(ChainSubmissionHostedService), hostedServices);
        Assert.Contains(typeof(LicenseHeartbeatService), hostedServices);
        Assert.Contains(typeof(MComplianceExportHostedService), hostedServices);
    }

    [Fact]
    public void AddMEnterpriseGovernance_WhenFallbackToOnlineActivationEnabled_ShouldRegisterChainSubmissionHostedService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            [$"{LicenseConfigs.SectionName}:FallbackToOnlineActivation"] = "true",
            [$"{LicenseConfigs.SectionName}:Online:Endpoint"] = "https://license.muonroi.com"
        });

        services.AddMEnterpriseGovernance(configuration);

        List<Type> hostedServices = [.. services
            .Where(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType != null)
            .Select(x => x.ImplementationType!)];

        Assert.Contains(typeof(ChainSubmissionHostedService), hostedServices);
    }

    [Fact]
    public void AddMEnterpriseGovernance_WhenServicesIsNull_ShouldThrow()
    {
        IServiceCollection? services = null;
        IConfiguration configuration = CreateConfiguration();

        Assert.Throws<MArgumentException>(() => services!.AddMEnterpriseGovernance(configuration));
    }

    [Fact]
    public void AddMEnterpriseGovernance_WhenConfigurationIsNull_ShouldThrow()
    {
        ServiceCollection services = new();
        IConfiguration? configuration = null;

        Assert.Throws<MArgumentException>(() => services.AddMEnterpriseGovernance(configuration!));
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?>? overrides = null)
    {
        Dictionary<string, string?> values = new()
        {
            [$"{LicenseConfigs.SectionName}:Mode"] = nameof(LicenseMode.Offline),
            [$"{LicenseConfigs.SectionName}:Online:Endpoint"] = "https://license.muonroi.com"
        };

        if (overrides != null)
        {
            foreach ((string key, string? value) in overrides)
            {
                values[key] = value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
