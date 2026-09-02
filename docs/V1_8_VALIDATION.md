# FieldSim v1.8 validation

## Static checks completed in the packaging environment

- every JSON document parses;
- every project/solution/XAML XML document parses;
- the web JavaScript passes `node --check`;
- all scenario entity keys are unique;
- every entity-to-ORBAT binding resolves;
- every small-arm assignment resolves;
- the ORBAT parent graph is acyclic;
- public platform records resolve to source records;
- support capability ids are unique;
- IDF-side synthetic placements are south of Hezbollah-side placements in local-world Y;
- the open-ended state guard exists before terminal tactical scoring;
- infrastructure and support regression checks are included in `FieldSim.Tests`.

## Required Windows compiler pass

The packaging environment does not contain the .NET SDK, `csc`, or `mcs`, so compiler success is not claimed. On Windows with the .NET 10 SDK installed, run:

```bat
build_windows.bat
```

That command builds the solution with warnings treated as errors and runs the executable regression suite. Any compiler or test failure remains a release blocker and should be reported with its exact file, line and error message.
