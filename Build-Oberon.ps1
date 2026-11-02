$ErrorActionPreference = 'Stop'

$projectRoot = $PSScriptRoot
$sourceDirectory = Join-Path $projectRoot 'Oberon'
$bundleDirectory = Join-Path $projectRoot 'PingN\PingN.Desktop\BundledCores\Oberon'

if (-not (Test-Path (Join-Path $sourceDirectory 'go.mod'))) {
    throw "Oberon source was not found at '$sourceDirectory'."
}

$goVersionText = (& go version 2>&1 | Out-String).Trim()
if ($LASTEXITCODE -ne 0 -or $goVersionText -notmatch 'go version go(?<major>\d+)\.(?<minor>\d+)') {
    throw 'Go 1.25 or later is required to build the updated Oberon core.'
}

$goMajor = [int]$Matches.major
$goMinor = [int]$Matches.minor
if ($goMajor -lt 1 -or ($goMajor -eq 1 -and $goMinor -lt 25)) {
    throw "Go 1.25 or later is required; found '$goVersionText'."
}

New-Item -ItemType Directory -Force -Path $bundleDirectory | Out-Null
Push-Location $sourceDirectory
try {
    & go build -trimpath -o (Join-Path $bundleDirectory 'oberon.exe') .
    if ($LASTEXITCODE -ne 0) {
        throw "Oberon build failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

foreach ($file in @('LICENSE', 'README.md', 'README.fa.md', 'UPSTREAM_README.md')) {
    Copy-Item -Force (Join-Path $sourceDirectory $file) (Join-Path $bundleDirectory $file)
}

@'
Oberon v0.4.5 Windows x64 executable, built from the source in the PingN source package.
Upstream: https://github.com/oberon-core/oberon
The executable is licensed under MIT. See LICENSE, README.md, and UPSTREAM_README.md.
PingN invokes Oberon in local SOCKS5 proxy mode; this mode forwards TCP and does
not require Wintun or elevated permissions. The Oberon trademark policy is in
TRADEMARK.md. This integration does not imply endorsement by the Oberon project.
'@ | Set-Content -Encoding utf8 (Join-Path $bundleDirectory 'PINGN-NOTICE.txt')

Write-Host "Built Oberon v0.4.5 and replaced '$bundleDirectory\oberon.exe'."
