using Microsoft.EntityFrameworkCore;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Shared;
using SMS.Infrastructure.Data;

namespace SMS.Infrastructure.Repositories;

public class StudentPromotionRepository(
    IDbContextFactory<AppDbContext> factory,
    IScopedDbContextProvider scopedDb) : IStudentPromotionRepository
{
    public void Add(StudentPromotion promotion) => scopedDb.Context.StudentPromotions.Add(promotion);

    public Task<IReadOnlyList<StudentPromotion>> GetRecentAsync(int take = 20, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<StudentPromotion>)await db.StudentPromotions.AsNoTracking()
                .Include(x => x.Student)
                .Include(x => x.FromSection)
                .ThenInclude(x => x.ClassRoom)
                .Include(x => x.ToSection)
                .ThenInclude(x => x.ClassRoom)
                .OrderByDescending(x => x.PromotedAt)
                .Take(take)
                .ToListAsync(cancellationToken),
            cancellationToken);
}
