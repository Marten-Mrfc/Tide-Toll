# sim: TideToll.Sim

Purpose: the engine-free simulation library. The game (`game/`) and the tests (`sim.tests/`) both reference it, so the same code runs in both.

Rules: no Godot types, no `System.Random`, no clock. Everything is seeded and deterministic. Grids are flat arrays indexed `y * width + x`.

Contents (Phase 0, ported from `prototypes/desire-lines/src/sim/`):

| File | What it holds |
| --- | --- |
| `Rng.cs` | `Mulberry32` and `Hash.Hash2`, bit for bit equal to the toy's `rng.ts` |
| `Grid.cs` | grid shape, neighbour tables and `Line4` (from `grid.ts`) |
| `BinaryHeap.cs`, `CostField.cs` | the heap and the Dijkstra cost-to-go field (from `path.ts`) |
| `SimParams.cs` | map and time scale |

Tests: `sim.tests/`. Run `dotnet test` from the repository root.
