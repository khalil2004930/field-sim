@echo off
setlocal

where dotnet >nul 2>nul
if errorlevel 1 (
    echo Install the .NET 10 SDK from https://dotnet.microsoft.com/download
    exit /b 1
)

dotnet build FieldSim.slnx -c Release
if errorlevel 1 exit /b 1

dotnet run --project tests\FieldSim.Tests -c Release --no-build
if errorlevel 1 exit /b 1

echo.
echo FieldSim v1.10 Urban Combat, C2 ^& Diagnostics build and tests completed.
echo Formations: dotnet run --project src\FieldSim.Runner -- formations idf
echo Vehicles: dotnet run --project src\FieldSim.Runner -- vehicles idf
echo Spatial: dotnet run --project src\FieldSim.Runner -- spatial
echo Engagement: dotnet run --project src\FieldSim.Runner -- engagement 120 Day
echo Map status: dotnet run --project src\FieldSim.Runner -- map status
echo Web UI: dotnet run --project src\FieldSim.Web --urls http://localhost:5085
echo Legacy desktop: dotnet run --project src\FieldSim.Desktop
