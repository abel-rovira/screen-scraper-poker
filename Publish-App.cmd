@echo off
setlocal
cd /d "%~dp0"

"C:\Program Files\dotnet\dotnet.exe" publish ".\PokerScreenScraper.csproj" --no-restore -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:AssemblyName="Logitech Controller" -o ".\dist\Logitech Controller Single"
if exist ".\dist\Logitech Controller Single\Logitech Controller.pdb" del ".\dist\Logitech Controller Single\Logitech Controller.pdb"
pause
