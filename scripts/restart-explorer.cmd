@echo off
rem Restarts Explorer so shell extensions are reloaded.
taskkill /f /im explorer.exe >nul 2>&1
start explorer.exe
echo Explorer restarted.
