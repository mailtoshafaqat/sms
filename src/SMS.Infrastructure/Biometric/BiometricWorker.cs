using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMS.Application.Interfaces;
using SMS.Domain.Common;
using SMS.Infrastructure.Biometric;

namespace SMS.Infrastructure.Biometric;

/// <summary>
/// Placeholder connector until ZKTeco SDK is wired for your device model.
/// Replace TestConnection/scan handling with vendor SDK calls.
/// </summary>
public class SimulatedBiometricConnector(
    ILogger<SimulatedBiometricConnector> logger) : IBiometricDeviceConnector
{
    public Task<bool> TestConnectionAsync(Application.DTOs.BiometricDeviceDto device, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Testing {BiometricType} device {DeviceName} ({ConnectionType})",
            BiometricTypeRules.GetDisplayName(device.BiometricType),
            device.Name,
            device.ConnectionType);
        return Task.FromResult(device.IsEnabled);
    }

    public Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Biometric connector configuration reloaded.");
        return Task.CompletedTask;
    }
}

public class BiometricWorker(
    ILogger<BiometricWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Biometric worker started. Connect ZKTeco SDK in SimulatedBiometricConnector.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}

