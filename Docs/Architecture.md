# GameServerTestGame – Project Architecture

Last updated: 2025-09-16

Maintenance: This document is a living overview. We will update it alongside any code or content changes that affect architecture, structure, or setup. If you add a new system, folder, or mechanic, please include a brief note here in the relevant section.

This document provides an overview of the project structure, how systems fit together, and how to set up and use each part. It is organized by location in the repository to help you quickly find relevant code and assets.

## Top-level

- `GameServerTestGame.sln`, `Game Server Test Game.sln`: Solution files for IDEs.
- `Assembly-CSharp.csproj`: Unity-generated C# project file.
- `Library/`, `Logs/`, `Temp/`, `UIElementsSchema/`, `UserSettings/`: Unity-generated folders. Do not edit or commit most of these.
- `Packages/`: Unity package manifest and lock file.
- `ProjectSettings/`: Unity project settings (graphics, input, multiplayer, etc.).

## Packages

- `Packages/manifest.json`: Declares Unity packages used by the project.
- `Packages/packages-lock.json`: Lock file for package versions.

## Project Settings

- `ProjectSettings/*.asset`: Engine configuration. Notable ones:
  - `GraphicsSettings.asset`, `UniversalRenderPipelineGlobalSettings.asset`: URP.
  - `InputManager.asset` and `Assets/InputSystem_Actions.inputactions`: Input.
  - `MultiplayerManager.asset`, `NetworkManager.asset`: Multiplayer/networking.

## Assets

The main game content and scripts live under `Assets/`.

### Scenes

- `Assets/Scenes/`: Unity scenes for the game. Open a scene to run the game in the editor.

### Resources

- `Assets/Resources/`: Assets loadable by name at runtime using `Resources.Load`. This project loads mechanic lists by name when not provided via inspector fields.
  - Expected names used in code: `"Primary Mechanic List"`, `"Modifier Mechanic List"` (TextAssets containing JSON arrays).

### Scripts

- `Assets/Scripts/`: All C# gameplay scripts. Key subsystems are under `Procederal Item Logic`.

#### Procederal Item Logic (data-driven mechanics)

Location: `Assets/Scripts/Procederal Item Logic/`

Purpose: A lightweight, data-driven system to compose “mechanics” (behaviours like Projectile, Orbit, Aura, Drain) on GameObjects and payload children.

Note: MechanicHost is deprecated. Prefer `ProcederalItemGenerator` for creating and wiring items. Use `MechanicRunner` to tick mechanics built by the generator.

Supporting API utilities and data live alongside these.

##### Core runtime interfaces and context

- `Mechanics/IMechanic.cs`:
  - Contract implemented by all mechanics.
  - Methods: `Initialize(MechanicContext ctx)`, `Tick(float dt)`.
- `Mechanics/MechanicContext.cs`:
  - Shared context passed to mechanics: `Owner`, `Payload`, `Target`, and optional `Rigidbody2D` references for owner/payload.

##### MechanicHost (deprecated)

- `Mechanics/MechanicHost.cs`:
  - Add this to a GameObject to orchestrate attached mechanic MonoBehaviours.
  - Can auto-create one or more payload children (`payloadCount`) with colliders and helper relay.
  - Supports editor toggles to override common parameters across attached mechanics (radius, orbit speed, damage, intervals, projectile destroy-on-hit).
  - Wires a `MechanicContext` for the host and payloads. Calls `Initialize` for each mechanic and drives `Tick` every frame.
  - Useful for prototyping and designing behaviour combos directly in the editor.

Deprecated usage (existing content only):
- Existing scenes using `MechanicHost` will continue to work, but avoid adding it to new content.
- Migrate to `ProcederalItemGenerator` + `MechanicRunner` using the guide below.

Editor notes (legacy):
- Host auto-creates payloads and drives `Tick`, but this is now replaced by `MechanicRunner`.

##### Mechanics (behaviours)

