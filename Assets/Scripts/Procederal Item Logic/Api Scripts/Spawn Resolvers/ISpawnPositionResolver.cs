using UnityEngine;

namespace Game.Procederal.Api
{
    /// Defines a strategy to compute a projectile spawn position and initial direction.
    public interface ISpawnPositionResolver
    {
        /// Returns a spawn world position and a unit direction for firing.
        /// Returns true if a direction could be determined; false to indicate caller should fallback.
        bool TryGetSpawn(
            Transform owner,
            float spawnRadius,
            out Vector3 position,
            out Vector2 direction
        );
    }
}
