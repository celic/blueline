<#
.SYNOPSIS
    Exports the seasons named in seed/manifest.json and, optionally, uploads them to a release.

.DESCRIPTION
    Run this after backfilling a season you want to make available. It exports each season listed
    in the manifest, records the checksum, and can attach the files to a GitHub release.

    Publishing needs a remote and the gh CLI. Without them the archives are still produced and the
    manifest still updated, so the files are ready to upload by hand.

.PARAMETER Tag
    Release tag to publish under. Defaults to the manifest's tag.

.PARAMETER Publish
    Actually upload. Without this the script only exports and updates the manifest.

.EXAMPLE
    ./scripts/publish-seasons.ps1
    ./scripts/publish-seasons.ps1 -Publish
#>
[CmdletBinding()]
param(
    [string] $Tag,
    [switch] $Publish
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Join-Path $PSScriptRoot '..' | Resolve-Path
$seedDirectory = Join-Path $repositoryRoot 'seed'
$manifestPath = Join-Path $seedDirectory 'manifest.json'

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$effectiveTag = if ($Tag) { $Tag } else { $manifest.release.tag }

foreach ($season in $manifest.seasons) {
    $target = Join-Path $seedDirectory $season.file

    Write-Host "Exporting $($season.label)..."
    dotnet run --project (Join-Path $repositoryRoot 'src/Blueline.Cli') -- `
        export $season.seasonId $target
    if ($LASTEXITCODE -ne 0) { throw "Export failed for $($season.label). Has it been backfilled?" }

    $season.sha256 = (Get-FileHash $target -Algorithm SHA256).Hash.ToLowerInvariant()
    $size = (Get-Item $target).Length / 1MB
    Write-Host ("  {0}  {1:N2} MB  {2}" -f $season.file, $size, $season.sha256.Substring(0, 16))
}

$manifest | ConvertTo-Json -Depth 6 | Set-Content $manifestPath -Encoding utf8
Write-Host "`nManifest updated with checksums."

if (-not $Publish) {
    Write-Host "`nNot publishing. Re-run with -Publish to upload, or attach the files by hand."
    exit 0
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'The gh CLI is required to publish. Install it, or upload the archives by hand.'
}

$assets = $manifest.seasons | ForEach-Object { Join-Path $seedDirectory $_.file }

if (gh release view $effectiveTag 2>$null) {
    Write-Host "Updating release $effectiveTag..."
    gh release upload $effectiveTag @assets --clobber
}
else {
    Write-Host "Creating release $effectiveTag..."
    gh release create $effectiveTag @assets `
        --title "Season archives" `
        --notes 'Season archives for Blueline. Fetch with scripts/fetch-seasons.ps1, or load one with `Blueline.Cli import`.'
}

Write-Host "`nPublished. Set release.baseUrl in seed/manifest.json to the release's download URL."
