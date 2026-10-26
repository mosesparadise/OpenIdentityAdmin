#requires -Version 7.4
<#
.SYNOPSIS
Initialize one disposable local demo through the existing Migrator stdin contracts.
.DESCRIPTION
Run from a second PowerShell terminal after Aspire reports schema completion.
No password is placed in process arguments, environment, configuration or a file.
Docker-authorized local operators are trusted. A failed/interrupted operation is
not retried; restart the complete disposable demo before attempting a clean run.
#>
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'DockerProcess.ps1')

$ids = Invoke-OwnedProcess -ProcessArguments @('ps', '--filter', 'label=openidentityadmin.local-demo.role=initializer', '--format', '{{.ID}}') -TimeoutSeconds 30
$candidates = @($ids -split "`r?`n" | Where-Object { $_ -match '^[a-f0-9]{12,64}$' })
if ($candidates.Count -ne 1) { throw 'Exactly one running local demo initializer is required. Stop other demo runs or wait for schema completion.' }
$container = $candidates[0]
$run = Invoke-OwnedProcess -ProcessArguments @('inspect', '--format', '{{index .Config.Labels "openidentityadmin.local-demo.run"}}', $container) -TimeoutSeconds 30
if ($run -notmatch '^[a-f0-9]{32}$') { throw 'The selected container has no valid local demo ownership label.' }
$userName = Read-Host 'Synthetic administrator username (1–128 ASCII letters, digits, dot, underscore, @ or hyphen)'
if ($userName -cnotmatch '^[A-Za-z0-9._@-]{1,128}$') { throw 'Invalid synthetic administrator username.' }
$securePassword = Read-Host 'New password (16–256 characters; native Identity policy also applies)' -AsSecureString
$pointer = [IntPtr]::Zero
$completed = $false
try {
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    $password = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    if ($password.Length -lt 16 -or $password.Length -gt 256) { throw 'Password length must be 16–256 characters.' }
    $document = @{ operationId = [Guid]::NewGuid().ToString(); operatorReference = 'local-demo'; userName = $userName; password = $password } | ConvertTo-Json -Compress
    if ([Text.Encoding]::UTF8.GetByteCount($document) -gt 4096) { throw 'Bootstrap input exceeds the supported byte limit.' }
    $null = Invoke-OwnedProcess -ProcessArguments @('exec', '-i', $container, 'dotnet', '/app/IdentityPlatform.Migrator.dll', '--Operation', 'initialize-administrator') -InputDocument $document
    $null = Invoke-OwnedProcess -ProcessArguments @('exec', '-i', $container, '/app/register.sh') -TimeoutSeconds 90
    $completed = $true
    Write-Host 'Pending administrator and browser registration initialized. Complete genuine password/TOTP enrollment before BFF sign-in.'
    Write-Host 'Issuer: https://issuer.dev.localhost:17443/account/administrator/enrollment'
    Write-Host 'BFF:    https://bff.dev.localhost:17444/bff/login'
}
catch {
    throw 'Initialization did not complete. Stop the whole disposable demo and start a clean run; no operation was automatically retried.'
}
finally {
    if (-not $completed) {
        # A pipeline interruption can bypass catch. Terminate only this run-owned
        # remote exec target as well as the local CLI; never retry a mutation.
        try { $null = Invoke-OwnedProcess -ProcessArguments @('stop', '--time', '5', $container) -TimeoutSeconds 15 } catch { }
    }
    if ($pointer -ne [IntPtr]::Zero) { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
    $securePassword.Dispose()
    $password = $null
    $document = $null
    # Managed strings and Docker operator access prevent a secure-erasure claim.
}
