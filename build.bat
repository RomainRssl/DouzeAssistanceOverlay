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

:: --- Vérifier que la version n'existe pas déjà sur GitHub ---
where gh >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    gh release view "v!VERSION!" --repo "%GITHUB_REPO%" >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        echo.
        echo  ERREUR : La release v!VERSION! existe deja sur GitHub.
        echo  Choisissez un autre numero de version.
        pause
        exit /b 1
    )
)

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
:: Étape 1b : Régénérer app.ico depuis logo.png
:: ============================================================
echo [1b] Regeneration app.ico depuis logo.png...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Add-Type -AssemblyName System.Drawing; $src=[System.Drawing.Image]::FromFile('LMUOverlay\Resources\logo.png'); $dst='LMUOverlay\Resources\app.ico'; $sizes=@(256,128,64,48,32,24,16); $streams=@(); foreach($sz in $sizes){$bmp=New-Object System.Drawing.Bitmap($sz,$sz);$g=[System.Drawing.Graphics]::FromImage($bmp);$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality;$g.DrawImage($src,0,0,$sz,$sz);$ms=New-Object System.IO.MemoryStream;$bmp.Save($ms,[System.Drawing.Imaging.ImageFormat]::Png);$streams+=$ms;$g.Dispose();$bmp.Dispose()};$src.Dispose();$out=New-Object System.IO.MemoryStream;$w=New-Object System.IO.BinaryWriter($out);$w.Write([uint16]0);$w.Write([uint16]1);$w.Write([uint16]$sizes.Count);$off=6+16*$sizes.Count;for($i=0;$i -lt $sizes.Count;$i++){$b=$streams[$i].ToArray();$d=if($sizes[$i]-ge 256){0}else{$sizes[$i]};$w.Write([byte]$d);$w.Write([byte]$d);$w.Write([byte]0);$w.Write([byte]0);$w.Write([uint16]1);$w.Write([uint16]32);$w.Write([uint32]$b.Length);$w.Write([uint32]$off);$off+=$b.Length};foreach($ms in $streams){$w.Write($ms.ToArray());$ms.Dispose()};$w.Flush();[System.IO.File]::WriteAllBytes($dst,$out.ToArray());$out.Dispose();$w.Dispose();Write-Host 'ICO OK'"
if %ERRORLEVEL% NEQ 0 ( echo ERREUR etape 1b & pause & exit /b 1 )
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
powershell -NoProfile -ExecutionPolicy Bypass -Command "$v='!VERSION!'; $url='https://github.com/RomainRssl/DouzeAssistanceOverlay/releases/download/v'+$v+'/DouzeAssistance_Setup_v'+$v+'.exe'; $xml=[string]::Format('<?xml version=\"1.0\" encoding=\"UTF-8\"?><item><version>{0}</version><url>{1}</url><changelog>https://github.com/RomainRssl/DouzeAssistanceOverlay/releases</changelog><mandatory>false</mandatory></item>',$v,$url); Set-Content -Path 'update.xml' -Value $xml -Encoding UTF8"
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
git push --set-upstream origin main
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
