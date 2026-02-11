@echo off
REM Start the SQL Server container for local development.
REM The window stays open; closing it stops the container.
cd /d "%~dp0"
echo SQL-container starten. Sluit dit venster om de container te stoppen.
echo.
docker compose up sql
if errorlevel 1 (
  echo.
  echo Er is een fout opgetreden. Controleer Docker en probeer opnieuw.
  pause
  exit /b 1
)