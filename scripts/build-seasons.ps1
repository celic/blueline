<#
.SYNOPSIS
    Exports the seasons named in seed/manifest.json to archive files.

.DESCRIPTION
    Run this after backfilling a season, to produce an archive the deployment can load without
    re-ingesting from the league. Each season is exported and its checksum recorded in the
    manifest.

    The archives are deliberately not published anywhere. They are collected data, needed only
    where the site actually runs, so move them to the deployment yourself — mounted into the
    container's volume, or copied into seed/ before building the image.

.EXAMPLE
    ./scripts/build-seasons.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$seedDirectory = Join-Path $repositoryRoot 'seed'
$manifestPath = Join-Path $seedDirectory 'manifest.json'

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

foreach ($season in $manifest.seasons) {
    $target = Join-Path $seedDirectory $season.file

    Write-Host "Exporting $($season.label)..."
    $cliProject = Join-Path $repositoryRoot 'src/Blueline.Cli'
    dotnet run --project $cliProject -- export $season.seasonId $target
    if ($LASTEXITCODE -ne 0) { throw "Export failed for $($season.label). Has it been backfilled?" }

    $season.sha256 = (Get-FileHash $target -Algorithm SHA256).Hash.ToLowerInvariant()
    $size = (Get-Item $target).Length / 1MB
    Write-Host ("  {0}  {1:N2} MB  {2}" -f $season.file, $size, $season.sha256.Substring(0, 16))
}

$manifest | ConvertTo-Json -Depth 6 | Set-Content $manifestPath -Encoding utf8
Write-Host "`nManifest updated with checksums."

Write-Host ""
Write-Host @"
Archives built. They are not published anywhere by design.

To use them, either copy them into seed/ before building the image, or mount them into the
container's data volume and run:

    docker compose run --rm --entrypoint dotnet blueline Blueline.Cli.dll import /data/<file>
"@
