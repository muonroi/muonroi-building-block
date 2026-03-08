

using Muonroi.Mediator.Mediator.Interfaces;

namespace Muonroi.AspNetCore.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public abstract class MControllerBase(IMediator mediator, IMLog<MControllerBase> logger) : ControllerBase
{
    protected IMediator Mediator { get; } = mediator;

    protected IMLog<MControllerBase> Logger { get; } = logger;

}

