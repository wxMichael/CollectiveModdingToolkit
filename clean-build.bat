@echo off
cd /d "%~dp0"
rem Rider's Avalonia previewer loads the Release output and locks it; stop it if it is running from this repo
powershell -NoProfile -Command "Get-CimInstance Win32_Process -Filter \"Name='dotnet.exe'\" | Where-Object { $_.CommandLine -like '*Avalonia.Designer.HostApp*' -and $_.CommandLine -like '*%CD%*' } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }"
rmdir /S /Q ".\publish" 2>nul
rmdir /S /Q ".\CollectiveModdingToolkit\bin" 2>nul
rmdir /S /Q ".\CollectiveModdingToolkit\obj" 2>nul
cls
dotnet publish CollectiveModdingToolkit\CollectiveModdingToolkit.csproj -c Release -o .\publish\
del .\publish\*.pdb .\publish\*.config 2>nul
