$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
function dl($name, $url) {
    $dest = Join-Path $dir ($name + ".png")
    if (Test-Path $dest) { Write-Host "SKIP $name (exists)"; return }
    try {
        Invoke-WebRequest -Uri $url -OutFile $dest -UserAgent "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" -Headers @{ "Referer" = "https://commons.wikimedia.org/" } -UseBasicParsing | Out-Null
        Write-Host "OK $name"
    } catch {
        Write-Host "FAIL $name : $($_.Exception.Message)"
        if (Test-Path $dest) { Remove-Item $dest }
    }
    Start-Sleep -Seconds 3
}
# Wikimedia SVG thumbnails must use allowed step sizes: 320, 400, 640, 800
dl "toyota"      "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c8/Toyota_logo.svg/320px-Toyota_logo.svg.png"
dl "peugeot"     "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f7/Peugeot_Logo.svg/320px-Peugeot_Logo.svg.png"
dl "bmw"         "https://upload.wikimedia.org/wikipedia/commons/thumb/4/44/BMW.svg/320px-BMW.svg.png"
dl "cadillac"    "https://upload.wikimedia.org/wikipedia/commons/thumb/7/7f/Cadillac_Logo_2021.svg/320px-Cadillac_Logo_2021.svg.png"
dl "isotta"      "https://upload.wikimedia.org/wikipedia/commons/thumb/4/40/Isotta-Fraschini-Logo.svg/320px-Isotta-Fraschini-Logo.svg.png"
dl "mclaren"     "https://upload.wikimedia.org/wikipedia/commons/thumb/b/bb/Mclaren_Logo_2021.svg/320px-Mclaren_Logo_2021.svg.png"
dl "astonmartin" "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b7/Aston_Martin_wordmark.svg/320px-Aston_Martin_wordmark.svg.png"
dl "ford"        "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a0/Ford_Motor_Company_Logo.svg/320px-Ford_Motor_Company_Logo.svg.png"
dl "mercedes"    "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9e/Mercedes-Benz_Logo_2010.svg/320px-Mercedes-Benz_Logo_2010.svg.png"
dl "chevrolet"   "https://upload.wikimedia.org/wikipedia/commons/thumb/7/7c/Logo_Chevrolet.svg/320px-Logo_Chevrolet.svg.png"
dl "acura"       "https://upload.wikimedia.org/wikipedia/commons/thumb/a/af/Acura_logo.svg/320px-Acura_logo.svg.png"
Write-Host "Done."
