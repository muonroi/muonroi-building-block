namespace TestProject.Aggregate.Sites.Alpha;

/// <summary>
/// Alpha site container handler — overrides HandleAsync with Alpha-specific logic.
/// </summary>
public sealed class AlphaContainerHandler : ContainerHandlerBase
{
    public override Task<ContainerResult> HandleAsync(
        string containerNo, string isoCode, CancellationToken ct = default)
        => Task.FromResult(new ContainerResult
        {
            Success = true,
            Message = $"ALPHA-Handled {containerNo}"
        });
}
