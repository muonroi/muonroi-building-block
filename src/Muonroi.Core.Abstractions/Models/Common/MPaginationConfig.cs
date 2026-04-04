namespace Muonroi.Core.Abstractions.Models.Common;

/// <summary> Represents the configuration for pagination. </summary>
public class MPaginationConfig(string sectionName = "PaginationConfigs")
{
    /// <summary> Gets the name of the configuration section. </summary>
    public string SectionName { get; } = sectionName;

    /// <summary> Gets or sets the default index of the page. </summary>
    public int DefaultPageIndex { get; set; }

    /// <summary> Gets or sets the default size of the page. </summary>
    public int DefaultPageSize { get; set; }

    /// <summary> Gets or sets the maximum allowed size of the page. </summary>
    public int MaxPageSize { get; set; }
}
