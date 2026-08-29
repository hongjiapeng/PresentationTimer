[CmdletBinding()]
param(
    [switch]$OpenInstaller,
    [switch]$WaitAfterOpening
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$officeInstallUrl = 'https://m365.cloud.microsoft/apps'
$powerPointProgId = 'PowerPoint.Application'
$wpsProgId = 'KWPP.Application'

function Get-ComRegistration {
    param(
        [Parameter(Mandatory)]
        [string]$ProgId
    )

    $clsidPath = "Registry::HKEY_CLASSES_ROOT\$ProgId\CLSID"
    $clsid = $null
    if (Test-Path -LiteralPath $clsidPath) {
        $clsid = (Get-Item -LiteralPath $clsidPath).GetValue('')
    }

    $comType = $null
    try {
        $comType = [Type]::GetTypeFromProgID($ProgId, $false)
    }
    catch [System.Runtime.InteropServices.COMException] {
        # Registry probing below still provides a useful result when activation is unavailable.
    }

    [pscustomobject]@{
        ProgId = $ProgId
        Registered = ($null -ne $comType -or $null -ne $clsid)
        Clsid = $clsid
        ComType = if ($null -ne $comType) { $comType.FullName } else { $null }
    }
}

function Get-PowerPointExecutable {
    $knownPaths = @(
        'C:\Program Files\Microsoft Office\root\Office16\POWERPNT.EXE',
        'C:\Program Files (x86)\Microsoft Office\root\Office16\POWERPNT.EXE',
        'C:\Program Files\Microsoft Office\Office16\POWERPNT.EXE',
        'C:\Program Files (x86)\Microsoft Office\Office16\POWERPNT.EXE'
    )

    @($knownPaths | Where-Object { Test-Path -LiteralPath $_ })
}

function Show-Status {
    $powerPoint = Get-ComRegistration -ProgId $powerPointProgId
    $wps = Get-ComRegistration -ProgId $wpsProgId
    $executables = @(Get-PowerPointExecutable)

    Write-Host ''
    Write-Host 'PresentationTimer PowerPoint environment check' -ForegroundColor Cyan
    Write-Host "Microsoft PowerPoint COM ($powerPointProgId): $(if ($powerPoint.Registered) { 'REGISTERED' } else { 'MISSING' })"
    if ($powerPoint.Clsid) {
        Write-Host "  CLSID: $($powerPoint.Clsid)"
    }
    if ($powerPoint.ComType) {
        Write-Host "  COM type: $($powerPoint.ComType)"
    }
    Write-Host "WPS COM ($wpsProgId): $(if ($wps.Registered) { 'FOUND (not used by current adapter)' } else { 'not found' })"
    Write-Host "Desktop PowerPoint executable: $(if ($executables.Count -gt 0) { 'FOUND' } else { 'MISSING' })"
    foreach ($executable in $executables) {
        Write-Host "  $executable"
    }

    [pscustomobject]@{
        PowerPoint = $powerPoint
        Wps = $wps
        Executables = $executables
    }
}

$status = Show-Status
if ($status.PowerPoint.Registered) {
    Write-Host ''
    Write-Host 'PowerPoint desktop COM is ready for PresentationTimer.' -ForegroundColor Green
    exit 0
}

Write-Host ''
Write-Host 'Microsoft PowerPoint desktop COM is not registered.' -ForegroundColor Yellow
Write-Host 'Install or repair Microsoft 365/Office desktop apps from the official Microsoft page:'
Write-Host "  $officeInstallUrl"

if (-not $OpenInstaller) {
    Write-Host 'Run this script again with -OpenInstaller to open that page automatically.'
    exit 1
}

Start-Process $officeInstallUrl
if ($WaitAfterOpening) {
    [void](Read-Host 'After installation and activation finish, press Enter to check again')
    $status = Show-Status
    if ($status.PowerPoint.Registered) {
        Write-Host ''
        Write-Host 'PowerPoint desktop COM is now ready for PresentationTimer.' -ForegroundColor Green
        exit 0
    }

    Write-Host ''
    Write-Host 'PowerPoint COM is still missing. Restart Windows after Office setup, then run this script again.' -ForegroundColor Yellow
}

exit 1
