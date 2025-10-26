using System.Collections.Generic;
using Game.Procederal;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Generic interval spawner that can spawn any payload mechanic using the generator.
    /// Payload mechanic name and settings are provided at runtime via SetPayloadSettings.
    /// Modifiers are forwarded via AddModifierSpec and applied to each spawned child.
    [DisallowMultipleComponent]
    public class GenericIntervalSpawner : MonoBehaviour, IModifierReceiver, IModifierOwnerProvider
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
        public float spawnRadius = 0f;
        public float lifetime = -1f; // <=0 means do not auto destroy

        [Tooltip(
            "If true, perform the first burst immediately on enable instead of waiting one interval."
        )]
        public bool immediateFirstBurst = false;

        [Header("Payload")]
        public string payloadMechanicName = null;
        private readonly List<(string key, object val)> _payloadSettings = new();

        [Header("Visuals (optional)")]
        public string spriteType = null; // circle | square | custom; if null, no sprite is created
        public string customSpritePath = null;
        public Color spriteColor = Color.white;

        [Header("Payload Shell")]
        [Tooltip("If false, skips adding a SpriteRenderer to spawned payload shells.")]
        public bool createSpriteRenderer = true;

        [Tooltip("If false, skips adding a CircleCollider2D to spawned payload shells.")]
        public bool createCollider = true;

        [Tooltip("Radius to apply when createCollider is true (in world units).")]
        public float colliderRadius = 0.5f;

        [Tooltip("If false, skips adding a Rigidbody2D to spawned payload shells.")]
        public bool createRigidBody = true;

        [Tooltip("Body type assigned when createRigidBody is true.")]
        public RigidbodyType2D rigidBodyType = RigidbodyType2D.Kinematic;

        [Tooltip("Freeze Z rotation on the payload Rigidbody2D when created.")]
        public bool freezeRotation = true;

        [Tooltip(
            "Attach an AutoDestroyAfterSeconds timer when lifetime > 0. Disable when payload mechanics manage lifetime themselves."
        )]
        public bool autoDestroyPayloads = true;

        [Header("Debug")]
        public bool debugLogs = false;

        // Generic modifier specs to add to each spawned payload
        private readonly List<(
            string mechanicName,
            (string key, object val)[] settings
        )> _modifierSpecs = new();

        private float _timer;
        private bool _stopped;
        private bool _didFirstBurst;

        private void OnEnable()
        {
            _timer = 0f;
            GameOverController.OnCountdownFinished += StopSpawning;
            EnsureResolver();
            _didFirstBurst = false;
            if (immediateFirstBurst && !_stopped)
            {
                // Perform exactly one immediate burst and mark it done.
                SpawnBurst();
                _didFirstBurst = true;
                _timer = 0f; // reset timer so next burst occurs after full interval
            }
        }

        private void OnDisable()
        {
            GameOverController.OnCountdownFinished -= StopSpawning;
        }

        public void SetPayloadSettings(params (string key, object val)[] settings)
        {
            _payloadSettings.Clear();
            if (settings == null)
                return;
            _payloadSettings.AddRange(settings);
        }

        public void AddModifierSpec(string mechanicName, params (string key, object val)[] settings)
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
                return;
            _modifierSpecs.Add((mechanicName, settings));
        }

        public Transform ModifierOwner => owner != null ? owner : transform;

        // Allow external code (e.g., strategy) to reset previously added modifier specs when reusing a spawner
        public void ClearModifierSpecs()
        {
            _modifierSpecs.Clear();
        }

        private void StopSpawning()
        {
            _stopped = true;
            if (debugLogs)
                Debug.Log("[GenericIntervalSpawner] Stopped by game over.", this);
        }

        private void Update()
        {
            if (_stopped)
                return;
            _timer += Time.deltaTime;
            float minInterval = Mathf.Max(0.01f, interval);
            if (!_didFirstBurst && !immediateFirstBurst)
            {
                // First burst should occur after first full interval (classic behavior)
                if (_timer >= minInterval)
                {
                    _timer = 0f;
                    SpawnBurst();
                    _didFirstBurst = true;
                }
            }
            else
            {
                if (_timer >= minInterval)
                {
                    _timer = 0f;
                    SpawnBurst();
                }
            }
        }

        private void SpawnBurst()
        {
            if (generator == null)
            {
                if (debugLogs)
                    Debug.LogWarning("[GenericIntervalSpawner] No generator assigned.", this);
                return;
            }
            if (string.IsNullOrWhiteSpace(payloadMechanicName))
            {
                if (debugLogs)
                    Debug.LogWarning("[GenericIntervalSpawner] No payloadMechanicName set.", this);
                return;
            }

            Transform center = owner != null ? owner : transform;
            int spawnCount = Mathf.Max(1, countPerInterval);
            for (int i = 0; i < spawnCount; i++)
            {
                if (debugLogs && i == 0)
                {
                    Debug.Log(
                        $"[GenericIntervalSpawner] Spawning burst of {spawnCount} for '{payloadMechanicName}' (countPerInterval={countPerInterval})",
                        this
                    );
                }
                // Resolve spawn pos and a suggested direction
                Vector3 pos = center.position;
                Vector2 dir = Vector2.right;
                bool got =
                    (_resolver != null)
                    && _resolver.TryGetSpawn(center, Mathf.Max(0f, spawnRadius), out pos, out dir);
                if (!got)
                {
                    var chaos =
                        GetComponent<ChaosSpawnPosition>()
                        ?? gameObject.AddComponent<ChaosSpawnPosition>();
                    got = chaos.TryGetSpawn(center, Mathf.Max(0f, spawnRadius), out pos, out dir);
                }
                if (!got)
                {
                    dir = Random.insideUnitCircle.normalized;
                    if (dir.sqrMagnitude < 0.001f)
                        dir = Vector2.right;
                    pos = center.position + (Vector3)(dir * Mathf.Max(0f, spawnRadius));
                }

                var shell = new SpawnHelpers.PayloadShellOptions
                {
                    parent = transform,
                    position = pos,
                    layer = owner != null ? owner.gameObject.layer : gameObject.layer,
                    spriteType =
                        (createSpriteRenderer && !string.IsNullOrEmpty(spriteType))
                            ? spriteType
                            : null,
                    customSpritePath = customSpritePath,
                    spriteColor = spriteColor,
                    createCollider = createCollider,
                    colliderRadius = colliderRadius,
                    createRigidBody = createRigidBody,
                    bodyType = rigidBodyType,
                    freezeRotation = freezeRotation,
                    addAutoDestroy = autoDestroyPayloads && lifetime > 0f,
                    lifetimeSeconds = lifetime > 0f ? lifetime : 0f,
                };
                var go = SpawnHelpers.CreatePayloadShell($"{payloadMechanicName}_Spawned", shell);
                if (go.transform.parent != transform)
                    go.transform.SetParent(transform, worldPositionStays: true);

                // Build payload settings for this instance (allow direction injection if requested via key)
                var settings = new List<(string key, object val)>(_payloadSettings);
                // If payload requested directionFromResolver, support either "vector2" or "degrees"
                // We look for a placeholder key: "directionFromResolver" with string value
                int idx = settings.FindIndex(kv =>
                    string.Equals(
                        kv.key,
                        "directionFromResolver",
                        System.StringComparison.OrdinalIgnoreCase
                    )
                );
                if (idx >= 0)
                {
                    var mode = settings[idx].val as string;
                    settings.RemoveAt(idx);
                    if (string.Equals(mode, "vector2", System.StringComparison.OrdinalIgnoreCase))
                        settings.Add(("direction", (Vector2)dir));
                    else if (
                        string.Equals(mode, "degrees", System.StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        float deg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                        settings.Add(
                            (
                                "direction",
                                deg.ToString(System.Globalization.CultureInfo.InvariantCulture)
                            )
                        );
                    }
                }

                generator.AddMechanicByName(go, payloadMechanicName, settings.ToArray());

                // Apply modifiers to payload
                if (_modifierSpecs.Count > 0)
                {
                    foreach (var spec in _modifierSpecs)
                        generator.AddMechanicByName(go, spec.mechanicName, spec.settings);
                }

                generator.InitializeMechanics(go, owner, generator.target);

                var runner = GetComponent<MechanicRunner>();
                if (runner != null)
                    runner.RegisterTree(go.transform);
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
                        "[GenericIntervalSpawner] SpawnResolverBehaviour does not implement ISpawnPositionResolver.",
                        this
                    );
            }
            if (_resolver == null)
                _resolver = GetComponent<ISpawnPositionResolver>();
            if (_resolver == null)
            {
                _resolver = gameObject.AddComponent<NeutralSpawnPositon>();
                spawnResolverBehaviour = (MonoBehaviour)_resolver;
            }
        }
    }
}
