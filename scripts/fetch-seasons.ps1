<#
.SYNOPSIS
    Downloads the season archives listed in seed/manifest.json.

.DESCRIPTION
    The archives are release assets rather than repository contents, so a fresh clone has the
    manifest but not the data. Run this before building the image, or before running the site
    locally if you would rather not re-ingest a season from the league's API.

    Already-present archives are left alone unless -Force is given, so re-running is cheap.

.PARAMETER BaseUrl
    Overrides the manifest's base URL, for a mirror or a local file share.

.PARAMETER Force
    Re-download archives that are already present.

.EXAMPLE
    ./scripts/fetch-seasons.ps1
#>
[CmdletBinding()]
param(
    [string] $BaseUrl,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'

# Join-Path takes only two paths in Windows PowerShell 5.1; the multi-argument form is 7+.
$seedDirectory = Resolve-Path (Join-Path (Join-Path $PSScriptRoot '..') 'seed')
$manifestPath = Join-Path $seedDirectory 'manifest.json'

if (-not (Test-Path $manifestPath)) {
    throw "No manifest at $manifestPath."
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$effectiveBaseUrl = if ($BaseUrl) { $BaseUrl } else { $manifest.release.baseUrl }

if (-not $effectiveBaseUrl) {
    Write-Warning @'
The manifest has no release base URL, so there is nowhere to download from yet.

Until the archives are published, build them locally instead:

    dotnet run --project src/Blueline.Cli -- backfill 20252026
    dotnet run --project src/Blueline.Cli -- export 20252026 seed/20252026.blueline.gz

Or let the site ingest a season by itself on first run, which is the default.
'@
    exit 0
}

$downloaded = 0
$skipped = 0
$failed = 0

foreach ($season in $manifest.seasons) {
    $target = Join-Path $seedDirectory $season.file

    if ((Test-Path $target) -and -not $Force) {
        Write-Host "  $($season.label): already present"
        $skipped++
        continue
    }

    $url = "$($effectiveBaseUrl.TrimEnd('/'))/$($season.file)"
    Write-Host "  $($season.label): downloading from $url"

    # To a temporary name first, so an interrupted download cannot leave a truncated archive
    # sitting where the app would try to import it.
    $temporary = "$target.partial"
    try {
        Invoke-WebRequest -Uri $url -OutFile $temporary -UseBasicParsing

        if ($season.sha256) {
            $actual = (Get-FileHash $temporary -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actual -ne $season.sha256.ToLowerInvariant()) {
                throw "Checksum mismatch for $($season.file): expected $($season.sha256), got $actual."
            }
        }
        else {
            Write-Warning "  $($season.label): no checksum in the manifest; downloaded without verification."
        }

        Move-Item $temporary $target -Force
        $downloaded++
    }
    catch {
        # One unavailable season should not stop the others, and the usual cause is simply that
        # the release has not been published yet. Any existing archive is left untouched.
        Write-Warning "  $($season.label): $($_.Exception.Message)"
        $failed++
    }
    finally {
        if (Test-Path $temporary) { Remove-Item $temporary -Force }
    }
}

Write-Host ""
Write-Host "$downloaded downloaded, $skipped already present, $failed unavailable."

if ($failed -gt 0) {
    Write-Host ""
    Write-Host @"
Some archives could not be downloaded. If the release does not exist yet, build them locally:

    dotnet run --project src/Blueline.Cli -- backfill <seasonId>
    pwsh ./scripts/publish-seasons.ps1

Or let the site ingest a season by itself on first run, which is the default.
"@
}
