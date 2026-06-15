using System.Text;

namespace Muonroi.RuleGen.Tests;

internal sealed class TempProjectContext : IDisposable
{
    public string RootPath { get; }
    public string ProjectPath { get; }
    public string HandlerPath { get; }
    public string RuntimeRulePath { get; }

    private TempProjectContext(string rootPath, string projectPath, string handlerPath, string runtimeRulePath)
    {
        RootPath = rootPath;
        ProjectPath = projectPath;
        HandlerPath = handlerPath;
        RuntimeRulePath = runtimeRulePath;
    }

    public static TempProjectContext Create(string handlerContent, string runtimeRuleJson)
    {
        string root = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        string sourceDir = Path.Combine(root, "src");
        Directory.CreateDirectory(sourceDir);

        string csproj = Path.Combine(root, "Sample.csproj");

        // Reference the already-built Muonroi assemblies from this test assembly's output directory
        // instead of a <ProjectReference> to the in-repo Muonroi.RuleEngine.Abstractions.csproj.
        // A ProjectReference makes the spawned `dotnet build` (compile-check) rebuild the shared
        // in-repo src/*/obj, which races other concurrent builds during a full-solution `dotnet test`
        // and fails non-deterministically with CS0006/CS2001 on the regenerated ref assemblies. The
        // prebuilt DLLs are copied here transitively, so a binary <Reference> gives the compile-check
        // the same API surface without touching any shared on-disk build artifact.
        string referenceDir = AppContext.BaseDirectory;
        IEnumerable<string> referenceItems = Directory
            .EnumerateFiles(referenceDir, "Muonroi.*.dll")
            .Select(dll =>
                $"    <Reference Include=\"{Path.GetFileNameWithoutExtension(dll)}\"><HintPath>{dll}</HintPath></Reference>");
        string references = string.Join(Environment.NewLine, referenceItems);

        string projectXml = $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
{{references}}
  </ItemGroup>
</Project>
""";
        File.WriteAllText(csproj, projectXml);

        string handlerPath = Path.Combine(sourceDir, "OrderHandler.cs");
        File.WriteAllText(handlerPath, handlerContent);

        string ruleJsonPath = Path.Combine(root, "runtime-rules.json");
        File.WriteAllText(ruleJsonPath, runtimeRuleJson);

        return new TempProjectContext(root, csproj, handlerPath, ruleJsonPath);
    }

    public string PathInRoot(string relative)
    {
        return Path.Combine(RootPath, relative);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
            // ignore cleanup errors in tests
        }
    }
}
