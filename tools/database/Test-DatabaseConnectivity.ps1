# Verifies SQL Server connectivity and demo login readiness for the configured database.
param(
    [string]$ServerInstance,
    [string]$Database
)

. "$PSScriptRoot\_Common.ps1"

$ErrorActionPreference = 'Stop'
$root = Get-RepositoryRoot
$target = Resolve-SqlTargetFromWebConfig -ServerInstance $ServerInstance -Database $Database -RepositoryRoot $root
$ServerInstance = $target.ServerInstance
$Database = $target.Database

$autoInit = $null
$webConfig = Get-WebConfigPath -RepositoryRoot $root
if (Test-Path $webConfig) {
    [xml]$config = Get-Content $webConfig
    $autoInit = $config.configuration.appSettings.add |
        Where-Object { $_.key -eq 'AutoInitializeDatabase' } |
        Select-Object -ExpandProperty value -First 1
}

Write-Host "Database connectivity check" -ForegroundColor Cyan
Write-Host "  Server:   $ServerInstance"
Write-Host "  Database: $Database"
Write-Host "  Web.config AutoInitializeDatabase: $autoInit"
Write-Host ""

Add-Type -AssemblyName System.Data

function Invoke-ScalarQuery {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$Query
    )

    $command = $Connection.CreateCommand()
    $command.CommandText = $Query
    $command.CommandTimeout = 30
    return $command.ExecuteScalar()
}

$issues = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

try {
    $masterConnectionString = "Data Source=$ServerInstance;Initial Catalog=master;Integrated Security=True"
    $masterConnection = New-Object System.Data.SqlClient.SqlConnection($masterConnectionString)
    $masterConnection.Open()
    $masterConnection.Close()
    Write-Host "[OK] SQL Server instance is reachable." -ForegroundColor Green
}
catch {
    Write-Host "[FAIL] Cannot connect to SQL Server instance '$ServerInstance'." -ForegroundColor Red
    Write-Host "       $($_.Exception.Message)"
    Write-Host ""
    Write-Host "Remediation:" -ForegroundColor Yellow
    Write-Host "  - Start SQL Server Express or LocalDB."
    Write-Host "  - Update AssetManagementConnection in src\AssetManagement.Web\Web.config."
    exit 1
}

