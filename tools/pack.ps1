# P7: Windows CLI zip + Blender add-on zip. Never copies work/, game archives, AES, or mappings.
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$dist = Join-Path $root "dist"
$publishDir = Join-Path $dist "cli"
$stage = Join-Path $dist "stage"
$cliName = "wuwa2blender-win-x64"
$cliRoot = Join-Path $stage $cliName
$cliZip = Join-Path $dist "$cliName.zip"
$addonZip = Join-Path $dist "wuwa_model_tools.zip"
$addonRoot = Join-Path $stage "wuwa_model_tools"

if (Test-Path $dist) {
    Remove-Item $dist -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDir, $cliRoot, $addonRoot | Out-Null

Write-Host "dotnet publish src/Wuwa.Cli win-x64 self-contained"
dotnet publish (Join-Path $root "src/Wuwa.Cli/Wuwa.Cli.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $publishDir `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item -Path (Join-Path $publishDir '*') -Destination $cliRoot -Recurse -Force

function Expand-EmbeddedNatives([string]$assemblyPath, [string]$destDir, [string]$prefix) {
    $assembly = [Reflection.Assembly]::LoadFrom($assemblyPath)
    foreach ($name in $assembly.GetManifestResourceNames()) {
        if (-not $name.StartsWith($prefix, [StringComparison]::Ordinal)) {
            continue
        }
        $leaf = $name.Substring($prefix.Length)
        if ($leaf -notmatch '\.(dll|so|dylib)$') {
            continue
        }
        $stream = $assembly.GetManifestResourceStream($name)
        if ($null -eq $stream) {
            throw "Missing embedded resource $name in $assemblyPath"
        }
        $out = Join-Path $destDir $leaf
        $file = [IO.File]::Create($out)
        try {
            $stream.CopyTo($file)
        }
        finally {
            $file.Dispose()
            $stream.Dispose()
        }
        Write-Host "extracted native $leaf"
    }
}

$conversionDll = Join-Path $publishDir "CUE4Parse-Conversion.dll"
if (-not (Test-Path $conversionDll)) {
    throw "Published CLI missing CUE4Parse-Conversion.dll"
}
Expand-EmbeddedNatives $conversionDll $cliRoot "CUE4Parse_Conversion.Resources."

function Copy-Into([string]$from, [string]$to) {
    $destDir = Split-Path -Parent $to
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    }
    Copy-Item $from $to -Force
}

Copy-Into (Join-Path $root "config/wuwa.example.json") (Join-Path $cliRoot "config/wuwa.example.json")
Copy-Into (Join-Path $root "config/search-aliases.json") (Join-Path $cliRoot "config/search-aliases.json")
Get-ChildItem (Join-Path $root "config/material-profiles") -Filter *.json | ForEach-Object {
    Copy-Into $_.FullName (Join-Path $cliRoot "config/material-profiles/$($_.Name)")
}
Copy-Into (Join-Path $root "blender/scripts/batch_import.py") (Join-Path $cliRoot "blender/scripts/batch_import.py")
Get-ChildItem (Join-Path $root "blender/addon/wuwa_model_tools") -Filter *.py | ForEach-Object {
    Copy-Into $_.FullName (Join-Path $cliRoot "blender/addon/wuwa_model_tools/$($_.Name)")
    Copy-Into $_.FullName (Join-Path $addonRoot $_.Name)
}
Copy-Into (Join-Path $root "config/material-profiles/3x.json") (Join-Path $addonRoot "profiles/3x.json")
Copy-Into (Join-Path $root "README.md") (Join-Path $cliRoot "README.md")
Copy-Into (Join-Path $root "THIRD_PARTY.md") (Join-Path $cliRoot "THIRD_PARTY.md")

Get-ChildItem $cliRoot -Recurse -Include *.pdb, *.user | Remove-Item -Force
Get-ChildItem $cliRoot, $addonRoot -Recurse -Directory -Filter __pycache__ | Remove-Item -Recurse -Force

$exe = Join-Path $cliRoot "wuwa2blender.exe"
$detex = Join-Path $cliRoot "Detex.dll"
if (-not (Test-Path $exe)) { throw "Published CLI missing: $exe" }
if (-not (Test-Path $detex)) { throw "Published CLI missing Detex.dll (BC texture decode)." }
if (-not (Test-Path (Join-Path $cliRoot "blender/scripts/batch_import.py"))) {
    throw "Published payload missing blender/scripts/batch_import.py"
}
if (-not (Test-Path (Join-Path $cliRoot "config/material-profiles/3x.json"))) {
    throw "Published payload missing config/material-profiles/3x.json"
}

$printed = & $exe --version
if ($LASTEXITCODE -ne 0) { throw "wuwa2blender --version failed with $LASTEXITCODE" }
Write-Host "published version: $printed"

if (Test-Path $cliZip) { Remove-Item $cliZip -Force }
if (Test-Path $addonZip) { Remove-Item $addonZip -Force }
Compress-Archive -Path $cliRoot -DestinationPath $cliZip -Force
Compress-Archive -Path $addonRoot -DestinationPath $addonZip -Force

Add-Type -AssemblyName System.IO.Compression.FileSystem

function Get-ZipNames([string]$zipPath) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        return @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    }
    finally {
        $archive.Dispose()
    }
}

$forbiddenFragments = @(
    'wuwa.local.json',
    'work/',
    '.pak',
    '.ucas',
    '.utoc',
    '.uasset',
    '.uexp',
    '.ubulk',
    '.usmap',
    'aes.json',
    '__pycache__/'
)

function Assert-CleanZip([string]$zipPath, [string[]]$required) {
    $names = Get-ZipNames $zipPath
    foreach ($need in $required) {
        if (-not ($names | Where-Object { $_ -eq $need -or $_.EndsWith("/$need") })) {
            throw "$zipPath missing required entry $need. Entries:`n$($names -join "`n")"
        }
    }
    foreach ($name in $names) {
        $lower = $name.ToLowerInvariant()
        foreach ($fragment in $forbiddenFragments) {
            if ($lower.Contains($fragment.ToLowerInvariant())) {
                throw "$zipPath contains forbidden entry $name"
            }
        }
    }
    Write-Host "$([IO.Path]::GetFileName($zipPath)): $($names.Count) entries, $((Get-Item $zipPath).Length) bytes"
}

Assert-CleanZip $cliZip @(
    "$cliName/wuwa2blender.exe",
    "$cliName/Detex.dll",
    "$cliName/config/wuwa.example.json",
    "$cliName/config/search-aliases.json",
    "$cliName/config/material-profiles/3x.json",
    "$cliName/blender/scripts/batch_import.py",
    "$cliName/blender/addon/wuwa_model_tools/__init__.py",
    "$cliName/README.md",
    "$cliName/THIRD_PARTY.md"
)
Assert-CleanZip $addonZip @(
    "wuwa_model_tools/__init__.py",
    "wuwa_model_tools/manifest_io.py",
    "wuwa_model_tools/pipeline.py",
    "wuwa_model_tools/profiles/3x.json"
)

Write-Host "Packed:"
Write-Host "  $cliZip"
Write-Host "  $addonZip"
