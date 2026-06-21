using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using SMS.Application.Services;

namespace SMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISchoolService, SchoolService>();
        services.AddScoped<IClassService, ClassService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<IStudentPromotionService, StudentPromotionService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IBiometricConfigService, BiometricConfigService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ILocalBiometricService, LocalBiometricService>();
        services.AddScoped<IAcademicYearService, AcademicYearService>();
        services.AddScoped<ITeacherAssignmentService, TeacherAssignmentService>();
        services.AddScoped<IAttendanceNotificationService, AttendanceNotificationService>();

        return services;
    }
}

