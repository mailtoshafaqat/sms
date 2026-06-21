# Resets the SMS admin password to the default (or custom) value.
# Run on the school server where SQL Server and appsettings.json are available.
#
# Examples:
#   .\scripts\reset-admin-password.ps1
#   .\scripts\reset-admin-password.ps1 -ConfigPath "C:\inetpub\sms"
#   .\scripts\reset-admin-password.ps1 -Email "admin@school.local" -Password "Admin@123"

param(
    [string]$Email = "admin@school.local",
    [string]$Password = "Admin@123",
    [string]$ConfigPath = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "tools\SMS.AdminReset\SMS.AdminReset.csproj"

if (-not (Test-Path $Project)) {
    Write-Error "Admin reset tool not found: $Project"
}

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $Root "src\SMS.Web"
}

Write-Host "SMS admin password reset"
Write-Host "  Config : $ConfigPath"
Write-Host "  Email  : $Email"
Write-Host ""

$args = @(
    "run",
    "--project", $Project,
    "--",
    "--config", $ConfigPath,
    "--email", $Email,
    "--password", $Password
)

Push-Location $Root
try {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Done. Sign in and change the password under Settings -> User Accounts."
