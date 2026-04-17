@echo off
echo === MU Data Server ===
echo Serving: C:\Games\MuDataServer
echo URL: http://192.168.1.222:8080/Data.zip
echo.
echo Keep this window open while Android downloads game files!
echo.
cd /d "C:\Games\MuDataServer"
python -m http.server 8080
pause
