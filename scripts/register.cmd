@echo off
rem ============================================================
rem  Devvio Archiver - developer registration (no installer).
rem  Registers the shell extension directly from a build folder.
rem  Usage:  register.cmd [path to DevvioArchiver.Shell.dll]
rem ============================================================
setlocal

net session >nul 2>&1
if errorlevel 1 (
    echo Please run this script as Administrator.
    pause
    exit /b 1
)

set "DLL=%~1"
if "%DLL%"=="" set "DLL=%~dp0..\stage\DevvioArchiver.Shell.dll"
if not exist "%DLL%" (
    echo Shell extension not found: "%DLL%"
    echo Build first with scripts\build.ps1 or pass the DLL path.
    pause
    exit /b 1
)

for %%F in ("%DLL%") do set "DLLDIR=%%~dpF"

echo Registering "%DLL%" ...
if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" (
    "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /codebase "%DLL%"
)
if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" (
    "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" /codebase "%DLL%"
)

set CLSID={991DE108-BB35-4F0D-B518-466CDEBC7E53}

for %%K in (
    "*\ShellEx\ContextMenuHandlers\DevvioArchiver"
    "Directory\ShellEx\ContextMenuHandlers\DevvioArchiver"
    "Directory\Background\ShellEx\ContextMenuHandlers\DevvioArchiver"
    "Drive\ShellEx\ContextMenuHandlers\DevvioArchiver"
    "Drive\Background\ShellEx\ContextMenuHandlers\DevvioArchiver"
) do (
    reg add "HKCR\%%~K" /ve /d "%CLSID%" /f >nul
)

reg add "HKLM\SOFTWARE\DevvioArchiver" /v AppPath /t REG_SZ /d "%DLLDIR%DevvioArchiver.App.exe" /f >nul

echo Restarting Explorer ...
taskkill /f /im explorer.exe >nul 2>&1
start explorer.exe

echo Done. Right-click any file or folder to see Devvio Archiver.
pause
