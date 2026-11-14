@echo off
cd /d "%~dp0"
rmdir /S /Q ".\publish" 2>nul
rmdir /S /Q ".\CollectiveModdingToolkit\bin" 2>nul
rmdir /S /Q ".\CollectiveModdingToolkit\obj" 2>nul
cls
dotnet publish CollectiveModdingToolkit\CollectiveModdingToolkit.csproj -c Release -o .\publish\
del .\publish\*.pdb .\publish\*.config 2>nul
