# FieldSim v1.7 static validation report

FieldSim v1.7 was prepared in an environment without the .NET SDK, `csc`, or `mcs`. This report therefore records **static validation only**. It does not claim compiler success.

## Checks completed

- All 20 JSON files parse successfully.
- `src/FieldSim.Web/wwwroot/app.js` passes `node --check`.
- 11 project/XAML/props XML files parse successfully.
- All eight projects referenced by `FieldSim.slnx` exist.
- WPF `MainWindow.xaml` has no duplicate `x:Name`; referenced handlers were checked against code-behind.
- 54 C# source files pass a comment/string-aware delimiter sanity check.
- v1.7 integrated scenario contains 12 unique stable entity assignments and an authored synthetic local-meter initial placement for every integration entity.
- Every scenario ORBAT assignment references one of the 60 ORBAT nodes.
- Every scenario weapon assignment references the included 18-record Hezbollah small-arms OSINT seed database.
- The ammunition identity dataset contains six cartridge families and intentionally contains no projectile/terminal-effect profile assignments.
- ORBAT parent references exist and no parent-cycle was found.
- Every stable scenario entity key exists in `VillageTrainingScenario.cs`.
- Legacy `CellSizeMeters = 120` and `UnitOrbatAssignments` patterns are absent.
- Third-party source/application names and supplied bundle filenames checked by the clean-room audit are absent from the package source tree.

## Compiler/test requirement

Run on Windows with the .NET 10 SDK:

```bat
build_windows.bat
```

That command is the authoritative compile and executable regression-test pass. Any compiler/runtime failure found there should be treated as a release blocker and hotfixed before the package is called build-clean.
