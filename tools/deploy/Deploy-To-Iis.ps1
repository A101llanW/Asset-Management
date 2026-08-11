# Deploy built AssetManagement.Web to local IIS (C:\inetpub\AssetManagement).
# Run in an elevated PowerShell session (Administrator).
param(
    [string]$SourceRoot = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "src"),
    [string]$PublishRoot = (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) ".build\publish"),
    [string]$SitePath = "C:\inetpub\AssetManagement",
    [string]$AppPoolName = "DefaultAppPool",
    [switch]$UsePublishFolder
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SitePath)) {
    throw "IIS site path not found: $SitePath"
}

$webConfig = Join-Path $SitePath "Web.config"
if (-not (Test-Path $webConfig)) {
    throw "Web.config not found in $SitePath"
}

Write-Host "Stopping app pool '$AppPoolName'..." -ForegroundColor Cyan
Import-Module WebAdministration -ErrorAction Stop
Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

try {
    if ($UsePublishFolder -and (Test-Path $PublishRoot)) {
        Write-Host "Copying publish output from $PublishRoot ..." -ForegroundColor Cyan
        robocopy $PublishRoot $SitePath /MIR /XD App_Data /XF Web.config connectionStrings.config machineKey.config /NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE" }
    }
    else {
        Write-Host "Syncing built files from $SourceRoot ..." -ForegroundColor Cyan
        $pairs = @(
            @("AssetManagement.Web\Scripts\app\purchase-create.js", "Scripts\app\purchase-create.js"),
            @("AssetManagement.Web\Global.asax", "Global.asax"),
            @("AssetManagement.Web\Scripts\app\purchase-request-create.js", "Scripts\app\purchase-request-create.js"),
            @("AssetManagement.Web\Content\css\site.css", "Content\css\site.css"),
            @("AssetManagement.Web\Content\css\am-animations.css", "Content\css\am-animations.css"),
            @("AssetManagement.Web\Views\PurchaseRequests\Create.cshtml", "Views\PurchaseRequests\Create.cshtml"),
            @("AssetManagement.Web\Views\PurchaseRequests\Index.cshtml", "Views\PurchaseRequests\Index.cshtml"),
            @("AssetManagement.Web\Views\PurchaseRequests\_TargetAssetPickerModal.cshtml", "Views\PurchaseRequests\_TargetAssetPickerModal.cshtml"),
            @("AssetManagement.Web\Views\Shared\_AssetAuditTab.cshtml", "Views\Shared\_AssetAuditTab.cshtml"),
            @("AssetManagement.Web\Views\AuditLogs\Index.cshtml", "Views\AuditLogs\Index.cshtml"),
            @("AssetManagement.Web\Views\SecurityLogs\Index.cshtml", "Views\SecurityLogs\Index.cshtml"),
            @("AssetManagement.Web\Areas\Platform\Views\SecurityLogs\Index.cshtml", "Areas\Platform\Views\SecurityLogs\Index.cshtml"),
            @("AssetManagement.Web\Areas\Platform\Views\Organizations\OrganizationDetails.cshtml", "Areas\Platform\Views\Organizations\OrganizationDetails.cshtml")
        )
        foreach ($pair in $pairs) {
            $from = Join-Path $SourceRoot $pair[0]
            $to = Join-Path $SitePath $pair[1]
            if (-not (Test-Path $from)) { throw "Missing source file: $from" }
            $destDir = Split-Path $to -Parent
            if (-not (Test-Path $destDir)) { New-Item -ItemType Directory -Path $destDir -Force | Out-Null }
            Copy-Item $from $to -Force
            Write-Host "  OK $($pair[1])"
        }

        $dlls = @(
            "AssetManagement.Application.dll",
            "AssetManagement.Infrastructure.dll",
            "AssetManagement.Domain.dll",
            "AssetManagement.Web.dll"
        )
        foreach ($dll in $dlls) {
            $from = Join-Path $SourceRoot "AssetManagement.Web\bin\$dll"
            $to = Join-Path $SitePath "bin\$dll"
            if (-not (Test-Path $from)) { throw "Missing DLL: $from (run MSBuild Release first)" }
            Copy-Item $from $to -Force
            Write-Host "  OK bin\$dll"
        }
    }
}
finally {
    Write-Host "Starting app pool '$AppPoolName'..." -ForegroundColor Cyan
    Start-WebAppPool -Name $AppPoolName
}

Write-Host ""
Write-Host "Deploy complete. Hard-refresh browser (Ctrl+Shift+R) and smoke-test:" -ForegroundColor Green
Write-Host "  http://192.168.30.122:8080/nanosoft/AuditLogs/Index"
Write-Host "  http://192.168.30.122:8080/nanosoft/SecurityLogs/Index (timestamps should show EAT)"
