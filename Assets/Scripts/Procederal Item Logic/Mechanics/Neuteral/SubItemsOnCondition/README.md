# SubItemsOnCondition Mechanic

`SubItemsOnConditionMechanic` lets a generated child spawn additional items when one of its rules is triggered. The rules are supplied as JSON (typically through the `spawnItemsOnConditions` field on `ItemParams`), and each entry is mapped to a runtime rule.

**Accepted keys:**
- When editing `ItemParams`, put the JSON array on the `spawnItemsOnConditions` field.
- When feeding values through a mechanic settings dictionary (e.g., an inspector text field), any of these keys will be detected: `spawnItemsOnCondition`, `SpawnItemsOnCondition`, `conditionSpec`, `spec`, `Spec`, or `json`.

## JSON Rule Example

```json
[
  {
    "condition": "mobContact",
    "primary": "DamageZone",
    "secondary": ["Drain"],
    "spawnCount": 2,
    "spawnOnce": true,
    "cooldownSeconds": 1.5,
    "debugLogs": false
  }
]
```

- Supply either a single JSON object or an array of objects. Strings that wrap the JSON (e.g., command outputs) are trimmed until the first `{` or `[`.
- `primary` is the mechanic name passed to `ProcederalItemGenerator` for the spawned child.
- `secondary` (optional) lists additional mechanic names that will be applied to the spawned child.
- `spawnCount` defaults to `1` and is clamped to be at least `1`.
- `spawnOnce` prevents the rule from firing more than once.
- `cooldownSeconds` enforces a delay between spawns when greater than zero.
- `debugLogs` enables verbose logging for that rule.

## Supported Conditions

| Condition value | Description |
| --- | --- |
| `mobContact` (aliases: `mob`, `contact`) | Fires when the payload's trigger collider touches something tagged `Mob`. |

Additional condition kinds can be introduced by expanding `SubItemsOnConditionMechanic.ConditionKind` and updating the parser.
