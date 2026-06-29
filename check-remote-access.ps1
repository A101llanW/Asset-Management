param(
    [int]$Port = 51901
)

$ErrorActionPreference = 'Continue'
Write-Host "=== Remote access diagnostics (port $Port) ==="
Write-Host ""

Write-Host "ZeroTier / network IPs:"
Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.InterfaceAlias -like 'ZeroTier*' -or $_.InterfaceAlias -eq 'Ethernet' } |
    ForEach-Object {
        Write-Host "  $($_.InterfaceAlias): $($_.IPAddress)"
    }

Write-Host ""
Write-Host "IIS Express:"
$iis = Get-Process iisexpress -ErrorAction SilentlyContinue
if ($iis) {
    Write-Host "  Running (PID $($iis.Id -join ', '))"
}
else {
    Write-Host "  NOT running - run .\start-remote-access.ps1"
}

Write-Host ""
Write-Host "Port binding:"
netstat -ano | findstr ":$Port " | ForEach-Object { Write-Host "  $_" }

$ztIp = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.InterfaceAlias -like 'ZeroTier*' -and $_.IPAddress -notlike '169.254.*' } |
    Select-Object -First 1 -ExpandProperty IPAddress

Write-Host ""
Write-Host "HTTP checks:"
$urls = @("http://127.0.0.1:$Port/Account/Login")
if ($ztIp) {
    $urls += "http://${ztIp}:$Port/Account/Login"
    $urls += "http://${ztIp}:$Port/nanosoft/Account/Login"
}
foreach ($url in $urls) {
    try {
        $sw = [Diagnostics.Stopwatch]::StartNew()
        $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10
        $sw.Stop()
        $ms = $sw.ElapsedMilliseconds
        Write-Host "  OK   $url (status $($r.StatusCode), ${ms} ms)"
    }
    catch {
        Write-Host "  FAIL $url - $($_.Exception.Message)"
    }
}

Write-Host ""
Write-Host "Firewall:"
$rule = Get-NetFirewallRule -DisplayName "Asset Management Remote ($Port)" -ErrorAction SilentlyContinue
if ($rule) {
    Write-Host "  Rule exists. Enabled=$($rule.Enabled) Profile=$($rule.Profile -join ',')"
}
else {
    Write-Host "  No firewall rule - run start-remote-access.ps1 as Administrator"
}

if ($ztIp) {
    Write-Host ""
    Write-Host "Use on remote device (same ZeroTier network):"
    Write-Host "  http://${ztIp}:$Port/nanosoft/Account/Login"
    Write-Host "  http://${ztIp}:$Port/Account/Login"
}
