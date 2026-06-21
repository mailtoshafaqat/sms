using Microsoft.EntityFrameworkCore;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Common;
using SMS.Domain.Entities.Attendance;
using SMS.Domain.Enums;
using SMS.Infrastructure.Data;

namespace SMS.Infrastructure.Repositories;

public class BiometricDeviceRepository(
    IDbContextFactory<AppDbContext> factory,
    IScopedDbContextProvider scopedDb) : IBiometricDeviceRepository
{
    public Task<BiometricDevice?> GetActiveDeviceAsync(bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.BiometricDevices.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.IsEnabled, cancellationToken);
            },
            cancellationToken);

    public Task<IReadOnlyList<BiometricDevice>> GetEnabledDevicesAsync(bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.BiometricDevices.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return (IReadOnlyList<BiometricDevice>)await query
                    .Where(x => x.IsEnabled)
                    .OrderBy(x => x.Name)
                    .ToListAsync(cancellationToken);
            },
            cancellationToken);

    public Task<IReadOnlyList<BiometricDevice>> GetAllAsync(bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.BiometricDevices.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return (IReadOnlyList<BiometricDevice>)await query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
            },
            cancellationToken);

    public Task<BiometricDevice?> GetByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.BiometricDevices.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            },
            cancellationToken);

    public Task<BiometricDevice?> GetEnabledDeviceForTypeAsync(
        BiometricType type,
        bool tracking = false,
        CancellationToken cancellationToken = default) =>
        DbContextAccess.ReadOrWriteAsync(
            factory,
            scopedDb,
            tracking,
            async db =>
            {
                var query = db.BiometricDevices.AsQueryable();
                if (!tracking)
                {
                    query = query.AsNoTracking();
                }

                var devices = await query
                    .Where(x => x.IsEnabled)
                    .ToListAsync(cancellationToken);

                return devices
                    .Where(x => BiometricTypeRules.Supports(x.BiometricType, type))
                    .OrderBy(x => x.BiometricType == type ? 0 : 1)
                    .ThenBy(x => x.Name)
                    .FirstOrDefault();
            },
            cancellationToken);

    public void Add(BiometricDevice device) => scopedDb.Context.BiometricDevices.Add(device);
}
