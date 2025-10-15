# Procederal Item Generator – Phase 0 Inventory

_Last updated: October 15, 2025_

## Scope

Phase 0 catalogs the existing mechanic descriptors that currently live in `Primary Mechanic List.json` and `Modifier Mechanic List.json`. The goal is to identify which fields act as generator-level metadata (used to drive spawn behavior or compatibility) versus mechanic-specific configuration (passed directly into the mechanic components). This audit will inform the migration path toward decentralized per-mechanic configs in later phases.

## Cross-cutting metadata

The following keys appear repeatedly and are treated as generator-facing metadata today:

- `AllowMultiple`
- `spawnBehavior`
- `childrenToSpawn`
- `spawnOnInterval`
- `interval`, `spawnInterval`, `intervalBetween`
- `immediateFirstBurst`
- `batchesAtOnce`, `maxBatchesAtOnce`
- `MechanicPath`
- `IncompatibleWith`
- Entries inside `MechanicOverrides`

Anything else tends to describe the mechanic payload itself (damage, radii, colours, etc.), but several values (for example `DestroyOnHit`, `direction`, and `spriteType`) are currently applied both by the generator and by downstream mechanics. Those are flagged below for clarifications during Phase 1.

## Primary mechanics

### Projectile

- **Generator metadata**: `AllowMultiple`, `spawnBehavior`, `childrenToSpawn`, `interval`, `immediateFirstBurst`
- **Mechanic-specific data**: `DestroyOnHit`, `damage`, `speed`, `disableSelfSpeed`, `direction`, `spriteType`, `spriteColor`, `lifetime`, `radius`
- **Notes**: `DestroyOnHit` is also toggled by modifiers (`Orbit`). `spriteType`/`spriteColor` are consumed by the generator’s payload helper and should remain accessible even after decentralisation.

### Aura

- **Generator metadata**: `AllowMultiple`, `spawnBehavior`, `childrenToSpawn`, `immediateFirstBurst`
- **Mechanic-specific data**: `DestroyOnHit`, `damagePerInterval`, `radius`, `interval`, `spriteType`, `spriteColor`
- **Notes**: `interval` doubles as generator timing when Aura is spawned directly. Keep watch for duplication during migration.

### Beam

- **Generator metadata**: `AllowMultiple`, `spawnBehavior`, `childrenToSpawn`, `immediateFirstBurst`
- **Mechanic-specific data**: `DestroyOnHit`, `damagePerInterval`, `damageInterval`, `spawnInterval`, `radius`, `speed`, `lifetime`, `direction`, `spriteColor`, `preserveHeadOnRedirect`, `anchorTailToPlayer`, `segmentOnRedirect`, `debugLogs`, `isSegmented`, `segments`, `maxSpeed`, `debugSpeed`
- **Notes**: `spawnInterval` is currently interpreted as generator timing when set. Document how Phase 1 configs will separate that concern.

### Strike

- **Generator metadata**: `AllowMultiple`, `spawnBehavior`, `childrenToSpawn`, `immediateFirstBurst`
- **Mechanic-specific data**: `DestroyOnHit`, `damagePerInterval`, `interval`, `spriteColor`, `showVisualization`
- **Notes**: `DestroyOnHit` might be redundant if strike never destroys itself—confirm before pruning.

### Ripple

- **Generator metadata**: `AllowMultiple`, `spawnBehavior`, `childrenToSpawn`, `interval`, `immediateFirstBurst`
- **Mechanic-specific data**: `DestroyOnHit`, `startRadius`, `endDiameter`, `growDuration`, `edgeThickness`, `damage`, `includePlayer`, `showVisualization`, `spriteColor`
- **Notes**: `interval` again influences both spawn timing and mechanic damage ticks.

### Whip

- **Generator metadata**: `AllowMultiple`, `spawnBehavior`, `childrenToSpawn`, `immediateFirstBurst`
- **Mechanic-specific data**: `DestroyOnHit`, `damagePerInterval`, `interval`, `outerRadius`, `width`, `arcLengthDeg`, `drawDuration`, `edgeOnly`, `edgeThickness`, `direction`, `showVisualization`, `spriteColor`
- **Notes**: `direction` is required for mechanic orientation; generator currently feeds it into payload creation.

### SwordSlash

- **Generator metadata**: `AllowMultiple`, `spawnOnInterval`, `interval`, `batchesAtOnce`, `maxBatchesAtOnce`, `childrenToSpawn`
- **Mechanic-specific data**: `seriesCount`, `intervalBetween`, `outerRadius`, `width`, `arcLengthDeg`, `edgeOnly`, `edgeThickness`, `speed`, `damage`, `spriteColor`
- **Notes**: There is no `DestroyOnHit` entry; projectile behaviour is composed via generator helper logic instead.

### ChildMovementMechanic

- **Generator metadata**: `AllowMultiple`
- **Mechanic-specific data**: `direction`, `speed`, `disableSelfSpeed`, `autoAddPhysicsBody`
- **Notes**: Purely helper mechanic; generator often injects these values directly.

## Modifier mechanics

### RippleOnHit

- **Generator metadata**: none (entirely mechanic-driven)
- **Mechanic-specific data**: all listed properties; `MechanicOverrides` currently empty
- **Notes**: `excludeOwner`/`requireMobTag` mirror primary projectile options—consider consolidating defaults.

### Orbit

- **Generator metadata**: `MechanicOverrides` (`DestroyOnHit`, `disableSelfSpeed`, `direction`, `spawnBehavior`, `childrenToSpawn`)
- **Mechanic-specific data**: `radius`, `angularSpeedDeg`, `startAngleDeg`, `childrenToSpawn`
- **Notes**: `childrenToSpawn` appears both in base properties and overrides; clarify intended precedence in Phase 1.

### Bounce

- **Generator metadata**: none
- **Mechanic-specific data**: all listed fields
- **Notes**: `segmentTargets` currently unused; confirm before migrating.

### Drain

- **Generator metadata**: none
- **Mechanic-specific data**: all listed fields
- **Notes**: Field casing varies (`LifeStealPercent` vs camelCase). Standardise during migration.

### Lock

- **Generator metadata**: none
- **Mechanic-specific data**: all listed fields
- **Notes**: Straightforward modifier.

### Track

- **Generator metadata**: none
- **Mechanic-specific data**: (empty) – behaviour defined entirely in code
- **Notes**: No properties to migrate; ensure schema tolerates empty arrays.

### DamageOverTime

- **Generator metadata**: none
- **Mechanic-specific data**: all listed fields
- **Notes**: `allowStacking`/`effectId` should remain accessible to remote configs.

### Explosion

- **Generator metadata**: none
- **Mechanic-specific data**: all listed fields
- **Notes**: `excludeOwner` parity with primary mechanics.

## Open questions for Phase 1

1. Do we keep generator timing fields (`interval`, `spawnInterval`, `spawnBehavior`, etc.) inside the same config asset, or split them into an explicit spawn profile alongside mechanic payload data?
2. Which properties will become computed defaults versus explicit configuration (e.g., `DestroyOnHit`)?
3. How should overrides be expressed once configs move into per-mechanic folders—nested JSON, layered assets, or code-based modifiers?
4. Do we normalise naming (`LifeStealPercent` vs `lifeStealChance`) before or after migration?

These answers will shape the schema for Phase 1 when we begin relocating configs next to their mechanics.
