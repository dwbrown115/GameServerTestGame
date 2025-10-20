using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Chooses a spawn position aimed directly at a random mob at the moment of spawn.
    /// No tracking after spawn; just initial direction toward the chosen 'Mob' tagged target.
    [DisallowMultipleComponent]
    public class NeutralSpawnPositon : MonoBehaviour, ISpawnPositionResolver
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
            position = center.position;
            direction = Vector2.right;

            var cpos = center.position;
            var target = TargetingServiceLocator.Service.PickRandomMob(center);
            if (target == null)
                return false;

            Vector2 dir = (target.position - cpos);
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.right;
            direction = dir.normalized;
            position = cpos + (Vector3)(direction * Mathf.Max(0f, spawnRadius));
            return true;
        }
    }
}
