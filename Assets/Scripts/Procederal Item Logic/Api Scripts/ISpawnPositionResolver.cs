#if false
// This file has been relocated to 'Api Scripts/Spawn Resolvers/ISpawnPositionResolver.cs'
using UnityEngine;

namespace Game.Procederal.Api
{
    public interface ISpawnPositionResolver
    {
        bool TryGetSpawn(
            Transform owner,
            float spawnRadius,
            out Vector3 position,
            out Vector2 direction
        );
    }
}
#endif
