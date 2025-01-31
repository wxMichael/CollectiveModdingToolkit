@echo off
cd "C:\Users\Michael\source\repos\CollectiveModdingToolkit"
rmdir /S /Q ".\publish"
rmdir /S /Q ".\CollectiveModdingToolkit\bin"
rmdir /S /Q ".\CollectiveModdingToolkit\obj"
rmdir /S /Q ".\MO2Launcher\bin"
rmdir /S /Q ".\MO2Launcher\obj"
rmdir /S /Q ".\MO2VFSBypass\bin"
rmdir /S /Q ".\MO2VFSBypass\obj"
cls
dotnet publish -c Release --sc -o ./publish/
del .\publish\*.pdb .\publish\*.config
