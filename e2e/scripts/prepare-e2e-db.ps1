param(
    [string]$ServerInstance = "(localdb)\MSSQLLocalDB",
    [string]$Database = "AssetManagementModuleDb_E2E"
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$webConfig = Join-Path $root "src\AssetManagement.Web\Web.config"
$webConfigBackup = "$webConfig.e2e-backup"

Write-Host "Resetting E2E database [$Database] on [$ServerInstance]..."

$masterConnectionString = "Data Source=$ServerInstance;Initial Catalog=master;Integrated Security=True"
$masterConnection = New-Object System.Data.SqlClient.SqlConnection($masterConnectionString)
$masterConnection.Open()
try {
    $dropDb = $masterConnection.CreateCommand()
    $dropDb.CommandTimeout = 120
    $dropDb.CommandText = @"
IF DB_ID(@databaseName) IS NOT NULL
BEGIN
    ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$Database];
END
"@
    $null = $dropDb.Parameters.AddWithValue("@databaseName", $Database)
    [void]$dropDb.ExecuteNonQuery()
}
finally {
    $masterConnection.Close()
}

& (Join-Path $root "initialize-database.ps1") -ServerInstance $ServerInstance -Database $Database

if (-not (Test-Path $webConfigBackup)) {
    Copy-Item $webConfig $webConfigBackup -Force
}

[xml]$config = Get-Content $webConfig
$connection = $config.configuration.connectionStrings.add | Where-Object { $_.name -eq 'AssetManagementConnection' }
$connection.connectionString = "Data Source=$ServerInstance;Initial Catalog=$Database;Integrated Security=True;MultipleActiveResultSets=True"
$captchaSetting = $config.configuration.appSettings.add | Where-Object { $_.key -eq 'LoginCaptchaEnabled' }
if ($captchaSetting) {
    $captchaSetting.value = 'false'
} else {
    $appSettings = $config.configuration.appSettings
    $captchaNode = $config.CreateElement('add')
    $captchaNode.SetAttribute('key', 'LoginCaptchaEnabled')
    $captchaNode.SetAttribute('value', 'false')
    $appSettings.AppendChild($captchaNode) | Out-Null
}
$autoInitSetting = $config.configuration.appSettings.add | Where-Object { $_.key -eq 'AutoInitializeDatabase' }
if ($autoInitSetting) {
    $autoInitSetting.value = 'false'
}
$syncAuditSetting = $config.configuration.appSettings.add | Where-Object { $_.key -eq 'SyncAuditWrites' }
if ($syncAuditSetting) {
    $syncAuditSetting.value = 'true'
} else {
    $appSettings = $config.configuration.appSettings
    $syncAuditNode = $config.CreateElement('add')
    $syncAuditNode.SetAttribute('key', 'SyncAuditWrites')
    $syncAuditNode.SetAttribute('value', 'true')
    $appSettings.AppendChild($syncAuditNode) | Out-Null
}
$outboxSetting = $config.configuration.appSettings.add | Where-Object { $_.key -eq 'OutboxDispatchIntervalSeconds' }
if ($outboxSetting) {
    $outboxSetting.value = '300'
}
$config.Save($webConfig)

Write-Host "Web.config pointed at E2E database [$Database]."
