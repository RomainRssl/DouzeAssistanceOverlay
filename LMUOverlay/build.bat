@echo off
setlocal EnableDelayedExpansion

:: Toujours s'exécuter depuis le dossier du script
cd /d "%~dp0"

:: ============================================================
:: Douze Assistance — Build + Installeur
:: Usage : build.bat [VERSION]   ex: build.bat 2.0.0
:: Pour publier sur GitHub, lancer release.bat ensuite.
:: ============================================================

set ISCC="C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
set CSPROJ=%~dp0LMUOverlay\LMUOverlay\LMUOverlay.csproj
set PUBLISH_DIR=%~dp0LMUOverlay\bin\Release\net8.0-windows\win-x64\publish
set DIST_DIR=%~dp0dist

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
echo [1/5] Mise a jour version dans .csproj ^(v!CURRENT_VERSION! -^> v!VERSION!^)...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$lt=[char]60; $gt=[char]62; $f='!CSPROJ!'; $v='!VERSION!'; $c=[IO.File]::ReadAllText($f); $tag=$lt+'Version'+$gt; $etag=$lt+'/Version'+$gt; $n=[regex]::Replace($c,$tag+'.*?'+$etag,$tag+$v+$etag); [IO.File]::WriteAllText($f,$n,[Text.Encoding]::UTF8); $check=[IO.File]::ReadAllText($f); if($check -notmatch ($tag+[regex]::Escape($v)+$etag)){ Write-Error 'Version non mise a jour'; exit 1 }"
if %ERRORLEVEL% NEQ 0 ( echo ERREUR etape 1 & pause & exit /b 1 )
echo      OK
echo.

:: ============================================================
:: Étape 1b : Mettre à jour le badge version dans README.md
:: ============================================================
echo [1b] Mise a jour version dans README.md...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$c = Get-Content -Path 'README.md' -Raw; $c = $c -replace 'version-[0-9]+\.[0-9]+\.[0-9]+-blue', 'version-!VERSION!-blue'; Set-Content -Path 'README.md' -Value $c -NoNewline"
if %ERRORLEVEL% NEQ 0 ( echo ERREUR etape 1b & pause & exit /b 1 )
echo      OK
echo.

:: ============================================================
:: Étape 1c : Régénérer app.ico depuis logo.png
:: ============================================================
echo [1c] Regeneration app.ico depuis logo.png...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Add-Type -AssemblyName System.Drawing; $src=[System.Drawing.Image]::FromFile('LMUOverlay\LMUOverlay\Resources\logo.png'); $dst='LMUOverlay\LMUOverlay\Resources\app.ico'; $sizes=@(256,128,64,48,32,24,16); $streams=@(); foreach($sz in $sizes){$bmp=New-Object System.Drawing.Bitmap($sz,$sz);$g=[System.Drawing.Graphics]::FromImage($bmp);$g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality;$g.DrawImage($src,0,0,$sz,$sz);$ms=New-Object System.IO.MemoryStream;$bmp.Save($ms,[System.Drawing.Imaging.ImageFormat]::Png);$streams+=$ms;$g.Dispose();$bmp.Dispose()};$src.Dispose();$out=New-Object System.IO.MemoryStream;$w=New-Object System.IO.BinaryWriter($out);$w.Write([uint16]0);$w.Write([uint16]1);$w.Write([uint16]$sizes.Count);$off=6+16*$sizes.Count;for($i=0;$i -lt $sizes.Count;$i++){$b=$streams[$i].ToArray();$d=if($sizes[$i]-ge 256){0}else{$sizes[$i]};$w.Write([byte]$d);$w.Write([byte]$d);$w.Write([byte]0);$w.Write([byte]0);$w.Write([uint16]1);$w.Write([uint16]32);$w.Write([uint32]$b.Length);$w.Write([uint32]$off);$off+=$b.Length};foreach($ms in $streams){$w.Write($ms.ToArray());$ms.Dispose()};$w.Flush();[System.IO.File]::WriteAllBytes($dst,$out.ToArray());$out.Dispose();$w.Dispose();Write-Host 'ICO OK'"
if %ERRORLEVEL% NEQ 0 ( echo ERREUR etape 1c & pause & exit /b 1 )
echo      OK
echo.

:: ============================================================
:: Étape 2 : Compilation
:: ============================================================
echo [2/5] Compilation...
dotnet publish %CSPROJ% -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=false -p:Version=!VERSION! -p:AssemblyVersion=!VERSION! -p:FileVersion=!VERSION! -o %PUBLISH_DIR%
if %ERRORLEVEL% NEQ 0 ( echo ERREUR etape 2 & pause & exit /b 1 )
echo      OK
echo.

:: ============================================================
:: Étape 3 : Préparer le dossier dist
:: ============================================================
echo [3/5] Preparation du dossier dist...
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
echo [4/5] Creation de l'installeur...
if not exist %ISCC% ( echo ERREUR: Inno Setup introuvable & pause & exit /b 1 )
%ISCC% "installer\DouzeAssistance.iss" /DMyAppVersion=!VERSION!
if %ERRORLEVEL% NEQ 0 ( echo ERREUR etape 4 & pause & exit /b 1 )
echo      OK — !OUTPUT_INSTALLER!
echo.

:: ============================================================
:: Étape 5 : update.xml
:: ============================================================
echo [5/5] Mise a jour update.xml...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$v='!VERSION!'; $url='https://github.com/RomainRssl/DouzeAssistanceOverlay/releases/download/v'+$v+'/DouzeAssistance_Setup_v'+$v+'.exe'; $xml=[string]::Format('<?xml version=\"1.0\" encoding=\"UTF-8\"?><item><version>{0}.0</version><url>{1}</url><changelog>https://github.com/RomainRssl/DouzeAssistanceOverlay/releases</changelog><mandatory>false</mandatory></item>',$v,$url); Set-Content -Path 'update.xml' -Value $xml -Encoding UTF8"
if %ERRORLEVEL% NEQ 0 ( echo ERREUR etape 5 & pause & exit /b 1 )
echo      OK
echo.

echo ============================================================
echo   BUILD TERMINE  —  v!VERSION!
echo   Installeur : !OUTPUT_INSTALLER!
echo.
echo   Pour publier sur GitHub, lancer maintenant :
echo     release.bat
echo ============================================================
pause
