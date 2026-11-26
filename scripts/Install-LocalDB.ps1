[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$MsiPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)

if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Open PowerShell as an administrator, then run this script again.'
}

if ([string]::IsNullOrWhiteSpace($MsiPath)) {
    $MsiPath = Read-Host 'Full path to SqlLocalDB.msi'
}

if (-not (Test-Path -LiteralPath $MsiPath -PathType Leaf)) {
    throw "Installer file not found: $MsiPath"
}

$MsiPath = (Resolve-Path -LiteralPath $MsiPath).Path

if ([IO.Path]::GetExtension($MsiPath) -ne '.msi') {
    throw "The selected file is not an MSI: $MsiPath"
}

$signature = Get-AuthenticodeSignature -FilePath $MsiPath
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft') {
    throw 'The MSI does not have a valid Microsoft signature. No installation was started.'
}

$logName = 'Lound-LocalDB-install-{0}.log' -f (Get-Date -Format 'yyyyMMdd-HHmmss')
$logPath = Join-Path $env:TEMP $logName
$msiexecPath = Join-Path $env:SystemRoot 'System32\msiexec.exe'
$arguments = @(
    '/i'
    ('"{0}"' -f $MsiPath)
    'IACCEPTSQLLOCALDBLICENSETERMS=YES'
    '/qn'
    '/norestart'
    '/L*v'
    ('"{0}"' -f $logPath)
)

Write-Host 'Installing SQL Server Express LocalDB...'
$process = Start-Process -FilePath $msiexecPath -ArgumentList $arguments -Wait -PassThru

switch ($process.ExitCode) {
    0 {
        Write-Host 'LocalDB installed successfully.'
        Write-Host 'Open a normal PowerShell window under your usual Windows account to run Lound.'
    }
    3010 {
        Write-Warning 'LocalDB installed. Windows requires a restart to finish.'
        Write-Host 'After restarting, open a normal PowerShell window under your usual Windows account to run Lound.'
    }
    default {
        throw "LocalDB setup exited with code $($process.ExitCode). Installer log: $logPath"
    }
}

Write-Host "Installer log: $logPath"
