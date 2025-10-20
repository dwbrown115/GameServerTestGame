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
            Transform center = owner != null ? owner : transform;
            for (int i = 0; i < spawnCount; i++)
            {
                var go = new GameObject("Ripple_Spawned");
                go.transform.SetParent(transform, worldPositionStays: true);
                go.transform.position = center.position;
                go.transform.localScale = Vector3.one;
                go.layer = (owner != null ? owner.gameObject.layer : go.layer);

                generator.AddMechanicByName(
                    go,
                    "Ripple",
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

                var runner = GetComponent<MechanicRunner>();
                if (runner != null)
                    runner.RegisterTree(transform);
            }
        }
    }
}
