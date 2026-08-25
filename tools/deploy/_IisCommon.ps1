# Shared helpers for IIS deploy scripts.
$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
    $dir = $PSScriptRoot
    while ($dir) {
        $sln = Join-Path $dir 'AssetManagementModule.sln'
        if (Test-Path $sln) {
            return $dir
        }

        $parent = Split-Path $dir -Parent
        if ($parent -eq $dir) {
            break
        }

        $dir = $parent
    }

    throw 'Repository root not found (expected AssetManagementModule.sln).'
}

function Resolve-MsBuildPath {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null | Select-Object -First 1
        if ($msbuild -and (Test-Path $msbuild)) {
            return $msbuild
        }
    }

    $fallback = 'msbuild'
    if (Get-Command $fallback -ErrorAction SilentlyContinue) {
        return $fallback
    }

    throw 'MSBuild not found. Install Visual Studio Build Tools or run from a Developer PowerShell prompt.'
}

function Test-IisAvailable {
    return (Get-Command Get-Website -ErrorAction SilentlyContinue) -ne $null
}

function Import-IisAdministration {
    if (-not (Get-Module -ListAvailable -Name WebAdministration)) {
        throw 'IIS WebAdministration module not found. Enable IIS Management Scripts and Tools on Windows.'
    }

    Import-Module WebAdministration -ErrorAction Stop
}

function Get-DefaultIisSettings {
    return [ordered]@{
        SiteName = 'AssetManagement'
        SitePath = 'C:\inetpub\AssetManagement'
        AppPoolName = 'AssetManagement'
        Port = 8080
        LoginPath = '/nanosoft/Account/Login'
    }
}

function Get-IisBaseUrl {
    param(
        [int]$Port = (Get-DefaultIisSettings).Port
    )

    return "http://localhost:$Port"
}

function Wait-ForWebApp {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 120
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -eq 200) {
                return $true
            }
        }
        catch {
            Start-Sleep -Seconds 2
        }
    }

    return $false
}

function Invoke-RobocopyMirror {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (-not (Test-Path $Source)) {
        throw "Source path not found: $Source"
    }

    if (-not (Test-Path $Destination)) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    }

    $args = @(
        $Source,
        $Destination,
        '/MIR',
        '/XD', 'obj', 'App_Data',
        '/XF', 'Web.config', 'connectionStrings.config', 'machineKey.config', 'smtp.secrets.config',
        '/NFL', '/NDL', '/NJH', '/NJS', '/NC', '/NS', '/NP'
    )

    & robocopy @args | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed with exit code $LASTEXITCODE"
    }
}
