# BeamMechanic Feature & Integration Notes (Updated: Head terminology & lifetime-only destruction)

This document summarizes the current BeamMechanic capabilities, JSON properties, lifecycle rules, and how external systems (e.g. bounce / steering modifiers) should now interact with it.

## Overview
The beam is a forward-growing continuous damage shape composed of:
- A rectangular body (variable length, fixed width)
- A half‑circle head (same diameter as width) – formerly called the tip
- Optional anchored tail (polyline) visually connecting back to the player when enabled

Damage is applied at fixed `interval` ticks to all qualifying targets inside both the rectangle and head colliders.
The beam now extends indefinitely; destruction is governed ONLY by `lifetime` (if > 0) or external destroy logic.

## Core Properties (JSON -> C#)
| JSON Key | BeamMechanic Field | Notes |
|----------|--------------------|-------|
| `maxDistance` (deprecated) | — | Ignored (kept only for backward JSON compatibility) |
| `speed` or `extendSpeed` | `speed` (migrates from `extendSpeed`) | Units/second head travel (legacy `extendSpeed` migrates if present) |
| `beamWidth` or `radius` | `beamWidth` | If `radius` provided, width = radius * 2 |
| `damageInterval` or `interval` | `damageInterval` | Seconds between damage ticks (legacy `interval` auto-mapped) |
| `damagePerInterval` or `damage` | `damagePerInterval` | Unified damage field |
| `direction` | `direction` | "right,left,up,down" or numeric degrees |
| `spriteColor` | `vizColor` | Parsed via `ColorUtils` (e.g. `#FFFFFFFF`) |
| `showVisualization` | `showVisualization` | Toggles runtime sprites |
| `vizSortingOrder` | `vizSortingOrder` | Sorting order for beam sprites (tail uses -1) |
| `debugLogs` | `debugLogs` | Verbose logging for redirects & lifetime |
| `lifetime` | `lifetime` | Seconds until forced despawn (0 = disabled) |
| `preserveHeadOnRedirect` | `preserveHeadOnRedirect` | Keep current head position when changing direction (only when NOT anchoring) |
| `preserveTipOnRedirect` (legacy) | `preserveHeadOnRedirect` | Backward compatibility mapping |
| `anchorTailToPlayer` | `anchorTailToPlayer` | Enables anchored polyline back to owner (default true) |
| `segmentOnRedirect` | `segmentOnRedirect` | Adds a corner node on each redirect (anchored mode only) |

### Backward Compatibility Keys
| Legacy Key | New Key | Behavior |
|------------|---------|----------|
| `preserveTipOnBounce` | `preserveHeadOnRedirect` | Auto-mapped via old chain (tip->head) |
| `preserveTipOnRedirect` | `preserveHeadOnRedirect` | Auto-mapped |
| `segmentOnBounce` | `segmentOnRedirect` | Auto-mapped |

## Targeting & Tracking
- Initial aim priority: `ctx.Target` → nearest mob (tag "Mob") → parsed `direction`.
- If a `TrackMechanic` component is present on the same GameObject, dynamic re-aiming is enabled:
  - Periodic retarget (`retargetInterval` from TrackMechanic)
  - Smooth turning limited by `turnRateDegPerSec`

## Redirect API (Generic Bounce / Steering)
External systems should no longer manipulate internals; they call:
```csharp
beam.Redirect(newDirectionVector);
```
### Redirect Outcomes
1. Anchored Tail Enabled (`anchorTailToPlayer == true`):
  - If `segmentOnRedirect` true: the current head position becomes a corner node; beam base is moved to the head; length resets and grows along new direction.
  - Head continuity is implied visually by the polyline path; only the LAST segment damages (the active beam body/head colliders).
2. Anchored Tail Disabled:
  - If `preserveHeadOnRedirect` true: base shifts so the existing head stays fixed in world space, maintaining full length.
  - Else: beam collapses (length reset to 0) and regrows from current base.

## Anchored Tail Visualization
- Implemented with a world-space `LineRenderer` named `BeamTail` (child of `BeamViz`).
- Tail width now always matches the head diameter (`beamWidth`), updating dynamically if `beamWidth` changes.
- Node[0] = player/owner position (updated every frame).
- Intermediate nodes = redirect corners (if `segmentOnRedirect`).
- Final implicit node = live beam head (updated dynamically each frame).
- Only the final geometric beam (head collider + active segment) deals damage; tail line is cosmetic.

