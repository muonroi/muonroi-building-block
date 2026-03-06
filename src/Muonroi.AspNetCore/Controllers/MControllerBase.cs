

using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.AspNetCore.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public abstract class MControllerBase(IMediator mediator, ILogger logger) : ControllerBase
{
    protected IMediator Mediator { get; } = mediator;

    protected ILogger Logger { get; } = logger;

}

