using Microsoft.EntityFrameworkCore;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Infrastructure.Data;

namespace SMS.Infrastructure.Repositories;

public sealed class UnitOfWork(
    IDbContextFactory<AppDbContext> factory,
    IExceptionLogService exceptionLogService) : IUnitOfWork, IScopedDbContextProvider, IDisposable
{
    private AppDbContext? _context;

    public AppDbContext Context => _context ??= factory.CreateDbContext();

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await Context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw await exceptionLogService.LogAndWrapAsync(ex, "Database.SaveChanges", cancellationToken: cancellationToken);
        }
    }

    public void Dispose() => _context?.Dispose();
}
