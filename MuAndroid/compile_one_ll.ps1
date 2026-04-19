param([string]$LlcExe, [string]$LlFile, [string]$MinSdk)
$n = [IO.Path]::GetFileNameWithoutExtension($LlFile)
$o = [IO.Path]::ChangeExtension($LlFile, ".o")
$t = if     ($n -match "x86_64")         { "x86_64-linux-android$MinSdk" }
     elseif ($n -match "aarch64|arm64")  { "aarch64-linux-android$MinSdk" }
     elseif ($n -match "armeabi|\.arm\.") { "armv7-linux-androideabi$MinSdk" }
     elseif ($n -match "\.x86\.")         { "i686-linux-android$MinSdk" }
     else                                 { "x86_64-linux-android$MinSdk" }
Write-Host "LLC $n -> $t"
& $LlcExe -O2 -filetype=obj -mtriple=$t -relocation-model=pic -o $o $LlFile
exit $LASTEXITCODE
