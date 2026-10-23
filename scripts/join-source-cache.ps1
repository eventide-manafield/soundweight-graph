param([string]$Directory = $PSScriptRoot)
$ErrorActionPreference = 'Stop'
$manifest = Get-Content -LiteralPath (Join-Path $Directory 'ffmpeg-dependency-sources-manifest.json') -Raw | ConvertFrom-Json
$target = Join-Path $Directory 'download-cache.zip'
if (Test-Path -LiteralPath $target) { throw 'download-cache.zip already exists; choose an empty output location.' }
foreach ($part in $manifest.parts) {
    $file = Join-Path $Directory $part.name
    if ((Get-Item -LiteralPath $file).Length -ne $part.bytes -or (Get-FileHash -LiteralPath $file).Hash -ne $part.sha256) { throw "Part checksum mismatch: $($part.name)" }
}
$output = [IO.File]::Create($target)
try {
    foreach ($part in $manifest.parts) {
        $inputStream = [IO.File]::OpenRead((Join-Path $Directory $part.name))
        try { $inputStream.CopyTo($output) } finally { $inputStream.Dispose() }
    }
} finally { $output.Dispose() }
if ((Get-Item -LiteralPath $target).Length -ne $manifest.bytes -or (Get-FileHash -LiteralPath $target).Hash -ne $manifest.sha256) { throw 'Joined source cache checksum mismatch' }
Write-Output 'Verified download-cache.zip. Extract cache.tar.gz, then use GNU tar on Linux/WSL to preserve the cache symlinks.'
