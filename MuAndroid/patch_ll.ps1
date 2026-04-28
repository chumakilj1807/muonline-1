param([string]$LlFile)
$content = Get-Content $LlFile -Raw -Encoding UTF8
$changed = $false
$toAdd = ""
if ($content -notmatch 'app_environment_variable_contents') {
    $toAdd += "`n@app_environment_variable_contents = dso_local local_unnamed_addr constant [1 x i8] c`"\00`""
    $changed = $true
}
if ($content -notmatch 'app_system_property_contents') {
    $toAdd += "`n@app_system_property_contents = dso_local local_unnamed_addr constant [1 x i8] c`"\00`""
    $changed = $true
}
if ($changed) {
    # Append without BOM using StreamWriter
    $writer = [System.IO.StreamWriter]::new($LlFile, $true, [System.Text.UTF8Encoding]::new($false))
    $writer.Write($toAdd)
    $writer.Close()
    $oFile = $LlFile -replace '\.ll$', '.o'
    if (Test-Path $oFile) { Remove-Item $oFile -Force }
    Write-Host "Patched: $LlFile"
}
