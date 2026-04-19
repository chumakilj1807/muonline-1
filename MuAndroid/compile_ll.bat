@echo off
REM compile_ll.bat LLFILE LLCEXE MINSDK
REM Compiles a single .ll file to .o using llc.exe
REM Triple is determined from the filename.

set LLFILE=%~1
set LLC=%~2
set MINSDK=%~3
if "%MINSDK%"=="" set MINSDK=29

REM Determine target triple from filename
set TRIPLE=x86_64-linux-android%MINSDK%
echo %LLFILE% | findstr /i "aarch64" >nul 2>&1 && set TRIPLE=aarch64-linux-android%MINSDK%
echo %LLFILE% | findstr /i "arm64-v8a" >nul 2>&1 && set TRIPLE=aarch64-linux-android%MINSDK%
echo %LLFILE% | findstr /i "armeabi" >nul 2>&1 && set TRIPLE=armv7-linux-androideabi%MINSDK%
echo %LLFILE% | findstr /i "\.arm\." >nul 2>&1 && set TRIPLE=armv7-linux-androideabi%MINSDK%
echo %LLFILE% | findstr /i "x86_64" >nul 2>&1 && set TRIPLE=x86_64-linux-android%MINSDK%

REM Output: same path with .o extension
set OUTFILE=%LLFILE:.ll=.o%

echo LLC: %LLFILE% -^> %TRIPLE%
"%LLC%" -O2 -filetype=obj -mtriple=%TRIPLE% -relocation-model=pic -o "%OUTFILE%" "%LLFILE%"
exit /b %ERRORLEVEL%
