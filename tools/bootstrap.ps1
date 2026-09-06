$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$dotnet = Get-Command dotnet -ErrorAction Stop
Write-Host "dotnet: $($dotnet.Source)"
dotnet --list-sdks
$local = Join-Path $root "config\wuwa.local.json"
$example = Join-Path $root "config\wuwa.example.json"
if (-not (Test-Path $local)) {
    Copy-Item $example $local
    Write-Host "Created config/wuwa.local.json from example. Fill in local game and Blender paths."
} else {
    Write-Host "config/wuwa.local.json already exists."
}
Write-Host "Next: dotnet run --project src/Wuwa.Cli -- doctor"
