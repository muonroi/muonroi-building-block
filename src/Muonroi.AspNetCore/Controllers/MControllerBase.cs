namespace Muonroi.AspNetCore.Controllers;

/// <inheritdoc />
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public abstract class MControllerBase(IMediator mediator, IMLog<MControllerBase> logger) : ControllerBase
{
    /// <summary>
    /// Gets the mediator instance.
    /// </summary>
    protected IMediator Mediator { get; } = mediator;

    /// <summary>
    /// Gets the controller logger.
    /// </summary>
    protected IMLog<MControllerBase> Logger { get; } = logger;

}

