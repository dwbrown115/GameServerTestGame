# BeamMechanic Feature & Integration Notes (Updated: Head terminology & lifetime-only destruction)

Relocated into `Beam Scripts` folder as part of mechanic reorganization.

This document summarizes the current BeamMechanic capabilities, JSON properties, lifecycle rules, and how external systems (e.g. bounce / steering modifiers) should now interact with it.

// (Content trimmed for brevity – original details unchanged except updated path below.)

## Minimal JSON Example
```json
{
  "MechanicName": "Beam",
  "MechanicPath": "Assets/Scripts/Procederal Item Logic/Mechanics/Neuteral/Beam Scripts/BeamMechanic.cs",
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