- `Mechanics/Neuteral/ProjectileMechanic.cs`:
  - Moves the payload along `direction * speed` (via Rigidbody2D velocity when available).
  - Fields: `direction: Vector2`, `speed: float`, `damage: int`, `destroyOnHit: bool`, `requireMobTag`, `excludeOwner`, `disableSelfSpeed: bool`.
  - `disableSelfSpeed` lets other mechanics (e.g., Orbit) fully control motion.
  - Handles trigger collisions, filters by owner/mob tags, calls `IDamageable.TakeDamage` and optionally destroys payload.
  - Unity Editor setup:
    - Add `ProjectileMechanic` to a payload GameObject (child) or to a host if the host’s `Payload` is used.
    - Add a `CircleCollider2D` (set `isTrigger = true`). Optionally add a `Rigidbody2D` (Kinematic) for velocity-based motion.
    - Set `direction` and `speed`. If combining with Orbit, set `disableSelfSpeed = true` (the generator and some modifiers do this automatically).
    - If you only want to hit enemies, keep `requireMobTag = true` and ensure enemies are tagged `Mob`.

- `Mechanics/Neuteral/OrbitMechanic.cs`:
  - Orbits the payload around `Target` or `Owner` using `radius`, `angularSpeedDeg`, and `startAngleDeg`.
  - Works with or without Rigidbody2D (uses MovePosition in FixedUpdate when rigidbody exists).
  - Unity Editor setup:
    - Add `OrbitMechanic` to the same object that holds the payload you wish to orbit.
    - Set `radius` and `angularSpeedDeg`. Positive values orbit counter-clockwise; use negative for clockwise.
    - Optionally add `Rigidbody2D` (Kinematic + Interpolate) on the payload for smoother movement via `FixedUpdate`.

- `Mechanics/Neuteral/AuraMechanic.cs`:
  - Area-of-effect damage at intervals within `radius` around owner/target.
  - Optional visualization (generated circle sprite), configurable `interval`, filtering by tags/layers, optional `centerOnTarget`.
  - If a `DrainMechanic` is co-located, it will use total damage to apply life-steal to the owner.
  - Unity Editor setup:
    - Add `AuraMechanic` to the host or payload GameObject that should emit the aura.
    - Set `radius`, `damagePerInterval`, and `interval`.
    - Ensure a 2D physics setup exists in the scene. No rigidbody is required; Aura queries with a temporary trigger.
    - Optional: Enable `showVisualization` for a helpful circle sprite; adjust `vizColor` and `vizSortingOrder`.
    - For enemy-only hits, leave `requireMobTag = true` and ensure enemies use tag `Mob`. To avoid hitting the player, keep `excludePlayer = true`.

- `Mechanics/Corruption/DrainMechanic.cs`:
  - Periodically damages within `radius`, healing the owner by `lifeStealRatio` of damage dealt.
  - Unity Editor setup:
    - Add `DrainMechanic` to the host or a payload GameObject near the owner.
    - Set `radius`, `damagePerInterval`, `interval`, and `lifeStealRatio` (0..1).
    - The component will look for `PlayerHealth` on the owner (or the object tagged `Player`) to apply healing.

Related helpers invoked by hosts/payloads:
- `Mechanics/MechanicTargetSetter.cs` (if present): assists with target assignment.
- `PayloadTriggerRelay` (generated on payloads by `MechanicHost`): forwards trigger events to host/mechanics.

##### Procedural builder (code + JSON)

- `ProcederalItemGenerator.cs` (namespace `Game.Procederal`):
  - Programmatic, performant item builder used at runtime to compose mechanics on spawned objects.
  - Inputs:
    - `ItemInstruction` (primary mechanic name + optional secondary list)
    - `ItemParams` (numbers/toggles for size, damage, radii, etc.)
  - Behaviour:
    - Creates a root GameObject named by the mechanic combination (e.g., `Item_Projectile+Orbit`), then builds sub-items (e.g., N projectiles) with visuals and colliders.
    - Resolves mechanic types from mechanic names using `MechanicCatalog` + `MechanicReflection`.
    - Applies mechanic settings from JSON “Properties”, with “Overrides” and “MechanicOverrides” taking precedence, then applies code-provided settings.
    - Normalizes certain fields from JSON (e.g., converts projectile `direction: "right"` to `Vector2.right`; `Orbit.direction: "clockwise"` flips sign of `angularSpeedDeg`).
    - When Orbit is present with Projectile, sets `ProjectileMechanic.disableSelfSpeed = true` to avoid double movement.
  - Outputs: A root object with initialized mechanics and child sub-items.

