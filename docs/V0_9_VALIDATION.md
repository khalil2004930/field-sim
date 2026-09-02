# v0.9 validation note

The packaging environment used for this revision does not include the .NET 10 SDK, so a native C# build could not be executed here.

The following static checks were completed before packaging:

- all JSON files parse successfully;
- all XAML, project and solution XML files parse successfully;
- all MainWindow XAML event handlers resolve to code-behind methods;
- all XAML `x:Name` identifiers are unique;
- C# files pass delimiter/balance checks after comments/string literals are excluded;
- IDF formation dataset has 217 unique nodes, valid parent links, valid source links and no hierarchy cycle;
- ground-vehicle dataset has 6 unique definitions, valid source links, signatures within 0–1 and synthetic armor indexes within 0–1000;
- the Desktop project now copies `data/` and `assets/` into its output and references Core, Domain, Data and Scenarios;
- the ZIP archive is tested after creation.

On Windows, `build_windows.bat` performs the authoritative `dotnet build -c Release` followed by the executable FieldSim test suite. Any compiler/runtime issue found there should be treated as the final source of truth.
