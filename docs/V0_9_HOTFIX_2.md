# FieldSim v0.9 Hotfix 2

Fixes the Windows WPF compile errors reported for `MainWindow.xaml.cs`:

- `CS0103: IOException does not exist in the current context`
- `CS0103: InvalidDataException does not exist in the current context`

Cause: the WPF project did not import `System.IO` explicitly.

Fix: added `using System.IO;` to `src/FieldSim.Desktop/MainWindow.xaml.cs`.

Hotfix 1 is retained, including the corrected parenthesized switch expression in `FormationModels.cs`.
