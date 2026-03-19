namespace Muonroi.Core.Abstractions.Models;

/// <summary>
/// Base class for a paged result.
/// </summary>
public abstract class MPagedResultModel
{
    /// <summary>
    /// The current page index.
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    /// The total number of pages.
    /// </summary>
    public int PageCount
    {
        get
        {
            if (PageSize == 0) return 0;
            double pageCount = (double)RowCount / PageSize;
            return (int)Math.Ceiling(pageCount);
        }
    }

    /// <summary>
    /// The page size.
    /// </summary>
    public int PageSize { get; set; }
    /// <summary>
    /// The total row count.
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// The index of the first row on the current page.
    /// </summary>
    public int FirstRowOnPage => (CurrentPage - 1) * PageSize + 1;

    /// <summary>
    /// The index of the last row on the current page.
    /// </summary>
    public int LastRowOnPage => Math.Min(CurrentPage * PageSize, RowCount);

    /// <summary>
    /// Additional data for the paged result.
    /// </summary>
    public string? AdditionalData { get; set; }
}
