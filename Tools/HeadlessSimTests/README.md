# P2-A Headless Simulation Tests

Unity Editor is not required. These tests compile the simulation core against a tiny `UnityEngine` shim and validate the Phase 2-A math model.

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet run --project Tools/HeadlessSimTests/HeadlessSimTests/HeadlessSimTests.csproj -c Release
```

Covers the 12 required P2-A tests plus a full 360-day stability validation.
