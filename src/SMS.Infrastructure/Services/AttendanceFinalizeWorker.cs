using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;

namespace SMS.Infrastructure.Services;

public class AttendanceFinalizeWorker(
    IServiceProvider services,
    ILogger<AttendanceFinalizeWorker> logger) : BackgroundService
{
    private DateOnly? _lastFinalizedDate;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var schoolRepository = scope.ServiceProvider.GetRequiredService<ISchoolRepository>();
                var attendanceService = scope.ServiceProvider.GetRequiredService<IAttendanceService>();
                var school = await schoolRepository.GetFirstAsync(cancellationToken: stoppingToken);
                if (school is not null)
                {
                    var now = DateTime.Now;
                    var today = DateOnly.FromDateTime(now);
                    var currentTime = TimeOnly.FromDateTime(now);
                    if (currentTime >= school.SchoolEndTime && _lastFinalizedDate != today)
                    {
                        await attendanceService.FinalizeDailyAttendanceAsync(today, userId: null, stoppingToken);
                        _lastFinalizedDate = today;
                        logger.LogInformation("Auto-finalized attendance for {Date} at {Time}", today, currentTime);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Attendance auto-finalize failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
