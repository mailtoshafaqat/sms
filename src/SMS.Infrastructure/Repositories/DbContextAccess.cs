using Microsoft.EntityFrameworkCore;
using SMS.Infrastructure.Data;

namespace SMS.Infrastructure.Repositories;

internal static class DbContextAccess
{
    public static async Task<T> ReadAsync<T>(
        IDbContextFactory<AppDbContext> factory,
        Func<AppDbContext, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await action(db);
    }

    public static Task<T> ReadOrWriteAsync<T>(
        IDbContextFactory<AppDbContext> factory,
        IScopedDbContextProvider scopedDb,
        bool tracking,
        Func<AppDbContext, Task<T>> action,
        CancellationToken cancellationToken = default) =>
        tracking
            ? action(scopedDb.Context)
            : ReadAsync(factory, action, cancellationToken);
}
