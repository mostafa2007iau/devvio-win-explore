@echo off
rem ============================================================
rem  Devvio Archiver - developer unregistration.
rem  Usage:  unregister.cmd [path to DevvioArchiver.Shell.dll]
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

if exist "%DLL%" (
    echo Unregistering "%DLL%" ...
    if exist "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" (
        "%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /u "%DLL%"
    )
    if exist "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" (
        "%WINDIR%\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" /u "%DLL%"
    )
)

for %%K in (
    "*\ShellEx\ContextMenuHandlers\DevvioArchiver"
    "Directory\ShellEx\ContextMenuHandlers\DevvioArchiver"
    "Directory\Background\ShellEx\ContextMenuHandlers\DevvioArchiver"
    "Drive\ShellEx\ContextMenuHandlers\DevvioArchiver"
    "Drive\Background\ShellEx\ContextMenuHandlers\DevvioArchiver"
) do (
    reg delete "HKCR\%%~K" /f >nul 2>&1
)

reg delete "HKLM\SOFTWARE\DevvioArchiver" /v AppPath /f >nul 2>&1

echo Restarting Explorer ...
taskkill /f /im explorer.exe >nul 2>&1
start explorer.exe

echo Done.
pause
