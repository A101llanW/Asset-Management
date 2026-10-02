$ErrorActionPreference = "Continue"
Set-Location "C:\Users\allan\Documents\Examples\CodexAsset"
& .\Start-Dev.ps1 -Port 8080 *>&1 | Tee-Object -FilePath "C:\Users\allan\Documents\Examples\CodexAsset\artifacts\start-dev-elevated.log"
exit $LASTEXITCODE
