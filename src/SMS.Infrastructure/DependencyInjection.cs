using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMS.Application;
using SMS.Application.Interfaces;
using SMS.Application.Interfaces.Repositories;
using SMS.Infrastructure.Biometric;
using SMS.Infrastructure.Configuration;
using SMS.Infrastructure.Data;
using SMS.Infrastructure.Identity;
using SMS.Infrastructure.Repositories;
using SMS.Infrastructure.Services;

namespace SMS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        void ConfigureDbContext(DbContextOptionsBuilder options) =>
            options.UseSqlServer(connectionString);

        services.AddDbContextFactory<AppDbContext>(ConfigureDbContext);
        services.AddDbContext<AppDbContext>(ConfigureDbContext);

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<UnitOfWork>();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<IScopedDbContextProvider>(sp => sp.GetRequiredService<UnitOfWork>());
        services.AddScoped<ISchoolRepository, SchoolRepository>();
        services.AddScoped<IAcademicYearRepository, AcademicYearRepository>();
        services.AddScoped<IClassRepository, ClassRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IStudentPromotionRepository, StudentPromotionRepository>();
        services.AddScoped<IAttendanceRepository, AttendanceRepository>();
        services.AddScoped<IBiometricDeviceRepository, BiometricDeviceRepository>();
        services.AddScoped<ILocalBiometricRepository, LocalBiometricRepository>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IAttendanceNotificationRepository, AttendanceNotificationRepository>();
        services.AddScoped<IStaffAttendanceRepository, StaffAttendanceRepository>();
        services.AddScoped<IExceptionLogRepository, ExceptionLogRepository>();
        services.AddScoped<IExceptionLogService, ExceptionLogService>();
        services.AddScoped<IMonthlyRegisterExportService, MonthlyRegisterExportService>();
        services.AddScoped<IStaffMonthlyRegisterExportService, StaffMonthlyRegisterExportService>();
        services.AddScoped<IUserAccessService, UserAccessService>();
        services.Configure<DatabaseBackupOptions>(configuration.GetSection(DatabaseBackupOptions.SectionName));
        services.AddScoped<IDatabaseBackupService, DatabaseBackupService>();
        services.AddSingleton<IBiometricDeviceConnector, SimulatedBiometricConnector>();
        services.AddHostedService<BiometricWorker>();
        services.AddHostedService<AttendanceFinalizeWorker>();

        services.AddApplication();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/login";
            options.AccessDeniedPath = "/login";
        });

        return services;
    }
}

