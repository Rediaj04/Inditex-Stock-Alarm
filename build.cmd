@echo off
setlocal
cd /d "%~dp0"
echo Compilando Bershka Monitor...
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
if errorlevel 1 (
  echo.
  echo ERROR: revisa que tengas el SDK de .NET 10 instalado.
  pause
  exit /b 1
)
echo.
echo Listo. Ejecutable en: publish\BershkaMonitor.exe
pause
