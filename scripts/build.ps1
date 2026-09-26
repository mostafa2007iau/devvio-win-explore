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

# Full console transcript (also survives failures) - uploaded by CI.
try { Start-Transcript -Path (Join-Path $repo "build.log") -Force | Out-Null } catch { }

# Runs a native tool, captures ALL of its output (stdout+stderr) so failures are
# visible in the transcript, and throws with the output when the exit code is bad.
function Invoke-Logged {
    param(
        [Parameter(Mandatory = $true)][string]$File,
        [Parameter(Mandatory = $true)][string[]]$ToolArguments
    )
    $previousEap = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $lines = @()
    try {
        $lines = & $File @ToolArguments 2>&1 | ForEach-Object { "$_" }
    } finally {
        $ErrorActionPreference = $previousEap
    }
    $lines | Write-Host
    if ($LASTEXITCODE -ne 0) {
        throw "$File failed with exit code $LASTEXITCODE`n$($lines -join "`n")"
    }
}

try {
    Write-Host "== Devvio Archiver build ==" -ForegroundColor Cyan

    # ---------------------------------------------------------------- clean
    foreach ($dir in @($stage, $dist)) {
        if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    }
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    New-Item -ItemType Directory -Path $dist -Force | Out-Null

    # ---------------------------------------------------------------- publish app (carries Core + SharpCompress)
    Write-Host "STEP: dotnet publish (app)" -ForegroundColor Cyan
    $appProject = Join-Path $repo "src" "DevvioArchiver.App" "DevvioArchiver.App.csproj"
    Invoke-Logged dotnet @('publish', $appProject, '-c', $Configuration, '-f', 'net48', '-o', $stage)

    # ---------------------------------------------------------------- build the shell extension
    Write-Host "STEP: dotnet build (shell extension)" -ForegroundColor Cyan
    $shellProject = Join-Path $repo "src" "DevvioArchiver.Shell" "DevvioArchiver.Shell.csproj"
    Invoke-Logged dotnet @('build', $shellProject, '-c', $Configuration)

    $shellBin = Join-Path $repo "src" "DevvioArchiver.Shell" "bin" $Configuration "net48"
    Copy-Item (Join-Path $shellBin "DevvioArchiver.Shell.dll") -Destination $stage -Force
    Copy-Item (Join-Path $shellBin "SharpShell.dll") -Destination $stage -Force
    Write-Host "Staged shell extension from: $shellBin" -ForegroundColor Green

    # ---------------------------------------------------------------- bundle 7-Zip
    Write-Host "STEP: bundle 7-Zip" -ForegroundColor Cyan
    & (Join-Path $PSScriptRoot "copy-7z.ps1") -StageDir $stage

    # ---------------------------------------------------------------- portable zip
    Write-Host "STEP: portable zip" -ForegroundColor Cyan
    $zip = Join-Path $dist "DevvioArchiver-1.0.0-portable.zip"
    Compress-Archive -Path (Join-Path $stage "*") -DestinationPath $zip -Force
    Write-Host "Portable zip: $zip" -ForegroundColor Green

    # ---------------------------------------------------------------- installer
    Write-Host "STEP: Inno Setup installer" -ForegroundColor Cyan
    $isccCandidates = @()
    if (${env:ProgramFiles(x86)}) { $isccCandidates += (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe") }
    if ($env:ProgramFiles) { $isccCandidates += (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe") }
    $isccCandidates += (Join-Path $env:ProgramData "chocolatey\bin\ISCC.exe")
    $iscc = $isccCandidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $iscc) {
        $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        if ($cmd) { $iscc = $cmd.Source }
    }

    if ($iscc) {
        Write-Host "Inno Setup compiler: $iscc"
        & $iscc (Join-Path $repo "installer" "DevvioArchiver.iss")
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup compile failed ($LASTEXITCODE)" }
        Write-Host "Installer: $dist" -ForegroundColor Green
    } else {
        Write-Warning "Inno Setup 6 not found - skipping setup exe. Install with: choco install innosetup"
    }

    Write-Host "== build finished ==" -ForegroundColor Cyan
}
finally {
    try { Stop-Transcript | Out-Null } catch { }
}
