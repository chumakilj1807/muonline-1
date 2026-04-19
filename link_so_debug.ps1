$ld      = 'C:\Program Files\dotnet\packs\Microsoft.Android.Sdk.Windows\36.1.53\tools\binutils\bin\ld.exe'
$proj    = 'C:\Users\admin\Downloads\muonline\MuAndroid'
$obj     = "$proj\obj\Debug\net10.0-android\android"
$outBase = "$proj\obj\Debug\net10.0-android\app_shared_libraries"

$abis = @(
    @{ abi = 'arm64-v8a'; rt = 'C:\Program Files\dotnet\packs\Microsoft.Android.Runtime.CoreCLR.36.android-arm64\36.1.53\runtimes\android-arm64\native'; builtins = 'libclang_rt.builtins-aarch64-android.a' },
    @{ abi = 'x86_64';    rt = 'C:\Program Files\dotnet\packs\Microsoft.Android.Runtime.CoreCLR.36.android-x64\36.1.53\runtimes\android-x64\native';    builtins = 'libclang_rt.builtins-x86_64-android.a' }
)

foreach ($a in $abis) {
    $abi    = $a.abi
    $rtDir  = $a.rt
    $outDir = "$outBase\$abi"
    $outSo  = "$outDir\libxamarin-app.so"

    if (Test-Path $outSo) {
        Write-Host "SKIP (exists): $abi\libxamarin-app.so ($((Get-Item $outSo).Length) bytes)"
        continue
    }

    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    $oFiles = @(
        "$obj\typemaps.$abi.o"
        "$obj\environment.$abi.o"
        "$obj\compressed_assemblies.$abi.o"
        "$obj\marshal_methods.$abi.o"
        "$obj\jni_remap.$abi.o"
    )

    $staticLibs = @(
        '--whole-archive'
        "$rtDir\libruntime-base-common-release.a"
        "$rtDir\libruntime-base-release.a"
        '--no-whole-archive'
        "$rtDir\libxamarin-startup-release.a"
        "$rtDir\libxa-shared-bits-release.a"
        "$rtDir\libxa-java-interop-release.a"
        "$rtDir\libpinvoke-override-dynamic-release.a"
        "$rtDir\libxa-lz4-release.a"
        "$rtDir\libnet-android.release-static-release.a"
        "$rtDir\libunwind_xamarin-release.a"
        "$rtDir\libc++_static.a"
        "$rtDir\libc++abi.a"
        "$rtDir\libunwind.a"
        "$rtDir\$($a.builtins)"
    )

    $args = @(
        '--no-relax'
        '-shared'
        '-soname', 'libxamarin-app.so'
        '--build-id=sha1'
        '-z', 'relro'
        '-z', 'now'
        '-z', 'nocopyreloc'
        '--enable-new-dtags'
        '-L', $rtDir
        '--rpath-link', $rtDir
        '-o', $outSo
    ) + $oFiles + $staticLibs + @(
        '-llog', '-lc', '-ldl', '-lm', '-lz'
    )

    Write-Host "LINK: $abi -> libxamarin-app.so"
    & $ld @args 2>&1 | Write-Host
    if ($LASTEXITCODE -eq 0 -and (Test-Path $outSo)) {
        Write-Host "  OK: $((Get-Item $outSo).Length) bytes"
    } else {
        Write-Host "  FAILED: $abi (exit $LASTEXITCODE)"
    }
}

Write-Host "Done"