Usage (Runtime):
- Put `ProcederalItemGenerator` on a spawner/owner object. Assign `owner`/`target` if needed.
- Optionally assign `primaryMechanicListJson` / `modifierMechanicListJson` overrides in the inspector. When left null, the generator now aggregates every per-mechanic JSON under `Resources/ProcederalMechanics/Primary` and `Resources/ProcederalMechanics/Modifier`.
- Call `Create(ItemInstruction, ItemParams, parent)` to build an item hierarchy.
 - Visuals: Projectiles get a default `SpriteRenderer` using a simple white circle sprite generated at runtime; size is driven by `projectileSize` scale. You can later override sprite/color via JSON or code.

Unity Editor setup (ProcederalItemGenerator):
- Add `ProcederalItemGenerator` to a scene object (e.g., your player or a dedicated spawner).
- In the inspector, assign:
  - `defaultParent` (optional): where generated items will be parented.
  - `owner`: center for orbits and some AoE mechanics (often the same object).
  - `target` (optional): if mechanics should center on a particular target.
  - `primaryMechanicListJson` and `modifierMechanicListJson`: drag in override TextAssets if you need a custom catalog; otherwise leave them blank to consume the per-mechanic files under `Resources/ProcederalMechanics`.
- Ticking: The generator now adds a `MechanicRunner` to the created root and registers all `IMechanic` instances under it so they automatically tick each frame.

Clarification: Should `Target` be set to the player?
- Short answer: Usually no—set `owner` to the player; leave `target` null unless you want certain mechanics to center on something else.
- Details:
  - Many mechanics (Orbit, Aura, Drain) compute their center as `Target` if assigned; otherwise they fall back to `Owner`.
  - For player-centric items (weapons orbiting the player, auras around the player), set `owner = PlayerTransform` and keep `target = null` (or also the player, both behave the same in this case).
  - Use `target` when the effect should center on something other than the owner—for example, orbiting around a boss, anchoring an aura at a world marker, or homing into an enemy.
  - Some mechanics also expose a `centerOnTarget` toggle (e.g., Aura/Drain). If enabled and `target` is set, the effect will use the target instead of the owner.

##### MechanicRunner (runtime ticking)

Location: `Assets/Scripts/Procederal Item Logic/Api Scripts/MechanicRunner.cs`

Purpose: A minimal component that finds `IMechanic` behaviours under a root and calls `Tick` every frame. The generator attaches and registers it automatically.

Unity Editor setup (MechanicRunner):
- Normally added by `ProcederalItemGenerator`. You can also add it manually to any GameObject and call `RegisterTree(rootTransform)` from your code to register a subtree.

##### API utilities for reflection & catalogs

- `Api Scripts/MechanicCatalog.cs`:
  - Reads Primary/Modifier JSON arrays and builds a name→path map. Tolerant to extra fields.

- `Api Scripts/MechanicReflection.cs`:
  - Resolves a C# type from a mechanic path or name, adds it as a component, and applies settings by reflection.
  - Performs basic type conversion for ints/floats/bools/enums and strings.

##### Item selection & generation control

- `Api Scripts/ItemGenerationController.cs`:
  - Central entry-point for starting generation.
  - Modes:
    - Offline: calls `OfflineItemGeneratorApi.MakeRandom(...)` to select a primary+modifier.
    - Online: calls a provided `IItemSelectionProvider` to obtain the selection.
  - Then calls `ProcederalItemGenerator.Create(...)` to build the item and attaches `MechanicRunner` automatically.
  - Current behavior: creates one item on Start when `spawnOnStart = true`. Leveling/upgrade loops can hook in later by calling `StartGeneration()` again with new selections/params.

- `Api Scripts/IItemSelectionProvider.cs`:
  - Interface for server/online selection. Implement this in a MonoBehaviour and assign it to the controller when `offlineMode = false`.

