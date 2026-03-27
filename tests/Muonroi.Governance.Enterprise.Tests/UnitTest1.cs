using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.Compliance;
using Muonroi.Governance.Enterprise;
using Muonroi.Governance.Enterprise.Compliance;
using Muonroi.Governance.Enterprise.License;
using Muonroi.Governance.Enterprise.Operations;
using Muonroi.Governance.Enterprise.Policy;
using Muonroi.Governance.Enterprise.ServerValidation;
using Muonroi.Governance.License;
using Muonroi.Governance.Operations;

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

        List<Type> hostedServices = services
            .Where(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType != null)
            .Select(x => x.ImplementationType!)
            .ToList();

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

        List<Type> hostedServices = services
            .Where(x => x.ServiceType == typeof(IHostedService) && x.ImplementationType != null)
            .Select(x => x.ImplementationType!)
            .ToList();

        Assert.Contains(typeof(ChainSubmissionHostedService), hostedServices);
    }

    [Fact]
    public void AddMEnterpriseGovernance_WhenServicesIsNull_ShouldThrow()
    {
        IServiceCollection? services = null;
        IConfiguration configuration = CreateConfiguration();

        Assert.Throws<ArgumentNullException>(() => services!.AddMEnterpriseGovernance(configuration));
    }

    [Fact]
    public void AddMEnterpriseGovernance_WhenConfigurationIsNull_ShouldThrow()
    {
        ServiceCollection services = new();
        IConfiguration? configuration = null;

        Assert.Throws<ArgumentNullException>(() => services.AddMEnterpriseGovernance(configuration!));
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
