[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^v?\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [string]$Remote = "origin",

    [switch]$SkipTests,

    [switch]$AllowDirty,

    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    Write-Host "> $FilePath $($Arguments -join ' ')"

    if ($DryRun) {
        return
    }

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')"
    }
}

function Get-CheckedOutput {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $output = & $FilePath @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath $($Arguments -join ' ')`n$output"
    }

    return ($output -join "`n")
}

function Test-RemoteTagExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TagName
    )

    $output = & git ls-remote --tags $Remote "refs/tags/$TagName" 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to query remote '$Remote' for tags.`n$output"
    }

    return -not [string]::IsNullOrWhiteSpace(($output -join "`n"))
}

$tag = $Version
if (-not $tag.StartsWith("v", [System.StringComparison]::OrdinalIgnoreCase)) {
    $tag = "v$tag"
}

$repoRoot = (Get-CheckedOutput "git" @("rev-parse", "--show-toplevel")).Trim()
Set-Location $repoRoot

Write-Host "Preparing PresentationTimer release $tag"

$insideWorkTree = Get-CheckedOutput "git" @("rev-parse", "--is-inside-work-tree")
if ($insideWorkTree.Trim() -ne "true") {
    throw "This script must be run inside a Git work tree."
}

$status = Get-CheckedOutput "git" @("status", "--porcelain")
if (-not $AllowDirty -and -not [string]::IsNullOrWhiteSpace($status)) {
    throw "Working tree is not clean. Commit or stash changes first, or pass -AllowDirty."
}

$localTag = & git tag --list $tag
if (-not [string]::IsNullOrWhiteSpace(($localTag -join "`n"))) {
    throw "Local tag '$tag' already exists."
}

if (Test-RemoteTagExists $tag) {
    throw "Remote tag '$tag' already exists on '$Remote'."
}

if (-not $SkipTests) {
    Invoke-Checked "dotnet" @("test", "PresentationTimer.sln", "--configuration", "Release", "-p:Platform=x64", "--verbosity", "normal")
}

Invoke-Checked "git" @("tag", "-a", $tag, "-m", "Release $tag")
Invoke-Checked "git" @("push", $Remote, $tag)

if ($DryRun) {
    Write-Host "Dry run completed. No tag was created or pushed."
} else {
    Write-Host "Release tag $tag pushed. GitHub Actions will create the release assets."
}
