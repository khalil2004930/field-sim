# v1.0 validation note

The package was statically validated for XML/XAML well-formedness, JSON parsing, duplicate C# type declarations, event-handler references and ZIP integrity in the build environment.

The build environment does not contain the .NET 10 SDK, so a real Windows/.NET compilation still has to be performed with `build_windows.bat`. If the compiler reports an error, use the exact file/line/error output for the next patch.
