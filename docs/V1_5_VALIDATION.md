# FieldSim v1.5 validation

Validation performed in the packaging environment:

- JavaScript syntax: `node --check` passed for `src/FieldSim.Web/wwwroot/app.js`.
- All JSON files parsed successfully.
- All project/solution XML files parsed successfully.
- Every DOM id referenced by the web JavaScript exists in `index.html`.
- New C# source files passed basic structural/brace checks.
- The legacy `Red APC` demo label was removed and replaced with a generic light support vehicle.
- The separately supplied third-party scenario-editor JavaScript and CSS bundle filenames are absent from the FieldSim package.
- ZIP integrity was checked after packaging.

The packaging environment does not contain the .NET 10 SDK, so a real C# compiler/build pass could not be run here. `build_windows.bat` remains the final compile-and-test check on a machine with .NET 10 installed.
