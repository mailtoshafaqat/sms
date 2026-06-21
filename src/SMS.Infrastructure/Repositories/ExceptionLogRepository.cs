using Microsoft.EntityFrameworkCore;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Shared;
using SMS.Infrastructure.Data;

namespace SMS.Infrastructure.Repositories;

public class ExceptionLogRepository(IDbContextFactory<AppDbContext> factory) : IExceptionLogRepository
{
    public async Task<int> AddAndSaveAsync(AppExceptionLog log, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.AppExceptionLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
        return log.Id;
    }

    public async Task<AppExceptionLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.AppExceptionLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<AppExceptionLog> Items, int TotalCount)> GetPagedAsync(
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = db.AppExceptionLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                x.Message.Contains(term)
                || x.Source.Contains(term)
                || (x.UserEmail != null && x.UserEmail.Contains(term))
                || (x.ConstraintName != null && x.ConstraintName.Contains(term))
                || x.Id.ToString() == term);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
