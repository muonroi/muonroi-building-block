using TestProject.Aggregate.Core.Contracts;

namespace TestProject.Aggregate.Core.Handlers;

/// <summary>
/// Abstract base for per-site container handlers.
/// Sites override to provide site-specific behavior.
/// </summary>
public abstract class ContainerHandlerBase : IContainerHandler
{
    public virtual Task<ContainerResult> HandleAsync(string containerNo, string isoCode, CancellationToken ct = default)
        => Task.FromResult(new ContainerResult { Success = true, Message = $"Handled {containerNo}" });

    public virtual Task<ContainerListResult> ListAsync(string billNo, CancellationToken ct = default)
        => Task.FromResult(new ContainerListResult
        {
            Items = [new ContainerItem("SAMPLE01", "22G1", "Active")],
            Total = 1
        });
}
