@echo off
setlocal
cd /d "%~dp0"

"C:\Program Files\dotnet\dotnet.exe" run --project ".\PokerScreenScraper.csproj"
pause
