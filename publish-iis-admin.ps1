$ErrorActionPreference = "Stop"
$log = "C:\Users\allan\Documents\Examples\CodexAsset\publish-iis-admin.log"
function Write-Log($m) { $line = "{0} {1}" -f (Get-Date -Format o), $m; Add-Content -Path $log -Value $line; Write-Host $line }
try {
  Write-Log "attempt5-copyonly elevated=$([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))"
  $root = "C:\Users\allan\Documents\Examples\CodexAsset"
  Import-Module WebAdministration
  $pool = "AssetManagement"
  function Get-PoolState { (Get-WebAppPoolState -Name $pool).Value }
  function Wait-PoolNotBusy {
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-PoolState) -in @("Starting","Stopping") -and (Get-Date) -lt $deadline) {
      Write-Log ("waiting-pool=" + (Get-PoolState)); Start-Sleep -Seconds 3
    }
  }
  Wait-PoolNotBusy
  $state = Get-PoolState
  Write-Log "pool-before=$state"
  if ($state -eq "Started") {
    try { Stop-WebAppPool -Name $pool } catch { Write-Log ("stop-warn=" + $_.Exception.Message) }
    Start-Sleep -Seconds 3
    Wait-PoolNotBusy
  }
  . "$root\tools\deploy\_IisCommon.ps1"
  Invoke-RobocopyMirror -Source "$root\src\AssetManagement.Web" -Destination "C:\inetpub\AssetManagement"
  Write-Log "robocopy-ok"
  Start-WebAppPool -Name $pool
  Start-Sleep -Seconds 4
  Wait-PoolNotBusy
  Write-Log ("pool-after=" + (Get-PoolState))
  if (Select-String -Path "C:\inetpub\AssetManagement\Views\Departments\Index.cshtml" -Pattern "isClasses" -Quiet) { Write-Log "inetpub-isClasses=YES" } else { Write-Log "inetpub-isClasses=NO" }
  try {
    $r = Invoke-WebRequest -Uri "http://127.0.0.1:8080/nanosoft/Account/Login" -UseBasicParsing -TimeoutSec 30
    Write-Log ("login-status=" + $r.StatusCode)
  } catch { Write-Log ("login-error=" + $_.Exception.Message) }
  Write-Log "DONE"
} catch {
  Write-Log ("ERROR: " + $_.Exception.Message)
  exit 1
}
