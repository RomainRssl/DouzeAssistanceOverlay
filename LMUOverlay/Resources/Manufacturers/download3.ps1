$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
function dlViaApi($name, $filename) {
    $dest = Join-Path $dir ($name + ".png")
    if (Test-Path $dest) { Write-Host "SKIP $name"; return }
    try {
        $api = "https://commons.wikimedia.org/w/api.php?action=query&titles=File:$([uri]::EscapeDataString($filename))&prop=imageinfo&iiprop=url&iiurlwidth=320&format=json"
        $json = Invoke-RestMethod -Uri $api -UseBasicParsing
        $pages = $json.query.pages
        $page = $pages.PSObject.Properties | Select-Object -First 1
        $thumbUrl = $page.Value.imageinfo[0].thumburl
        if (-not $thumbUrl) { Write-Host "NO URL $name"; return }
        Invoke-WebRequest -Uri $thumbUrl -OutFile $dest -UseBasicParsing | Out-Null
        Write-Host "OK $name"
    } catch {
        Write-Host "FAIL $name : $($_.Exception.Message)"
        if (Test-Path $dest) { Remove-Item $dest }
    }
    Start-Sleep -Seconds 5
}
dlViaApi "ford"  "Ford_Motor_Company_Logo.svg"
dlViaApi "acura" "Acura_logo.svg"
Write-Host "Done."
