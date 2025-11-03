# Orbit Behaviors

Use these identifiers (set via `pathId` in JSON or inspector) to control how orbiting children move.

## Available Paths

- `Circular` – Default circular orbit. Honors `radius`, `angularSpeedDeg`, and `startAngleDeg`.
- `FigureEight` (aliases: `Figure8`, `Lemniscate`) – Lemniscate style orbit that crosses through the center. Respects the same core settings as `Circular` and supports an additional rotation offset.

## Rotation Offsets

- `PathRotationDeg` (mechanic setting) – Rotates the path plane around the center. Exposed in JSON via `OrbitPathRotationBaseDeg` and `OrbitPathRotationStepDeg` when spawning multiple children.
- `OrbitPathRotationBaseDeg` – Starting offset (degrees) applied to the first child.
- `OrbitPathRotationStepDeg` – Increment applied per child index to fan out multiple figure-eight paths.

## Configuration Tips

- Combine `startAngleDeg` with rotation offsets when you need consistent spacing along the path and distinct orientation per child.
- Negative `angularSpeedDeg` values orbit clockwise; positive values run counterclockwise.
- Leave `pathId` empty to fall back to `Circular`.
