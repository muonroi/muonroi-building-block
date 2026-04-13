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
        string repoRoot = CliProcess.GetRepoRoot();
        string root = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        string sourceDir = Path.Combine(root, "src");
        Directory.CreateDirectory(sourceDir);

        string csproj = Path.Combine(root, "Sample.csproj");
        string abstractionsCsproj = Path.Combine(repoRoot, "src", "Muonroi.RuleEngine.Abstractions", "Muonroi.RuleEngine.Abstractions.csproj");

        string projectXml = $$"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="{{abstractionsCsproj}}" />
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
