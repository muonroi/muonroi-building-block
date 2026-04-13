using Xunit;

namespace Muonroi.RuleGen.Tests;

public sealed class ProliferateCommandTests
{
    [Fact]
    public async Task Proliferate_WhenWorkflowNameMissing_ShouldFailFast()
    {
        CliRunResult result = await CliProcess.RunAsync(
            "proliferate",
            "--ruleset-file", "missing.json");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--workflow-name is required", result.Combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Proliferate_WhenRulesetFileMissing_ShouldFailFast()
    {
        CliRunResult result = await CliProcess.RunAsync(
            "proliferate",
            "--workflow-name", "order-approval");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--ruleset-file is required", result.Combined, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Proliferate_WhenRulesetFileDoesNotExist_ShouldReturnHelpfulError()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-tests", Guid.NewGuid().ToString("N"), "ruleset.json");

        CliRunResult result = await CliProcess.RunAsync(
            "proliferate",
            "--workflow-name", "order-approval",
            "--ruleset-file", missingPath);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Ruleset file not found", result.Combined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ruleset.json", result.Combined, StringComparison.OrdinalIgnoreCase);
    }
}
