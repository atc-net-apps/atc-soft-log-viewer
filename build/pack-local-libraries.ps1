<#
.SYNOPSIS
    Packs Atc.LogCollector and Atc.LogAnalyzer from a local atc-logviewer clone into local-packages/.

.DESCRIPTION
    Temporary until the libraries are on nuget.org (atc-logviewer LOG-5). Then delete this script,
    the local-libraries source in nuget.config, and switch the package versions to the released ones.

    The packages always get version 1.0.0-local, which the app references. The script removes that
    version from the NuGet global packages folder, so the next restore picks up the new build.

.PARAMETER AtcLogViewerPath
    Path to the atc-net/atc-logviewer clone. Defaults to a sibling clone: ../../atc-net/atc-logviewer.

.EXAMPLE
    ./build/pack-local-libraries.ps1
    dotnet build AtcSoft.Log.Viewer.slnx
#>
[CmdletBinding()]
param(
    [string]$AtcLogViewerPath
)

$ErrorActionPreference = 'Stop'
if (-not $AtcLogViewerPath) {
    # Windows PowerShell does not set $PSScriptRoot in parameter defaults
    $AtcLogViewerPath = Join-Path $PSScriptRoot '..\..\..\atc-net\atc-logviewer'
}
$version = '1.0.0-local'
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repoRoot 'local-packages'
$globalPackages = ((dotnet nuget locals global-packages --list) -replace '^global-packages:\s*', '').Trim()

New-Item -ItemType Directory -Force -Path $output | Out-Null
Get-ChildItem $output -Filter '*.nupkg' | Remove-Item

foreach ($project in 'Atc.LogCollector', 'Atc.LogAnalyzer') {
    $csproj = Join-Path $AtcLogViewerPath "src\$project\$project.csproj"
    dotnet pack $csproj -c Release -o $output -p:Version=$version
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet pack failed for $project"
    }

    $cached = Join-Path $globalPackages (Join-Path $project.ToLowerInvariant() $version)
    if (Test-Path $cached) {
        Remove-Item $cached -Recurse -Force
    }
}

Write-Host "Packed $version to $output"