@echo off
REM Start the SQL Server container for local development.
REM Run from repo root, or the script will cd to its directory first.
cd /d "%~dp0"
docker compose up -d sql --wait
if errorlevel 1 exit /b 1
echo SQL container is up. You can now run or debug the web app.
exit /b 0
