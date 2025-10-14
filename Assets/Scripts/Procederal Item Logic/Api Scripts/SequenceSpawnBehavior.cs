using System.Collections.Generic;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Reusable one-shot (or manually triggered) sequence spawner.
    /// Spawns N children in order, tagging each with SequenceIndexTag (index/total) and applying a payload mechanic.
    /// Optional spacing time between spawns; supports immediate batch spawn when spacing <= 0.
    /// Designed to be data-driven via config assembled before calling BeginSequence.
    [DisallowMultipleComponent]
    public class SequenceSpawnBehavior : MonoBehaviour
    {
        [Header("Wiring")]
        public ProcederalItemGenerator generator;
        public Transform owner;

        [Header("Sequence Settings")]
        public int sequenceCount = 3; // number of children
        public float spacingSeconds = 0.05f; // delay between child spawns
        public bool autoStartOnEnable = false; // start sequence automatically
        public bool alignToOwnerVelocity = true; // derive forward direction from owner velocity if present
        public bool aimAtNearestEnemy = false; // if true, overrides direction each spawn toward nearest enemy

        [Tooltip(
            "Optional explicit direction override; ignored if aimAtNearestEnemy or alignToOwnerVelocity resolves a direction."
        )]
        public Vector2 explicitDirection = Vector2.right;

        [Header("Child Payload")]
        public string payloadMechanicName = "Projectile"; // e.g., Projectile or SwordSlash
        public float lifetime = -1f; // <=0 persistent
        public bool excludeOwner = true;
        public bool requireMobTag = true;
        public float travelSpeed = -1f; // for projectile-like payloads; if >0 inserted as speed
        public int damage = -1; // optional override

        [Header("Visuals (optional)")]
        public string spriteType = null; // circle/square/custom
        public string customSpritePath = null;
        public Color spriteColor = Color.white;

        [Header("Debug")]
        public bool debugLogs = false;

        private readonly List<(string key, object val)> _extraSettings = new();
        private readonly List<(
            string mechanicName,
            (string key, object val)[] settings
        )> _modifierSpecs = new();
        private int _spawned;
        private float _timer;
        private bool _active;
        private Vector2 _baseDir = Vector2.right;
        private BatchContainer _currentBatch;
        private int _batchSerial;

        public bool IsActive => _active;

        [Header("Spawn Origin")]
        [Tooltip(
            "If true, use owner position as the spawn center. If false, use this object's transform position."
        )]
        public bool useOwnerPositionForSpawn = true;

        private void OnEnable()
        {
            if (autoStartOnEnable)
                BeginSequence();
        }

        public void SetExtraPayloadSettings(params (string key, object val)[] settings)
        {
            _extraSettings.Clear();
            if (settings == null)
                return;
            _extraSettings.AddRange(settings);
        }

        public void AddModifierSpec(string mechanicName, params (string key, object val)[] settings)
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
                return;
            _modifierSpecs.Add((mechanicName, settings));
        }

        public void BeginSequence()
        {
            if (generator == null || sequenceCount <= 0)
            {
                if (debugLogs)
                    Debug.LogWarning("[SequenceSpawnBehavior] Cannot start sequence.", this);
                return;
            }
            // Create a parent container for this batch that will self-destroy when all its children are gone
            var own = owner != null ? owner : transform;
            var batchGo = new GameObject($"{payloadMechanicName}_Batch_{++_batchSerial}");
            batchGo.transform.SetParent(transform, worldPositionStays: true);
            var spawnPos =
                (useOwnerPositionForSpawn && own != null) ? own.position : transform.position;
            batchGo.transform.position = spawnPos;
            batchGo.layer = gameObject.layer;
            _currentBatch = batchGo.AddComponent<BatchContainer>();
            _currentBatch.debugLogs = debugLogs;

            _spawned = 0;
            _timer = 0f;
            _active = true;
            ResolveBaseDirection();
            if (spacingSeconds <= 0f)
            {
                // Spawn all instantly
                for (int i = 0; i < sequenceCount; i++)
                    SpawnOne(i);
                _active = false;
            }
        }

        private void Update()
        {
            if (!_active)
                return;
            _timer += Time.deltaTime;
            if (_spawned < sequenceCount && _timer >= spacingSeconds)
            {
                _timer = 0f;
                SpawnOne(_spawned);
                if (_spawned >= sequenceCount)
                    _active = false;
            }
        }

        private Vector2 ResolveForwardVector()
        {
            var own = owner != null ? owner : transform;
            Vector2 resolved =
                explicitDirection.sqrMagnitude > 0.001f
                    ? explicitDirection.normalized
                    : Vector2.right;
            if (alignToOwnerVelocity && own != null)
            {
                var rb = own.GetComponent<Rigidbody2D>();
                if (rb != null && rb.linearVelocity.sqrMagnitude > 0.01f)
                    resolved = rb.linearVelocity.normalized;
            }
            if (aimAtNearestEnemy && own != null)
            {
                var target = FindNearestMob(own);
                if (target != null)
                {
                    Vector2 to = (Vector2)(target.position - own.position);
                    if (to.sqrMagnitude > 1e-6f)
                        resolved = to.normalized;
                }
            }
            return resolved;
        }

        private void ResolveBaseDirection()
        {
            _baseDir = ResolveForwardVector();
        }

        public Vector2 PeekForwardDirection()
        {
            return ResolveForwardVector();
        }

        private Transform FindNearestMob(Transform origin)
        {
            // Minimal placeholder; extend with tag/layer filtering as needed
            float best = float.MaxValue;
            Transform bestT = null;
            var all = GameObject.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            foreach (var t in all)
            {
                if (t == null || t == origin)
                    continue;
                if (!t.CompareTag("Mob"))
                    continue;
                float d = (t.position - origin.position).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    bestT = t;
                }
            }
            return bestT;
        }

        private void SpawnOne(int index)
        {
            var own = owner != null ? owner : transform;
            var go = new GameObject($"{payloadMechanicName}_Seq_{index}");
            // Parent under the current batch container (falls back to this if missing)
            var parentT = (_currentBatch != null ? _currentBatch.transform : transform);
            go.transform.SetParent(parentT, worldPositionStays: true);
            var spawnPos =
                (useOwnerPositionForSpawn && own != null) ? own.position : transform.position;
            go.transform.position = spawnPos;
            go.layer = (own != null ? own.gameObject.layer : go.layer);

            // Register with batch so the container destroys itself once all children are gone
            if (_currentBatch != null)
                _currentBatch.RegisterChild(go);

            // Optional visuals (skip for SwordSlash; it has its own visualization via SwordSlashPayload)
            if (
                !string.IsNullOrEmpty(spriteType)
                && !string.Equals(
                    payloadMechanicName,
                    "SwordSlash",
                    System.StringComparison.OrdinalIgnoreCase
                )
            )
            {
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
            }

            // Basic collider & RB (so projectile / slash mechanics can interact)
            var cc = go.AddComponent<CircleCollider2D>();
            cc.isTrigger = true;
            cc.radius = 0.5f;
            var rbChild = go.AddComponent<Rigidbody2D>();
            rbChild.bodyType = RigidbodyType2D.Kinematic;
            rbChild.interpolation = RigidbodyInterpolation2D.Interpolate;
            rbChild.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Assemble payload settings
            var settings = new List<(string key, object val)>(_extraSettings);
            settings.Add(("excludeOwner", excludeOwner));
            settings.Add(("requireMobTag", requireMobTag));
            settings.Add(("debugLogs", debugLogs));
            // Direction & speed (if projectile-like). We always insert direction; consumer decides how to use.
            settings.Add(("direction", _baseDir));
            if (travelSpeed > 0f)
                settings.Add(("speed", travelSpeed));
            if (damage > 0)
                settings.Add(("damage", damage));

            // If this sequence is driving a SwordSlash, compose the payload + movement + damage here
            // so children always have the correct mechanics applied.
            if (
                string.Equals(
                    payloadMechanicName,
                    "SwordSlash",
                    System.StringComparison.OrdinalIgnoreCase
                )
            )
            {
                // Base crescent payload
                generator.AddMechanicByName(
                    go,
                    "SwordSlash",
                    System.Array.Empty<(string key, object val)>()
                );
                // Orientation
                go.transform.right = _baseDir;
                // Projectile (damage + on-hit), with movement disabled
                var proj = new List<(string key, object val)>(6)
                {
                    ("direction", _baseDir),
                    ("damage", damage > 0 ? damage : 8),
                    ("requireMobTag", requireMobTag),
                    ("excludeOwner", excludeOwner),
                };
                proj.Add(("disableSelfSpeed", true));
                // Prevent Projectile from adding its own circle sprite; SwordSlash handles visuals
                proj.Add(("spriteType", null));
                if (travelSpeed > 0f)
                    proj.Add(("speed", travelSpeed));
                generator.AddMechanicByName(go, "Projectile", proj.ToArray());
                // Movement
                var move = new List<(string key, object val)>(3)
                {
                    ("direction", _baseDir),
                    ("speed", travelSpeed > 0f ? travelSpeed : 10f),
                    ("disableSelfSpeed", false),
                };
                generator.AddMechanicByName(go, "ChildMovementMechanic", move.ToArray());
            }
            else
            {
                generator.AddMechanicByName(go, payloadMechanicName, settings.ToArray());
            }

            // Apply modifier specs (sequence-level)
            if (_modifierSpecs.Count > 0)
            {
                foreach (var spec in _modifierSpecs)
                {
                    // For SwordSlash movement we already set direction/speed; pass-through other modifiers.
                    generator.AddMechanicByName(go, spec.mechanicName, spec.settings);
                }
            }

            generator.InitializeMechanics(go, owner, generator.target);

            // Ensure child mechanics tick even if spawned after root registration
            var runner = GetComponent<MechanicRunner>();
            if (runner == null)
                runner = gameObject.AddComponent<MechanicRunner>();
            runner.RegisterTree(go.transform);

            // Lifetime
            if (lifetime > 0f)
            {
                var auto = go.AddComponent<_AutoDestroyAfterSeconds>();
                auto.seconds = lifetime;
            }

            // Tag with sequence index
            var tag = go.AddComponent<SequenceIndexTag>();
            tag.index = index;
            tag.total = sequenceCount;

            _spawned++;
            if (debugLogs)
                Debug.Log($"[SequenceSpawnBehavior] Spawned {index + 1}/{sequenceCount}", this);
        }

        private class _AutoDestroyAfterSeconds : MonoBehaviour
        {
            public float seconds = 5f;
            private float _t;

            private void Update()
            {
                _t += Time.deltaTime;
                if (_t >= seconds)
                    Destroy(gameObject);
            }
        }

        // Container that tracks children for a single batch and self-destroys when the last child is destroyed
        private class BatchContainer : MonoBehaviour
        {
            public int remaining;
            public bool debugLogs;

            public void RegisterChild(GameObject child)
            {
                remaining = Mathf.Max(0, remaining) + 1;
                var tracker = child.AddComponent<_BatchChildTracker>();
                tracker.container = this;
            }

            public void OnChildDestroyed()
            {
                remaining = Mathf.Max(0, remaining - 1);
                if (debugLogs)
                    Debug.Log(
                        $"[SequenceSpawnBehavior] Batch child destroyed. Remaining: {remaining}",
                        this
                    );
                if (remaining <= 0)
                    Destroy(gameObject);
            }
        }

        private class _BatchChildTracker : MonoBehaviour
        {
            public BatchContainer container;

            private void OnDestroy()
            {
                // If parent container still exists, notify it
                if (container != null)
                    container.OnChildDestroyed();
            }
        }
    }
}
