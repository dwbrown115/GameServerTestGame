using UnityEngine;

// Deprecated legacy adapter: functionality moved into individual Mechanics.IPrimaryHitModifier implementations.
// Retained as an inert shell to avoid Unity prefab/scene serialization breakage until assets are migrated.
namespace Mechanics
{
    [DisallowMultipleComponent]
    public class StrikeEffectAdapter : MonoBehaviour, IStrikeHitModifier
    {
        // Intentionally empty. Remove this component from prefabs/scenes after migration.
    }
}