Usage (Runtime):
- Add `ItemGenerationController` to the Player (or a spawner).
- Assign the `generator` reference (on the same object or drag it in).
- For offline testing, you can assign override TextAssets, but leaving them null will automatically use the per-mechanic files in `Resources/ProcederalMechanics`.
- For online mode, set `offlineMode = false` and plug a component implementing `IItemSelectionProvider` into `selectionProviderBehaviour`.

Editor wiring details:
- Generator: In the controller’s `generator` field, select the GameObject where you placed `ProcederalItemGenerator` (often the same Player object). The controller will reuse any JSON overrides set on the generator; when none are assigned it automatically reads the per-mechanic catalogs from `Resources/ProcederalMechanics`.
- Selection Provider Behavior: Leave this EMPTY when `offlineMode = true`. When `offlineMode = false`, drag a component here that implements `IItemSelectionProvider` (e.g., a network-backed provider). For a simple example, you can use `Examples/FixedSelectionProvider` which always returns a fixed combination such as Projectile + Orbit.
- Output Parent: Optional Transform used as the parent for the generated item root. Good choices:
  - A child under the player like `Player/GeneratedItems` to keep hierarchy tidy.
  - A world anchor object if you want items grouped elsewhere. If left empty, the generator will use `defaultParent` (if set) or its own Transform.

Quick start:
1. Add `ProcederalItemGenerator` to the Player, set `owner = Player`, and either assign the JSON TextAssets or rely on `Resources`.
2. Add `ItemGenerationController` to the same Player.
3. In the controller, set `generator` to the Player’s generator component, keep `offlineMode = true` for now, and (optionally) set `outputParent` to a `Player/GeneratedItems` Transform.
4. Press Play. One item will be generated at Start (you can toggle `spawnOnStart`).
5. To simulate an “online” selection, set `offlineMode = false` and assign a `FixedSelectionProvider` (or your real provider) into `selectionProviderBehaviour`.

##### Offline combinator (authoring & UX)

- `Api Scripts/OfflineItemGeneratorApi.cs`:
  - Reads available primaries/modifiers from the JSON lists.
  - Selects a random compatible pair and applies “modifier overrides” onto `ItemParams` in-memory.
  - Useful for tooling, quick previews, or server-side selection logic.

Usage (Editor/Tools):
- Pass the two JSON TextAssets to `MakeRandom` to get:
  - `ItemInstruction` (primary + optional single modifier)
  - `ItemParams` (in-memory overrides applied)
  - `debug` text for logging.

Where it runs and how to wire it:
- The API is a static utility, not a component. Don’t attach it to GameObjects.
- Call it from your own MonoBehaviour (e.g., a spawner/controller on the Player) to pick a combo at runtime, then feed the result into `ProcederalItemGenerator.Create`.
- Example flow:
  1) Place `ProcederalItemGenerator` on the Player and assign `owner/target` and JSON TextAssets.
  2) From code, call `OfflineItemGeneratorApi.MakeRandom(primaryJson, modifierJson)`.
  3) Take the returned `instruction` and `parameters` and pass them to `generator.Create(instruction, parameters, parent)`.
  4) The generator will attach `MechanicRunner` to tick the generated mechanics.

Note:
- Prefer `ItemGenerationController` for gameplay; `OfflineItemGeneratorApi` can be called directly in tooling or tests.
 - Visuals defaults: Projectiles are rendered as white circles by default. Future customization can expose `spriteType` and `spriteColor` (hex) in JSON to override.

##### JSON data (mechanic catalogs)


Each file includes:


At runtime the loader aggregates every file in those folders (sorted by asset name), merges `Generator → Properties → Overrides`, and exposes the combined dictionary through `MechanicsRegistry`. Assigning `primaryMechanicListJson` / `modifierMechanicListJson` in the inspector still works as an override if you want a custom bundle for testing, but the per-file resources are now the default source of truth.

