namespace TestProject.Aggregate.Core.Contracts;

/// <summary>
/// Per-site handler for container aggregate operations.
/// Each site registers its own implementation as a keyed service.
/// </summary>
public interface IContainerHandler
{
    /// <summary>
    /// Handles container-related operations, such as validation or status updates, based on the provided container number and ISO code.
    /// </summary>
    /// <param name="containerNo"></param>
    /// <param name="isoCode"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<ContainerResult> HandleAsync(string containerNo, string isoCode, CancellationToken ct = default);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="billNo"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<ContainerListResult> ListAsync(string billNo, CancellationToken ct = default);
}

public class ContainerResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public class ContainerListResult
{
    public List<ContainerItem> Items { get; set; } = [];
    public int Total { get; set; }
}

public record ContainerItem(string ContainerNo, string IsoCode, string Status);
