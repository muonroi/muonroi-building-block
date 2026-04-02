using TestProject.Aggregate.Core.Contracts;
using TestProject.Aggregate.Core.Handlers;

namespace TestProject.Aggregate.Sites.Bravo;

/// <summary>
/// Bravo site container handler — overrides both HandleAsync and ListAsync with Bravo-specific logic.
/// </summary>
public sealed class BravoContainerHandler : ContainerHandlerBase
{
    public override Task<ContainerResult> HandleAsync(
        string containerNo, string isoCode, CancellationToken ct = default)
        => Task.FromResult(new ContainerResult
        {
            Success = true,
            Message = $"BRAVO-Handled {containerNo}"
        });

    public override Task<ContainerListResult> ListAsync(
        string billNo, CancellationToken ct = default)
        => Task.FromResult(new ContainerListResult
        {
            Items =
            [
                new ContainerItem("BRAVO001", "22G1", "Active"),
                new ContainerItem("BRAVO002", "45G1", "InTransit"),
                new ContainerItem("BRAVO003", "20RF", "Pending")
            ],
            Total = 3
        });
}
