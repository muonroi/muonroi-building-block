using FluentAssertions;
using Muonroi.Governance.Abstractions.License;
using Muonroi.Governance.Compliance;
using Muonroi.Governance.Enterprise.Compliance;
using Muonroi.Logging.Abstractions;
using NSubstitute;

namespace Muonroi.Governance.Enterprise.Tests.Compliance;

public sealed class MComplianceExportHostedServiceTests
{
    [Fact]
    public async Task StartAsync_ShouldRunExportOnce_AndStopCleanly()
    {
        TaskCompletionSource<bool> called = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IMComplianceExportService exportService = Substitute.For<IMComplianceExportService>();
        exportService.ExportAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                called.TrySetResult(true);
                return Task.FromResult(new MComplianceExportRunResult
                {
                    IsEnabled = true,
                    ExportedCount = 3,
                    ChainEntryCount = 2,
                    ControlPlaneAuditCount = 1,
                    LastRecordHash = "hash-1"
                });
            });

        MComplianceExportHostedService service = new(
            exportService,
            new LicenseConfigs
            {
                Compliance = new MComplianceConfigs
                {
                    ExportIntervalMinutes = 0
                }
            },
            Substitute.For<IMLog<MComplianceExportHostedService>>());

        await service.StartAsync(CancellationToken.None);
        await called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        await exportService.Received(1).ExportAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenExportThrows_ShouldSwallowFailure_AndRemainStoppable()
    {
        TaskCompletionSource<bool> called = new(TaskCreationOptions.RunContinuationsAsynchronously);
        IMComplianceExportService exportService = Substitute.For<IMComplianceExportService>();
        exportService.ExportAsync(Arg.Any<CancellationToken>())
            .Returns<Task<MComplianceExportRunResult>>(_ =>
            {
                called.TrySetResult(true);
                throw new InvalidOperationException("boom");
            });

        MComplianceExportHostedService service = new(
            exportService,
            new LicenseConfigs
            {
                Compliance = new MComplianceConfigs
                {
                    ExportIntervalMinutes = 1
                }
            },
            Substitute.For<IMLog<MComplianceExportHostedService>>());

        await service.StartAsync(CancellationToken.None);
        await called.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.StopAsync(CancellationToken.None);

        await exportService.Received(1).ExportAsync(Arg.Any<CancellationToken>());
    }
}
