namespace Muonroi.Data.EntityFrameworkCore.Extensions;

/// <summary>
/// Provides helper extensions for <see cref="DbContext"/>.
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Inserts a set of entities in a transaction-friendly manner.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="dbContext">The database context.</param>
    /// <param name="entities">Entities to insert.</param>
    public static async Task BulkInsertAsync<T>(this DbContext dbContext, IEnumerable<T>? entities) where T : class
    {
        if (entities != null)
        {
            List<T> enumerable = [.. entities];
            if (enumerable.Count == 0)
            {
                return;
            }

            if (dbContext.Database.IsInMemory())
            {
                await dbContext.Set<T>().AddRangeAsync(enumerable).ConfigureAwait(false);
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                return;
            }

            await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync().ConfigureAwait(false);
            try
            {
                await dbContext.Set<T>().AddRangeAsync(enumerable).ConfigureAwait(false);
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                await transaction.CommitAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}
