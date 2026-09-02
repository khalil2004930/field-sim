# FieldSim v1.1 validation

## Completed in the packaging environment

- Parsed all JSON and GeoJSON files successfully.
- Parsed WPF XAML and solution XML successfully.
- Verified the map manifest resolves all required files.
- Verified 24 public settlement features, including all 12 scenario-village handoff IDs.
- Verified the included Geofabrik `.poly` files contain coordinate rings.
- Verified every XAML event-handler name resolves to a code-behind method.
- Verified balanced source braces and inspected the v1.1 changes against the v1.0 base.
- Scanned the map package for prohibited facility/target/deployment property fields.

## Required on Windows

The packaging environment does not contain a .NET SDK or a Windows WPF runtime. Run:

```bat
dotnet build .\FieldSim.slnx -c Release
dotnet run --project .\tests\FieldSim.Tests\FieldSim.Tests.csproj -c Release
dotnet run --project .\src\FieldSim.Runner\FieldSim.Runner.csproj -- map status
dotnet run --project .\src\FieldSim.Desktop\FieldSim.Desktop.csproj
```

The executable regression suite checks projection round-trip precision, geodesic distance, required map layers, optional-layer reporting, public-geography boundaries and all earlier deterministic simulation behavior.
