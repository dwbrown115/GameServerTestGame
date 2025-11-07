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

        // Generic modifier specs to add to each ripple (if any applicable modifiers are desired)
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

                if (runner != null)
                    runner.RegisterTree(go.transform);
            }
        }
    }
}
