param([string]$OutputDirectory, [switch]$IncludeLong)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $project 'artifacts/fixtures/한글 공백 & 특수' }
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$ffmpeg = Join-Path $project 'third_party/tools/ffmpeg.exe'
function Invoke-Fixture([string]$name, [string[]]$inputs, [int]$tracks) {
    $arguments = @('-nostdin', '-v', 'error', '-y', '-f', 'lavfi', '-i', 'color=c=0x14243a:s=320x180:r=2:d=20') + $inputs
    $arguments += @('-map', '0:v')
    for ($i = 1; $i -le $tracks; $i++) { $arguments += @('-map', "$($i):a") }
    $arguments += @('-c:v', 'mpeg4', '-q:v', '10', '-c:a', 'aac', '-b:a', '64k', '-t', '20', (Join-Path $OutputDirectory $name))
    & $ffmpeg @arguments
    if ($LASTEXITCODE -ne 0) { throw "Fixture failed: $name" }
}
$mic = 'aevalsrc=0.6*sin(2*PI*440*t)*between(t\,6\,7):s=16000:d=20'
$chat = 'aevalsrc=0.6*sin(2*PI*600*t)*between(t\,8\,9):s=16000:d=20'
$game = 'aevalsrc=0.4*sin(2*PI*220*t)*between(t\,5\,6):s=16000:d=20'
$mix = 'aevalsrc=0.3*sin(2*PI*440*t)*between(t\,6\,7)+0.3*sin(2*PI*600*t)*between(t\,8\,9)+0.2*sin(2*PI*220*t)*between(t\,5\,6):s=16000:d=20'
$silent = 'anullsrc=r=16000:cl=mono:d=20'
$steady = 'aevalsrc=0.5*sin(2*PI*300*t):s=16000:d=20'
$six = @()
foreach ($source in @($mix,$mic,$chat,$game,$silent,$steady)) { $six += @('-f','lavfi','-i',$source) }
Invoke-Fixture 'OBS_6tracks.mp4' $six 6
Invoke-Fixture 'burst_lag.mp4' @('-f','lavfi','-i',$mic,'-f','lavfi','-i',$chat) 2
Invoke-Fixture 'steady_background.mp4' @('-f','lavfi','-i',$steady) 1
Invoke-Fixture 'silent.mp4' @('-f','lavfi','-i',$silent) 1
Invoke-Fixture 'one_audio.mp4' @('-f','lavfi','-i',$mic) 1
Invoke-Fixture 'offset_streams.mp4' @('-f','lavfi','-i',$steady,'-itsoffset','3','-f','lavfi','-i',$steady) 2
Invoke-Fixture 'no_audio.mp4' @() 0
[System.IO.File]::WriteAllText((Join-Path $OutputDirectory 'corrupt.mp4'), 'not an MP4')
if ($IncludeLong) {
    & $ffmpeg -nostdin -v error -y -stream_loop -1 -i (Join-Path $OutputDirectory 'OBS_6tracks.mp4') -map 0 -c copy -t 18000 (Join-Path $OutputDirectory 'long_5h.mp4')
    if ($LASTEXITCODE -ne 0) { throw 'Long fixture generation failed' }
}
Write-Output "Fixtures: $OutputDirectory"
