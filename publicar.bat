@echo off
rem Gera o Trin0 Master APP pronto para levar a outro PC (nao precisa instalar o .NET la).
rem Saida: ..\Trin0MasterApp-dist\Trin0MasterApp.exe + LEIA-ME.txt
cd /d "%~dp0"
echo Fechando o Trin0MasterApp se estiver aberto ...
taskkill /f /im Trin0MasterApp.exe >nul 2>&1
taskkill /f /im ResetExplode.exe >nul 2>&1
echo Gerando Trin0MasterApp.exe a partir do codigo atual ...
rmdir /s /q obj\Release >nul 2>&1
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o "..\Trin0MasterApp-dist" -nologo -v q
if errorlevel 1 (
    echo.
    echo FALHOU. Veja os erros acima. O .exe antigo NAO foi atualizado.
    pause
    exit /b 1
)
copy /y "LEIA-ME.txt" "..\Trin0MasterApp-dist\LEIA-ME.txt" >nul
echo.
echo Pronto: %~dp0..\Trin0MasterApp-dist
for %%F in ("..\Trin0MasterApp-dist\Trin0MasterApp.exe") do echo Arquivo gerado em %%~tF (confira se e de agora^)
pause