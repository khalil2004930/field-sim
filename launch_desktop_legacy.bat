@echo off
setlocal
dotnet run --project src\FieldSim.Desktop\FieldSim.Desktop.csproj
if errorlevel 1 pause
