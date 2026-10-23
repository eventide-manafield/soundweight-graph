param([string]$Archive)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$thirdParty = Join-Path $project 'third_party'
$expected = @('D33EAD5ED152CE6E2BEB6D8B0B209984ACFD9E9513E2A55B6855E6AB0956B5EF', '5148F63C3CF28FBEE7BC0679C3F1C5794EDC5F36F583E7C82595D2DA8F9330A8')
if (-not $Archive) { $Archive = Join-Path $thirdParty 'ffmpeg-download.zip' }
if (-not (Test-Path -LiteralPath $Archive)) {
    $url = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-10-22-13-06/ffmpeg-n8.1.3-23-g5c4f5744cd-win64-lgpl-8.1.zip'
    Invoke-WebRequest $url -OutFile $Archive
}
if ((Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash -notin $expected) { throw 'FFmpeg archive SHA-256 mismatch' }
$zip = [System.IO.Compression.ZipFile]::OpenRead($Archive)
try { $folder = $zip.Entries[0].FullName.Split('/')[0] } finally { $zip.Dispose() }
$unpacked = Join-Path $thirdParty 'download'
Expand-Archive -LiteralPath $Archive -DestinationPath $unpacked -Force
$source = Join-Path $unpacked $folder
$tools = Join-Path $thirdParty 'tools'
New-Item -ItemType Directory -Force $tools | Out-Null
$hashes = [ordered]@{}
foreach ($name in @('ffmpeg.exe', 'ffprobe.exe')) {
    Copy-Item -LiteralPath (Join-Path $source "bin/$name") -Destination (Join-Path $tools $name) -Force
    $hashes[$name] = (Get-FileHash -LiteralPath (Join-Path $tools $name) -Algorithm SHA256).Hash
}
$build = (& (Join-Path $tools 'ffmpeg.exe') -version 2>&1 | Out-String)
if ($build -match '--enable-gpl|--enable-nonfree') { throw 'Unexpected FFmpeg license configuration' }
$build | Set-Content -LiteralPath (Join-Path $tools 'build-info.txt') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $source 'LICENSE.txt') -Destination (Join-Path $tools 'FFmpeg-LGPL-3.0.txt') -Force
@{ Version = '8.1.3-5c4f5744cd-20261022'; Hashes = $hashes } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $tools 'manifest.json') -Encoding utf8
Write-Output 'Embedded FFmpeg tools prepared and verified.'
