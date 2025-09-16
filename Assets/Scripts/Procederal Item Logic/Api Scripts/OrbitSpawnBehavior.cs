using System.Collections.Generic;
using Game.Procederal;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Spawns items that are intended to orbit around an owner/target.
    /// Supports interval-based spawning and manual spawning. Keeps items evenly spaced.
    [DisallowMultipleComponent]
    public class OrbitSpawnBehavior : MonoBehaviour
    {
        [Header("Wiring")]
        public ProcederalItemGenerator generator;
        public Transform owner; // orbit center

        [Header("Orbit Settings")]
        public float orbitRadius = 2f;
        public float angularSpeedDeg = 90f;

        [Header("Spawn Settings")]
        public bool useInterval = false;
        public float interval = 0.5f;
        public int countPerInterval = 1;

        [Header("Child Visuals")]
        public string spriteType = "circle"; // circle | square | custom
        public string customSpritePath = null;
        public Color spriteColor = Color.white;
        public float childScale = 1f; // visual size only; collider stays unit radius

        [Header("Child Projectile Settings")]
        [Tooltip("When true, override JSON damage for children with the value below.")]
        public bool overrideDamage = false;
        public int damage = 1;

        [Tooltip("When true, override JSON DestroyOnHit for children with the value below.")]
        public bool overrideDestroyOnHit = false;
        public bool destroyOnHit = true;
        public bool excludeOwner = true;
        public bool requireMobTag = true;
        public float lifetime = -1f; // seconds; <=0 means persistent

        [Header("Modifiers (owner-level)")]
        [Tooltip("If true, ensure owner has DrainMechanic and children report damage to it.")]
        public bool applyDrain = false;

        [Range(0f, 1f)]
        public float drainLifeStealRatio = 0.25f;

        [Header("Debug")]
        public bool debugLogs = false;

        private readonly List<GameObject> _items = new();
        private float _timer;
        private bool _stopped;

        private void OnEnable()
        {
            _timer = 0f;
            EnsureOwnerLevelModifiers();
            GameOverController.OnCountdownFinished += StopSpawning;
            // Ensure any override flags are applied to pre-existing children (e.g., scene-linked)
            ApplyOverridesToChildren();
        }

        private void OnDisable()
        {
            GameOverController.OnCountdownFinished -= StopSpawning;
        }

        private void Update()
        {
            if (_stopped || !useInterval)
                return;
            _timer += Time.deltaTime;
            if (_timer >= Mathf.Max(0.01f, interval))
            {
                _timer = 0f;
                Spawn(countPerInterval);
            }
        }

        public void Spawn(int count = 1)
        {
            if (generator == null)
            {
                if (debugLogs)
                    Debug.LogWarning("[OrbitSpawnBehavior] No generator assigned.", this);
                return;
            }
            count = Mathf.Max(1, count);
            var before = _items.Count;
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("OrbitChild");
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.localPosition = Vector3.zero; // will be placed by Orbit
                go.transform.localScale = Vector3.one * Mathf.Max(0.0001f, childScale);

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

                // Collider + RB
                var cc = go.AddComponent<CircleCollider2D>();
                cc.isTrigger = true;
                cc.radius = 1f; // unit; scale drives visual size
                go.layer = (owner != null ? owner.gameObject.layer : go.layer);
                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

                // Mechanics: Projectile (self speed disabled) + Orbit
                var projSettings = new System.Collections.Generic.List<(string key, object val)>
                {
                    ("excludeOwner", excludeOwner),
                    ("requireMobTag", requireMobTag),
                    ("disableSelfSpeed", true),
                    ("debugLogs", debugLogs),
                };
                if (overrideDamage)
                    projSettings.Add(("damage", damage));
                if (overrideDestroyOnHit)
                    projSettings.Add(("destroyOnHit", destroyOnHit));
                generator.AddMechanicByName(go, "Projectile", projSettings.ToArray());

                generator.AddMechanicByName(
                    go,
                    "Orbit",
                    new (string key, object val)[]
                    {
                        ("radius", Mathf.Max(0f, orbitRadius)),
                        ("angularSpeedDeg", angularSpeedDeg),
                        ("debugLogs", debugLogs),
                    }
                );

                generator.InitializeMechanics(
                    go,
                    owner != null ? owner : transform,
                    generator.target
                );

                // If destroyOnHit is explicitly disabled, do not auto-destroy by lifetime
                if (lifetime > 0f && !(overrideDestroyOnHit && !destroyOnHit))
                {
                    var ad = go.AddComponent<_AutoDestroy>();
                    ad.seconds = lifetime;
                    ad.onDestroyed = () => OnItemDestroyed(go);
                }

                _items.Add(go);
            }

            if (_items.Count != before)
            {
                RedistributeAngles();

                // Re-apply overrides across all children to ensure consistency
                ApplyOverridesToChildren();

                var runner = GetComponent<MechanicRunner>();
                if (runner != null)
                    runner.RegisterTree(transform);
            }
        }

        public void SpawnAt(Vector3 worldPosition)
        {
            // Spawn one, then place it near the desired position before redistribution.
            int before = _items.Count;
            Spawn(1);
            if (_items.Count > before)
            {
                var go = _items[_items.Count - 1];
                go.transform.position = worldPosition;
                // After redistribution, exact position is governed by Orbit; this helps pick initial angle.
                RedistributeAngles(alignToClosestOfLast: true);
            }
        }

        public void Despawn(GameObject item)
        {
            if (item == null)
                return;
            if (_items.Remove(item))
            {
                Destroy(item);
                RedistributeAngles();
            }
        }

        public IReadOnlyList<GameObject> Items => _items;

        private void OnItemDestroyed(GameObject go)
        {
            _items.Remove(go);
            RedistributeAngles();
        }

        private void RedistributeAngles(bool alignToClosestOfLast = false)
        {
            int n = _items.Count;
            if (n <= 0)
                return;
            float baseAngle = 0f;
            // Optionally align the last item's angle to match its current polar location (helps when SpawnAt used)
            if (alignToClosestOfLast && n > 0)
            {
                var last = _items[n - 1].transform;
                Vector3 center = owner != null ? owner.position : transform.position;
                Vector2 v = (Vector2)(last.position - center);
                if (v.sqrMagnitude > 0.0001f)
                    baseAngle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            }
            for (int i = 0; i < n; i++)
            {
                float angle = baseAngle + (360f * i / n);
                var orbit = _items[i].GetComponent<Mechanics.Neuteral.OrbitMechanic>();
                if (orbit != null)
                {
                    orbit.radius = Mathf.Max(0f, orbitRadius);
                    orbit.angularSpeedDeg = angularSpeedDeg;
                    orbit.SetAngleDeg(angle, repositionNow: true);
                }
            }
        }

        private void StopSpawning()
        {
            _stopped = true;
        }

        /// Applies selected override flags (damage, destroyOnHit) to all existing child projectiles.
        public void ApplyOverridesToChildren()
        {
            if ((_items == null) || _items.Count == 0)
                return;
            foreach (var go in _items)
            {
                if (go == null)
                    continue;
                var pm = go.GetComponent<Mechanics.Neuteral.ProjectileMechanic>();
                if (pm == null)
                    continue;
                if (overrideDamage)
                    pm.damage = damage;
                if (overrideDestroyOnHit)
                    pm.destroyOnHit = destroyOnHit;
                // Remove existing lifetime destroy if we are disabling destroyOnHit
                if (overrideDestroyOnHit && !destroyOnHit)
                {
                    var ad = go.GetComponent<_AutoDestroy>();
                    if (ad != null)
                        Destroy(ad);
                }
            }
        }

        private void EnsureOwnerLevelModifiers()
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
            public System.Action onDestroyed;
            private float _t;

            private void Update()
            {
                _t += Time.deltaTime;
                if (_t >= seconds)
                {
                    onDestroyed?.Invoke();
                    Destroy(gameObject);
                }
            }
        }
    }
}
