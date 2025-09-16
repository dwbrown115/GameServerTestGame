#if false
// This file has been relocated to 'Api Scripts/Spawn Resolvers/ChaosSpawnPosition.cs'
using UnityEngine;

namespace Game.Procederal.Api
{
    [DisallowMultipleComponent]
    public class ChaosSpawnPosition : MonoBehaviour, ISpawnPositionResolver
    {
        public Transform centerOverride;
        public bool TryGetSpawn(Transform owner, float spawnRadius, out Vector3 position, out Vector2 direction)
        {
            position = (centerOverride != null ? centerOverride : (owner != null ? owner : transform)).position;
            direction = Vector2.right;
            return true;
        }
    }
}
#endif
