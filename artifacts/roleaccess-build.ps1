$ErrorActionPreference = "Stop"
$root = "C:\Users\allan\Documents\Examples\CodexAsset"
$log = Join-Path $root "artifacts\roleaccess-build.log"
$exitFile = Join-Path $root "artifacts\roleaccess-build-exit.txt"
function W($m){ $line = "{0} {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $m; Add-Content $log $line; Write-Output $line }
"" | Set-Content $log -Encoding UTF8
try {
  if (Test-Path $exitFile) { Remove-Item $exitFile -Force }
  $msbuild = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
  $stamp = Get-Date -Format "yyyyMMdd_HHmmss"
  $pkg = Join-Path $root ("artifacts\web-publish-roleaccess-" + $stamp)
  New-Item -ItemType Directory -Path $pkg -Force | Out-Null
  W ("pkg=" + $pkg)
  W ("HEAD=" + (git -C $root rev-parse HEAD).Trim())
  W ("HEAD_MSG=" + (git -C $root log -1 --pretty=%s))
  $dirty = git -C $root status --porcelain --untracked-files=no
  if ($dirty) { throw ("Tree not clean: " + ($dirty -join "; ")) }
  W "tracked-clean=true"
  $nr = "/p:NuGetPackageRoot=" + $root + "\.nuget\packages\"
  & $msbuild (Join-Path $root "src\AssetManagement.Web\AssetManagement.Web.csproj") /restore /t:Rebuild /p:Configuration=Release /p:Platform=AnyCPU $nr /v:m /nologo "/flp:LogFile=$root\artifacts\roleaccess-build.msbuild.txt;Verbosity=minimal;Summary"
  if ($LASTEXITCODE -ne 0) { throw "msbuild rebuild failed: $LASTEXITCODE" }
  W "BUILD_OK"
  & $msbuild (Join-Path $root "src\AssetManagement.Web\AssetManagement.Web.csproj") /t:WebPublish /p:Configuration=Release /p:Platform=AnyCPU $nr /p:WebPublishMethod=FileSystem /p:PublishProvider=FileSystem /p:DeleteExistingFiles=True /p:publishUrl=$pkg /v:m /nologo
  if ($LASTEXITCODE -ne 0) { throw "msbuild webpublish failed: $LASTEXITCODE" }
  W "PUBLISH_OK"
  $wc = Join-Path $pkg "Web.config"
  if (Test-Path $wc) { Move-Item $wc (Join-Path $pkg "Web.config.EXCLUDED_DO_NOT_COPY") -Force; W "excluded-Web.config-from-package" }
  foreach ($n in @("Web","Application","Infrastructure","Domain")) {
    $dll = Join-Path $pkg ("bin\AssetManagement." + $n + ".dll")
    if (-not (Test-Path $dll)) { throw "Published DLL missing: $dll" }
    W ("pkg-" + $n + "-dll-sha256=" + (Get-FileHash $dll -Algorithm SHA256).Hash + " time=" + (Get-Item $dll).LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"))
  }
  foreach ($r in @("Views\Shared\_SidebarNav.cshtml","Views\Roles\Edit.cshtml")) {
    $fp = Join-Path $pkg $r
    if (-not (Test-Path $fp)) { throw "Package file missing: $r" }
    W ("pkg-file " + $r + " sha256=" + (Get-FileHash $fp -Algorithm SHA256).Hash)
  }
  Set-Content (Join-Path $root "artifacts\roleaccess-pkg-path.txt") $pkg -Encoding ASCII
  "0" | Set-Content $exitFile
  W "DONE"
  exit 0
} catch {
  W ("ERROR=" + $_.Exception.Message)
  "1" | Set-Content $exitFile
  exit 1
}





