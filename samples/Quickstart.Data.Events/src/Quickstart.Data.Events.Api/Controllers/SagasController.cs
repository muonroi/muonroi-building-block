namespace Quickstart.Data.Events.Api.Controllers;

/// <summary>
/// Exercises the saga persistence and outbox modelling provided by
/// Muonroi.Data.EntityFrameworkCore.Events.
///
/// The <see cref="OrderSagaDbContext"/> (an MSagaDbContext) persists IMuonroiSaga
/// state keyed by CorrelationId and stamps CreationTime / LastModificationTime and
/// the ambient TenantId on save. The outbox endpoint shows the EventOutbox entity
/// that MEventOutboxDbContext.SaveWithOutboxAsync writes inside the same transaction
/// as a business change (the canonical transactional-outbox pattern).
/// </summary>
[ApiController]
[Route("api/sagas")]
public class SagasController(OrderSagaDbContext db) : ControllerBase
{
    // ---------------------------------------------------------------------------
    // 1. Start a saga
    //    POST /api/sagas
    //
    //    A new OrderSaga is persisted. MSagaDbContext.SaveChangesAsync stamps
    //    CreationTime/LastModificationTime and injects the ambient TenantId.
    // ---------------------------------------------------------------------------
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Start([FromBody] StartSagaRequest request, CancellationToken token)
    {
        var saga = new OrderSaga
        {
            CorrelationId = Guid.NewGuid(),
            Amount = request.Amount,
            State = "Pending"
        };

        await db.OrderSagas.AddAsync(saga, token);
        await db.SaveChangesAsync(token);

        return Ok(new { saga.CorrelationId, saga.State, saga.Amount, saga.CreationTime, saga.TenantId });
    }

    // ---------------------------------------------------------------------------
    // 2. Advance a saga
    //    POST /api/sagas/{correlationId}/advance
    //
    //    Updates saga state. LastModificationTime is refreshed on save.
    // ---------------------------------------------------------------------------
    [HttpPost("{correlationId:guid}/advance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Advance(Guid correlationId, [FromQuery] string state, CancellationToken token)
    {
        OrderSaga? saga = await db.OrderSagas.FirstOrDefaultAsync(s => s.CorrelationId == correlationId, token);
        if (saga is null)
        {
            return NotFound(new { message = $"Saga {correlationId} not found." });
        }

        saga.State = string.IsNullOrWhiteSpace(state) ? "Completed" : state;
        await db.SaveChangesAsync(token);

        return Ok(new { saga.CorrelationId, saga.State, saga.LastModificationTime });
    }

    // ---------------------------------------------------------------------------
    // 3. List sagas
    //    GET /api/sagas
    // ---------------------------------------------------------------------------
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken token)
    {
        List<OrderSaga> sagas = await db.OrderSagas.ToListAsync(token);
        return Ok(sagas.Select(s => new { s.CorrelationId, s.State, s.Amount, s.CreationTime }));
    }

    // ---------------------------------------------------------------------------
    // 4. Outbox entry shape
    //    GET /api/sagas/outbox-example
    //
    //    Shows the EventOutbox entity that MEventOutboxDbContext.SaveWithOutboxAsync
    //    persists alongside a business change. A relay then publishes Pending rows and
    //    flips Status to Published — guaranteeing at-least-once delivery without 2PC.
    // ---------------------------------------------------------------------------
    [HttpGet("outbox-example")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult OutboxExample()
    {
        var outbox = new EventOutbox
        {
            EventName = "OrderPlaced",
            EventType = typeof(StartSagaRequest).AssemblyQualifiedName,
            EventContent = "{\"amount\":42.0}",
            Status = EventOutboxStatus.Pending
        };

        return Ok(new
        {
            outbox.EventName,
            outbox.EventType,
            outbox.EventContent,
            status = outbox.Status.ToString(),
            statuses = Enum.GetNames<EventOutboxStatus>(),
            note = "Call dbContext.SaveWithOutboxAsync(integrationEvent, jsonService) on an " +
                   "MEventOutboxDbContext to atomically persist this row with your business data."
        });
    }
}

/// <summary>Request body for starting a saga.</summary>
public record StartSagaRequest(decimal Amount);
