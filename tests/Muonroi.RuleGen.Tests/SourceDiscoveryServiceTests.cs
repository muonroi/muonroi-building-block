namespace Muonroi.RuleGen.Tests;

public sealed class SourceDiscoveryServiceTests
{
    [Fact]
    public void Discover_WithMissingProject_Throws()
    {
        string root = CreateTempDirectory();

        try
        {
            Action act = () => SourceDiscoveryService.Discover(
                root,
                source: null,
                sourceDir: null,
                projectPath: "missing.csproj",
                includePattern: "**/*.cs",
                excludes: []);

            act.Should().Throw<FileNotFoundException>();
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void Discover_FromDirectory_Applies_Exclude_Patterns_And_Ignores_Generated_Files()
    {
        string root = CreateTempDirectory();
        string sourceDir = Path.Combine(root, "src");
        Directory.CreateDirectory(sourceDir);
        File.WriteAllText(Path.Combine(sourceDir, "Keep.cs"), "class Keep {}");
        File.WriteAllText(Path.Combine(sourceDir, "Ignore.Generated.cs"), "class IgnoreGenerated {}");
        File.WriteAllText(Path.Combine(sourceDir, "SkipMe.cs"), "class SkipMe {}");

        try
        {
            IReadOnlyList<string> files = SourceDiscoveryService.Discover(
                root,
                source: null,
                sourceDir: sourceDir,
                projectPath: null,
                includePattern: "**/*.cs",
                excludes: ["**/Skip*.cs"]);

            files.Should().ContainSingle();
            files[0].Should().EndWith("Keep.cs");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void Discover_FromProject_Falls_Back_To_Directory_When_No_Compile_Items_Exist()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "Sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        File.WriteAllText(Path.Combine(root, "RuleA.cs"), "class RuleA {}");

        try
        {
            IReadOnlyList<string> files = SourceDiscoveryService.Discover(
                root,
                source: null,
                sourceDir: null,
                projectPath: "Sample.csproj",
                includePattern: "**/*.cs",
                excludes: []);

            files.Should().ContainSingle();
            files[0].Should().EndWith("RuleA.cs");
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void Discover_WithExplicitFile_Returns_File()
    {
        string root = CreateTempDirectory();
        string filePath = Path.Combine(root, "Rule.cs");
        File.WriteAllText(filePath, "class Rule {}");

        try
        {
            IReadOnlyList<string> files = SourceDiscoveryService.Discover(
                root,
                source: filePath,
                sourceDir: null,
                projectPath: null,
                includePattern: "**/*.cs",
                excludes: []);

            files.Should().Equal(filePath);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-source", Guid.NewGuid().ToString("N"));
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
