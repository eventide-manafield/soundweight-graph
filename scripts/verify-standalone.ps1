$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$isolated = Join-Path $project 'artifacts/단독 실행 확인'
New-Item -ItemType Directory -Force $isolated | Out-Null
$exe = Join-Path $isolated 'SoundWeightGraph.exe'
Copy-Item -LiteralPath (Join-Path $project 'artifacts/publish/SoundWeightGraph.exe') -Destination $exe -Force
if (@(Get-ChildItem -LiteralPath $isolated -File).Count -ne 1) { throw 'Expected EXE only' }
$report = Join-Path $project 'artifacts/standalone-smoke.json'
$info = [System.Diagnostics.ProcessStartInfo]::new($exe)
$info.UseShellExecute = $false
$info.WorkingDirectory = $isolated
$info.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
$info.Environment['PATH'] = Join-Path $env:SystemRoot 'System32'
$info.Environment['DOTNET_ROOT'] = Join-Path $isolated 'no-installed-runtime'
$info.Environment['DOTNET_ROOT_X64'] = Join-Path $isolated 'no-installed-runtime'
$info.ArgumentList.Add('--smoke-test')
$info.ArgumentList.Add((Join-Path $project 'artifacts/fixtures/한글 공백 & 특수/OBS_6tracks.mp4'))
$info.ArgumentList.Add($report)
$process = [System.Diagnostics.Process]::Start($info)
$modulePath = $null
$observedModules = @()
$timer = [System.Diagnostics.Stopwatch]::StartNew()
while (-not $process.HasExited -and $timer.Elapsed.TotalSeconds -lt 30 -and -not $modulePath) {
    $process.Refresh()
    try { $observedModules = @($process.Modules | Select-Object ModuleName,FileName); $modulePath = ($observedModules | Where-Object ModuleName -EQ 'PresentationNative_cor3.dll' | Select-Object -First 1).FileName } catch { }
    if (-not $modulePath) { Start-Sleep -Milliseconds 100 }
}
if (-not $process.WaitForExit(60000)) { throw "Standalone test timed out. PID=$($process.Id)" }
if ($process.ExitCode -ne 0) { throw "Standalone exit code $($process.ExitCode)" }
$result = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
if (-not $result.Success) { throw 'Standalone smoke failed' }
if (-not $modulePath -or $modulePath -notlike '*\.net\SoundWeightGraph\*' -or @($observedModules | Where-Object FileName -Like '*\Program Files\dotnet\*').Count -gt 0) { throw 'Bundled desktop runtime not verified' }
@{ Success = $true; BundledDesktopModule = $modulePath; FilesInExecutableDirectory = @(Get-ChildItem -LiteralPath $isolated -File).Count; PathContainsDotnetOrFfmpeg = $false; CleanWindowsMachineTested = $false } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $project 'artifacts/standalone-environment.json')
Get-Content -LiteralPath $report
Get-Content -LiteralPath (Join-Path $project 'artifacts/standalone-environment.json')
