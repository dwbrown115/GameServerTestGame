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

            // Find all mobs in scene (tag-based); pick a random one
            var mobs = GameObject.FindGameObjectsWithTag("Mob");
            if (mobs == null || mobs.Length == 0)
            {
                // No mob found; cannot determine direction
                return false;
            }
            Transform best = null;
            var cpos = center.position;
            int idx = Random.Range(0, mobs.Length);
            best = mobs[idx].transform;

            if (best == null)
                return false;

            Vector2 dir = (best.position - cpos);
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.right;
            direction = dir.normalized;
            position = cpos + (Vector3)(direction * Mathf.Max(0f, spawnRadius));
            return true;
        }
    }
}
