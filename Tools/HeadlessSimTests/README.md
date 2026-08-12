# P2-A / P2-A2 Headless Simulation Tests

Unity Editor is not required. These tests compile the simulation core against a tiny `UnityEngine` shim and validate the Phase 2-A math model.

```bash
dotnet run --project Tools/HeadlessSimTests/HeadlessSimTests/HeadlessSimTests.csproj -c Release
```

Modes:

| Arg | What it runs |
|-----|----------------|
| *(default)* | Phase2ATests + FertilityModifierDiagnostic + Phase2A2Tests |
| `p2a` | Phase 2-A acceptance suite only |
| `fertility` | Fertility ×0.70/1.00/1.30 diagnostic |
| `p2a2` | **P2-A2 math diagnostic** (population / water / food / K / events / FastForward) |

P2-A2 observes only — it does **not** modify simulation formulas or balance parameters.
