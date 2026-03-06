using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace Muonroi.RuleGen.VisualStudio;

public sealed class RuleGenOptionsPage : DialogPage
{
    [Category("Muonroi RuleGen")]
    [DisplayName("Executable Path")]
    [Description("Optional full path to muonroi-rule executable or dll. Leave empty to auto-detect from dotnet tool or repo source.")]
    public string ExecutablePath { get; set; } = string.Empty;

    [Category("Muonroi RuleGen")]
    [DisplayName("Default Extra Args")]
    [Description("Optional extra arguments appended to every command, e.g. --config .rulegenrc.json")]
    public string DefaultExtraArgs { get; set; } = string.Empty;

    [Category("Muonroi RuleGen")]
    [DisplayName("Source Project Path")]
    [Description("Optional full path to Muonroi.RuleGen.csproj used as fallback when packaged tool is unavailable.")]
    public string SourceProjectPath { get; set; } = string.Empty;

    [Category("Muonroi RuleGen")]
    [DisplayName("Enable Source Project Fallback")]
    [Description("When true, VSIX can fallback to 'dotnet run --project tools/Muonroi.RuleGen/Muonroi.RuleGen.csproj' if packaged tool is not available.")]
    public bool EnableSourceProjectFallback { get; set; } = true;
}
