using System;
using System.Collections.Generic;
using Game.Procederal;
using Game.Procederal.Core;
using Mechanics.Neuteral;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Generic interval spawner that can spawn any payload mechanic using the generator.
    /// Payload mechanic name and settings are provided at runtime via SetPayloadSettings.
    /// Modifiers are forwarded via AddModifierSpec and applied to each spawned child.
    [DisallowMultipleComponent]
    public partial class GenericIntervalSpawner
        : MonoBehaviour,
            IModifierReceiver,
            IModifierOwnerProvider
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

        [Tooltip("Uniform scale applied to spawned payload shells (1 = original size).")]
        public float spawnScale = 1f;

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

        [Tooltip("Parent spawned payloads to this spawner. Disable to detach into world space.")]
        public bool parentSpawnedToSpawner = true;

        [Tooltip(
            "Attach an AutoDestroyAfterSeconds timer when lifetime > 0. Disable when payload mechanics manage lifetime themselves."
        )]
        public bool autoDestroyPayloads = true;

        [Header("Owner Collisions")]
        [Tooltip("When true, spawned payload colliders will ignore the owner's colliders.")]
        public bool ignoreCollisionsWithOwner = false;

        [Header("Owner Modifiers")]
        [Tooltip(
            "If true, ensure an owner DrainMechanic exists so payloads can report damage drain."
        )]
        public bool applyDrain = false;

        [Range(0f, 1f)]
        public float drainLifeStealRatio = 0.5f;

        [Header("Duplicate Avoidance")]
        [Tooltip(
            "If true, skip spawning when an existing payload mechanic is already within duplicateCheckRadius of the owner."
        )]
        public bool avoidDuplicateNearOwner = false;

        [Tooltip(
            "Radius used for owner-centric duplicate checking (fallbacks to colliderRadius when <= 0)."
        )]
        public float duplicateCheckRadius = 0f;

        [Tooltip(
            "Mechanic token checked when avoidDuplicateNearOwner is enabled (ex: DamageZone)."
        )]
        public string duplicateMechanicName = null;

        [Header("Concurrency")]
        [Tooltip("When true, enforce a maximum number of active payloads for this spawner.")]
        public bool enforceActiveChildLimit = false;

        [Tooltip("Maximum active payloads when enforcing child limit. 0 uses countPerInterval.")]
        public int maxActiveChildren = 0;

        [Tooltip(
            "When true, release the oldest active payload to free a slot once the limit is reached."
        )]
        public bool recycleOldestChild = true;

        [Header("Debug")]
        public bool debugLogs = false;

        [Header("Collisions")]
        [Tooltip(
            "When set, sphere overlap checks are performed against the provided layer mask before spawning."
        )]
        public bool preventOverlap = false;

        [Tooltip(
            "Radius used for overlap avoidance checks. Falls back to colliderRadius when <= 0."
        )]
        public float overlapRadius = 0.5f;

        [Tooltip("Layer mask to test when preventOverlap is enabled.")]
        public LayerMask overlapLayerMask = ~0;

        // Generic modifier specs to add to each spawned payload
        private readonly List<(
            string mechanicName,
            (string key, object val)[] settings
        )> _modifierSpecs = new();

        private float _timer;
        private bool _stopped;
        private bool _didFirstBurst;
        private int _executedBursts;
        private bool _burstLimitActive;
        private int _maxBurstLimit = -1;
        private readonly List<Transform> _trackedChildren = new();
        private readonly HashSet<Transform> _trackedChildSet = new();
        private readonly Queue<GameObject> _pooledChildren = new();
        private readonly HashSet<GameObject> _pooledChildSet = new();
        private Collider2D[] _ownerCollidersCache = System.Array.Empty<Collider2D>();
        private Transform _cachedOwnerForColliders;
        private bool _loggedOwnerCollisionIgnore;

        private void OnEnable()
        {
            _timer = 0f;
            _stopped = false;
            _didFirstBurst = false;
            _executedBursts = 0;
            _burstLimitActive = false;
            _maxBurstLimit = -1;
            RebuildTrackedChildren();
            GameOverController.OnCountdownFinished += StopSpawning;
            EnsureResolver();
            EnsureOwnerModifiers();
            ResetOwnerColliderCache();
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
            ClearTrackedChildren();
            _burstLimitActive = false;
            _maxBurstLimit = -1;
            _executedBursts = 0;
            ResetOwnerColliderCache();
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
            if (_burstLimitActive && _maxBurstLimit >= 0 && _executedBursts >= _maxBurstLimit)
            {
                if (debugLogs)
                {
                    Debug.Log("[GenericIntervalSpawner] Burst limit reached; disabling.", this);
                }
                StopAndDisable();
                return;
            }

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
            var runner = GetComponent<MechanicRunner>();
            int spawnCount = Mathf.Max(1, countPerInterval);
            bool spawnedAny = false;

            if (debugLogs)
                LogAttachedChildCount("pre-burst child count");

            int activeChildLimit = enforceActiveChildLimit ? GetChildLimit() : int.MaxValue;
            int activeChildren = GetActiveChildCount();
            if (activeChildren >= activeChildLimit)
            {
                if (debugLogs)
                {
                    Debug.Log(
                        $"[GenericIntervalSpawner] Active child limit {activeChildLimit} already reached; skipping burst.",
                        this
                    );
                }
                return;
            }
            for (int i = 0; i < spawnCount; i++)
            {
                if (!EnsureChildSlotAvailable())
                    break;

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
                    dir = UnityEngine.Random.insideUnitCircle.normalized;
                    if (dir.sqrMagnitude < 0.001f)
                        dir = Vector2.right;
                    pos = center.position + (Vector3)(dir * Mathf.Max(0f, spawnRadius));
                }

                if (preventOverlap && IsOverlapping(pos))
                {
                    if (debugLogs)
                    {
                        Debug.Log(
                            $"[GenericIntervalSpawner] Skipping spawn due to overlap at {pos}.",
                            this
                        );
                    }
                    continue;
                }

                if (avoidDuplicateNearOwner && ShouldSkipForDuplicateNearOwner())
                {
                    if (debugLogs)
                    {
                        Debug.Log(
                            "[GenericIntervalSpawner] Skipping spawn; existing payload near owner.",
                            this
                        );
                    }
                    continue;
                }

                var settings = BuildPayloadSettings(dir);
                var reused = TryReuseInactiveChild(pos, settings, runner);
                if (reused != null)
                {
                    spawnedAny = true;
                    if (debugLogs)
                        LogAttachedChildCount($"post-reuse #{i + 1} child count");
                    if (GetActiveChildCount() >= maxActiveChildren)
                        break;
                    continue;
                }

                var shell = new SpawnHelpers.PayloadShellOptions
                {
                    parent = parentSpawnedToSpawner ? transform : null,
                    position = pos,
                    layer = owner != null ? owner.gameObject.layer : gameObject.layer,
                    spriteType =
                        (createSpriteRenderer && !string.IsNullOrEmpty(spriteType))
                            ? spriteType
                            : null,
                    customSpritePath = customSpritePath,
                    spriteColor = spriteColor,
                    uniformScale = Mathf.Max(0.0001f, spawnScale),
                    createCollider = createCollider,
                    colliderRadius = colliderRadius,
                    createRigidBody = createRigidBody,
                    bodyType = rigidBodyType,
                    freezeRotation = freezeRotation,
                    addAutoDestroy = autoDestroyPayloads && lifetime > 0f,
                    lifetimeSeconds = lifetime > 0f ? lifetime : 0f,
                };
                var go = SpawnHelpers.CreatePayloadShell($"{payloadMechanicName}_Spawned", shell);
                bool reactivateAfterSetup = go != null && go.activeSelf;
                if (reactivateAfterSetup)
                    go.SetActive(false);
                if (parentSpawnedToSpawner)
                {
                    if (go.transform.parent != transform)
                        go.transform.SetParent(transform, worldPositionStays: true);
                }
                else
                {
                    Game.Procederal.ProcederalItemGenerator.DetachToWorld(
                        go,
                        worldPositionStays: true
                    );
                }

                generator.AddMechanicByName(go, payloadMechanicName, settings.ToArray());

                // Apply modifiers to payload
                if (_modifierSpecs.Count > 0)
                {
                    foreach (var spec in _modifierSpecs)
                        generator.AddMechanicByName(go, spec.mechanicName, spec.settings);
                    ApplyModifierSettingsToExisting(go);
                }

                generator.InitializeMechanics(go, owner, generator.ResolveTargetOrDefault());

                if (!VerifyActivationGuards(go, out var guardFailure))
                {
                    HandleActivationGuardFailure(go, guardFailure, fromPool: false);
                    continue;
                }

                if (reactivateAfterSetup)
                    go.SetActive(true);

                // Ensure payloads advertise their generator ownership so AutoDespawn and pooling work.
                var handle = go.GetComponent<GeneratedObjectHandle>();
                if (handle == null)
                    handle = go.AddComponent<GeneratedObjectHandle>();
                handle.Initialize(generator, payloadMechanicName ?? string.Empty);

                if (ignoreCollisionsWithOwner)
                    IgnoreOwnerCollisions(go);

                if (runner != null)
                    runner.RegisterTree(go.transform);

                TrackSpawnedChild(go);
                spawnedAny = true;

                if (debugLogs)
                    LogAttachedChildCount($"post-spawn #{i + 1} child count");

                if (enforceActiveChildLimit && GetActiveChildCount() >= activeChildLimit)
                    break;
            }

            _executedBursts++;
            if (!spawnedAny && debugLogs)
            {
                Debug.Log(
                    "[GenericIntervalSpawner] Burst completed with no payloads spawned.",
                    this
                );
            }

            if (_burstLimitActive && _maxBurstLimit >= 0 && _executedBursts >= _maxBurstLimit)
            {
                if (debugLogs)
                {
                    Debug.Log("[GenericIntervalSpawner] Burst budget consumed; disabling.", this);
                }
                StopAndDisable();
            }
        }

        private bool IsOverlapping(Vector3 position)
        {
            float radius = overlapRadius > 0f ? overlapRadius : colliderRadius;
            if (radius <= 0f)
                radius = 0.5f;

            var hits = Physics2D.OverlapCircleAll(position, radius, overlapLayerMask);
            if (hits == null || hits.Length == 0)
                return false;

            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i];
                if (hit == null)
                    continue;

                // Ignore collisions with the owner or this spawner itself
                if (owner != null && hit.transform.IsChildOf(owner))
                    continue;
                if (hit.transform == transform)
                    continue;

                return true;
            }

            return false;
        }

        private bool ShouldSkipForDuplicateNearOwner()
        {
            Transform ownerRef = owner != null ? owner : transform;
            if (ownerRef == null)
                return false;

            float radius = duplicateCheckRadius > 0f ? duplicateCheckRadius : colliderRadius;
            if (radius <= 0f)
                radius = 0.5f;

            if (
                string.Equals(
                    duplicateMechanicName,
                    "DamageZone",
                    System.StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return DamageZoneMechanic.HasZoneWithinRadius(ownerRef.position, radius);
            }

            return false;
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

        private void EnsureOwnerModifiers()
        {
            if (!applyDrain)
                return;

            Transform drainOwner = owner != null ? owner : transform;
            if (drainOwner == null)
                return;

            var drain = drainOwner.GetComponentInChildren<Mechanics.Corruption.DrainMechanic>();
            if (drain == null)
                drain = drainOwner.gameObject.AddComponent<Mechanics.Corruption.DrainMechanic>();

            drain.lifeStealRatio = Mathf.Clamp01(drainLifeStealRatio);
            drain.debugLogs = drain.debugLogs || debugLogs;
        }

        private void ResetOwnerColliderCache()
        {
            _cachedOwnerForColliders = owner;
            _loggedOwnerCollisionIgnore = false;
            _ownerCollidersCache =
                owner != null
                    ? owner.GetComponentsInChildren<Collider2D>(includeInactive: true)
                    : System.Array.Empty<Collider2D>();
        }

        private void IgnoreOwnerCollisions(GameObject payload)
        {
            if (!ignoreCollisionsWithOwner || owner == null || payload == null)
                return;

            if (owner != _cachedOwnerForColliders)
                ResetOwnerColliderCache();

            if (_ownerCollidersCache == null || _ownerCollidersCache.Length == 0)
                return;

            var payloadColliders = payload.GetComponentsInChildren<Collider2D>(true);
            if (payloadColliders == null || payloadColliders.Length == 0)
                return;

            for (int i = 0; i < _ownerCollidersCache.Length; i++)
            {
                var ownerCollider = _ownerCollidersCache[i];
                if (ownerCollider == null)
                    continue;
                for (int j = 0; j < payloadColliders.Length; j++)
                {
                    var payloadCollider = payloadColliders[j];
                    if (payloadCollider == null)
                        continue;
                    Physics2D.IgnoreCollision(payloadCollider, ownerCollider, true);
                }
            }

            if (debugLogs && !_loggedOwnerCollisionIgnore)
            {
                _loggedOwnerCollisionIgnore = true;
                Debug.Log(
                    $"[GenericIntervalSpawner] Ignoring collisions between payload '{payload.name}' and owner '{owner.name}' colliders ({_ownerCollidersCache.Length})",
                    this
                );
            }
        }

        private void LogAttachedChildCount(string context)
        {
            if (!debugLogs)
                return;

            var target = transform;
            int childCount = target != null ? target.childCount : 0;
            Debug.Log(
                $"[GenericIntervalSpawner] {context}: '{name}' currently has {childCount} attached child(ren) ({GetActiveChildCount()} active).",
                this
            );
        }

        private int GetActiveChildCount()
        {
            PruneTrackedChildren();
            int count = 0;
            for (int i = 0; i < _trackedChildren.Count; i++)
            {
                var child = _trackedChildren[i];
                if (child == null)
                    continue;
                if (child.gameObject.activeInHierarchy)
                    count++;
            }
            return count;
        }

        private List<(string key, object val)> BuildPayloadSettings(Vector2 dir)
        {
            var settings = new List<(string key, object val)>(_payloadSettings);
            InjectDirectionFromResolver(settings, dir);
            return settings;
        }

        private static void InjectDirectionFromResolver(
            List<(string key, object val)> settings,
            Vector2 dir
        )
        {
            if (settings == null || settings.Count == 0)
                return;

            int idx = settings.FindIndex(kv =>
                string.Equals(
                    kv.key,
                    "directionFromResolver",
                    System.StringComparison.OrdinalIgnoreCase
                )
            );
            if (idx < 0)
                return;

            var mode = settings[idx].val as string;
            settings.RemoveAt(idx);
            if (string.Equals(mode, "vector2", System.StringComparison.OrdinalIgnoreCase))
            {
                settings.Add(("direction", (Vector2)dir));
                return;
            }

            if (string.Equals(mode, "degrees", System.StringComparison.OrdinalIgnoreCase))
            {
                float deg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                settings.Add(
                    ("direction", deg.ToString(System.Globalization.CultureInfo.InvariantCulture))
                );
            }
        }

        private GameObject TryReuseInactiveChild(
            Vector3 position,
            List<(string key, object val)> payloadSettings,
            MechanicRunner runner
        )
        {
            if (generator == null)
                return null;

            while (true)
            {
                var go = DequeuePooledChild();
                if (go == null)
                    return null;

                generator.NotifyBorrowedPooledInstance(go);
                PrepareReusedPayload(go, position);
                ApplySettingsToExistingMechanics(go, payloadSettings, payloadMechanicName);
                ApplyModifierSettingsToExisting(go);

                generator.InitializeMechanics(go, owner, generator.ResolveTargetOrDefault());

                if (!VerifyActivationGuards(go, out var guardFailure))
                {
                    HandleActivationGuardFailure(go, guardFailure, fromPool: true);
                    continue;
                }

                go.SetActive(true);

                if (ignoreCollisionsWithOwner)
                    IgnoreOwnerCollisions(go);

                if (runner != null)
                    runner.RegisterTree(go.transform);

                TrackSpawnedChild(go);

                if (debugLogs)
                {
                    Debug.Log($"[GenericIntervalSpawner] Reused pooled payload '{go.name}'.", this);
                }

                return go;
            }
        }

        private GameObject DequeuePooledChild()
        {
            while (_pooledChildren.Count > 0)
            {
                var go = _pooledChildren.Dequeue();
                if (go == null)
                    continue;

                _pooledChildSet.Remove(go);

                if (!go.activeInHierarchy && !go.activeSelf)
                    return go;
            }

            return null;
        }

        private void PrepareReusedPayload(GameObject go, Vector3 position)
        {
            if (go == null)
                return;

            if (parentSpawnedToSpawner)
            {
                if (go.transform.parent != transform)
                    go.transform.SetParent(transform, worldPositionStays: true);
            }
            else
            {
                Game.Procederal.ProcederalItemGenerator.DetachToWorld(go, worldPositionStays: true);
            }

            go.transform.position = position;
            go.transform.localScale = Vector3.one * Mathf.Max(0.0001f, spawnScale);
            go.layer = owner != null ? owner.gameObject.layer : gameObject.layer;

            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            var resettables = go.GetComponents<IPooledPayloadResettable>();
            if (resettables != null)
            {
                for (int i = 0; i < resettables.Length; i++)
                {
                    resettables[i]?.ResetForPool();
                }
            }
        }

        private void ApplySettingsToExistingMechanics(
            GameObject go,
            List<(string key, object val)> settings,
            string mechanicName
        )
        {
            if (go == null || generator == null || string.IsNullOrWhiteSpace(mechanicName))
                return;
            if (settings == null || settings.Count == 0)
                return;

            for (int i = 0; i < settings.Count; i++)
            {
                var kv = settings[i];
                generator.SetExistingMechanicSetting(go, mechanicName, kv.key, kv.val);
            }
        }

        private void ApplyModifierSettingsToExisting(GameObject go)
        {
            if (go == null || generator == null)
                return;
            if (_modifierSpecs.Count == 0)
                return;

            for (int i = 0; i < _modifierSpecs.Count; i++)
            {
                var spec = _modifierSpecs[i];
                if (string.IsNullOrWhiteSpace(spec.mechanicName) || spec.settings == null)
                    continue;

                if (
                    string.Equals(
                        spec.mechanicName,
                        "SubItemsOnCondition",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    var sub = go.GetComponent<SubItemsOnConditionMechanic>();
                    if (sub != null)
                    {
                        var dict = BuildSettingsDictionary(spec.settings);
                        bool enableDebug = ExtractBool(dict, "debugLogs");
                        if (enableDebug)
                            sub.debugLogs = true;
                        SubItemsOnConditionMechanicSettings.Apply(sub, dict);
                        continue;
                    }
                }

                for (int k = 0; k < spec.settings.Length; k++)
                {
                    var kv = spec.settings[k];
                    generator.SetExistingMechanicSetting(go, spec.mechanicName, kv.key, kv.val);
                }
            }
        }

        private static Dictionary<string, object> BuildSettingsDictionary(
            (string key, object val)[] settings
        )
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (settings == null || settings.Length == 0)
                return dict;
            for (int i = 0; i < settings.Length; i++)
            {
                var kv = settings[i];
                if (string.IsNullOrWhiteSpace(kv.key))
                    continue;
                dict[kv.key] = kv.val;
            }
            return dict;
        }

        private static bool ExtractBool(Dictionary<string, object> dict, string key)
        {
            if (dict == null || string.IsNullOrWhiteSpace(key))
                return false;
            if (!dict.TryGetValue(key, out var raw) || raw == null)
                return false;
            switch (raw)
            {
                case bool b:
                    return b;
                case string s when bool.TryParse(s, out var parsed):
                    return parsed;
                case int i:
                    return i != 0;
                default:
                    return false;
            }
        }

        private bool VerifyActivationGuards(GameObject go, out string failure)
        {
            failure = null;
            if (go == null)
                return true;

            var guards = go.GetComponents<IMechanicActivationGuard>();
            if (guards == null || guards.Length == 0)
                return true;

            for (int i = 0; i < guards.Length; i++)
            {
                var guard = guards[i];
                if (guard == null)
                    continue;
                if (!guard.IsMechanicReady(out var reason))
                {
                    failure = reason;
                    return false;
                }
            }

            return true;
        }

        private void HandleActivationGuardFailure(GameObject go, string reason, bool fromPool)
        {
            if (debugLogs)
            {
                Debug.LogWarning(
                    $"[GenericIntervalSpawner] Activation blocked for '{go?.name ?? "<null>"}' (fromPool={fromPool}): {reason}",
                    this
                );
            }

            if (go == null)
                return;

            if (fromPool)
            {
                go.SetActive(false);
                return;
            }

            Destroy(go);
        }
    }
}
