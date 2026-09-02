# FieldSim v1.2 validation

## Completed in the packaging environment

- Parsed all JSON/GeoJSON, WPF XAML and solution XML.
- Verified unique WPF control names and resolution of all event-handler references.
- Verified balanced source braces and inspected every v1.2 engine/UI integration point.
- Verified all 12 scenario village IDs and required theater-map layers remain present.
- Verified the autonomous scenario has 12 distinct starts, 12 command destinations, one G07 objective and a flat/open engagement zone.
- Verified every added combat event is typed and the UI consumes activity and combat timelines.
- Verified the ZIP archive after packaging.

## Required executable validation on Windows

The packaging environment has no .NET SDK or Windows WPF runtime. With the .NET 10 SDK installed, run:

```bat
dotnet build .\FieldSim.slnx -c Release
dotnet run --project .\tests\FieldSim.Tests\FieldSim.Tests.csproj -c Release
dotnet run --project .\src\FieldSim.Runner\FieldSim.Runner.csproj -- engagement 180 Day
dotnet run --project .\src\FieldSim.Desktop\FieldSim.Desktop.csproj
```

The new regression test fails if units have no orders/routes, never move, never make contact, never fire, never create a casualty state or diverge for identical seeds.
