@echo off
setlocal
cd /d "%~dp0"

echo Compilando version portable (autocontenida, no necesita .NET instalado)...
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o release

if errorlevel 1 (
  echo.
  echo ERROR: revisa que tengas el SDK de .NET 10 instalado.
  pause
  exit /b 1
)

echo.
echo Listo. Un solo ejecutable en: release\BershkaMonitor.exe
echo Puedes copiarlo a cualquier carpeta (Escritorio, Documentos, USB...).
pause
