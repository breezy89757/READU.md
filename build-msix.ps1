<#
.SYNOPSIS
    Builds READU.md in Release and packages it as an MSIX bundle for the Microsoft Store.
.DESCRIPTION
    Builds each platform self-contained (the .NET runtime and Windows App SDK ship inside), stages the
    output without debug files, stamps the csproj <Version> into a copy of src\Package.appxmanifest,
    and packs everything into release\READU.md_<version>.msixbundle with `winapp package`. The Store
    signs what you upload; pass -Cert only to sideload-test locally.
.PARAMETER Platforms
    Platforms to build: x64 and/or ARM64 (default: both).
.PARAMETER Cert
    Optional .pfx to sign the bundle with (for local installation tests).
.EXAMPLE
    .\build-msix.ps1
    .\build-msix.ps1 -Platforms x64 -Cert .\devcert.pfx
#>
param(
    [ValidateSet("x64", "ARM64")]
    [string[]]$Platforms = @("x64", "ARM64"),
    [string]$Cert
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\READU.md.csproj"
$manifestSource = Join-Path $PSScriptRoot "src\Package.appxmanifest"
[xml]$proj = Get-Content $project
$version = ($proj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ }) | Select-Object -First 1
$tfm = ($proj.Project.PropertyGroup | ForEach-Object { $_.TargetFramework } | Where-Object { $_ }) | Select-Object -First 1
if (-not $version) { throw "No <Version> in $project." }
$packageVersion = "$version.0"
Write-Host "Packaging READU.md $packageVersion for $($Platforms -join ', ') ..." -ForegroundColor Cyan

$releaseDir = Join-Path $PSScriptRoot "release"
$stagingRoot = Join-Path $releaseDir "msix-staging"
if (Test-Path $stagingRoot) { Remove-Item $stagingRoot -Recurse -Force }
New-Item $stagingRoot -ItemType Directory -Force | Out-Null

# Manifest with the csproj version (four parts), and the values Visual Studio fills in for its tokens.
$manifestText = (Get-Content $manifestSource -Raw).
    Replace('$targetnametoken$', 'READU.md').
    Replace('$targetentrypoint$', 'Windows.FullTrustApplication')
[xml]$manifest = $manifestText
$manifest.Package.Identity.Version = $packageVersion
$stagedManifest = Join-Path $stagingRoot "Package.appxmanifest"
$manifest.Save($stagedManifest)

$layouts = @()
foreach ($platform in $Platforms) {
    $rid = "win-$($platform.ToLower())"
    Write-Host "`nBuilding $platform ..." -ForegroundColor Cyan
    # Self-contained (a packaged app can't use an installed .NET), and no debug info: it would embed
    # this machine's source paths in the shipped DLLs.
    & dotnet build $project -c Release "-p:Platform=$platform" "-p:RuntimeIdentifier=$rid" -p:SelfContained=true `
        -p:DebugType=none -p:DebugSymbols=false -p:ContinuousIntegrationBuild=true --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Build failed for $platform." }

    $output = Join-Path $PSScriptRoot "src\bin\$platform\Release\$tfm\$rid"
    if (-not (Test-Path $output)) { throw "Build output not found: $output" }

    # Stage what the app needs at runtime: no debug symbols, no build bookkeeping (the .appxrecipe lists
    # local paths; winapp writes the package's own AppxManifest.xml), no leftovers from local runs.
    $layout = Join-Path $stagingRoot $platform
    New-Item $layout -ItemType Directory -Force | Out-Null
    Get-ChildItem $output -File |
        Where-Object { $_.Extension -notin @(".pdb", ".appxrecipe") -and $_.Name -ne "AppxManifest.xml" } |
        Copy-Item -Destination $layout
    Get-ChildItem $output -Directory | Where-Object { $_.Name -notin @("AppX", "READU.md.exe.WebView2") } |
        Copy-Item -Destination $layout -Recurse
    Get-ChildItem $layout -Recurse -Filter *.pdb | Remove-Item -Force

    # The tile, Store and file-type images are package content that the build doesn't copy to its output
    # (Visual Studio adds them when it packages): bring them in, then check that every file the manifest
    # names is really there, since Partner Center rejects a package that misses one.
    Copy-Item (Join-Path $PSScriptRoot "src\Images") (Join-Path $layout "Images") -Recurse -Force
    $referenced = [regex]::Matches($manifestText, '[\w\\]+\.png') | ForEach-Object { $_.Value } | Sort-Object -Unique
    $missing = $referenced | Where-Object { -not (Test-Path (Join-Path $layout $_)) }
    if ($missing) { throw "The $platform layout lacks files the manifest names: $($missing -join ', ')" }

    # The app's XAML (App.xbf, MainWindow.xbf, WinUI resources) is looked up in resources.pri. The build
    # already writes the full index under that name; winapp must not replace it with its own, which only
    # indexes the tile images (the app would then fail to start).
    if (-not (Test-Path (Join-Path $layout "resources.pri"))) { throw "resources.pri not found in the $platform layout." }

    $layouts += $layout
}

# One platform makes a single .msix (winapp only bundles several); both make the .msixbundle the Store takes.
$extension = if ($layouts.Count -gt 1) { "msixbundle" } else { "msix" }
$bundle = Join-Path $releaseDir "READU.md_$packageVersion.$extension"
if (Test-Path $bundle) { Remove-Item $bundle -Force }
$packArgs = @("package") + $layouts + @("--manifest", $stagedManifest, "--output", $bundle, "--skip-pri")
if ($Cert) { $packArgs += @("--cert", $Cert) }
Write-Host "`nPackaging ..." -ForegroundColor Cyan
& winapp @packArgs
if ($LASTEXITCODE -ne 0) { throw "winapp package failed." }

$size = [math]::Round((Get-Item $bundle).Length / 1MB, 1)
Write-Host @"

Done: release\$(Split-Path $bundle -Leaf) ($size MB)

Next steps:
  1. Make sure Identity Name / Publisher in src\Package.appxmanifest match Partner Center (Product identity).
  2. Upload the .msixbundle in Partner Center (Apps and games > READU.md > Packages).
"@ -ForegroundColor Green
