using System.Diagnostics;
using System.Reflection;

namespace TestProject.Aggregate.IntegrationTests;

/// <summary>
/// Integration tests verifying that Bravo's per-site proto compiles correctly
/// with shared.proto import resolved via Muonroi.Tenancy.SiteProfile.Grpc.targets.
///
/// Covers:
///   GRPC-02 — per-site proto with shared.proto import compiles successfully
///   TOOL-01 — .targets auto-configures AdditionalImportDirs without manual csproj setup
/// </summary>
public sealed class ProtoCompilationTests
{
    // Navigate from the test DLL location (bin/Debug/net8.0/) up to Bravo project.
    // Test assembly: tests/TestProject.Aggregate.IntegrationTests/bin/Debug/net8.0/
    // Bravo:         tests/TestProject.Aggregate/src/TestProject.Aggregate.Sites.Bravo/
    private static string BravoCsprojPath { get; } = Path.GetFullPath(
        Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
            "../../../../TestProject.Aggregate/src/TestProject.Aggregate.Sites.Bravo/TestProject.Aggregate.Sites.Bravo.csproj"
        )
    );

    private static string BravoProjectDir { get; } = Path.GetDirectoryName(BravoCsprojPath)!;

    /// <summary>
    /// GRPC-02: Build Sites.Bravo (which imports shared.proto). Assert exit code 0.
    /// This proves the per-site proto with shared import compiles end-to-end.
    /// </summary>
    [Fact]
    public void BravoProto_CompilesSuccessfully_WithSharedImport()
    {
        // Arrange
        BravoCsprojPath.Should().NotBeNullOrEmpty("Bravo csproj path must be resolvable");
        File.Exists(BravoCsprojPath).Should().BeTrue($"Bravo csproj must exist at: {BravoCsprojPath}");

        // Act
        var (exitCode, output) = RunDotnetBuild(BravoCsprojPath);

        // Assert
        exitCode.Should().Be(0,
            because: $"dotnet build should succeed for Sites.Bravo. Build output:\n{output}");

        // Build output should not contain build errors (warnings are OK)
        var errorLines = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line =>
                line.Contains(": error ", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Build FAILED", StringComparison.OrdinalIgnoreCase))
            .ToList();

        errorLines.Should().BeEmpty(
            because: $"Build output should have no errors. Found:\n{string.Join('\n', errorLines)}");
    }

    /// <summary>
    /// GRPC-02: After building Bravo, verify that generated C# in obj/ contains "SharedContainerInfo".
    /// This confirms the proto compiler resolved shared.proto and generated the message type.
    /// </summary>
    [Fact]
    public void BravoProto_GeneratedCSharp_ContainsSharedContainerInfo()
    {
        // Ensure Bravo is built first
        var (exitCode, buildOutput) = RunDotnetBuild(BravoCsprojPath);
        exitCode.Should().Be(0, because: $"Bravo must build successfully before checking generated output.\n{buildOutput}");

        // Scan obj/ directory recursively for generated .cs files containing SharedContainerInfo
        var objDir = Path.Combine(BravoProjectDir, "obj");
        Directory.Exists(objDir).Should().BeTrue($"obj/ directory must exist after build: {objDir}");

        var generatedFiles = Directory.GetFiles(objDir, "*.cs", SearchOption.AllDirectories);
        generatedFiles.Should().NotBeEmpty("Bravo build should produce generated .cs files in obj/");

        var filesWithSharedContainerInfo = generatedFiles
            .Where(f => File.ReadAllText(f).Contains("SharedContainerInfo", StringComparison.Ordinal))
            .ToList();

        filesWithSharedContainerInfo.Should().NotBeEmpty(
            because: "At least one generated .cs file in Bravo's obj/ must reference 'SharedContainerInfo'. " +
                     $"Scanned {generatedFiles.Length} files in: {objDir}");
    }

    /// <summary>
    /// TOOL-01: The Bravo csproj must NOT contain "AdditionalImportDirs" — the .targets
    /// auto-configures this via ItemDefinitionGroup, so no manual setup is needed.
    /// </summary>
    [Fact]
    public void BravoCsproj_HasNoManualAdditionalImportDirs()
    {
        File.Exists(BravoCsprojPath).Should().BeTrue($"Bravo csproj must exist at: {BravoCsprojPath}");

        var csprojContent = File.ReadAllText(BravoCsprojPath);

        csprojContent.Should().NotContain(
            "AdditionalImportDirs",
            because: "Muonroi.Tenancy.SiteProfile.Grpc.targets auto-configures AdditionalImportDirs via " +
                     "ItemDefinitionGroup — the consumer project must not set it manually (TOOL-01)");
    }

    /// <summary>
    /// TOOL-01: Build output for Sites.Bravo must not contain proto file resolution errors.
    /// This confirms MuonroiSharedProtoDir was resolved correctly by the .targets auto-config.
    /// </summary>
    [Fact]
    public void BravoBuild_NoProtoFileNotFoundErrors()
    {
        // Act
        var (exitCode, output) = RunDotnetBuild(BravoCsprojPath);

        // Assert: no proto-related "file not found" errors
        var protoNotFoundPatterns = new[]
        {
            "Could not find file",
            "not found: shared.proto",
            "proto file not found",
            "Import 'shared.proto' was not found"
        };

        foreach (var pattern in protoNotFoundPatterns)
        {
            output.Should().NotContain(
                pattern,
                because: $"Build output must not contain '{pattern}' — " +
                         "this would indicate .targets failed to resolve the shared.proto import path. " +
                         $"Build output:\n{output}");
        }

        // Build must succeed regardless
        exitCode.Should().Be(0,
            because: $"Bravo must build without proto import errors. Build output:\n{output}");
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Runs `dotnet build` on the given csproj and returns (exitCode, combinedOutput).
    /// </summary>
    private static (int exitCode, string output) RunDotnetBuild(string csprojPath)
    {
        var psi = new ProcessStartInfo("dotnet", $"build \"{csprojPath}\" --no-incremental -v:minimal")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet process");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        var combined = stdout + "\n" + stderr;
        return (process.ExitCode, combined);
    }
}
