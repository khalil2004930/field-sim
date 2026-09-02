@echo off
setlocal
where dotnet >nul 2>nul
if errorlevel 1 (
    echo Install the .NET 10 SDK from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)
start "FieldSim browser" cmd /c "timeout /t 2 /nobreak >nul & start \"\" http://localhost:5085"
dotnet run --project src\FieldSim.Web\FieldSim.Web.csproj --urls http://localhost:5085
if errorlevel 1 pause