- `Game.Procederal.Ota.MechanicOtaManifest` reads `Resources/ProcederalMechanics/index.json` (and any overlays) so the registry can merge OTA-delivered manifests with built-in mechanics before the generator resolves settings.
- During development (`UNITY_EDITOR` / debug builds) the registry pulls JSON straight from `Assets/Scripts/Procederal Item Logic/Mechanics/*/*.json` so edits beside scripts are always reflected without rebuilding resources.

##### Pooling & lifecycle (hierarchies)

- Every generated root now carries both a `GeneratedObjectHandle` (tracks the owning generator and pool key) and a `PooledPayloadManifest` that snapshots every `IPooledPayloadResettable` component in the hierarchy. When a payload returns to the pool, manifests reset components instead of destroying children so future borrows stay intact.
- Before detaching a payload from its current parent (e.g., when SubItems spawn something that should live outside the trigger), call `ProcederalItemGenerator.EnsureManifestBeforeDetach(go)`. Many mechanics already do this automatically; the helper guarantees the manifest captures the latest hierarchy/fingerprint before it leaves the player.
- Use `MechanicLifecycleUtility.Release(go)` (or `Release(component)`) to despawn items. It forwards to `ProcederalItemGenerator.ReleaseTree`, which now releases the entire hierarchy in one call. If a manifest is present, the factory keeps children/components; if not, it tears the tree down before pooling to avoid stale state.
- The built-in `DefaultItemObjectFactory` and `SimpleItemObjectPoolBehaviour` respect manifests. With one attached they just call `ResetForPool`; without one they destroy extra children/components and keep only the bare root in the pool.
- `SubItemsOnCondition` rules expose a `"detachFromSpawner"` toggle (default `true`). Leave it enabled to keep pooled payloads parentless; disable it only for effects that must stay attached (for example, orbiting tethers or modifiers that rely on local offsets).
- Drop `PoolDiagnosticsLogger` on any object (or the generator) to periodically print per-key pool stats. It queries the active object factory (default or custom) via the new `ProcederalItemGenerator.GetActiveObjectFactory` helper and works with any factory implementing `IPooledItemDiagnostics`.

Editor tags, layers, and physics notes:
- Tags:
  - Enemies that should be damaged by Projectile/Aura/Drain with `requireMobTag = true` must be tagged `Mob` (or have a parent with tag `Mob`).
  - The player object should be tagged `Player` if you want `excludePlayer` logic to take effect in Aura/Drain.
- Physics:
  - For payloads that collide/detect via triggers, set their `CircleCollider2D.isTrigger = true`.
  - For Orbit used with physics, set payload `Rigidbody2D` to Kinematic, Interpolate, and Continuous, and let Orbit move it in `FixedUpdate`.

### Other Script Areas

- `Assets/Scripts/UI/*`: UI components such as `SkinsGridItem`, `ProgrammaticSkinItemState`.
- `Assets/Scripts/Utilities/*`: Small interfaces/utilities like `ISpawnItemResponseHandler`.

## Data Flow Overview

1. Author JSON lists for primaries/modifiers in `Assets/Scripts/Procederal Item Logic/` or place them as TextAssets under `Resources/` with the expected names.
2. At runtime, `ProcederalItemGenerator` resolves mechanic names to types via `MechanicCatalog` and adds components via `MechanicReflection`.
3. Settings are merged as: `Properties` → `Overrides`/`MechanicOverrides` → code-provided settings.
4. Mechanics receive a `MechanicContext` and are ticked each frame by hosts/generators.

## Setup & Usage Recipes

### A. Prototype in-Editor with MechanicHost
Deprecated. Use the generator-driven flow instead.

### B. Runtime build with JSON + ProcederalItemGenerator

1. Create an empty GameObject named `RuntimeBuilder` and add `ProcederalItemGenerator`.
2. Assign `owner` (e.g., the player transform). Optionally assign `target`.
3. Assign TextAssets for `primaryMechanicListJson` and `modifierMechanicListJson` in the inspector, or place them in `Resources/` with the exact names `Primary Mechanic List` and `Modifier Mechanic List`.
4. From code, construct:
   - `var instr = new ItemInstruction { primary = "Projectile", secondary = new List<string>{"Orbit"} };`
   - `var p = new ItemParams { subItemCount = 4, orbitRadius = 2f, orbitSpeedDeg = 90f, projectileDamage = 5 };`
