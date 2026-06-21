using SMS.Domain.Entities.Shared;

namespace SMS.Application.Interfaces.Repositories;

public interface IStudentPromotionRepository
{
    void Add(StudentPromotion promotion);

    Task<IReadOnlyList<StudentPromotion>> GetRecentAsync(int take = 20, CancellationToken cancellationToken = default);
}
