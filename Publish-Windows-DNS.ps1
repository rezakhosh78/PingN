param([ValidateSet('win-x64','win-x86')][string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$arch = if ($Runtime -eq 'win-x86') { '386' } else { 'amd64' }
$masterArch = if ($arch -eq '386') { 'X86' } else { 'AMD64' }
$stage = Join-Path $PSScriptRoot ('dns-stage-' + [guid]::NewGuid().ToString('N'))
$output = Join-Path $PSScriptRoot "release\$Runtime"
New-Item -ItemType Directory -Force $stage | Out-Null
try {
    $release = Invoke-RestMethod 'https://api.github.com/repos/masterking32/MasterDnsVPN/releases/latest' -Headers @{ 'User-Agent' = 'PingN' }
    $asset = $release.assets | Where-Object name -eq "MasterDnsVPN_Client_Windows_$masterArch.zip" | Select-Object -First 1
    if (!$asset -or !$asset.digest -or !$asset.digest.StartsWith('sha256:')) { throw 'MasterDNS release asset / SHA256 is missing.' }
    $zip = Join-Path $stage 'master.zip'
    Invoke-WebRequest $asset.browser_download_url -OutFile $zip
    if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $asset.digest.Substring(7)) { throw 'MasterDNS checksum mismatch.' }
    Expand-Archive $zip (Join-Path $stage 'master')
    $dnsttName = "dnstt-client-windows-$arch.exe"
    $dnsttRelease = Invoke-RestMethod 'https://api.github.com/repos/NNdroid/dnstt/releases/latest' -Headers @{ 'User-Agent' = 'PingN' }
    $dnsttAsset = $dnsttRelease.assets | Where-Object name -eq $dnsttName | Select-Object -First 1
    if (!$dnsttAsset -or !$dnsttAsset.digest -or !$dnsttAsset.digest.StartsWith('sha256:')) { throw 'DNSTT client asset / SHA256 is missing.' }
    $expected = $dnsttAsset.digest.Substring(7)
    $dnstt = Join-Path $stage 'dnstt-client.exe'
    Invoke-WebRequest $dnsttAsset.browser_download_url -OutFile $dnstt
    if ((Get-FileHash $dnstt -Algorithm SHA256).Hash -ne $expected) { throw 'DNSTT client checksum mismatch.' }
    dotnet publish (Join-Path $PSScriptRoot 'PingN\PingN.Desktop\PingN.Desktop.csproj') -c Release -r $Runtime --self-contained true -o $output
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    $masterDest = Join-Path $output 'bin\dnstunnels\masterdns'
    $dnsttDest = Join-Path $output 'bin\dnstunnels\dnstt-ssh'
    New-Item -ItemType Directory -Force $masterDest,$dnsttDest | Out-Null
    $clientNames = @('masterdnsvpn-client.exe', "MasterDnsVPN_Client_Windows_$masterArch.exe", "MasterDnsVPN_Client_Windows_$masterArch")
    $clientFiles = @(Get-ChildItem (Join-Path $stage 'master') -Recurse -File | Where-Object { $_.Name -in $clientNames -or $_.Name -match "^MasterDnsVPN_Client_Windows_${masterArch}_v[0-9][A-Za-z0-9._-]*\.exe$" })
    if ($clientFiles.Count -ne 1) { throw 'Expected one MasterDNS client executable in the archive.' }
    $exe = $clientFiles[0]
    if (!$exe) { throw 'MasterDNS executable is absent from the release archive.' }
    Copy-Item $exe.FullName (Join-Path $masterDest 'masterdnsvpn-client.exe')
    $sample = Get-ChildItem (Join-Path $stage 'master') -Recurse -Filter client_config.toml* | Select-Object -First 1
    if ($sample) { Copy-Item $sample.FullName (Join-Path $masterDest 'client_config.toml.simple') }
    Copy-Item $dnstt (Join-Path $dnsttDest 'dnstt-client.exe')
    Set-Content (Join-Path $masterDest 'SOURCE.txt') "https://github.com/masterking32/MasterDnsVPN $($release.tag_name) $($asset.digest)"
    Set-Content (Join-Path $dnsttDest 'SOURCE.txt') "https://github.com/NNdroid/dnstt ; client release $($dnsttRelease.tag_name) ; SHA256:$expected"
    Set-Content (Join-Path $masterDest 'version.txt') $release.tag_name
    Set-Content (Join-Path $dnsttDest 'version.txt') $dnsttRelease.tag_name
    Write-Host "Published with both verified DNS client cores: $output"
} finally { Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue }
