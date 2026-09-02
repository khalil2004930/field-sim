# FieldSim v0.9 Hotfix 1

Fixes a C# switch-expression precedence error in `FormationModels.OrdinalSuffix`.

Before:

```csharp
return absolute % 10 switch
```

After:

```csharp
return (absolute % 10) switch
```

The original expression produced CS0019 because the compiler parsed the switch expression in a way that attempted to apply `%` to an `int` and the resulting string expression.

No simulation data or behavior was changed by this hotfix.
