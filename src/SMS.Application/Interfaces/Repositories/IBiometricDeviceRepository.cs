using SMS.Domain.Entities.Attendance;
using SMS.Domain.Enums;

namespace SMS.Application.Interfaces.Repositories;

public interface IBiometricDeviceRepository
{
    Task<BiometricDevice?> GetActiveDeviceAsync(bool tracking = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BiometricDevice>> GetEnabledDevicesAsync(bool tracking = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BiometricDevice>> GetAllAsync(bool tracking = false, CancellationToken cancellationToken = default);
    Task<BiometricDevice?> GetByIdAsync(int id, bool tracking = false, CancellationToken cancellationToken = default);
    Task<BiometricDevice?> GetEnabledDeviceForTypeAsync(BiometricType type, bool tracking = false, CancellationToken cancellationToken = default);
    void Add(BiometricDevice device);
}

