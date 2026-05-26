# Economy Revamp

BepInEx/Harmony mod workspace for Sailwind economy investigation and future economy changes.

## Current scope

- Loads as a BepInEx plugin.
- Samples Sailwind economy state on the Unity main thread.
- Publishes a read-only loopback HTTP API at `http://127.0.0.1:17777/api/economy`.
- Serves a single-page viewer at `http://127.0.0.1:17777/` when the `web` folder is beside the DLL.

The first snapshot includes:

- `IslandMarket` production, current supply, player goods counts, buy/sell/base prices, and stock availability.
- `IslandMissionOffice` hidden warehouse slots and counts via reflection.
- `DebugMarketTracker` economy constants.
- `CurrencyMarket` rates and exchange settings.
- Port destinations and approved known price reports.
- Expected economy-cycle deltas after the current soft-cap pressure is applied.
- Per-market `econTimer` countdowns, derived in-game seconds to next cycle, real-time estimates, and Sun/time-scale context.

## In-game tools

Press `F6` to toggle the Economy Debug window. It can run `1`, `10`, `50`, or `100` `IslandMarket.EconCycle` passes across all loaded markets. The `50` button mirrors the market preheat count used by the base game.

## Build

```powershell
dotnet build EconomyRevamp.csproj
```

Build output lands in `bin\Debug\` and includes `bin\Debug\EconomyRevamp.dll` plus `bin\Debug\web\index.html`.

## Deploy

Either copy `EconomyRevamp.dll` and the `web` folder into a Sailwind BepInEx plugin folder, or build with:

```powershell
dotnet build EconomyRevamp.csproj /p:DeployToGame=true
```

The deploy target uses:

```text
D:\Program Files (x86)\Steam\steamapps\common\Sailwind\BepInEx\plugins\EconomyRevamp
```

## Config

BepInEx config keys are under `Loopback API`:

- `Enabled`: starts or disables the loopback API.
- `Port`: defaults to `17777`.
- `SnapshotIntervalSeconds`: defaults to `2`.
