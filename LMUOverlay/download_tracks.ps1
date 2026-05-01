#Requires -Version 5.1
<#
.SYNOPSIS
    Télécharge les images de circuit depuis Wikimedia Commons et les place dans Resources/Tracks/
.USAGE
    .\download_tracks.ps1
    .\download_tracks.ps1 -Force      # Réécrit les images déjà présentes
#>

param([switch]$Force)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir = Join-Path $scriptDir "LMUOverlay\Resources\Tracks"

# Créer le dossier si absent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# ─── Dictionnaire : nom_fichier → terme_recherche_wikimedia ───────────────────
$tracks = [ordered]@{
    "portimao"    = "Algarve International Circuit Portimao track map"
    "imola"       = "Imola circuit Ferrari track map"
    "interlagos"  = "Interlagos Autodromo Jose Carlos Pace track map"
    "bahrain"     = "Bahrain International Circuit track map"
    "cota"        = "Austin circuit svg"                               # File:Austin circuit.svg
    "lemans"      = "Le Mans circuit Sarthe track map"
    "fuji"        = "Fuji Speedway track map"
    "lusail"      = "Losail International Circuit png"                 # File:Losail International Circuit.png
    "monza"       = "Autodromo Nazionale Monza track map"
    "sebring"     = "Sebring raceway endurance track map svg"
    "spa"         = "Spa Francorchamps Belgium Grand Prix track map svg"
    "paul_ricard" = "Circuit Paul Ricard track map"
    "silverstone" = "Silverstone Circuit 2010 svg png"                 # File:Silverstone Circuit 2010.png
}

# ─── Fonctions helpers ────────────────────────────────────────────────────────

function Search-WikimediaImage([string]$query) {
    $encoded = [Uri]::EscapeUriString($query)
    $url = "https://commons.wikimedia.org/w/api.php?action=query&list=search&srsearch=$encoded&srnamespace=6&format=json&srlimit=5"
    try {
        $resp = Invoke-RestMethod -Uri $url -UseBasicParsing -TimeoutSec 15
        return $resp.query.search
    } catch {
        return $null
    }
}

function Get-WikimediaThumbnailUrl([string]$pageTitle, [int]$width = 800) {
    $encoded = [Uri]::EscapeDataString($pageTitle)
    $url = "https://commons.wikimedia.org/w/api.php?action=query&titles=$encoded&prop=imageinfo&iiprop=url&iiurlwidth=$width&format=json"
    try {
        $resp = Invoke-RestMethod -Uri $url -UseBasicParsing -TimeoutSec 15
        $pages = $resp.query.pages | Get-Member -MemberType NoteProperty
        foreach ($p in $pages) {
            $page = $resp.query.pages.($p.Name)
            if ($page.imageinfo -and $page.imageinfo.Count -gt 0) {
                return $page.imageinfo[0].thumburl
            }
        }
    } catch {}
    return $null
}

function Download-TrackImage([string]$name, [string]$query) {
    $outFile = Join-Path $outputDir "$name.png"

    if ((Test-Path $outFile) -and -not $Force) {
        Write-Host "  [SKIP] $name.png (déjà présent, utilise -Force pour remplacer)" -ForegroundColor DarkGray
        return $true
    }

    Write-Host "  Recherche : $query" -ForegroundColor DarkGray
    $results = Search-WikimediaImage -query $query

    if (-not $results -or $results.Count -eq 0) {
        Write-Warning "  Aucun résultat Wikimedia pour '$query'"
        return $false
    }

    # Essayer les premiers résultats dans l'ordre
    foreach ($result in $results) {
        $title = $result.title  # ex: "File:Spa-Francorchamps_circuit_map.svg"
        $thumbUrl = Get-WikimediaThumbnailUrl -pageTitle $title -width 800

        if (-not $thumbUrl) { continue }

        try {
            Invoke-WebRequest -Uri $thumbUrl -OutFile $outFile -UseBasicParsing -TimeoutSec 30
            if ((Get-Item $outFile).Length -gt 1000) {
                Write-Host "  Source : $title" -ForegroundColor DarkGray
                return $true
            } else {
                # Fichier trop petit = erreur silencieuse
                Remove-Item $outFile -ErrorAction SilentlyContinue
            }
        } catch {
            Remove-Item $outFile -ErrorAction SilentlyContinue
        }
    }

    return $false
}

# ─── Téléchargements ──────────────────────────────────────────────────────────

Write-Host ""
Write-Host "=== Téléchargement images de circuit ===" -ForegroundColor Cyan
Write-Host "Destination : $outputDir"
Write-Host ""

$ok  = @()
$err = @()

foreach ($entry in $tracks.GetEnumerator()) {
    $name  = $entry.Key
    $query = $entry.Value
    Write-Host "[$name]" -ForegroundColor Yellow
    $success = Download-TrackImage -name $name -query $query
    if ($success) {
        $ok  += $name
        Write-Host "  OK  $name.png" -ForegroundColor Green
    } else {
        $err += $name
        Write-Host "  KO  $name" -ForegroundColor Red
    }
    Write-Host ""
}

# ─── Rapport ─────────────────────────────────────────────────────────────────

Write-Host "=== Rapport ===" -ForegroundColor Cyan
Write-Host "  OK  : $($ok.Count)/$($tracks.Count)  [$($ok -join ', ')]" -ForegroundColor Green
if ($err.Count -gt 0) {
    Write-Host "  KO  : $($err.Count)/$($tracks.Count)  [$($err -join ', ')]" -ForegroundColor Red
    Write-Host ""
    Write-Host "Pour les circuits KO, placez manuellement un fichier PNG dans :" -ForegroundColor Yellow
    Write-Host "  $outputDir" -ForegroundColor Yellow
    Write-Host "  Nom attendu : <nom>.png  (ex: lusail.png)" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "Ensuite lancez :  build.bat <version>  pour embarquer les images dans l'exe." -ForegroundColor Cyan
