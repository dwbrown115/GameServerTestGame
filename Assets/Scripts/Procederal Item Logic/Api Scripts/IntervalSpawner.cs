using System.Collections.Generic;
using Game.Procederal;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Spawns GameObjects periodically using a resolver for spawn position and direction.
    /// Applies configured modifiers to each spawned child.
    [DisallowMultipleComponent]
    public class IntervalSpawner : MonoBehaviour
    {
        [Header("Wiring")]
        public ProcederalItemGenerator generator;
        public Transform owner;

        [Header("Spawn Resolver")]
        [Tooltip(
            "Optional explicit resolver; if none, a NeutralSpawnPositon will be added by default."
        )]
        public MonoBehaviour spawnResolverBehaviour; // should implement ISpawnPositionResolver
        private ISpawnPositionResolver _resolver;

        [Header("Spawn Settings")]
        public float interval = 0.5f;
        public int countPerInterval = 1;
        public float spawnRadius = 0f; // distance from owner to place initial spawn
        public float lifetime = -1f; // seconds; <=0 means no auto-destroy

        [Header("Projectile Settings")]
        [Tooltip("When true, override JSON damage for projectiles with the value below.")]
        public bool overrideDamage = false;
        public int damage = 10;

        [Tooltip("When true, override JSON DestroyOnHit for projectiles with the value below.")]
        public bool overrideDestroyOnHit = false;
        public bool destroyOnHit = true;
        public bool excludeOwner = true;
        public bool requireMobTag = true;
        public float projectileSpeed = -1f; // if > 0, overrides JSON default

        [Header("Modifiers (applies to each child)")]
        [Tooltip(
            "If true, projectiles will report damage to an owner DrainMechanic configured by this spawner."
        )]
        public bool applyDrain = false;

        [Range(0f, 1f)]
        public float drainLifeStealRatio = 0.5f;

        // Runtime modifier specs to apply to each spawned child
        private readonly List<(
            string mechanicName,
            (string key, object val)[] settings
        )> _modifierSpecs = new();

        [Header("Visuals")]
        public string spriteType = "circle"; // circle | square | custom
        public string customSpritePath = null;
        public Color spriteColor = Color.white;

        [Header("Debug")]
        public bool debugLogs = false;

        private float _timer;
        private readonly List<GameObject> _spawned = new();
        private bool _stopped;

        private void OnEnable()
        {
            _timer = 0f;
            GameOverController.OnCountdownFinished += StopSpawning;
            EnsureResolver();
            EnsureModifiers();
        }

        private void OnDisable()
        {
            GameOverController.OnCountdownFinished -= StopSpawning;
        }

        private void Update()
        {
            if (_stopped)
                return;
            _timer += Time.deltaTime;
            if (_timer >= Mathf.Max(0.01f, interval))
            {
                _timer = 0f;
                SpawnBurst();
            }
        }

        public void AddModifierSpec(string mechanicName, params (string key, object val)[] settings)
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
                return;
            _modifierSpecs.Add((mechanicName, settings));
        }

        private void StopSpawning()
        {
            _stopped = true;
            if (debugLogs)
                Debug.Log("[IntervalSpawner] Stopped by game over.", this);
        }

        private void SpawnBurst()
        {
            if (generator == null)
            {
                if (debugLogs)
                    Debug.LogWarning("[IntervalSpawner] No generator assigned.", this);
                return;
            }
            Transform center = owner != null ? owner : transform;
            int beforeCount = _spawned.Count;
            for (int i = 0; i < Mathf.Max(1, countPerInterval); i++)
            {
                // Compute spawn pos + initial direction using resolver, with fallbacks
                Vector3 pos = center.position;
                Vector2 dir = Vector2.right;
                bool got =
                    (_resolver != null)
                    && _resolver.TryGetSpawn(center, Mathf.Max(0f, spawnRadius), out pos, out dir);
                if (!got)
                {
                    var chaos = GetComponent<ChaosSpawnPosition>();
                    if (chaos == null)
                        chaos = gameObject.AddComponent<ChaosSpawnPosition>();
                    got = chaos.TryGetSpawn(center, Mathf.Max(0f, spawnRadius), out pos, out dir);
                }
                if (!got)
                {
                    // Final safety fallback: random
                    dir = Random.insideUnitCircle.normalized;
                    if (dir.sqrMagnitude < 0.001f)
                        dir = Vector2.right;
                    pos = center.position + (Vector3)(dir * Mathf.Max(0f, spawnRadius));
                }

                var go = new GameObject("Projectile_Spawned");
                go.transform.SetParent(transform, worldPositionStays: true);
                go.transform.position = pos;
                go.transform.localScale = Vector3.one;

                // Visuals
                var sr = go.AddComponent<SpriteRenderer>();
                Sprite chosen = null;
                switch ((spriteType ?? "circle").ToLowerInvariant())
                {
                    case "custom":
                        if (!string.IsNullOrEmpty(customSpritePath))
                            chosen = Resources.Load<Sprite>(customSpritePath);
                        if (chosen == null)
                            chosen = ProcederalItemGenerator.GetUnitCircleSprite();
                        break;
                    case "square":
                        chosen = ProcederalItemGenerator.GetUnitSquareSprite();
                        break;
                    case "circle":
                    default:
                        chosen = ProcederalItemGenerator.GetUnitCircleSprite();
                        break;
                }
                sr.sprite = chosen;
                sr.color = spriteColor;

                // Collider and RB
                var cc = go.AddComponent<CircleCollider2D>();
                cc.isTrigger = true;
                cc.radius = 1f;
                go.layer = (owner != null ? owner.gameObject.layer : go.layer);
                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Dynamic;
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                // Mechanics: add primary Projectile and configured modifiers before initializing
                var settings = new List<(string key, object val)>
                {
                    ("direction", (Vector2)dir),
                    ("excludeOwner", excludeOwner),
                    ("requireMobTag", requireMobTag),
                    ("disableSelfSpeed", false),
                    ("debugLogs", debugLogs),
                };
                if (overrideDamage)
                    settings.Add(("damage", damage));
                if (overrideDestroyOnHit)
                    settings.Add(("destroyOnHit", destroyOnHit));
                if (projectileSpeed > 0f)
                    settings.Add(("speed", projectileSpeed));

                generator.AddMechanicByName(go, "Projectile", settings.ToArray());

                // Apply modifier specs to each child (skip Drain here; it's owner-level)
                if (_modifierSpecs.Count > 0)
                {
                    foreach (var spec in _modifierSpecs)
                    {
                        if (
                            string.Equals(
                                spec.mechanicName,
                                "Drain",
                                System.StringComparison.OrdinalIgnoreCase
                            )
                        )
                            continue; // handled at owner level
                        generator.AddMechanicByName(go, spec.mechanicName, spec.settings);
                    }
                }

                // Initialize mechanics with proper context once after adding all
                generator.InitializeMechanics(go, owner, generator.target);

                // If destroyOnHit is explicitly disabled, do not auto-destroy by lifetime
                if (lifetime > 0f && !(overrideDestroyOnHit && !destroyOnHit))
                {
                    go.AddComponent<_AutoDestroy>().seconds = lifetime;
                }

                _spawned.Add(go);
            }

            var runner = GetComponent<MechanicRunner>();
            if (runner != null && _spawned.Count > beforeCount)
            {
                runner.RegisterTree(transform);
            }
        }

        private void EnsureResolver()
        {
            if (_resolver != null)
                return;
            if (spawnResolverBehaviour != null)
            {
                _resolver = spawnResolverBehaviour as ISpawnPositionResolver;
                if (_resolver == null && debugLogs)
                    Debug.LogWarning(
                        "[IntervalSpawner] Assigned SpawnResolverBehaviour does not implement ISpawnPositionResolver.",
                        this
                    );
            }
            if (_resolver == null)
            {
                _resolver = GetComponent<ISpawnPositionResolver>();
            }
            if (_resolver == null)
            {
                _resolver = gameObject.AddComponent<NeutralSpawnPositon>();
                spawnResolverBehaviour = (MonoBehaviour)_resolver;
            }
        }

        private void EnsureModifiers()
        {
            if (!applyDrain)
                return;
            Transform drainOwner = owner != null ? owner : transform;
            if (drainOwner == null)
                return;
            var drain = drainOwner.GetComponentInChildren<Mechanics.Corruption.DrainMechanic>();
            if (drain == null)
            {
                drain = drainOwner.gameObject.AddComponent<Mechanics.Corruption.DrainMechanic>();
            }
            drain.lifeStealRatio = Mathf.Clamp01(drainLifeStealRatio);
            drain.debugLogs = drain.debugLogs || debugLogs;
        }

        private class _AutoDestroy : MonoBehaviour
        {
            public float seconds = 1f;
            private float _t;

            private void Update()
            {
                _t += Time.deltaTime;
                if (_t >= seconds)
                {
                    Destroy(gameObject);
                }
            }
        }
    }
}