$databaseExists = $false
try {
    $masterConnection = New-Object System.Data.SqlClient.SqlConnection($masterConnectionString)
    $masterConnection.Open()
  $databaseExists = [int](Invoke-ScalarQuery -Connection $masterConnection -Query "SELECT COUNT(1) FROM sys.databases WHERE name = N'$($Database.Replace("'", "''"))'") -gt 0
    $masterConnection.Close()
}
catch {
    [void]$issues.Add("Could not enumerate databases on '$ServerInstance': $($_.Exception.Message)")
}

if (-not $databaseExists) {
    Write-Host "[FAIL] Database '$Database' does not exist." -ForegroundColor Red
    [void]$issues.Add("Database '$Database' is missing.")
}
else {
    Write-Host "[OK] Database '$Database' exists." -ForegroundColor Green
}

if (-not $databaseExists) {
    Write-Host ""
    Write-Host "Remediation:" -ForegroundColor Yellow
    Write-Host "  .\tools\database\Initialize-Database.ps1 -ServerInstance `"$ServerInstance`" -Database `"$Database`""
    exit 1
}

$connectionString = "Data Source=$ServerInstance;Initial Catalog=$Database;Integrated Security=True;MultipleActiveResultSets=True"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
$connection.Open()

try {
    $usersTableExists = [int](Invoke-ScalarQuery -Connection $connection -Query "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'Users'") -gt 0
    if (-not $usersTableExists) {
        Write-Host "[FAIL] Schema not applied (Users table missing)." -ForegroundColor Red
        [void]$issues.Add("Database exists but schema/seed has not been applied.")
    }
    else {
        Write-Host "[OK] Core schema is present (Users table exists)." -ForegroundColor Green
    }

    if ($usersTableExists) {
        $platformAdminCount = [int](Invoke-ScalarQuery -Connection $connection -Query "SELECT COUNT(1) FROM [Users] WHERE [Email] = N'superadmin@asset.local' AND [OrganizationId] IS NULL")
        if ($platformAdminCount -gt 0) {
            Write-Host "[OK] Platform admin account exists (superadmin@asset.local)." -ForegroundColor Green
        }
        else {
            Write-Host "[FAIL] Platform admin account is missing." -ForegroundColor Red
            [void]$issues.Add("No platform administrator row for superadmin@asset.local.")
        }

        $nanosoftAdminCount = [int](Invoke-ScalarQuery -Connection $connection -Query @"
SELECT COUNT(1)
FROM [Users] u
INNER JOIN [Organization] o ON o.[Id] = u.[OrganizationId]
WHERE o.[Slug] = N'nanosoft'
  AND u.[Email] = N'nanosoft@asset.local'
"@)
        if ($nanosoftAdminCount -gt 0) {
            Write-Host "[OK] Demo tenant admin exists (nanosoft@asset.local)." -ForegroundColor Green
        }
        else {
            Write-Host "[WARN] Demo tenant admin not found (nanosoft@asset.local)." -ForegroundColor Yellow
            [void]$warnings.Add("Tenant demo user nanosoft@asset.local is missing; run Initialize-Database.ps1 or Unlock-Logins.ps1.")
        }

        $lockedLoginCount = 0
        if ([int](Invoke-ScalarQuery -Connection $connection -Query "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = N'LoginAttempts'") -gt 0) {
            $lockedLoginCount = [int](Invoke-ScalarQuery -Connection $connection -Query "SELECT COUNT(1) FROM [LoginAttempts]")
            if ($lockedLoginCount -gt 0) {
                Write-Host "[WARN] LoginAttempts has $lockedLoginCount row(s); lockouts may block sign-in." -ForegroundColor Yellow
                [void]$warnings.Add("Login lockout records present.")
            }
        }
    }
}
finally {
    $connection.Close()
}

Write-Host ""
if ($issues.Count -eq 0) {
    Write-Host "Database connectivity and demo login prerequisites look good." -ForegroundColor Green
    if ($warnings.Count -gt 0) {
        Write-Host ""
        Write-Host "Warnings:" -ForegroundColor Yellow
        foreach ($warning in $warnings) {
            Write-Host "  - $warning"
        }
        Write-Host ""
        Write-Host "Optional repair:" -ForegroundColor Yellow
        Write-Host "  .\tools\database\Unlock-Logins.ps1 -ServerInstance `"$ServerInstance`" -Database `"$Database`""
    }
    else {
        Write-Host ""
        Write-Host "Platform login: /Account/Login -> superadmin@asset.local / P@ssw0rd!"
        Write-Host "Tenant login:   /nanosoft/Account/Login -> nanosoft@asset.local / P@ssw0rd!"
    }
    exit 0
}

Write-Host "Issues found:" -ForegroundColor Red
foreach ($issue in $issues) {
    Write-Host "  - $issue"
}

Write-Host ""
Write-Host "Remediation:" -ForegroundColor Yellow
Write-Host "  # Full schema + seed (safe; does not drop the database):"
Write-Host "  .\tools\database\Initialize-Database.ps1 -ServerInstance `"$ServerInstance`" -Database `"$Database`""
Write-Host ""
Write-Host "  # Or repair demo/platform accounts and clear lockouts:"
Write-Host "  .\tools\database\Unlock-Logins.ps1 -ServerInstance `"$ServerInstance`" -Database `"$Database`""
Write-Host ""
Write-Host "  # If Web.config still points at an E2E database, restore dev settings:"
Write-Host "  .\tools\database\Restore-E2eWebConfig.ps1"
Write-Host "  # then ensure Initial Catalog=AssetManagementModuleDb in src\AssetManagement.Web\Web.config"

exit 1
