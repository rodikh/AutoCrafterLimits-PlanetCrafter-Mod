# AutoCrafterLimits

Adds per-AutoCrafter output and input constraints for The Planet Crafter.

## Features

- **Output limit** per Auto-Crafter: craft until target stock exists (in range or planet-wide).
- **Input thresholds** per ingredient: only craft when each enabled threshold is met.
- **Planet-wide counting**: optional toggle for both output and input to count across all player-built containers on the planet (default: in-range only).
- **Hybrid behavior** (`Output && Input`): all enabled constraints must pass.
- Cached inventory scans (2-second refresh).
- Per-crafter JSON persistence (keyed by world object id).
- **IMGUI** limits window (toggle with the **Limits** button).
- Blocked-crafting reason shown as an orange/red message near the bottom-right (~78% height) when a constraint stops crafting.

## Installation

1. Install [BepInEx](https://docs.bepinex.dev/) for The Planet Crafter.
2. Extract the mod zip into your game folder (e.g. `Steam\steamapps\common\The Planet Crafter`).
3. The DLL should end up at `BepInEx\plugins\AutoCrafterLimits.dll`.

## UI usage

1. Open an Auto-Crafter.
2. Click **Limits** (placed next to the selected recipe image in the panel).
3. Configure:
   - **Craft until have X**: Enable output limit + target amount (`0` = unlimited).
   - **Count in containers planet-wide**: Optional. When enabled, output limit counts across all player-built containers; when disabled, only containers in Auto-Crafter range.
   - **When ingredients ≥ X**: Enable input thresholds + per-ingredient values (`0` = no threshold for that ingredient).
   - **Count in containers planet-wide**: Same option for input thresholds.
4. Duplicate ingredients in a recipe (e.g. two of the same ore) are shown as **one row per resource kind**.

## Counting rules

- **In range (default):** Inventories/containers in Auto-Crafter range plus the Auto-Crafter's own inventory. Range uses the game's `GetGroupsInRangeForListing()`, so modded range changes are respected.
- **Planet-wide (optional):** All player-built containers on the planet.
- **Excluded:** Dropped world items (not in an inventory).

## Save file

Per-machine settings are stored **per game save** in a BepInEx subfolder:

`BepInEx/config/AutoCrafterLimits/{saveFileName}.json`

Config is loaded when you load a save and saved when the game saves (manual or auto-save). Entries are removed when the corresponding Auto-Crafter is destroyed.

---

## Source layout (what each file does)

| File | Role |
|------|------|
| **`Plugin.cs`** | BepInEx plugin entry: initializes `ModRuntime`, creates UI `GameObject` with `AutoCrafterLimitsUi` and `GameSaveListener`, applies Harmony (`PatchAll`). |
| **`ModRuntime.cs`** | Core runtime: **should-craft** evaluation (output + input rules, planet-wide option), **cached range/planet-wide scan** (~2s), block-reason strings, cleanup when a crafter is removed. |
| **`AutoCrafterLimitsUi.cs`** | UI: adds the **Limits** `uGUI` button, positions it beside the recipe preview image, draws the **IMGUI** settings window (`GUILayout.Window`), and draws the blocked message overlay near the bottom of the screen. |
| **`Patches.cs`** | Harmony patches: `MachineAutoCrafter.CraftIfPossible`, `UiWindowGroupSelector` open/update/close, `JSONExport.LoadFromJson`/`CreateNewSaveFile` (reload config), `WorldObjectsHandler.DestroyWorldObject`. |
| **`FieldInfoWrapper.cs`** | Small helper to read private `UiWindowGroupSelector._autoCrafter` without hardcoding fragile manual reflection at call sites. |
| **`AutoCrafterLimitConfig.cs`** | In-memory model for one Auto-Crafter: toggles, target output, planet-wide flags, per-ingredient thresholds; **adapts** when the recipe changes. |
| **`AutoCrafterConfigStore.cs`** | Loads/saves crafter configs to `BepInEx/config/AutoCrafterLimits/{saveFileName}.json`; `GetOrCreate`, `Remove`, `Save`; manual JSON serialization (JsonUtility fails on arrays). |
| **`JsonHelper.cs`** | Deserializes persisted JSON on load (handles arrays that `JsonUtility` cannot). |
| **`GameSaveListener.cs`** | Subscribes to `SavedDataHandler.OnSaved` to persist config when the game saves. |
| **`PersistenceModels.cs`** | Serializable DTOs (`PersistedStore`, `PersistedCrafterConfig`, `PersistedThreshold`) for Unity’s JSON format. |
| **`AutoCrafterLimits.csproj`** | .NET SDK project: `netstandard2.1`, references BepInEx, Harmony, game `Managed` assemblies (including `UnityEngine.JSONSerializeModule` for `JsonUtility`). |
| **`mod-spec.md`** | Design/spec notes for the mod (behavior, UX, persistence shape). Not compiled into the DLL. |
| **`.gitignore`** | Ignores `bin/` and `obj/` build output. |

---

## Build

```powershell
dotnet build -c Debug
dotnet build -c Release
```

## Deploy

```powershell
Copy-Item ".\bin\Release\netstandard2.1\AutoCrafterLimits.dll" `
  "C:\Program Files (x86)\Steam\steamapps\common\The Planet Crafter\BepInEx\plugins\AutoCrafterLimits.dll" -Force
```

Or run `.\deploy.ps1` to build and copy in one step. If copy fails with a file lock, close the game, then try again.

To create a distribution zip for Nexus Mods: `.\pack.ps1`
