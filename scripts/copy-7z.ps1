# Copies 7z.exe + 7z.dll into <StageDir>\7z so the app has the full codec set
# (7z, zip, rar, cab, iso, wim, lzh, arj, cpio, deb, rpm, ... ) even when the
# user has no 7-Zip installed. Looks for an installed 7-Zip first.

param(
    [Parameter(Mandatory = $true)]
    [string]$StageDir
)

$ErrorActionPreference = "Stop"

$target = Join-Path $StageDir "7z"
New-Item -ItemType Directory -Path $target -Force | Out-Null

$candidates = @()

foreach ($reg in @("HKLM:\SOFTWARE\7-Zip", "HKLM:\SOFTWARE\WOW6432Node\7-Zip")) {
    if (Test-Path $reg) {
        $path = (Get-ItemProperty $reg -ErrorAction SilentlyContinue).Path
        if ($path) { $candidates += (Join-Path $path.TrimEnd('\') "7z.exe") }
    }
}
$candidates += (Join-Path $env:ProgramFiles "7-Zip\7z.exe")
if (${env:ProgramFiles(x86)}) { $candidates += (Join-Path ${env:ProgramFiles(x86)} "7-Zip\7z.exe") }

$sevenZip = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $sevenZip) {
    Write-Warning "7-Zip was not found on this machine. The app will fall back to the managed engine (reduced format set). Install 7-Zip (https://www.7-zip.org) and re-run the build to bundle it."
    exit 0
}

$dir = Split-Path $sevenZip -Parent
Copy-Item $sevenZip -Destination $target -Force
Copy-Item (Join-Path $dir "7z.dll") -Destination $target -Force
Write-Host "Bundled 7-Zip from: $sevenZip" -ForegroundColor Green
