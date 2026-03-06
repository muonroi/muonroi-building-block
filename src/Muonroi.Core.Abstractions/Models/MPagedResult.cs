namespace Muonroi.Core.Abstractions.Models;

public class MPagedResult<T> : MPagedResultModel where T : class
{
    public IEnumerable<T> Items { get; set; } = [];
}
