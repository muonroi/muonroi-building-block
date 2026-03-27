using FluentAssertions;
using Muonroi.RuleGen.Services;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public sealed class CompileCheckServiceTests
{
    [Fact]
    public void DiscoverNearestCompileTarget_Returns_Closest_Project_Before_Root_Solution()
    {
        string root = CreateTempDirectory();
        string nested = Path.Combine(root, "src", "feature");
        Directory.CreateDirectory(nested);
        string solutionPath = Path.Combine(root, "Sample.sln");
        string projectPath = Path.Combine(root, "src", "Sample.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(solutionPath, string.Empty);
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");

        try
        {
            string? result = CompileCheckService.DiscoverNearestCompileTarget(nested);
            result.Should().Be(projectPath);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task BuildAsync_WithMissingTarget_Throws()
    {
        Func<Task> act = () => CompileCheckService.BuildAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.csproj"));
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task BuildAsync_Builds_Minimal_Project_Successfully()
    {
        string root = CreateTempDirectory();
        string projectPath = Path.Combine(root, "Sample.csproj");
        string programPath = Path.Combine(root, "Program.cs");

        File.WriteAllText(
            projectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(programPath, "public static class Program { public static void Main() { } }");

        try
        {
            var result = await CompileCheckService.BuildAsync(projectPath);

            result.Success.Should().BeTrue();
            result.ExitCode.Should().Be(0);
            result.TargetPath.Should().Be(projectPath);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-compile", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