## Lifetime & Despawn Rules
Destruction triggers when:
1. `lifetime > 0` AND elapsed >= lifetime.

If `lifetime == 0`, the beam persists indefinitely (unless externally destroyed) and keeps extending.

## Damage & Modifier Dispatch
Each damage tick:
- Collect colliders: active tail segment + head (anchored) OR body + head (legacy box+head mode).
- Filter by `excludeOwner` and optional `requireMobTag`.
- Deduplicate per tick in anchored mode (shared set) to avoid double damage across segment + head.
- Apply damage via `IDamageable.TakeDamage`.

### Generic Primary Hit Event
For every successful individual hit Beam emits a primary-agnostic event to all `IPrimaryHitModifier` components on the same GameObject:
```csharp
var info = new PrimaryHitInfo(target, hitPoint, hitNormal, damagePerInterval, this);
mod.OnPrimaryHit(info);
```
Beam itself has no knowledge of concrete modifiers (stun, DoT, ripple, explosion, drain, etc.).

### Segment Modifiers
If `isSegmented` is true, per-segment totals ("head", "tail") are aggregated and dispatched to any `IBeamSegmentModifier` via `OnBeamSegmentDamage` (target set from `GetTargetSegments()`).

### Drain
Life steal now typically flows through a `DrainMechanic` implementing `IPrimaryHitModifier`. Legacy `drainSegments` still supported for segment-specific reporting; prefer the generic path for new content.

## Integration Tips
- For former bounce logic: replace old maxDistance-based reflection behavior with physics check + call to `Redirect(reflectedDir)`.
- To force an immediate 90° turn: `beam.Redirect(Quaternion.Euler(0,0,90) * currentDir)`.
- To simulate a guided beam (non-TrackMechanic approach): periodically compute homing vector to target and call Redirect with small incremental direction changes.

## Performance Considerations
- Collider Overlap arrays sized at 64. If large swarms exceed this regularly, consider pooling or multi-pass handling.
- Tail `LineRenderer` updates every frame only when anchoring is enabled. Disable `anchorTailToPlayer` if profiling shows overhead and feature not needed.

## Common Edge Cases & Guidance
| Scenario | Behavior / Recommendation |
|----------|---------------------------|
| Redirect with zero vector | Ignored safely |
| Owner destroyed mid-beam | Beam remains; tail base stops moving (node[0] no longer updated) |
| Track target disappears | Falls back to nearest mob search next retarget cycle |
| Extremely fast extendSpeed | Visual may overshoot collision expectations; consider per-frame collision sweeps if needed later |
| Very small beamWidth | Clamp ensures > 0 for collider; visual may look thin | 

## Extending Further
Potential future enhancements (not yet implemented):
- Damage along entire anchored tail polyline (segment sampling).
- Smoothing / easing between redirects to avoid sharp corners.
- Adaptive retarget radius sourced from TrackMechanic config.
- Optional per-node glow or particle effects at corners.

## Minimal JSON Example
```json
{
  "MechanicName": "Beam",
  "MechanicPath": "Assets/Scripts/Procederal Item Logic/Mechanics/Neuteral/BeamMechanic.cs",
  "Properties": [
    { "AllowMultiple": false },
    { "damagePerInterval": 3 },
    { "interval": 0.1 },
    { "beamWidth": 1.0 },
    { "speed": 20 },
  { "preserveHeadOnRedirect": true },
    { "lifetime": 3.0 },
    { "direction": "right" },
    { "spriteColor": "#FFFFFFFF" },
  { "anchorTailToPlayer": true },
  { "segmentOnRedirect": true }
  ]
}
```

## Quick Checklist When Adding New Redirect-Based Modifiers
- Does the modifier compute a new normalized direction? (Normalize before calling Redirect)
- Should the modifier also inject a corner (anchored tail)? (Set `segmentOnRedirect` true in JSON)
- Need to preserve the current tip? (If not anchoring, set `preserveTipOnRedirect` true)
- Want lifetime-limited behavior? Ensure `lifetime` > 0.

---
Last updated: generic primary-hit dispatch adoption & segment modifier clarification.
