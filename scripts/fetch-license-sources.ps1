param([string]$OutputRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$directory = Join-Path $OutputRoot 'docs/licenses/upstream'
New-Item -ItemType Directory -Force $directory | Out-Null
$items = @(
    @{Name='FFmpeg-COPYING.GPLv3.txt';Url='https://raw.githubusercontent.com/FFmpeg/FFmpeg/5c4f5744cd60943125a78db692b728210102f255/COPYING.GPLv3'},
    @{Name='FFmpeg-COPYING.LGPLv3.txt';Url='https://raw.githubusercontent.com/FFmpeg/FFmpeg/5c4f5744cd60943125a78db692b728210102f255/COPYING.LGPLv3'},
    @{Name='ScottPlot-5.1.59-LICENSE.txt';Url='https://raw.githubusercontent.com/ScottPlot/ScottPlot/fc81e78241e532daa97b47049b399dff81e80054/LICENSE'},
    @{Name='OpenTK-4.9.4-LICENSE.md';Url='https://raw.githubusercontent.com/opentk/opentk/8d1d462877936922a3989f56aac8c17add824919/LICENSE.md'},
    @{Name='OpenTK.GLWpfControl-4.3.3-LICENSE.md';Url='https://raw.githubusercontent.com/opentk/GLWpfControl/d5c1851ffe537374eeda852a4ea958b94647ad9c/LICENSE.md'},
    @{Name='BtbN-build-recipes-LICENSE.txt';Url='https://raw.githubusercontent.com/BtbN/FFmpeg-Builds/937868360a21d711ec94a33f5591d345ad9b7754/LICENSE'},
    @{Name='OpenTK-4.9.4-THIRD_PARTIES.md';Url='https://raw.githubusercontent.com/opentk/opentk/8d1d462877936922a3989f56aac8c17add824919/THIRD_PARTIES.md'},
    @{Name='WindowsSDK-license.rtf';Url='https://download.microsoft.com/download/0/F/F/0FF2B061-47DD-4F55-89B6-FD1D8C44F14D/sdk_license.rtf'}
)
$records = foreach ($item in $items) {
    $target = Join-Path $directory $item.Name
    Invoke-WebRequest -Uri $item.Url -OutFile $target -TimeoutSec 30
    [ordered]@{File="upstream/$($item.Name)";Url=$item.Url;SHA256=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash}
}
Add-Type -AssemblyName System.Windows.Forms
$reader = New-Object System.Windows.Forms.RichTextBox
try {
    $reader.LoadFile((Join-Path $directory 'WindowsSDK-license.rtf'))
    $plain = Join-Path $directory 'WindowsSDK-license.txt'
    $reader.Text | Set-Content -LiteralPath $plain -Encoding utf8
    $records += [ordered]@{File='upstream/WindowsSDK-license.txt';Url=$items[-1].Url;SHA256=(Get-FileHash -LiteralPath $plain).Hash;Transformation='Plain text extracted from the retained RTF using Windows RichTextBox; the RTF is authoritative.'}
}
finally { $reader.Dispose() }
$records | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutputRoot 'docs/licenses/upstream-provenance.json') -Encoding utf8
Write-Output "Downloaded $($items.Count) version-pinned license files."
