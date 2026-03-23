@echo off
setlocal EnableDelayedExpansion

:: Toujours s'exécuter depuis le dossier du script
cd /d "%~dp0"

:: ============================================================
:: Douze Assistance — Build + Installeur + Release GitHub
:: Usage : build.bat [VERSION]   ex: build.bat 2.0.0
:: ============================================================

set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
set CSPROJ=LMUOverlay\LMUOverlay.csproj
set GITHUB_REPO=RomainRssl/DouzeAssistanceOverlay
set PUBLISH_DIR=LMUOverlay\bin\Release\net8.0-windows\win-x64\publish
set DIST_DIR=dist

:: --- Lire la version actuelle ---
for /f "tokens=3 delims=<>" %%v in ('findstr "<Version>" %CSPROJ%') do set CURRENT_VERSION=%%v
if "!CURRENT_VERSION!"=="" set CURRENT_VERSION=1.0.0

:: --- Déterminer la version cible ---
if "%~1"=="" (
    echo Version actuelle : !CURRENT_VERSION!
    set VERSION=!CURRENT_VERSION!
    set /p VERSION=Nouvelle version [Entree = garder !CURRENT_VERSION!] :
    if "!VERSION!"=="" set VERSION=!CURRENT_VERSION!
) else (
    set VERSION=%~1
)

set OUTPUT_INSTALLER=DouzeAssistance_Setup_v!VERSION!.exe

echo.
echo ============================================================
echo   Douze Assistance — Build v!VERSION!
echo ============================================================
echo.

:: ============================================================
:: Étape 1 : Mettre à jour la version dans le .csproj
:: ============================================================
echo [1/6] Mise a jour version dans .csproj ^(v!CURRENT_VERSION! -^> v!VERSION!^)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content -Path '%CSPROJ%' -Raw; $c = $c -replace '<Version>[^<]*</Version>', '<Version>!VERSION!</Version>'; Set-Content -Path '%CSPROJ%' -Value $c -NoNewline"
if %ERRORLEVEL% NEQ 0 ( echo ERREUR etape 1 & pause & exit /b 1 )
echo      OK
echo.

:: ============================================================
:: Étape 2 : Compilation
:: ============================================================
echo [2/6] Compilation...
dotnet publish %CSPROJ% -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=false -o %PUBLISH_DIR%
if %ERRORLEVEL% NEQ 0 ( echo ERREUR etape 2 & pause & exit /b 1 )
echo      OK
echo.

:: ============================================================
:: Étape 3 : Préparer le dossier dist
:: ============================================================
echo [3/6] Preparation du dossier dist...
if exist "%DIST_DIR%" rmdir /s /q "%DIST_DIR%"
mkdir "%DIST_DIR%"
copy "%PUBLISH_DIR%\DouzeAssistance.exe" "%DIST_DIR%\" >nul
for %%f in ("%PUBLISH_DIR%\*.dll")  do copy "%%f" "%DIST_DIR%\" >nul
for %%f in ("%PUBLISH_DIR%\*.json") do copy "%%f" "%DIST_DIR%\" >nul
if exist "LMUOverlay\openvr_api.dll" copy "LMUOverlay\openvr_api.dll" "%DIST_DIR%\" >nul
echo      OK
echo.

:: ============================================================
:: Étape 4 : Inno Setup
:: ============================================================
echo [4/6] Creation de l'installeur...
if not exist %ISCC% ( echo ERREUR: Inno Setup introuvable & pause & exit /b 1 )
%ISCC% "installer\DouzeAssistance.iss" /DMyAppVersion=!VERSION!
if %ERRORLEVEL% NEQ 0 ( echo ERREUR etape 4 & pause & exit /b 1 )
echo      OK — !OUTPUT_INSTALLER!
echo.

:: ============================================================
:: Étape 5 : update.xml
:: ============================================================
echo [5/6] Mise a jour update.xml...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$v='!VERSION!'; $url='https://github.com/RomainRssl/DouzeAssistanceOverlay/releases/download/v'+$v+'/DouzeAssistance_Setup_v'+$v+'.exe'; $xml='<?xml version=''1.0'' encoding=''UTF-8''?><item><version>'+$v+'</version><url>'+$url+'</url><changelog>https://github.com/RomainRssl/DouzeAssistanceOverlay/releases</changelog><mandatory>false</mandatory></item>'; Set-Content -Path 'update.xml' -Value $xml -Encoding UTF8"
if %ERRORLEVEL% NEQ 0 ( echo ERREUR etape 5 & pause & exit /b 1 )
echo      OK
echo.

:: ============================================================
:: Étape 6 : Git + GitHub Release
:: ============================================================
echo [6/6] Publication GitHub...

where git >nul 2>&1
if %ERRORLEVEL% NEQ 0 ( echo ERREUR: git non trouve & pause & exit /b 1 )

git add update.xml %CSPROJ%
git commit -m "Release v!VERSION!"
git push
if %ERRORLEVEL% NEQ 0 ( echo ERREUR: git push echoue & pause & exit /b 1 )
echo      Push OK

where gh >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo  gh CLI non installe — release GitHub ignoree.
    echo  Installez depuis https://cli.github.com puis : gh auth login
    goto :Done
)

gh release create "v!VERSION!" "!OUTPUT_INSTALLER!" --repo "%GITHUB_REPO%" --title "Douze Assistance v!VERSION!" --notes "Release v!VERSION!"
if %ERRORLEVEL% NEQ 0 ( echo ERREUR: gh release create echoue & pause & exit /b 1 )
echo      Release GitHub OK

:Done
echo.
echo ============================================================
echo   TERMINE  —  v!VERSION!
echo   Installeur : !OUTPUT_INSTALLER!
echo ============================================================
pause
