using Microsoft.EntityFrameworkCore;
using Muonroi.Core.Abstractions.Context;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Data.EntityFrameworkCore.Saga;
using Muonroi.Governance.License;
using Muonroi.Logging.Abstractions;
using Muonroi.Mediator.Mediator.Interfaces;

namespace Quickstart.Data.Events.Api.Sagas;

/// <summary>
/// A concrete <see cref="MSagaDbContext"/> for the sample.
///
/// MSagaDbContext is the package's base saga context: it tracks every IMuonroiSaga
/// entity, keys them by CorrelationId, indexes TenantId, and stamps saga timestamps
/// on save. Its constructor requires an IMediator (for domain-event dispatch) and
/// accepts optional license/logging/time/execution-context collaborators.
///
/// Registered via AddMuonroiSagaDbContext&lt;OrderSagaDbContext&gt;() — the package's
/// primary saga registration extension — in Program.cs.
/// </summary>
public class OrderSagaDbContext(
    DbContextOptions options,
    IMediator mediator,
    ILicenseGuard? licenseGuard = null,
    IMLog<Muonroi.Data.EntityFrameworkCore.Entity.MDbContext>? logger = null,
    IMDateTimeService? dateTimeService = null,
    ISystemExecutionContextAccessor? executionContextAccessor = null)
    : MSagaDbContext(options, mediator, licenseGuard, logger, dateTimeService, executionContextAccessor)
{
    /// <summary>Gets the order sagas set.</summary>
    public DbSet<OrderSaga> OrderSagas => Set<OrderSaga>();
}
