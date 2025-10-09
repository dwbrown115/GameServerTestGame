# Mechanic Scaffolder

Adds menu item: `Tools > Create Mechanic...`

Generates a dedicated folder per mechanic with:
- <Name>Mechanic.cs (MonoBehaviour + IMechanic implementation stub)
- <Name>MechanicSettings.cs (static Apply helper parsing a settings dictionary)
- README.md documenting starter usage

## Usage
1. Open Unity Editor.
2. Menu: Tools > Create Mechanic...
3. Fill in:
   - Mechanic Name (PascalCase recommended; sanitizer will strip invalid chars)
   - Category Folder (top-level grouping under the base path)
   - Base Mechanics Path (defaults to existing mechanics root)
4. Press `Create Mechanic`.
5. (Optional) Files auto-open if the toggle is enabled.

## Conventions
- Namespace format: `Mechanics.<Category>` (spaces replaced with underscores).
- Settings class name: `<Name>MechanicSettings`.
- Mechanic class name: `<Name>Mechanic` implements `IMechanic` (Initialize + Tick).
- Extend settings parsing within the generated `Apply` method.

## Customization Ideas
- Add pooled destruction logic instead of `enabled = false` when lifetime elapses.
- Inject additional common mixins (interfaces) through template edits in `MechanicScaffolder.cs`.
- Auto-register new mechanic in a central registry JSON (future enhancement).

## Safety
If a file already exists it is skipped (no overwrite). Delete the folder manually if you want to regenerate.

---
Generated automatically; adjust as your project patterns evolve.
