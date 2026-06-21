using Microsoft.EntityFrameworkCore;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Enums;
using SMS.Infrastructure.Data;

namespace SMS.Infrastructure.Repositories;

public class LocalBiometricRepository(
    IDbContextFactory<AppDbContext> factory,
    IScopedDbContextProvider scopedDb) : ILocalBiometricRepository
{
    public Task<StudentLocalTemplate?> GetTemplateAsync(
        int studentId,
        BiometricType type,
        bool tracking = false,
        CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.StudentLocalTemplates.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.StudentId == studentId && x.BiometricType == type, cancellationToken);
            },
            cancellationToken);

    public Task<StudentLocalTemplate?> GetByExternalIdAsync(
        string externalId,
        bool tracking = false,
        CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.StudentLocalTemplates.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.ExternalId == externalId, cancellationToken);
            },
            cancellationToken);

    public Task<IReadOnlyList<StudentLocalTemplate>> GetFaceTemplatesAsync(CancellationToken cancellationToken = default) =>
        GetTemplatesByTypeAsync(BiometricType.Face, cancellationToken);

    public Task<IReadOnlyList<StudentLocalTemplate>> GetTemplatesByTypeAsync(
        BiometricType type,
        CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadAsync(
            factory,
            async db => (IReadOnlyList<StudentLocalTemplate>)await db.StudentLocalTemplates.AsNoTracking()
                .Where(x => x.BiometricType == type)
                .ToListAsync(cancellationToken),
            cancellationToken);

    public void Add(StudentLocalTemplate template) => scopedDb.Context.StudentLocalTemplates.Add(template);

    public void Remove(StudentLocalTemplate template) => scopedDb.Context.StudentLocalTemplates.Remove(template);
}
