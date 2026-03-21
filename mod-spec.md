# AutoCrafter Limit Mod — Updated Spec

## 🎯 Goal
Add configurable constraints to the Auto-Crafter so it behaves predictably and avoids:
- Overproduction
- Resource starvation

Supports **combined (hybrid) constraints**:
- Output stock limit - craft until you have x output items in range of crafter (in storage and in the crafter's inventory).
- Input resource thresholds - craft only if you have more than x of each input material in range (in storage or in the crafter's inventory).

---

## 🧠 Core Design

### Constraints Model (Composable)
All enabled constraints must pass:

```
ShouldCraft = PassesOutputLimit && PassesInputThreshold
```

---

## ⚙️ Configuration (Per AutoCrafter)

```
class AutoCrafterLimitConfig {
    bool EnableOutputLimit;
    int TargetOutputAmount;

    bool EnableInputThreshold;
    Dictionary<Item, int> InputThresholds;
}
```

### Notes
- `InputThreshold = 0` → always allow crafting for that resource
- Config is stored per AutoCrafter instance

---

## 📦 Item Counting Rules (Simplified)

Always include:
- ✅ Items in containers within range
- ✅ Items inside the AutoCrafter

Never include:
- ❌ Dropped world items

---

## 📏 Range Handling

- Use **vanilla AutoCrafter range logic**
- Respect **modded range overrides automatically**
- Do NOT hardcode range values

---

## 🔄 Behavior Rules

### 🟢 Output Constraint

```
Passes if:
current_output_in_range < TargetOutputAmount
```

- show information in autocrafter UI if crafting is blocked because of this constraint

---

### 🔵 Input Constraint

For each ingredient:

```
Passes if:
available_amount >= configured_threshold
```

- If any ingredient fails → crafting blocked
- Threshold is per resource
- show information in autocrafter UI if crafting is blocked because of this constraint

---

## 🧮 Core Logic

```
bool ShouldCraft(AutoCrafter ac) {
    var config = GetConfig(ac);

    var items = ScanItemsInRange(ac);

    if (config.EnableOutputLimit) {
        int count = Count(items, ac.Recipe.Output);
        if (count >= config.TargetOutputAmount)
            return false;
    }

    if (config.EnableInputThreshold) {
        foreach (var ingredient in ac.Recipe.Ingredients) {
            int threshold = config.InputThresholds.GetOrDefault(ingredient, 0);
            if (threshold == 0) continue;

            int count = Count(items, ingredient);
            if (count < threshold)
                return false;
        }
    }

    return true;
}
```

---

## ⚡ Performance

### Required Optimization
- Cache scan results per AutoCrafter
- Refresh every 1–3 seconds

Optional future:
- Event-driven updates (inventory changes, drones)

---

## 🖥️ UI / UX

### Integration
- Add button to AutoCrafter panel (similar to logistics button)

### Panel Features
- Toggle:
  - [ ] Enable Output Limit
  - [ ] Enable Input Threshold

- Output section:
  - Numeric input for target amount (0 is unlimited)

- Input section:
  - List all recipe ingredients
  - Numeric input per resource (0 is no limit)

### Example UI

```
[✔] Limit Output
    Target: [10]

[✔] Input Thresholds
    Iron: [50]
    Silicon: [20]
    Aluminum: [0]
```

---

## 💾 Persistence

- Store config per AutoCrafter (unique ID)
- Save as JSON
- Cleanup on autocrafter deconstruction

```
{
  "AutoCrafters": {
    "id_123": {
      "EnableOutputLimit": true,
      "TargetOutputAmount": 10,
      "EnableInputThreshold": true,
      "InputThresholds": {
        "Iron": 50,
        "Silicon": 20
      }
    }
  }
}
```

---

## ⚠️ Edge Cases

- Multiple AutoCrafters → naturally coordinated via shared range
- Recipe change → reset or adapt thresholds
- Missing config entries → default to safe values

---

## 🚀 MVP Scope

- Output + Input constraints (hybrid)
- Cached scanning
- Basic UI panel
- JSON persistence

---

## 🔮 Future Extensions

- Per-item output limits
- Global factory constraints
- Drone-aware logic
- Power-aware crafting limits
