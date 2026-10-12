# role-access-scope local IIS :8080 publish (branch fix/role-access-scope 87de8b8 = code 5857956). No migrations (099 already applied locally; verified as pre-check). Run via roleaccess-launch-elevated.ps1 ONCE.
$ErrorActionPreference = "Stop"
$root = "C:\Users\allan\Documents\Examples\CodexAsset"
$log = Join-Path $root "artifacts\roleaccess-iis-publish.log"
$exitFile = Join-Path $root "artifacts\roleaccess-iis-publish-exit.txt"
function W($m){ $line = "{0} {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $m; Add-Content $log $line; Write-Output $line }
"" | Set-Content $log -Encoding UTF8
$iisRoot = "C:\inetpub\AssetManagement"
$wcPath = Join-Path $iisRoot "Web.config"
$secrets = Join-Path $iisRoot "smtp.secrets.config"
$pool = "AssetManagement"
$expectedWc = "27426D6E91024DEC055C07EE1D54ACB35A97317BE49F7A281B4D2DA39CF7D4D3"
$expectedDll = "9EDFCD0F13E0C97AD311806CB48FD97FA1DFB4865CC8C04F531B8CE7D04E93C3"
$expectedPkgDll = "0BE0611B0E535ACEBD6B9DCF2381CC4BD2B5DC5A2604890EB5782A536499001D"
$excludeNames = @("Web.config","Web.config.EXCLUDED_DO_NOT_COPY","Web.Debug.config","Web.Release.config","smtp.secrets.config","connectionStrings.config","machineKey.config")
$dbServer = ".\SQLEXPRESS"; $dbName = "AssetManagementModuleDb"
# $migration = Join-Path $root "database\scripts\004_Migrations\097_AssetRequestTarget.sql"
$migrationSha = "50B81AEE6BBE81647A8B9B36129F9E3592A63215DCD6625AAEB620DEEDB799A4"
# $migration098 = Join-Path $root "database\scripts\004_Migrations\098_PurchaseOrderLines.sql"
$migration098Sha = "BC9C1FA93BB7744F45404523CAE9FF8F3807CE01E5F0E0526FCE2CD11EBB2BDA"
$bakRoot = $null; $copyStarted = $false
function Wait-Pool($target) {
  $deadline = (Get-Date).AddSeconds(90)
  do { Start-Sleep 2; $st = (Get-WebAppPoolState -Name $pool).Value; if ($st -eq $target) { break } } while ((Get-Date) -lt $deadline)
  return $st
}
function Rollback($reason) {
  W ("ROLLBACK reason=" + $reason)
  try { if ((Get-WebAppPoolState -Name $pool).Value -eq "Started") { Stop-WebAppPool -Name $pool; W ("rb-pool=" + (Wait-Pool "Stopped")) } } catch { W ("rb-stop-warn=" + $_.Exception.Message) }
  if ($copyStarted -and $bakRoot) {
    & robocopy $bakRoot $iisRoot /E /R:1 /W:1 /NP /NFL /NDL /XF Web.config smtp.secrets.config /XD _bak | Out-Null
    W ("rb-robocopy-exit=" + $LASTEXITCODE)
  }
  Start-WebAppPool -Name $pool
  W ("rb-pool=" + (Wait-Pool "Started"))
  W ("rb-web-dll=" + (Get-FileHash (Join-Path $iisRoot "bin\AssetManagement.Web.dll") -Algorithm SHA256).Hash)
  W ("rb-webconfig=" + (Get-FileHash $wcPath -Algorithm SHA256).Hash)
}
try {
  if (Test-Path $exitFile) { Remove-Item $exitFile -Force }
  $elev = ([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
  W ("elevated=" + $elev)
  if (-not $elev) { throw "Not elevated" }
  $pkg = "C:\Users\allan\Documents\Examples\CodexAsset\artifacts\web-publish-roleaccess-20261012_192314"
  if (-not (Test-Path (Join-Path $pkg "bin\AssetManagement.Web.dll"))) { throw "Package missing: $pkg" }
  if (Test-Path (Join-Path $pkg "Web.config")) { throw "Package still contains Web.config; aborting" }
  W ("pkg=" + $pkg)
  $pkgDll = (Get-FileHash (Join-Path $pkg "bin\AssetManagement.Web.dll") -Algorithm SHA256).Hash; W ("pkg-web-dll=" + $pkgDll)
  if ($pkgDll -ne $expectedPkgDll) { throw "Package Web.dll is not the role-access build; aborting" }
  $wcBefore = (Get-FileHash $wcPath -Algorithm SHA256).Hash
  W ("webconfig-before=" + $wcBefore)
  if ($wcBefore -ne $expectedWc) { throw "Unexpected Web.config hash; aborting" }
  $dllBefore = (Get-FileHash (Join-Path $iisRoot "bin\AssetManagement.Web.dll") -Algorithm SHA256).Hash
  W ("web-dll-before=" + $dllBefore)
  if ($dllBefore -ne $expectedDll) { throw "Unexpected live Web.dll (expected rel1009i 9EDFCD0F...93C3); aborting" }
  $secretsBefore = (Get-FileHash $secrets -Algorithm SHA256).Hash
  W ("secrets-before=" + $secretsBefore)

  # --- full backup of site root (excluding the old _bak folder) ---
  $stamp = Get-Date -Format "yyyyMMdd_HHmmss"
  $bakRoot = "C:\inetpub\_backups\AssetManagement_root_pre_roleaccess_" + $stamp
  New-Item -ItemType Directory -Path $bakRoot -Force | Out-Null
  & robocopy $iisRoot $bakRoot /E /COPY:DAT /R:1 /W:1 /NP /NFL /NDL /XD (Join-Path $iisRoot "_bak") | Out-Null
  $rc = $LASTEXITCODE
  W ("backup-robocopy-exit=" + $rc)
  if ($rc -ge 8) { throw "backup robocopy failed: $rc" }
  if ((Get-FileHash (Join-Path $bakRoot "Web.config") -Algorithm SHA256).Hash -ne $wcBefore) { throw "Web.config backup hash mismatch" }
  if ((Get-FileHash (Join-Path $bakRoot "bin\AssetManagement.Web.dll") -Algorithm SHA256).Hash -ne $dllBefore) { throw "Web.dll backup hash mismatch" }
  $bakCount = (Get-ChildItem $bakRoot -Recurse -File).Count
  W ("bakRoot=" + $bakRoot + " files=" + $bakCount)
  Set-Content (Join-Path $root "artifacts\roleaccess-iis-bakpath.txt") $bakRoot -Encoding ASCII

  # --- DB: COPY_ONLY backup (safety), no migrations; 099 must already be applied ---
  $dbBak = "C:\Program Files\Microsoft SQL Server\MSSQL15.SQLEXPRESS\MSSQL\Backup\" + $dbName + "_pre_roleaccess_publish_" + $stamp + ".bak"
  & sqlcmd -S $dbServer -E -b -Q ("BACKUP DATABASE [" + $dbName + "] TO DISK=N'" + $dbBak + "' WITH COPY_ONLY, CHECKSUM, INIT; RESTORE VERIFYONLY FROM DISK=N'" + $dbBak + "' WITH CHECKSUM") | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "DB backup failed: $LASTEXITCODE" }
  W ("db-backup=" + $dbBak)
  $chk99 = (& sqlcmd -S $dbServer -E -b -d $dbName -h -1 -W -Q "SET NOCOUNT ON; SELECT CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Permission WHERE Code=N'Assets.ViewAll') AND EXISTS (SELECT 1 FROM dbo.SchemaMigrationHistory WHERE ScriptName=N'099_AssetsViewAllPermission.sql') THEN 1 ELSE 0 END AS varchar(1))" | Out-String).Trim()
  W ("db-099-verified=" + $chk99)
  if ($chk99 -ne "1") { throw "099 not applied; aborting (site untouched)" }  Import-Module WebAdministration
  W ("pool-before=" + (Get-WebAppPoolState -Name $pool).Value)
  if ((Get-WebAppPoolState -Name $pool).Value -eq "Started") { Stop-WebAppPool -Name $pool }
  W ("pool-stopped-state=" + (Wait-Pool "Stopped"))

  $copyStarted = $true; $copied = 0
  Get-ChildItem $pkg -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($pkg.Length).TrimStart('\')
    if ($excludeNames -contains $_.Name) { W ("skip=" + $rel); return }
    $dst = Join-Path $iisRoot $rel
    $dstDir = Split-Path $dst -Parent
    if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
    Copy-Item $_.FullName $dst -Force; $copied++
  }
  W ("copy-complete files=" + $copied)
  if ((Get-FileHash $wcPath -Algorithm SHA256).Hash -ne $wcBefore) { throw "Web.config changed during copy" }
  W "webconfig-unchanged-by-copy=true"
  if ((Get-FileHash $secrets -Algorithm SHA256).Hash -ne $secretsBefore) { throw "smtp.secrets.config changed during copy" }
  W "secrets-unchanged=true"
  $mismatch = 0; $checked = 0
  Get-ChildItem $pkg -Recurse -File | ForEach-Object {
    if ($excludeNames -contains $_.Name) { return }
    $rel = $_.FullName.Substring($pkg.Length).TrimStart('\')
    $d = Join-Path $iisRoot $rel; $checked++
    if (-not (Test-Path $d) -or (Get-FileHash $d -Algorithm SHA256).Hash -ne (Get-FileHash $_.FullName -Algorithm SHA256).Hash) { $mismatch++; W ("hash-mismatch=" + $rel) }
  }
  W ("all-files-checked=" + $checked + " mismatches=" + $mismatch)
  if ($mismatch -gt 0) { throw "hash mismatches after copy" }
  foreach ($n in @("Web","Application","Infrastructure","Domain")) { $f = Join-Path $iisRoot ("bin\AssetManagement." + $n + ".dll"); W ("iis-" + $n + "-dll-sha256=" + (Get-FileHash $f -Algorithm SHA256).Hash + " time=" + (Get-Item $f).LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")) }

  Start-WebAppPool -Name $pool
  W ("pool-after=" + (Wait-Pool "Started"))
  W ("PUBLISHED_AT=" + (Get-Date -Format "yyyy-MM-dd HH:mm:ss") + " EAT")
  $bad = @()
  foreach ($u in @("http://127.0.0.1:8080/Account/Login","http://127.0.0.1:8080/A46138179/Account/Login","http://127.0.0.1:8080/Account/Login?reason=access_denied","http://127.0.0.1:8080/Account/ForgotPassword")) {
    try {
      $rr = Invoke-WebRequest -Uri $u -UseBasicParsing -TimeoutSec 180
      $ysod = ($rr.Content -match 'Server Error in|Runtime Error')
      W ("login-status " + $u + "=" + $rr.StatusCode + " ysod=" + $ysod)
      if ($rr.StatusCode -ne 200 -or $ysod) { $bad += $u }
    } catch {
      $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { $_.Exception.Message }
      W ("login-status " + $u + "=" + $code); $bad += $u
    }
  }
  if ($bad.Count -gt 0) { throw ("login pages failing: " + ($bad -join ", ")) }
  $iisDll = (Get-FileHash (Join-Path $iisRoot "bin\AssetManagement.Web.dll") -Algorithm SHA256).Hash; W ("iis-web-dll-final=" + $iisDll); if ($iisDll -ne $expectedPkgDll) { throw "live Web.dll does not match package after publish" }
  W ("webconfig-final=" + (Get-FileHash $wcPath -Algorithm SHA256).Hash)
  "0" | Set-Content $exitFile
  W "DONE"
  exit 0
} catch {
  W ("ERROR=" + $_.Exception.Message)
  try { if ($copyStarted) { Rollback $_.Exception.Message } } catch { W ("ROLLBACK-ERROR=" + $_.Exception.Message) }
  "1" | Set-Content $exitFile
  exit 1
}








