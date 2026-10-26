#requires -Version 7.4
<#
.SYNOPSIS
Start the disposable B3 demo from Windows with Docker Desktop Linux containers.
.DESCRIPTION
Requires the pinned SDK and a trusted development certificate covering
*.dev.localhost. No unrelated certificates or Docker resources are removed.
#>
[CmdletBinding()]
param([ValidatePattern('^[A-Za-z][A-Za-z0-9]{0,63}$')][string]$Provider = 'PostgreSql')
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'DockerProcess.ps1')
$started = $false
$repository = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
Push-Location $repository
$run = [Guid]::NewGuid().ToString('N')
try {
    if ((& dotnet --version) -ne '10.0.401' -or $LASTEXITCODE -ne 0) { throw 'Install the pinned .NET SDK 10.0.401.' }
    if ((& docker info --format '{{.OSType}}') -ne 'linux' -or $LASTEXITCODE -ne 0) { throw 'Start Docker Desktop using Linux containers.' }
    & dotnet dev-certs https --check --trust
    if ($LASTEXITCODE -ne 0) { throw 'Create/trust a development HTTPS certificate with dotnet dev-certs https --trust, then retry.' }
    $store = [Security.Cryptography.X509Certificates.X509Store]::new('My', 'CurrentUser')
    try {
        $store.Open([Security.Cryptography.X509Certificates.OpenFlags]::ReadOnly)
        $matching = @($store.Certificates | Where-Object {
            $_.HasPrivateKey -and $_.NotBefore -le [DateTime]::Now -and $_.NotAfter -gt [DateTime]::Now -and
            @($_.Extensions | Where-Object { $_.Oid.Value -eq '1.3.6.1.4.1.311.84.1.1' }).Count -eq 1 -and
            $_.MatchesHostname('issuer.dev.localhost', $true, $false) -and $_.MatchesHostname('bff.dev.localhost', $true, $false)
        })
        if ($matching.Count -eq 0) { throw 'The development certificate must cover *.dev.localhost. Review stale certificates and generate a .NET 10 development certificate; this script does not remove certificates.' }
    }
    finally { $store.Dispose() }
    Write-Host "Starting disposable demo $run with provider $Provider. Keep this terminal open."
    Write-Host 'After schema completion, run ./scripts/local-demo/Initialize-LocalDemo.ps1 in a second PowerShell terminal.'
    $started = $true
    & dotnet run --project src/aspire/IdentityPlatform.AppHost -- --LocalDemo:Enabled=true "--LocalDemo:RunId=$run" "--Database:Provider=$Provider"
    if ($LASTEXITCODE -ne 0) { throw 'The local demo stopped with an error. No initialization operation was retried.' }
}
finally {
    # Remove only this run, including anonymous volumes attached to its remaining containers.
    if ($started) {
        try {
            $owned = (Invoke-OwnedProcess -ProcessArguments @('ps', '-aq', '--filter', "label=openidentityadmin.local-demo.run=$run") -TimeoutSeconds 30) -split "`r?`n"
            foreach ($container in $owned) {
                if ($container -match '^[a-f0-9]{12,64}$') {
                    $null = Invoke-OwnedProcess -ProcessArguments @('rm', '--force', '--volumes', $container) -TimeoutSeconds 30
                }
            }
        }
        catch { Write-Warning "Owned demo cleanup was incomplete. Inspect containers labelled openidentityadmin.local-demo.run=$run; do not prune unrelated resources." }
    }
    Pop-Location
}
