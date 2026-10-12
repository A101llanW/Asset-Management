$script = "C:\Users\allan\Documents\Examples\CodexAsset\artifacts\roleaccess-iis-publish.ps1"
$launcherLog = "C:\Users\allan\Documents\Examples\CodexAsset\artifacts\roleaccess-iis-launcher.log"
Set-Content -Path $launcherLog -Value ("[{0}] Launching elevated roleaccess IIS publish (fix/role-access-scope) (UAC prompt)..." -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss")) -Encoding UTF8
try {
  $p = Start-Process -FilePath "powershell.exe" -ArgumentList @("-NoProfile","-ExecutionPolicy","Bypass","-File",$script) -Verb RunAs -Wait -PassThru -ErrorAction Stop
  Add-Content -Path $launcherLog -Value ("[{0}] UAC accepted; elevated process exit: {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $p.ExitCode)
  exit $p.ExitCode
} catch {
  $e = $_.Exception; $code = if ($e.InnerException -and $e.InnerException.NativeErrorCode) { $e.InnerException.NativeErrorCode } elseif ($e.NativeErrorCode) { $e.NativeErrorCode } else { "n/a" }
  Add-Content -Path $launcherLog -Value ("[{0}] UAC/launch FAILED native={1} msg={2}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $code, $e.Message)
  exit 1223
}







