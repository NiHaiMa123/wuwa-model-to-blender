$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    $python = Get-Command py -ErrorAction SilentlyContinue
}
if ($python) {
    Write-Host "Python unit tests (no bpy)"
    Get-ChildItem "$root/tests/python/test_*.py" | ForEach-Object {
        Write-Host "  $($_.Name)"
        & $python.Source $_.FullName
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
} else {
    Write-Host "Skipping Python tests: python not on PATH."
}

Write-Host "dotnet unit tests (no game archives)"
dotnet test "$root/WuwaModelToBlender.slnx"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$local = Join-Path $root "config\wuwa.local.json"
if (Test-Path $local) {
    $blender = (Get-Content $local -Raw | ConvertFrom-Json).blender.executable
    if ($blender -and (Test-Path $blender)) {
        Write-Host "Blender smoke (self-authored UEFormat fixture)"
        $env:WUWA_BLENDER_SMOKE = "1"
        dotnet test "$root/tests/Wuwa.Extractor.Tests" --filter HeadlessImport_SelfAuthoredUeFormatFixture --no-build
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } else {
        Write-Host "Skipping Blender smoke: blender.executable missing."
    }
    dotnet run --project "$root/src/Wuwa.Cli" -- doctor
} else {
    Write-Host "Skipping doctor / Blender smoke: config/wuwa.local.json is missing."
}

if ($env:WUWA_INTEGRATION_TESTS -eq "1") {
    Write-Host "Integration tests already included when WUWA_INTEGRATION_TESTS=1 during dotnet test."
} else {
    Write-Host "Skipping game-install integration tests. Set WUWA_INTEGRATION_TESTS=1 to enable."
}
