@echo off
setlocal EnableDelayedExpansion

:: Toujours s'exécuter depuis le dossier du script
cd /d "%~dp0"

:: ============================================================
:: Douze Assistance — Publication GitHub
:: Usage : release.bat [VERSION]   ex: release.bat 2.0.0
:: Si VERSION est omis, lit la version depuis le .csproj.
:: Lancer build.bat avant ce script.
:: ============================================================

set CSPROJ=%~dp0LMUOverlay\LMUOverlay\LMUOverlay.csproj
set GITHUB_REPO=RomainRssl/DouzeAssistanceOverlay

:: --- Lire la version depuis le .csproj ---
for /f "tokens=3 delims=<>" %%v in ('findstr "<Version>" %CSPROJ%') do set CSPROJ_VERSION=%%v
if "!CSPROJ_VERSION!"=="" set CSPROJ_VERSION=1.0.0

:: --- Déterminer la version cible ---
if "%~1"=="" (
    set VERSION=!CSPROJ_VERSION!
) else (
    set VERSION=%~1
)

set OUTPUT_INSTALLER=DouzeAssistance_Setup_v!VERSION!.exe

echo.
echo ============================================================
echo   Douze Assistance — Publication GitHub v!VERSION!
echo ============================================================
echo.

:: --- Vérifier que l'installeur existe (build.bat doit avoir tourné) ---
if not exist "!OUTPUT_INSTALLER!" (
    echo  ERREUR : Installeur introuvable : !OUTPUT_INSTALLER!
    echo  Lance build.bat d'abord pour generer l'installeur.
    pause
    exit /b 1
)

:: --- Vérifier que git est disponible ---
where git >nul 2>&1
if %ERRORLEVEL% NEQ 0 ( echo ERREUR: git non trouve & pause & exit /b 1 )

:: --- Vérifier que gh est disponible ---
where gh >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo  ERREUR : gh CLI non installe.
    echo  Installez depuis https://cli.github.com puis : gh auth login
    pause
    exit /b 1
)

:: --- Vérifier que la release n'existe pas déjà sur GitHub ---
gh release view "v!VERSION!" --repo "%GITHUB_REPO%" >nul 2>&1
if !ERRORLEVEL! EQU 0 (
    echo.
    echo  ERREUR : La release v!VERSION! existe deja sur GitHub.
    echo  Choisissez un autre numero de version ou supprimez la release existante.
    pause
    exit /b 1
)

:: ============================================================
:: Étape 1 : Git commit + push
:: ============================================================
echo [1/2] Commit et push Git...

git add update.xml %CSPROJ% README.md
git commit -m "Release v!VERSION!"
if %ERRORLEVEL% NEQ 0 ( echo ERREUR: git commit echoue & pause & exit /b 1 )

git pull --rebase --autostash origin main
if %ERRORLEVEL% NEQ 0 ( echo ERREUR: git pull echoue & pause & exit /b 1 )

git push --set-upstream origin main
if %ERRORLEVEL% NEQ 0 ( echo ERREUR: git push echoue & pause & exit /b 1 )
echo      Push OK
echo.

:: ============================================================
:: Étape 2 : Créer la release GitHub
:: ============================================================
echo [2/2] Creation de la release GitHub...
gh release create "v!VERSION!" "!OUTPUT_INSTALLER!" --repo "%GITHUB_REPO%" --title "Douze Assistance v!VERSION!" --notes "Release v!VERSION!"
if %ERRORLEVEL% NEQ 0 ( echo ERREUR: gh release create echoue & pause & exit /b 1 )
echo      Release GitHub OK
echo.

echo ============================================================
echo   PUBLIE  —  v!VERSION!
echo   https://github.com/%GITHUB_REPO%/releases/tag/v!VERSION!
echo ============================================================
pause
