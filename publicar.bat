@echo off
cd /d "%~dp0"

echo Fechando processos antigos se estiverem abertos...
powershell -NoProfile -Command "Get-Process -Name 'Trin0MasterApp','ResetExplode' -ErrorAction SilentlyContinue | Stop-Process -Force" >nul 2>&1
taskkill /f /im Trin0MasterApp.exe >nul 2>&1
taskkill /f /im ResetExplode.exe >nul 2>&1

echo Gerando Trin0MasterApp.exe a partir do codigo atual...
if exist obj\Release rmdir /s /q obj\Release >nul 2>&1

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o "..\Trin0MasterApp-dist" -nologo -v q
if errorlevel 1 (
    echo.
    echo FALHOU. Se disse que o arquivo esta em uso, feche o app no Gerenciador de Tarefas.
    pause
    exit /b 1
)

copy /y "LEIA-ME.txt" "..\Trin0MasterApp-dist\LEIA-ME.txt" >nul

echo.
echo ========================================================
echo Sucesso! Executavel gerado na pasta Trin0MasterApp-dist
echo ========================================================
echo.
pause