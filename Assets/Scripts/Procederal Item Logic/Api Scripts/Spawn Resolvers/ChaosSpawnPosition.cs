using UnityEngine;

namespace Game.Procederal.Api
{
    /// Chooses a random outward direction from the center (previous default behavior).
    [DisallowMultipleComponent]
    public class ChaosSpawnPosition : MonoBehaviour, ISpawnPositionResolver
    {
        [Tooltip(
            "Optional override for what to consider as the center; defaults to this or assigned owner."
        )]
        public Transform centerOverride;

        public bool TryGetSpawn(
            Transform owner,
            float spawnRadius,
            out Vector3 position,
            out Vector2 direction
        )
        {
            Transform center =
                centerOverride != null ? centerOverride : (owner != null ? owner : transform);
            var dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.001f)
                dir = Vector2.right;
            direction = dir;
            position = center.position + (Vector3)(dir * Mathf.Max(0f, spawnRadius));
            return true;
        }
    }
}
