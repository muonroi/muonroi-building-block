using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Data.Abstractions.Entities;
using Muonroi.Mapping.Abstractions;

namespace Muonroi.Services.Abstractions;

/// <summary>
/// Base service operating on generic entity/DTO types.
/// Core provides shared CRUD logic. Site overrides only what's different via virtual hooks.
///
/// KEY DESIGN: Data access via DbContext.Set&lt;TEntity&gt;() — never concrete DbSet property names.
/// This works because EF resolves Set&lt;T&gt;() to the correct table at runtime.
///
/// Extension points (override these, NOT the CRUD methods):
/// - BeforeCreate/AfterCreate — pre/post create hooks
/// - BeforeUpdate/AfterUpdate — pre/post update hooks
/// - ValidateAsync — business validation before save
/// - ApplyDefaultValues — set site-specific defaults on new entities
/// </summary>
/// <remarks>
/// Initialize with DbContext and mapper.
/// </remarks>
public abstract class MServiceBase<TEntity, TDto>(DbContext context, IEntityMapper<TEntity, TDto> mapper)
    where TEntity : class, IEntityBase
    where TDto : class
{
    /// <summary>
    /// The DbContext for data access. Use Set&lt;TEntity&gt;() for queries.
    /// </summary>
    protected readonly DbContext Context = MGuard.NotNull(context);

    /// <summary>
    /// Entity-DTO mapper. Core maps shared fields, site overrides MapSiteSpecific.
    /// </summary>
    protected readonly IEntityMapper<TEntity, TDto> Mapper = MGuard.NotNull(mapper);

    /// <summary>
    /// Get entity by primary key and map to DTO.
    /// </summary>
    public virtual async Task<TDto?> GetByIdAsync<TKey>(TKey id, CancellationToken ct = default)
    {
        var entity = await Context.Set<TEntity>().FindAsync([id], ct);
        return entity is null ? null : Mapper.ToDto(entity);
    }

    /// <summary>
    /// Get entities matching predicate and map to DTOs.
    /// </summary>
    public virtual async Task<List<TDto>> GetByConditionAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
        var entities = await Context.Set<TEntity>()
            .Where(predicate)
            .ToListAsync(ct);
        return entities.Select(Mapper.ToDto).ToList();
    }

    /// <summary>
    /// Create entity from DTO. Calls hooks: ValidateAsync -> ApplyDefaultValues -> BeforeCreate -> save -> AfterCreate.
    /// </summary>
    public virtual async Task<TDto> CreateAsync(TDto dto, CancellationToken ct = default)
    {
        var entity = Mapper.ToEntity(dto);
        await ValidateAsync(entity, ct);
        ApplyDefaultValues(entity);
        await BeforeCreate(entity, ct);
        Context.Set<TEntity>().Add(entity);
        await Context.SaveChangesAsync(ct);
        await AfterCreate(entity, ct);
        return Mapper.ToDto(entity);
    }

    /// <summary>
    /// Update existing entity from DTO. Calls hooks: ValidateAsync -> BeforeUpdate -> save -> AfterUpdate.
    /// </summary>
    public virtual async Task<TDto> UpdateAsync(TEntity entity, TDto dto, CancellationToken ct = default)
    {
        Mapper.ApplyUpdate(entity, dto);
        await ValidateAsync(entity, ct);
        await BeforeUpdate(entity, ct);
        Context.Set<TEntity>().Update(entity);
        await Context.SaveChangesAsync(ct);
        await AfterUpdate(entity, ct);
        return Mapper.ToDto(entity);
    }

    /// <summary>
    /// Delete entity.
    /// </summary>
    public virtual async Task<bool> DeleteAsync(TEntity entity, CancellationToken ct = default)
    {
        Context.Set<TEntity>().Remove(entity);
        var affected = await Context.SaveChangesAsync(ct);
        return affected > 0;
    }

    // ═══════════════════════════════════════════════
    // HOOK METHODS — Site overrides THESE
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Validate entity before create or update.
    /// Override to add site-specific validation rules.
    /// Throw exception or use result pattern to reject invalid entities.
    /// Default: no-op (all entities valid).
    /// </summary>
    protected virtual Task ValidateAsync(TEntity entity, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Apply default values to new entities.
    /// Override for site-specific defaults (e.g., OperationMethod, Category, Status).
    /// Called after mapper but before BeforeCreate.
    /// Default: no-op.
    /// </summary>
    protected virtual void ApplyDefaultValues(TEntity entity) { }

    /// <summary>
    /// Hook called before entity is added to DbSet.
    /// Override for pre-save enrichment (e.g., generate codes, resolve references).
    /// Default: no-op.
    /// </summary>
    protected virtual Task BeforeCreate(TEntity entity, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Hook called after entity is saved (post-commit).
    /// Override for side effects (e.g., send notification, publish event).
    /// Default: no-op.
    /// </summary>
    protected virtual Task AfterCreate(TEntity entity, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Hook called before entity is updated.
    /// Override for pre-update enrichment or change detection.
    /// Default: no-op.
    /// </summary>
    protected virtual Task BeforeUpdate(TEntity entity, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Hook called after entity update is saved.
    /// Override for post-update side effects.
    /// Default: no-op.
    /// </summary>
    protected virtual Task AfterUpdate(TEntity entity, CancellationToken ct) => Task.CompletedTask;
}
