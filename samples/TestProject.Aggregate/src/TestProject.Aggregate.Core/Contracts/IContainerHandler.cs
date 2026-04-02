namespace TestProject.Aggregate.Core.Contracts;

/// <summary>
/// Per-site handler for container aggregate operations.
/// Each site registers its own implementation as a keyed service.
/// </summary>
public interface IContainerHandler
{
    Task<ContainerResult> HandleAsync(string containerNo, string isoCode, CancellationToken ct = default);
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
