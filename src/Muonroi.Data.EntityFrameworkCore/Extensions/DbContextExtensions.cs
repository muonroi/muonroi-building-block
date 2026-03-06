namespace Muonroi.Data.EntityFrameworkCore.Extensions;

public static class DbContextExtensions
{
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
