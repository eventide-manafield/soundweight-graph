param([string]$Executable, [string]$ReportName = 'app-smoke')
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
if (-not $Executable) { $Executable = Join-Path $project 'src/SoundWeightGraph.App/bin/Release/net10.0-windows10.0.19041.0/win-x64/SoundWeightGraph.exe' }
$fixture = Join-Path $project 'artifacts/fixtures/한글 공백 & 특수/OBS_6tracks.mp4'
$report = Join-Path $project "artifacts/$ReportName.json"
$arguments = @('--smoke-test', ('"' + $fixture + '"'), ('"' + $report + '"'))
$process = Start-Process -FilePath $Executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
if (-not $process.WaitForExit(60000)) { throw "Smoke test timed out. PID=$($process.Id)" }
if ($process.ExitCode -ne 0) { throw "App exited with code $($process.ExitCode)" }
if (-not (Test-Path -LiteralPath $report)) { throw 'No smoke report' }
Get-Content -LiteralPath $report
$result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if (-not $result.Success) { throw 'Smoke test failed' }
