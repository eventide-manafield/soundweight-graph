param([switch]$Test, [switch]$Publish)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
Push-Location $project
try {
    if (-not (Test-Path 'third_party/tools/manifest.json')) { & ./scripts/prepare-tools.ps1 }
    dotnet restore SoundWeightGraph.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
    dotnet build SoundWeightGraph.sln -c Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    if ($Test) {
        & ./scripts/generate-fixtures.ps1 -IncludeLong
        dotnet test tests/SoundWeightGraph.Tests -c Release --no-build --logger 'trx;LogFileName=tests.trx' --results-directory artifacts/test-results
        if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
        & ./scripts/smoke-test.ps1
    }
    if ($Publish) {
        dotnet publish src/SoundWeightGraph.App -c Release --no-restore -o artifacts/publish
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
        $files = @(Get-ChildItem artifacts/publish -File)
        if ($files.Count -ne 1 -or $files[0].Name -ne 'SoundWeightGraph.exe') { throw 'Expected exactly one EXE in publish directory' }
        Get-FileHash artifacts/publish/SoundWeightGraph.exe -Algorithm SHA256 | Format-List
        & ./scripts/smoke-test.ps1 -Executable (Join-Path $project 'artifacts/publish/SoundWeightGraph.exe') -ReportName 'published-smoke'
    }
}
finally { Pop-Location }
