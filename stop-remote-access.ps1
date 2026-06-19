param(
    [int]$Port = 51980
)

$ErrorActionPreference = 'SilentlyContinue'
Get-Process iisexpress | Stop-Process -Force
foreach ($p in @(8080, $Port)) {
    netsh http delete urlacl url="http://+:$p/" | Out-Null
    netsh http delete urlacl url="http://*:$p/" | Out-Null
}
Write-Host "Stopped IIS Express and cleared HTTP reservations on ports 8080 and $Port."
