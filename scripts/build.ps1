# Devvio Archiver - build script (Windows, PowerShell)
# Builds the app, stages all files, bundles 7-Zip and (if Inno Setup is
# installed) compiles the setup exe. Works both locally and on CI.

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repo = Split-Path $PSScriptRoot -Parent
$stage = Join-Path $repo "stage"
$dist = Join-Path $repo "dist"
$shellOut = Join-Path $repo "obj" "shellout"

Write-Host "== Devvio Archiver build ==" -ForegroundColor Cyan

# ---------------------------------------------------------------- clean
foreach ($dir in @($stage, $dist, $shellOut)) {
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
}
New-Item -ItemType Directory -Path $stage -Force | Out-Null
New-Item -ItemType Directory -Path $dist -Force | Out-Null

# ---------------------------------------------------------------- publish app (carries Core + SharpCompress)
$appProject = Join-Path $repo "src" "DevvioArchiver.App" "DevvioArchiver.App.csproj"
dotnet publish $appProject -c $Configuration -f net48 -o $stage
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# ---------------------------------------------------------------- build the shell extension
$shellProject = Join-Path $repo "src" "DevvioArchiver.Shell" "DevvioArchiver.Shell.csproj"
dotnet build $shellProject -c $Configuration -f net48 -o $shellOut
if ($LASTEXITCODE -ne 0) { throw "dotnet build (shell) failed" }
Copy-Item (Join-Path $shellOut "DevvioArchiver.Shell.dll") -Destination $stage -Force
Copy-Item (Join-Path $shellOut "SharpShell.dll") -Destination $stage -Force

# ---------------------------------------------------------------- bundle 7-Zip
& (Join-Path $PSScriptRoot "copy-7z.ps1") -StageDir $stage

# ---------------------------------------------------------------- portable zip
$zip = Join-Path $dist "DevvioArchiver-1.0.0-portable.zip"
Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -Force
Write-Host "Portable zip: $zip" -ForegroundColor Green

# ---------------------------------------------------------------- installer
$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    $iscc = Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"
}
if (Test-Path $iscc) {
    & $iscc (Join-Path $repo "installer" "DevvioArchiver.iss")
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed" }
    Write-Host "Installer: $dist" -ForegroundColor Green
} else {
    Write-Warning "Inno Setup 6 not found - skipping setup exe. Install with: choco install innosetup"
}

Write-Host "== build finished ==" -ForegroundColor Cyan
