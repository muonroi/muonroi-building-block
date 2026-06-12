using System;

namespace Muonroi.RuleGen.VisualStudio;

internal static class PackageIds
{
    public const string PackageGuidString = "4A57B18C-56CC-40A4-A628-E4C020C93EC6";
    public const string CommandSetGuidString = "E3BC86C2-67AE-4D5B-8372-BFEBC8E78D5B";
    public const string OutputPaneGuidString = "8F030849-03C5-4902-A31A-7214D2E2D6A8";

    public static readonly Guid CommandSetGuid = Guid.Parse(CommandSetGuidString);
    public static readonly Guid OutputPaneGuid = Guid.Parse(OutputPaneGuidString);

    public const int ExtractCommandId = 0x0100;
    public const int MergeCommandId = 0x0101;
    public const int WatchCommandId = 0x0102;
    public const int StopWatchCommandId = 0x0103;
}
