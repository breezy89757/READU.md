<#
.SYNOPSIS
    Build and package READU.md for GitHub Release.
.DESCRIPTION
    Compiles the project in Release mode, copies output + docs into a clean
    staging folder, and zips it for distribution.
.PARAMETER Platform
    Target platform: x64 (default) or ARM64.
.EXAMPLE
    .\build-release.ps1
    .\build-release.ps1 -Platform ARM64
#>
param(
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

# ── Extract version from csproj ──────────────────────────────────────────────
$csproj = Join-Path $PSScriptRoot "src\READU.md.csproj"
[xml]$proj = Get-Content $csproj
$version = ($proj.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ }) | Select-Object -First 1
if (-not $version) { $version = "0.0.0" }
Write-Host "Building READU.md v$version ($Platform) ..." -ForegroundColor Cyan

# ── Build ────────────────────────────────────────────────────────────────────
$buildArgs = @(
    "build", $csproj,
    "-c", "Release",
    "-p:Platform=$Platform",
    "--nologo"
)
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

# ── Locate build output ─────────────────────────────────────────────────────
$tfm = $proj.Project.PropertyGroup | ForEach-Object { $_.TargetFramework } | Where-Object { $_ }
$outputDir = Join-Path $PSScriptRoot "src\bin\$Platform\Release\$tfm\win-$($Platform.ToLower())"
if (-not (Test-Path $outputDir)) {
    # Try without RID suffix (framework-dependent)
    $outputDir = Join-Path $PSScriptRoot "src\bin\$Platform\Release\$tfm"
}
if (-not (Test-Path $outputDir)) { throw "Build output not found at $outputDir" }
Write-Host "Output: $outputDir" -ForegroundColor Gray

# ── Stage release files ──────────────────────────────────────────────────────
$stagingDir = Join-Path $PSScriptRoot "release\READU.md-v$version-$Platform"
if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
New-Item $stagingDir -ItemType Directory -Force | Out-Null

# Copy app binaries (exclude PDB only)
Get-ChildItem $outputDir -File | Where-Object {
    $_.Extension -ne ".pdb"
} | Copy-Item -Destination $stagingDir

# Copy subdirectories needed by Windows App SDK / WinUI / WebView2 resources.
# Only skip reference assemblies, which are not needed at runtime.
Get-ChildItem $outputDir -Directory | Where-Object {
    $_.Name -ne "ref"
} | Copy-Item -Destination $stagingDir -Recurse

# ── Bundle documentation ─────────────────────────────────────────────────────
$docsToBundle = @("README.md", "LICENSE", "CHANGELOG.md", "THIRD-PARTY-NOTICES.md")
foreach ($doc in $docsToBundle) {
    $docPath = Join-Path $PSScriptRoot $doc
    if (Test-Path $docPath) {
        Copy-Item $docPath -Destination $stagingDir
        Write-Host "  + $doc" -ForegroundColor Green
    }
}

# ── Create INSTALL.txt for non-technical users ───────────────────────────────
$installGuide = @"
============================================================
  READU.md v$version — Installation Guide
============================================================

  Thank you for downloading READU.md!

  QUICK START
  -----------
  1. Extract this entire folder to any location you like
     (e.g. C:\Tools\READU.md\)

  2. Double-click  READU.md.exe  to launch

  3. Open a Markdown file:
     - Drag & drop any .md file onto the window
     - Or press Ctrl+O to browse

  OPTIONAL: SET AS DEFAULT .MD VIEWER
  ------------------------------------
  1. Right-click any .md file in File Explorer
  2. Choose "Open with" > "Choose another app"
  3. Click "More apps" then "Look for another app on this PC"
  4. Browse to READU.md.exe and select it
  5. Check "Always use this app to open .md files"

  KEYBOARD SHORTCUTS
  -------------------
  Ctrl+O           Open file
  Ctrl+E           Toggle Edit / Read mode
  Ctrl+S           Save (edit mode)
  Ctrl+P           Print / Export PDF
  Ctrl+Shift+S     Screenshot (full page)
  Ctrl+Tab         Next tab
  Ctrl+W           Close tab
  Ctrl+ +/-        Zoom in / out

  REQUIREMENTS
  -------------
  - Windows 10 (1903+) or Windows 11
  - WebView2 Runtime (pre-installed on most Windows 10/11 PCs)
    If missing, download from:
    https://developer.microsoft.com/en-us/microsoft-edge/webview2

  FEEDBACK & ISSUES
  ------------------
  https://github.com/breezy89757/READU.md/issues

============================================================
"@
$installGuide | Out-File (Join-Path $stagingDir "INSTALL.txt") -Encoding UTF8
Write-Host "  + INSTALL.txt" -ForegroundColor Green

# ── Create zip ───────────────────────────────────────────────────────────────
$zipName = "READU.md-v$version-$Platform.zip"
$zipPath = Join-Path $PSScriptRoot "release\$zipName"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$stagingDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
$zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host "`nPackaged: $zipName ($($zipSize) MB)" -ForegroundColor Green

# ── Summary ──────────────────────────────────────────────────────────────────
$fileCount = (Get-ChildItem $stagingDir -Recurse -File).Count
Write-Host @"

Release ready!
  Zip:   release\$zipName
  Files: $fileCount
  Size:  $zipSize MB

Next steps:
  1. Test the zip — extract somewhere and run READU.md.exe
  2. Upload to GitHub Release:
     gh release create v$version release\$zipName --title "v$version" --notes-file CHANGELOG.md
"@ -ForegroundColor Cyan
