using SMS.Infrastructure.Data;

namespace SMS.Infrastructure.Repositories;

public interface IScopedDbContextProvider
{
    AppDbContext Context { get; }
}
