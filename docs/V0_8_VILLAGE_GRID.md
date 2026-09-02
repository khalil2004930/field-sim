# v0.8 village-grid layer

The desktop observer now treats a village map as a self-contained 13×13 tactical state.

## Reference format

- Row: Alpha through Mike (13 rows)
- Column: 01 through 13
- Cell reference: `Alpha-01`
- Keypad reference: `Alpha-01 / KP5`

Keypads are arranged as:

```text
7 8 9
4 5 6
1 2 3
```

The current catalog contains three sectors and four fictional village placeholders per sector. `VillageMapCatalog.cs` is the single place to replace those placeholders when the project later receives approved village names/art.

`VillageTrainingScenario.cs` generates deterministic schematic terrain for each placeholder using a seed stored in the catalog. It is deliberately non-georeferenced.
