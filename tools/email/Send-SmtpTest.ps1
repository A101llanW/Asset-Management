param(
    [string]$To = "you@example.com",
    [string]$SecretsPath = (Join-Path $PSScriptRoot "..\..\src\AssetManagement.Web\smtp.secrets.config")
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $SecretsPath)) {
    $SecretsPath = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "src\AssetManagement.Web\smtp.secrets.config"
}

if (-not (Test-Path $SecretsPath)) {
    throw "Missing smtp.secrets.config. Copy smtp.secrets.config.example and set your Gmail app password."
}

[xml]$xml = Get-Content $SecretsPath
$settings = @{}
foreach ($add in $xml.appSettings.add) {
    $settings[$add.key] = $add.value
}

foreach ($required in @("SmtpHost", "SmtpPort", "FromEmail", "SmtpUser", "SmtpPassword")) {
    if ([string]::IsNullOrWhiteSpace($settings[$required]) -or $settings[$required] -like "REPLACE_*") {
        throw "Set $required in smtp.secrets.config before running this test."
    }
}

$enableSsl = $true
if ($settings.ContainsKey("SmtpEnableSsl")) {
    [bool]::TryParse($settings["SmtpEnableSsl"], [ref]$enableSsl) | Out-Null
}

$client = New-Object System.Net.Mail.SmtpClient($settings["SmtpHost"], [int]$settings["SmtpPort"])
$client.EnableSsl = $enableSsl
$client.UseDefaultCredentials = $false
$client.Credentials = New-Object System.Net.NetworkCredential($settings["SmtpUser"], $settings["SmtpPassword"])

$fromName = if ($settings["FromName"]) { $settings["FromName"] } else { "Asset Management Module" }
$message = New-Object System.Net.Mail.MailMessage
$message.From = New-Object System.Net.Mail.MailAddress($settings["FromEmail"], $fromName)
$message.To.Add($To)
$message.Subject = "SMTP Test - Asset Management Module"
$timestamp = Get-Date -Format "u"
$message.Body = "<p>MFA email delivery test at ${timestamp}.</p><p>If you received this, SMTP is working.</p>"
$message.IsBodyHtml = $true

try {
    $client.Send($message)
    Write-Host "OK: Test email sent to $To via $($settings['SmtpHost'])."
    exit 0
}
catch {
    throw "Send failed: $($_.Exception.Message)"
}