5. Call `Create(instr, p, parentTransform)` to spawn and initialize the item.
6. The attached `MechanicRunner` will tick all generated `IMechanic` components automatically.

### Recommended scene wiring (who holds what)

Attach to Player GameObject (or the owning entity):
- `ProcederalItemGenerator`
  - References:
    - `owner`: Drag the Player’s Transform (usually `self`).
    - `target`: Leave null for player-centered effects, or set a specific Transform for target-centered effects.
    - `defaultParent`: Optional. If set, generated items parent here; otherwise they parent under the generator or provided parent.
    - `primaryMechanicListJson` / `modifierMechanicListJson`: Assign TextAssets or rely on `Resources`.
- (Optional) Any controller scripts that decide when to spawn or update items using the generator.

Create as their own GameObjects (runtime):
- Generated item roots and sub-items are created by the generator at runtime. You don’t pre-place mechanics on the player when using this flow.
- `MechanicRunner` is automatically added to each generated item root by the generator.

When making references:
- For player-centric items (orbiting weapons, auras around player): set `owner = Player`, keep `target = null`.
- For effects anchored elsewhere (boss orbit, waypoint aura, focused enemy): set `owner = Player` (who owns/spawns it), and set `target = ThatOtherTransform`. Mechanics that support target centering will use it (e.g., `centerOnTarget` toggles).
- Parent for organization: set `defaultParent` to a child empty under player like `Player/GeneratedItems` to keep hierarchy tidy.

### Migration: MechanicHost → Generator + Runner

1. Remove `MechanicHost` from your scene objects (or disable it).
2. Add `ProcederalItemGenerator` to a suitable object (player or spawner) and assign `owner/target`.
3. Create or reuse the JSON TextAssets for primaries/modifiers and assign them in the generator inspector (or put them under `Resources/`).
4. From code, build items with `Create(...)` using `ItemInstruction` and `ItemParams`.
5. Confirm a `MechanicRunner` exists on the created root and is registered (added automatically by the generator). Mechanics will now tick without the host.

### C. Randomized combos with OfflineItemGeneratorApi

1. Reference the two JSON TextAssets.
2. Call `OfflineItemGeneratorApi.MakeRandom(primaryJson, modifierJson)`.
3. Use the returned `instruction` + `parameters` to pass into `ProcederalItemGenerator.Create`.

## Conventions & Extensibility

- Add new mechanics by:
  1) Creating a MonoBehaviour that implements `IMechanic` with `Initialize` and `Tick`.
  2) Adding an entry in the appropriate JSON with `MechanicName` and `MechanicPath` (path to the .cs file or full type name).
  3) Optionally specifying default `Properties` and `Overrides`/`MechanicOverrides`.
- `MechanicReflection` will set public fields and properties matching keys (case-insensitive, basic type coercion supported).
- `ProcederalItemGenerator.NormalizeSettings` can be extended to map string tokens to vectors/enums or to massage legacy keys.

## Troubleshooting

- Mechanics not found: Ensure `MechanicName` is present in JSON and `MechanicPath` points to a valid script/type. For Unity paths, the filename is used to infer type.
- JSON not loading: Either assign TextAssets in the generator inspector or put them in `Resources/` with exact names.
- Duplicate motion with Projectile+Orbit: Ensure `disableSelfSpeed` is true (set automatically when Orbit is present via generator or via `MechanicOverrides`).
- Aura visualization not visible: Check `AuraMechanic.showVisualization`, sorting order, and that the scene camera renders the layer.
- ProcederalItemGenerator "does nothing": The generator is passive until you call `Create(...)`. Trigger it by:
  - Adding `SampleRandomItemSpawner` to the Player (or spawner) with `spawnOnStart = true`, or use its context menu action at runtime.
  - Calling `generator.Create(instruction, parameters, parent)` from your own script (e.g., on input or a timer).
  - Verifying that `primaryMechanicListJson`/`modifierMechanicListJson` are assigned (or present in `Resources/`) and that `owner` is set.

---
