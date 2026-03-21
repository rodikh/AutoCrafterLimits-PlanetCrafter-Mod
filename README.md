# AutoCrafterLimits

Adds per-AutoCrafter output and input constraints for The Planet Crafter.

## Features

- Output limit per Auto-Crafter (craft until target stock exists in range).
- Input thresholds per ingredient (only craft when each threshold is met).
- Hybrid behavior (`Output && Input`): all enabled constraints must pass.
- Cached inventory scans (2-second refresh).
- Per-crafter JSON persistence (keyed by world object id).
- **IMGUI** limits window (toggle with the **Limits** button).
- Blocked-crafting reason shown as an on-screen message (near the bottom of the screen when a constraint stops crafting).

## UI usage

1. Open an Auto-Crafter.
2. Click **Limits** (placed next to the selected recipe image in the panel).
3. Configure:
   - **Enable Output Limit** + target amount (`0` = unlimited)
   - **Enable Input Thresholds** + per-ingredient values (`0` = no threshold for that ingredient)
4. Duplicate ingredients in a recipe (e.g. two of the same ore) are shown as **one row per resource kind**.

## Counting rules

- **Included:** inventories/containers in Auto-Crafter range, plus the Auto-Crafter’s own inventory.
- **Excluded:** dropped world items (not in an inventory).

Range uses the game’s own Auto-Crafter range logic (`GetGroupsInRangeForListing()`), so modded range changes are respected.

## Save file

Per-machine settings are stored at:

`BepInEx\config\AutoCrafterLimits.json`

Entries are removed when the corresponding world object is destroyed.

---

## Source layout (what each file does)

| File | Role |
|------|------|
| **`Plugin.cs`** | BepInEx plugin entry: initializes `ModRuntime`, creates the long-lived UI `GameObject`, applies Harmony (`PatchAll`), saves config on unload. |
| **`ModRuntime.cs`** | Core runtime: loads config path, **should-craft** evaluation (output + input rules), **cached range/inventory scan** (~2s), block-reason strings for UI, cleanup when a crafter is removed. |
| **`AutoCrafterLimitsUi.cs`** | UI: adds the **Limits** `uGUI` button, positions it beside the recipe preview image, draws the **IMGUI** settings window (`GUILayout.Window`), and draws the blocked message overlay. |
| **`Patches.cs`** | Harmony patches only: `MachineAutoCrafter.CraftIfPossible` (server-side gate), `UiWindowGroupSelector` open/update/close (attach UI), `WorldObjectsHandler.DestroyWorldObject` (drop persisted state). |
| **`FieldInfoWrapper.cs`** | Small helper to read private `UiWindowGroupSelector._autoCrafter` without hardcoding fragile manual reflection at call sites. |
| **`AutoCrafterLimitConfig.cs`** | In-memory model for one Auto-Crafter: toggles, target output, per-ingredient thresholds; **adapts** threshold map when the recipe changes. |
| **`AutoCrafterConfigStore.cs`** | Loads/saves all crafter configs to `AutoCrafterLimits.json` via `JsonUtility`; `GetOrCreate`, `Remove`, `Save`. |
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

If copy fails with a file lock, close the game (or any process holding the DLL), then copy again.
