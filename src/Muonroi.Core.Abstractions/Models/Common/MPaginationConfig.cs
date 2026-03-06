namespace Muonroi.Core.Abstractions.Models.Common;

public class MPaginationConfig(string sectionName = "PaginationConfigs")
{
    public string SectionName { get; } = sectionName;
    public int DefaultPageIndex { get; set; }
    public int DefaultPageSize { get; set; }
    public int MaxPageSize { get; set; }
}
