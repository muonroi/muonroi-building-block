namespace Muonroi.RuleEngine.Proliferation.Export;

/// <summary>
/// Supported test report export formats for CI/CD pipeline integration.
/// </summary>
public enum TestReportFormat
{
    /// <summary>xUnit XML format — supported by GitHub Actions, Azure DevOps, Jenkins.</summary>
    XUnitXml = 0,

    /// <summary>TRX format — Visual Studio Test Results.</summary>
    Trx = 1
}
