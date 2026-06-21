using SMS.Application.DTOs;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Domain.Entities.Attendance;

namespace SMS.Application.Services;

public class BiometricConfigService(
    ISchoolRepository schoolRepository,
    IBiometricDeviceRepository biometricDeviceRepository,
    IBiometricDeviceConnector connector,
    IUnitOfWork unitOfWork) : IBiometricConfigService
{
    public async Task<BiometricDeviceDto?> GetActiveDeviceAsync(CancellationToken cancellationToken = default)
    {
        var device = await biometricDeviceRepository.GetActiveDeviceAsync(cancellationToken: cancellationToken);
        return device is null ? null : Map(device);
    }

    public async Task<IReadOnlyList<BiometricDeviceDto>> GetEnabledDevicesAsync(CancellationToken cancellationToken = default)
    {
        var devices = await biometricDeviceRepository.GetEnabledDevicesAsync(cancellationToken: cancellationToken);
        return devices.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<BiometricDeviceDto>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        var devices = await biometricDeviceRepository.GetAllAsync(cancellationToken: cancellationToken);
        return devices.Select(Map).ToList();
    }

    public async Task<int> SaveDeviceAsync(BiometricDeviceDto dto, CancellationToken cancellationToken = default)
    {
        var school = await schoolRepository.GetFirstAsync(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("School not found.");

        BiometricDevice entity;

        if (dto.Id > 0)
        {
            entity = await biometricDeviceRepository.GetByIdAsync(dto.Id, tracking: true, cancellationToken)
                ?? throw new InvalidOperationException("Device not found.");
        }
        else
        {
            entity = new BiometricDevice { SchoolId = school.Id };
            biometricDeviceRepository.Add(entity);
        }

        entity.Name = dto.Name.Trim();
        entity.IsEnabled = dto.IsEnabled;
        entity.BiometricType = dto.BiometricType;
        entity.ConnectionType = dto.ConnectionType;
        entity.IpAddress = dto.IpAddress?.Trim();
        entity.Port = dto.Port;
        entity.MachineNumber = dto.MachineNumber;
        entity.ComPort = dto.ComPort?.Trim();
        entity.DuplicateScanBlockSeconds = dto.DuplicateScanBlockSeconds;
        entity.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await connector.ReloadAsync(cancellationToken);

        return entity.Id;
    }

    public async Task<bool> TestConnectionAsync(int deviceId, CancellationToken cancellationToken = default)
    {
        var device = await biometricDeviceRepository.GetByIdAsync(deviceId, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Device not found.");

        var connected = await connector.TestConnectionAsync(Map(device), cancellationToken);

        var tracked = await biometricDeviceRepository.GetByIdAsync(deviceId, tracking: true, cancellationToken)
            ?? throw new InvalidOperationException("Device not found.");

        tracked.IsConnected = connected;
        tracked.LastConnectedAt = connected ? DateTime.UtcNow : tracked.LastConnectedAt;
        tracked.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return connected;
    }

    private static BiometricDeviceDto Map(BiometricDevice device) => new()
    {
        Id = device.Id,
        Name = device.Name,
        IsEnabled = device.IsEnabled,
        BiometricType = device.BiometricType,
        ConnectionType = device.ConnectionType,
        IpAddress = device.IpAddress,
        Port = device.Port,
        MachineNumber = device.MachineNumber,
        ComPort = device.ComPort,
        DuplicateScanBlockSeconds = device.DuplicateScanBlockSeconds,
        IsConnected = device.IsConnected,
        LastConnectedAt = device.LastConnectedAt,
        LastScanAt = device.LastScanAt
    };
}

public class DashboardService(
    IAttendanceService attendanceService,
    IBiometricConfigService biometricConfigService) : IDashboardService
{
    public async Task<DashboardStatsDto> GetDashboardAsync(string? userId = null, CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var summary = await attendanceService.GetDailySummaryAsync(today, userId, cancellationToken);
        var logs = await attendanceService.GetRecentLogsAsync(10, today, cancellationToken);
        var devices = await biometricConfigService.GetEnabledDevicesAsync(cancellationToken);

        return new DashboardStatsDto(summary, logs, devices);
    }
}

