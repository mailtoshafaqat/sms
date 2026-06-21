using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SMS.Infrastructure.Data;
using SMS.Infrastructure.Identity;

const string DefaultEmail = "admin@school.local";
const string DefaultPassword = "Admin@123";

var options = ParseArgs(args);
if (options.ShowHelp)
{
    PrintHelp();
    return 0;
}

if (!Directory.Exists(options.ConfigDirectory))
{
    Console.Error.WriteLine($"Config folder not found: {options.ConfigDirectory}");
    return 1;
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(options.ConfigDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Production.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Connection string 'DefaultConnection' is missing from appsettings.");
    return 1;
}

var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<AppDbContext>(builder =>
    builder.UseSqlServer(connectionString));

services.AddIdentity<ApplicationUser, IdentityRole>(identityOptions =>
    {
        identityOptions.Password.RequireDigit = true;
        identityOptions.Password.RequireLowercase = true;
        identityOptions.Password.RequireUppercase = true;
        identityOptions.Password.RequireNonAlphanumeric = false;
        identityOptions.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

await using var provider = services.BuildServiceProvider(true);
await using var scope = provider.CreateAsyncScope();

var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

var email = options.Email.Trim();
var password = options.Password;

if (!await roleManager.RoleExistsAsync("Admin"))
{
    await roleManager.CreateAsync(new IdentityRole("Admin"));
}

var user = await userManager.FindByEmailAsync(email);
if (user is null)
{
    user = new ApplicationUser
    {
        UserName = email,
        Email = email,
        EmailConfirmed = true,
        DisplayName = "System Admin",
        IsActive = true
    };

    var createResult = await userManager.CreateAsync(user, password);
    if (!createResult.Succeeded)
    {
        Console.Error.WriteLine("Failed to create admin account:");
        foreach (var error in createResult.Errors)
        {
            Console.Error.WriteLine($"  - {error.Description}");
        }

        return 1;
    }

    await userManager.AddToRoleAsync(user, "Admin");
    Console.WriteLine($"Created admin account: {email}");
    Console.WriteLine("Password set to the value you provided.");
    Console.WriteLine("Sign in and change the password under Settings → User Accounts.");
    return 0;
}

if (!user.IsActive)
{
    user.IsActive = true;
    await userManager.UpdateAsync(user);
}

await userManager.SetLockoutEndDateAsync(user, null);

var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
var resetResult = await userManager.ResetPasswordAsync(user, resetToken, password);
if (!resetResult.Succeeded)
{
    Console.Error.WriteLine("Failed to reset password:");
    foreach (var error in resetResult.Errors)
    {
        Console.Error.WriteLine($"  - {error.Description}");
    }

    return 1;
}

if (!await userManager.IsInRoleAsync(user, "Admin"))
{
    await userManager.AddToRoleAsync(user, "Admin");
}

Console.WriteLine($"Password reset for: {email}");
Console.WriteLine("Sign in with the new password, then change it under Settings → User Accounts.");
return 0;

static CliOptions ParseArgs(string[] args)
{
    var options = new CliOptions
    {
        ConfigDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "SMS.Web")),
        Email = DefaultEmail,
        Password = DefaultPassword
    };

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "-h":
            case "--help":
                options.ShowHelp = true;
                break;
            case "-c":
            case "--config":
                if (i + 1 >= args.Length)
                {
                    throw new InvalidOperationException("Missing value for --config");
                }

                options.ConfigDirectory = Path.GetFullPath(args[++i]);
                break;
            case "-e":
            case "--email":
                if (i + 1 >= args.Length)
                {
                    throw new InvalidOperationException("Missing value for --email");
                }

                options.Email = args[++i];
                break;
            case "-p":
            case "--password":
                if (i + 1 >= args.Length)
                {
                    throw new InvalidOperationException("Missing value for --password");
                }

                options.Password = args[++i];
                break;
        }
    }

    return options;
}

static void PrintHelp()
{
    Console.WriteLine("""
        SMS admin password reset

        Resets (or creates) the admin login using the same password rules as the web app.
        Reads the SQL connection string from appsettings.json in the config folder.

        Usage:
          dotnet run --project tools/SMS.AdminReset -- [options]

        Options:
          -c, --config <folder>   Folder containing appsettings.json (default: src/SMS.Web)
          -e, --email <email>   Account email (default: admin@school.local)
          -p, --password <pwd>  New password (default: Admin@123)
          -h, --help            Show this help

        Examples:
          dotnet run --project tools/SMS.AdminReset
          dotnet run --project tools/SMS.AdminReset -- --config C:\inetpub\sms
        """);
}

sealed class CliOptions
{
    public const string DefaultEmail = "admin@school.local";
    public const string DefaultPassword = "Admin@123";

    public bool ShowHelp { get; set; }
    public string ConfigDirectory { get; set; } = string.Empty;
    public string Email { get; set; } = DefaultEmail;
    public string Password { get; set; } = DefaultPassword;
}
