using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// <summary>
    /// Helper that configures a co-located <see cref="GenericIntervalSpawner"/> to behave like the legacy
    /// projectile-centric interval spawner while exposing projectile-specific inspector options.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GenericIntervalSpawner))]
    public class ProjectileIntervalSpawner : MonoBehaviour
    {
        public enum DirectionInjectionMode
        {
            Vector2,
            Degrees,
        }

        [Header("Projectile Overrides")]
        [Tooltip(
            "When true, override projectile damage using the value below instead of JSON defaults."
        )]
        public bool overrideDamage = false;

        public int damage = 10;

        [Tooltip(
            "When true, override DestroyOnHit using the value below instead of JSON defaults."
        )]
        public bool overrideDestroyOnHit = false;

        public bool destroyOnHit = true;
        public bool excludeOwner = true;
        public bool requireMobTag = true;
        public float projectileSpeed = -1f;

        [Tooltip("When true, disable the Projectile mechanic's self-driven movement.")]
        public bool disableProjectileSelfSpeed = false;

        [Header("Direction Injection")]
        [Tooltip("Insert resolver direction into payload settings each spawn.")]
        public bool injectResolverDirection = true;

        public DirectionInjectionMode directionMode = DirectionInjectionMode.Vector2;

        [Header("Owner Modifiers")]
        [Tooltip("If true, ensure an owner DrainMechanic exists to collect projectile damage.")]
        public bool applyDrain = false;

        [Range(0f, 1f)]
        public float drainLifeStealRatio = 0.5f;

        private GenericIntervalSpawner _spawner;

        private void Awake()
        {
            EnsureSpawner();
        }

        private void OnEnable()
        {
            ApplyConfiguration();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ApplyConfiguration();
        }
#endif

        private void EnsureSpawner()
        {
            if (_spawner != null)
                return;
            _spawner =
                GetComponent<GenericIntervalSpawner>()
                ?? gameObject.AddComponent<GenericIntervalSpawner>();
        }

        public void ApplyConfiguration()
        {
            EnsureSpawner();
            if (_spawner == null)
                return;

            _spawner.payloadMechanicName = "Projectile";
            _spawner.ignoreCollisionsWithOwner = excludeOwner;
            _spawner.applyDrain = applyDrain;
            _spawner.drainLifeStealRatio = drainLifeStealRatio;

            var payload = new List<(string key, object val)>();
            if (injectResolverDirection)
            {
                string resolverMode =
                    directionMode == DirectionInjectionMode.Degrees ? "degrees" : "vector2";
                payload.Add(("directionFromResolver", resolverMode));
            }

            payload.Add(("excludeOwner", excludeOwner));
            payload.Add(("requireMobTag", requireMobTag));
            payload.Add(("disableSelfSpeed", disableProjectileSelfSpeed));
            payload.Add(("debugLogs", _spawner.debugLogs));

            if (overrideDamage)
                payload.Add(("damage", damage));
            if (overrideDestroyOnHit)
                payload.Add(("destroyOnHit", destroyOnHit));
            if (projectileSpeed > 0f)
                payload.Add(("speed", projectileSpeed));

            _spawner.SetPayloadSettings(payload.ToArray());
        }
    }
}
