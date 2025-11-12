using System.Collections.Generic;
using Game.Procederal;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Api
{
    [DisallowMultipleComponent]
    public class RippleIntervalSpawner : MonoBehaviour, IModifierReceiver, IModifierOwnerProvider
    {
        [Header("Wiring")]
        public ProcederalItemGenerator generator;
        public Transform owner;

        [Header("Spawn Settings")]
        public float interval = 0.5f;
        public int countPerInterval = 1;

        [Header("Ripple Settings")]
        public float startRadius = 1f;
        public float endDiameter = 8f;
        public float growDuration = 1.5f;
        public float edgeThickness = 0.2f;
        public int damage = 5;
        public bool excludeOwner = true;
        public bool requireMobTag = true;
        public bool showVisualization = true;
        public Color vizColor = new Color(0.3f, 0.7f, 1f, 0.6f);
        public bool debugLogs = false;

        [Tooltip("Parent spawned ripples to this spawner. Disable to detach into world space.")]
        public bool parentSpawnedToSpawner = true;

        [Header("Lifecycle")]
        [Tooltip("Maximum number of burst cycles to emit before auto-destroying (0 = infinite).")]
        public int maxBursts = 0;

        [Tooltip("Automatically destroy the spawner after this many seconds (<= 0 disables).")]
        public float selfDestructAfterSeconds = -1f;

        [Tooltip("Allow the spawner to destroy itself after finishing its bursts.")]
        public bool enableSelfDestruct = true;

        [Tooltip("When true, ignore maxBursts and continue spawning indefinitely.")]
        public bool ignoreMaxBurstLimit = false;

        [Tooltip(
            "Used when no explicit self-destruct timer is supplied. Lets you control cleanup separately from grow duration."
        )]
        public float fallbackSelfDestructSeconds = 0.5f;

        // Generic modifier specs to add to each ripple (if any applicable modifiers are desired)
        private readonly List<(
            string mechanicName,
            (string key, object val)[] settings
        )> _modifierSpecs = new();
        private readonly List<GameObject> _spawnedInstances = new();

        private float _timer;
        private bool _stopped;
        private int _burstCount;
        private float _lifeTimer;
        private bool _destroyed;
        private bool _selfDestructScheduled;

        private void OnEnable()
        {
            _timer = 0f;
            _lifeTimer = 0f;
            _burstCount = 0;
            _destroyed = false;
            _stopped = false;
            _selfDestructScheduled = false;
            _spawnedInstances.Clear();
            if (debugLogs)
            {
                string burstsText = maxBursts > 0 ? maxBursts.ToString() : "∞";
                Debug.Log(
                    $"[RippleIntervalSpawner] Enabled. interval={interval:F2}s countPerInterval={countPerInterval} maxBursts={burstsText} ignoreMaxBurstLimit={ignoreMaxBurstLimit} enableSelfDestruct={enableSelfDestruct} selfDestructAfterSeconds={selfDestructAfterSeconds} fallback={fallbackSelfDestructSeconds:F2}",
                    this
                );
            }
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

        private void Update()
        {
            if (_destroyed)
                return;

            if (!_stopped)
            {
                _timer += Time.deltaTime;
                if (_timer >= Mathf.Max(0.01f, interval))
                {
                    _timer = 0f;
                    SpawnBurst();
                }
            }

            if (!enableSelfDestruct || selfDestructAfterSeconds <= 0f)
            {
                _lifeTimer = 0f;
                _selfDestructScheduled = false;
                return;
            }

            bool hasActive = HasActiveSpawnedInstances();
            if (hasActive)
            {
                _lifeTimer += Time.deltaTime;
                if (!_selfDestructScheduled && _lifeTimer >= selfDestructAfterSeconds)
                {
                    _selfDestructScheduled = true;
                    if (debugLogs)
                    {
                        Debug.Log(
                            "[RippleIntervalSpawner] Self-destruct threshold reached; waiting for active ripples to finish.",
                            this
                        );
                    }
                }
            }

            if (_selfDestructScheduled && !hasActive)
            {
                if (debugLogs)
                {
                    Debug.Log(
                        "[RippleIntervalSpawner] Self-destruct completing; releasing spawner.",
                        this
                    );
                }

                DestroySpawner();
            }
        }

        private void StopSpawning()
        {
            _stopped = true;
            if (debugLogs)
                Debug.Log("[RippleIntervalSpawner] Stopped by game over.", this);
        }

        private void SpawnBurst()
        {
            if (generator == null)
            {
                if (debugLogs)
                    Debug.LogWarning("[RippleIntervalSpawner] No generator assigned.", this);
                return;
            }
            int spawnCount = Mathf.Max(1, countPerInterval);
            Vector3 spawnPos = transform.position;
            var layerSource = owner != null ? owner : transform;
            var runner = GetComponent<MechanicRunner>();
            if (runner == null)
            {
                runner = gameObject.AddComponent<MechanicRunner>();
                runner.debugLogs = debugLogs;
                runner.RegisterTree(transform);
            }
            for (int i = 0; i < spawnCount; i++)
            {
                var go = new GameObject("RipplePrimary_Spawned");
                if (parentSpawnedToSpawner)
                {
                    go.transform.SetParent(transform, worldPositionStays: false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    go.transform.localScale = Vector3.one;
                }
                else
                {
                    go.transform.SetParent(null, worldPositionStays: false);
                    go.transform.position = spawnPos;
                    go.transform.localScale = Vector3.one;
                }

                if (parentSpawnedToSpawner)
                    go.transform.position = transform.position;
                else
                    go.transform.rotation = transform.rotation;

                if (layerSource != null)
                    go.layer = layerSource.gameObject.layer;
                else
                    go.layer = gameObject.layer;

                generator.AddMechanicByName(
                    go,
                    "RipplePrimary",
                    new (string key, object val)[]
                    {
                        ("startRadius", startRadius),
                        ("endDiameter", endDiameter),
                        ("growDuration", growDuration),
                        ("edgeThickness", edgeThickness),
                        ("damage", damage),
                        ("excludeOwner", excludeOwner),
                        ("requireMobTag", requireMobTag),
                        ("showVisualization", showVisualization),
                        ("vizColor", vizColor),
                        ("debugLogs", debugLogs),
                    }
                );
                // Apply any modifier specs to the ripple, if desired later
                foreach (var spec in _modifierSpecs)
                {
                    generator.AddMechanicByName(go, spec.mechanicName, spec.settings);
                }
                generator.InitializeMechanics(go, owner, generator.target);
                TrackSpawnedInstance(go);

                if (runner != null)
                    runner.RegisterTree(go.transform);
            }

            _burstCount++;
            if (debugLogs)
            {
                var totalText = maxBursts > 0 ? maxBursts.ToString() : "∞";
                Debug.Log(
                    $"[RippleIntervalSpawner] Spawned burst {_burstCount}/{totalText}.",
                    this
                );
            }

            if (ignoreMaxBurstLimit && maxBursts > 0 && _burstCount == maxBursts && debugLogs)
            {
                Debug.Log(
                    "[RippleIntervalSpawner] Max burst limit reached but ignoreMaxBurstLimit is true; continuing to spawn.",
                    this
                );
            }

            if (!ignoreMaxBurstLimit && maxBursts > 0 && _burstCount >= maxBursts)
            {
                _stopped = true;
                if (!enableSelfDestruct)
                {
                    selfDestructAfterSeconds = -1f;
                    if (debugLogs)
                    {
                        Debug.Log(
                            "[RippleIntervalSpawner] Max bursts reached; self-destruct disabled.",
                            this
                        );
                    }
                    return;
                }

                if (selfDestructAfterSeconds <= 0f)
                {
                    float fallback =
                        fallbackSelfDestructSeconds > 0f
                            ? fallbackSelfDestructSeconds
                            : growDuration;
                    selfDestructAfterSeconds = Mathf.Max(fallback, 0.1f);
                    _lifeTimer = 0f;
                    if (debugLogs)
                    {
                        Debug.Log(
                            $"[RippleIntervalSpawner] Max bursts reached; scheduling fallback self-destruct in {selfDestructAfterSeconds:F2}s.",
                            this
                        );
                    }
                }
            }
        }

        private void DestroySpawner()
        {
            if (_destroyed)
                return;

            _destroyed = true;
            _stopped = true;
            _selfDestructScheduled = false;
            _spawnedInstances.Clear();

            if (generator != null)
                generator.ReleaseTree(gameObject);
            else
                MechanicLifecycleUtility.Release(gameObject);
        }

        private void TrackSpawnedInstance(GameObject go)
        {
            if (go == null)
                return;
            _spawnedInstances.Add(go);
        }

        private bool HasActiveSpawnedInstances()
        {
            bool hasActive = false;
            for (int i = _spawnedInstances.Count - 1; i >= 0; i--)
            {
                var go = _spawnedInstances[i];
                if (go == null)
                {
                    _spawnedInstances.RemoveAt(i);
                    continue;
                }

                if (!go.activeInHierarchy)
                    continue;

                hasActive = true;
            }

            return hasActive;
        }
    }
}
