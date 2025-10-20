using System.Collections.Generic;
using Game.Procederal;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Periodically spawns a series of Sword Slash crescents that travel forward.
    /// Uses a static crescent payload plus a Projectile mechanic for motion and on-hit.
    [DisallowMultipleComponent]
    public class SwordSlashIntervalSpawner
        : MonoBehaviour,
            IModifierReceiver,
            IModifierOwnerProvider,
            IAimAtNearestEnemyToggle
    {
        [Header("Wiring")]
        public ProcederalItemGenerator generator;
        public Transform owner;

        [Header("Spawn Settings")]
        public float interval = 0.8f; // time between series bursts
        public int seriesCount = 3; // number of slashes per burst
        public float intervalBetween = 0.08f; // temporal spacing converted to spatial via speed

        [Header("Crescent Geometry")]
        public float outerRadius = 1.5f;
        public float width = 0.5f;
        public float arcLengthDeg = 120f;
        public bool edgeOnly = true;
        public float edgeThickness = 0.2f;
        public Color vizColor = Color.white;

        [Header("Projectile Settings")]
        public int damage = 8;
        public float speed = 12f;
        public bool excludeOwner = true;
        public bool requireMobTag = true;

        [Header("Behavior")]
        [Tooltip(
            "When true, each burst aims toward the nearest enemy at spawn time; otherwise uses owner velocity."
        )]
        public bool aimAtNearestEnemy = false;

        [Header("Debug")]
        public bool debugLogs = false;

        // Runtime modifier specs to apply to each spawned slash
        private readonly List<(
            string mechanicName,
            (string key, object val)[] settings
        )> _modifierSpecs = new();

        private float _timer;
        private bool _stopped;

        private void OnEnable()
        {
            _timer = 0f;
            GameOverController.OnCountdownFinished += StopSpawning;
        }

        private void OnDisable()
        {
            GameOverController.OnCountdownFinished -= StopSpawning;
        }

        public void AddModifierSpec(string mechanicName, params (string key, object val)[] settings)
        {
            if (string.IsNullOrWhiteSpace(mechanicName))
                return;
            _modifierSpecs.Add((mechanicName, settings));
        }

        public Transform ModifierOwner => owner != null ? owner : transform;

        public bool AimAtNearestEnemy
        {
            get => aimAtNearestEnemy;
            set => aimAtNearestEnemy = value;
        }

        private void StopSpawning()
        {
            _stopped = true;
            if (debugLogs)
                Debug.Log("[SwordSlashIntervalSpawner] Stopped by game over.", this);
        }

        private void Update()
        {
            if (_stopped)
                return;
            _timer += Time.deltaTime;
            if (_timer >= Mathf.Max(0.01f, interval))
            {
                _timer = 0f;
                SpawnSeriesBurst();
            }
        }

        private void SpawnSeriesBurst()
        {
            if (generator == null)
            {
                if (debugLogs)
                    Debug.LogWarning("[SwordSlashIntervalSpawner] No generator assigned.", this);
                return;
            }
            Transform ownerT = owner != null ? owner : transform;

            // Determine direction: owner velocity by default
            Vector2 dir = Vector2.right;
            var ownerRb = ownerT != null ? ownerT.GetComponent<Rigidbody2D>() : null;
            if (ownerRb != null && ownerRb.linearVelocity.sqrMagnitude > 0.01f)
                dir = ownerRb.linearVelocity.normalized;

            // If configured, aim at nearest Mob
            if (aimAtNearestEnemy && ownerT != null)
            {
                dir = TargetingServiceLocator.Service.ResolveDirectionToNearestMob(
                    ownerT,
                    dir,
                    filter: t => t != ownerT
                );
            }

            int count = Mathf.Max(1, seriesCount);
            float spacing = Mathf.Max(0f, intervalBetween) * Mathf.Max(0.01f, speed);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = ownerT.position - (Vector3)(dir * spacing * i);
                SpawnOneSlash(pos, dir);
            }

            // Register with runner for ticking
            var runner = GetComponent<MechanicRunner>();
            if (runner != null)
                runner.RegisterTree(transform);
        }

        private void SpawnOneSlash(Vector3 pos, Vector2 dir)
        {
            var go = new GameObject("SwordSlash_Spawned");
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one;
            go.layer = (owner != null ? owner.gameObject.layer : go.layer);

            // Payload geometry (crescent)
            var payload = go.AddComponent<Mechanics.Neuteral.SwordSlashPayload>();
            payload.outerRadius = outerRadius;
            payload.width = width;
            payload.arcLengthDeg = arcLengthDeg;
            payload.edgeOnly = edgeOnly;
            payload.edgeThickness = edgeThickness;
            payload.showVisualization = true;
            payload.vizColor = vizColor;

            // Face travel direction (local +X) toward dir
            go.transform.right = dir;

            // Physics body for reliable triggers
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            // Kick off movement immediately to avoid any initial idle frame before Tick runs
            rb.linearVelocity = dir * Mathf.Max(0.01f, speed);
            // Kick off movement immediately (before first Tick) to avoid a stationary frame
            rb.linearVelocity = dir * speed;
            // Kick off movement immediately to avoid an initial idle frame before tick/physics
            rb.linearVelocity = dir * speed;

            // Primary projectile mechanic (for on-hit + damage). Movement is driven by ChildMovementMechanic.
            var primarySettings = new List<(string key, object val)>
            {
                ("direction", (Vector2)dir),
                ("speed", speed),
                ("damage", damage),
                ("requireMobTag", requireMobTag),
                ("excludeOwner", excludeOwner),
                ("destroyOnHit", true),
                ("disableSelfSpeed", true), // let ChildMovementMechanic control velocity
                ("debugLogs", debugLogs),
            };
            generator.AddMechanicByName(go, "Projectile", primarySettings.ToArray());

            // Unified child movement controller (ensures immediate velocity on spawn)
            generator.AddMechanicByName(
                go,
                "ChildMovementMechanic",
                new (string key, object val)[]
                {
                    ("direction", (Vector2)dir),
                    ("speed", speed),
                    ("disableSelfSpeed", false),
                    ("debugLogs", debugLogs),
                }
            );

            // Apply configured modifier specs (e.g., RippleOnHit) to each child
            if (_modifierSpecs.Count > 0)
            {
                foreach (var spec in _modifierSpecs)
                {
                    // Skip Track here; aiming is handled by aimAtNearestEnemy
                    if (
                        string.Equals(
                            spec.mechanicName,
                            "Track",
                            System.StringComparison.OrdinalIgnoreCase
                        )
                    )
                        continue;
                    generator.AddMechanicByName(go, spec.mechanicName, spec.settings);
                }
            }

            // Initialize mechanics
            generator.InitializeMechanics(go, owner, generator.target);

            // Safety lifetime cleanup
            var auto = go.AddComponent<_AutoDestroyAfterSeconds>();
            auto.seconds = 4f;
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
    }
}
