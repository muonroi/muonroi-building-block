namespace Muonroi.Core.Abstractions.Models;

/// <summary>
/// Represents a paged result.
/// </summary>
/// <typeparam name="T">The type of items in the result.</typeparam>
public class MPagedResult<T> : MPagedResultModel where T : class
{
    /// <summary>
    /// The items on the current page.
    /// </summary>
    public IEnumerable<T> Items { get; set; } = [];
}
