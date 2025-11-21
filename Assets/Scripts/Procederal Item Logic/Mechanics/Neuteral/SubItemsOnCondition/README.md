# SubItemsOnCondition Mechanic

`SubItemsOnConditionMechanic` lets a generated child spawn additional items when one of its rules is triggered. The rules are supplied as JSON (typically through the `spawnItemsOnConditions` field on `ItemParams`), and each entry is mapped to a runtime rule.

**Accepted keys:**
- When editing `ItemParams`, put the JSON array on the `spawnItemsOnConditions` field.
- When feeding values through a mechanic settings dictionary (e.g., an inspector text field), any of these keys will be detected: `spawnItemsOnCondition`, `SpawnItemsOnCondition`, `conditionSpec`, `spec`, `Spec`, or `json`.

## JSON Rule Example

```json
[
  {
    "condition": "onDamage",
    "target": "any",
    "primary": "Projectile",
    "secondary": [
      "Track",
      "ExplodeOnHit"
    ],
    "spawnCount": 3,
    "detachFromSpawner": true,
    "spawnOnce": false,
    "cooldownSeconds": 0.25,
    "debugLogs": true
  }
]
```

- Every available rule key (`condition`, `target`, `primary`, `secondary`, `spawnCount`, `spawnOnce`, `cooldownSeconds`, `debugLogs`) is shown above; omit any of them to fall back to defaults.

- Supply either a single JSON object or an array of objects. Strings that wrap the JSON (e.g., command outputs) are trimmed until the first `{` or `[`.
- `primary` is the mechanic name passed to `ProcederalItemGenerator` for the spawned child.
- `secondary` (optional) lists additional mechanic names that will be applied to the spawned child.
- `spawnCount` defaults to `1` and is clamped to be at least `1`.
- `detachFromSpawner` defaults to `true`. When `true`, each spawned payload is detached from whoever triggered the rule so it becomes a root-level object; set it to `false` if a mechanic needs the spawned item to remain parented (for example orbit-style effects).
- `spawnOnce` prevents the rule from firing more than once.
- `cooldownSeconds` enforces a delay between spawns when greater than zero.
- `debugLogs` enables verbose logging for that rule.
- `target` selects which tag the rule listens for (`mob`, `player`, or `any`). Defaults to `mob` when omitted.

## Supported Conditions

| Condition value | Description |
| --- | --- |
| `mobContact` (aliases: `mob`, `contact`) | Fires when the payload's trigger collider touches something whose tag matches the rule's `target` filter. |
| `onDamage` (aliases: `hit`, `primaryHit`) | Fires after a primary mechanic reports damage through the `IPrimaryHitModifier` pipeline, spawning at the damaged target or hit point. |

Additional condition kinds can be introduced by expanding `SubItemsOnConditionMechanic.ConditionKind` and updating the parser.

## Naming + Pool Management

Each spawned object receives a deterministic name consisting of the sanitized `primary` value plus every `secondary` entry, joined with underscores, followed by a dash and an 8-character identifier (for example `projectile_track-4ubgnjm`).

- `SanitizeForFileName` lowercases identifiers, strips invalid characters, keeps alphanumerics plus `_`/`-`, and converts the rest to `_`. Treat the portion before the dash as the **base name**; anything after the dash is the unique token.
- `ExtractTrackedBaseName` removes Unity's `(Clone)` suffix and returns the part before the first dash. Base names let the mechanic keep precise tallies in `_trackedObjectsCounts` so limits apply to every variation of the same prefab.
- Legacy prefabs without a dash remain valid but won't be summarized in debug output unless you rename them (recommended) or extend `ExtractTrackedBaseName` to accept alternative delimiters.

### Enforcing Per-Name Limits

To prevent runaway projectile pools, the mechanic exposes `_maxActivePerBaseName`:

1. `0` (default) disables the global cap and defers to each rule's `spawnCount`.
2. Positive values cap the number of simultaneously active payloads for every base name.
3. When a spawn would exceed the cap, `EnsureCapacityForBaseName` recycles the oldest `SubItemSpawnHandle` before instantiating a new payload.
4. Recycling notifies the handle, returns the payload to the generator pool, and destroys the associated `GameObject`, so stale projectiles don't remain in the hierarchy.

Enable the mechanic's `debugLogs` to see live `current/max` totals per base name whenever objects are spawned or recycled.

### Integration Tips

- Choose descriptive `primary`/`secondary` identifiers so base names align with your pooling intentions (example: `"primary": "Projectile", "secondary": ["Track"]` → `projectile_track`).
- Keep `_maxActivePerBaseName` in sync with your global pool configuration (e.g., "Max Pooled Instances Per Key") for predictable behavior.
- When testing, toggle `debugLogs` for individual rules to confirm that counts rise and fall as expected and that recycling occurs before limits are breached.
- For scene-wide reset buttons or wave transitions, call `ResetRuntimeStates` so any remaining handles are cleared before the next round.

### Lifecycle Notes

- The mechanic automatically adds a `PayloadTriggerRelay` so collision-driven rules fire without extra wiring.
- `SubItemSpawnHandle` implements `IPooledPayloadResettable`, giving pool managers a consistent way to clear state during reuse.
- `ClearRules`, `OnDisable`, and `ResetRuntimeStates` tear down trackers and detach handle events, ensuring recycled objects from previous waves don't contribute to the next run's counts.
