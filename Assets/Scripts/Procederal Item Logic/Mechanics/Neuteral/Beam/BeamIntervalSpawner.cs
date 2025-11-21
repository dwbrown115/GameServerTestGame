using System.Collections.Generic;
using Game.Procederal;
using Game.Procederal.Core;
using UnityEngine;

namespace Game.Procederal.Api
{
    /// Spawns Beam mechanics periodically using a resolver for spawn position and direction.
    [DisallowMultipleComponent]
    public class BeamIntervalSpawner : MonoBehaviour, IModifierReceiver, IModifierOwnerProvider
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

        [Header("Debug")]
        public bool debugLogs = false;

        [Tooltip("Parent spawned beams to this spawner. Disable to detach into world space.")]
        public bool parentSpawnedToSpawner = true;

        // Settings to apply to each spawned Beam
        private readonly List<(string key, object val)> _beamSettings = new();

        // Generic modifier specs to add to each spawned beam
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
            EnsureResolver();
        }

        private void OnDisable()
        {
            GameOverController.OnCountdownFinished -= StopSpawning;
        }

        public void SetBeamSettings(params (string key, object val)[] settings)
        {
            _beamSettings.Clear();
            if (settings == null)
                return;
            _beamSettings.AddRange(settings);
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
                Debug.Log("[BeamIntervalSpawner] Stopped by game over.", this);
        }

        private void SpawnBurst()
        {
            if (generator == null)
            {
                if (debugLogs)
                    Debug.LogWarning("[BeamIntervalSpawner] No generator assigned.", this);
                return;
            }
            Transform center = owner != null ? owner : transform;

            int spawnCount = Mathf.Max(1, countPerInterval);
            var runner = GetComponent<MechanicRunner>();
            for (int i = 0; i < spawnCount; i++)
            {
                // Compute spawn pos + suggested direction
                Vector3 pos = center.position;
                Vector2 dir = Vector2.right;
                bool got =
                    (_resolver != null)
                    && _resolver.TryGetSpawn(center, Mathf.Max(0f, spawnRadius), out pos, out dir);
                if (!got)
                {
                    // Fallback to chaos resolver
                    var chaos = GetComponent<ChaosSpawnPosition>();
                    if (chaos == null)
                        chaos = gameObject.AddComponent<ChaosSpawnPosition>();
                    got = chaos.TryGetSpawn(center, Mathf.Max(0f, spawnRadius), out pos, out dir);
                }
                if (!got)
                {
                    // Final random fallback
                    dir = Random.insideUnitCircle.normalized;
                    if (dir.sqrMagnitude < 0.001f)
                        dir = Vector2.right;
                    pos = center.position + (Vector3)(dir * Mathf.Max(0f, spawnRadius));
                }

                var go = new GameObject("Beam_Spawned");
                if (parentSpawnedToSpawner)
                {
                    go.transform.SetParent(transform, worldPositionStays: true);
                }
                else
                {
                    Game.Procederal.ProcederalItemGenerator.DetachToWorld(
                        go,
                        worldPositionStays: true
                    );
                }
                go.transform.position = pos;
                go.transform.localScale = Vector3.one;
                go.layer = (owner != null ? owner.gameObject.layer : go.layer);

                // Build settings for this beam instance; start with provided ones
                var settings = new List<(string key, object val)>(_beamSettings);

                // If we have a spawn direction, pass it as degrees string (Beam parses degrees or tokens)
                float deg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                settings.Add(
                    ("direction", deg.ToString(System.Globalization.CultureInfo.InvariantCulture))
                );

                // Ensure common filters unless already specified
                bool hasExclude = settings.Exists(kv =>
                    string.Equals(kv.key, "excludeOwner", System.StringComparison.OrdinalIgnoreCase)
                );
                bool hasRequire = settings.Exists(kv =>
                    string.Equals(
                        kv.key,
                        "requireMobTag",
                        System.StringComparison.OrdinalIgnoreCase
                    )
                );
                bool hasViz = settings.Exists(kv =>
                    string.Equals(
                        kv.key,
                        "showVisualization",
                        System.StringComparison.OrdinalIgnoreCase
                    )
                );
                if (!hasExclude)
                    settings.Add(("excludeOwner", true));
                if (!hasRequire)
                    settings.Add(("requireMobTag", true));
                if (!hasViz)
                    settings.Add(("showVisualization", true));

                generator.AddMechanicByName(go, "Beam", settings.ToArray());
                // Apply modifier specs (e.g., DoT, Lock) to the beam payload
                if (_modifierSpecs.Count > 0)
                {
                    foreach (var spec in _modifierSpecs)
                    {
                        generator.AddMechanicByName(go, spec.mechanicName, spec.settings);
                    }
                }
                generator.InitializeMechanics(go, owner, generator.ResolveTargetOrDefault());

                // Register with runner for ticking
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
                        "[BeamIntervalSpawner] Assigned SpawnResolverBehaviour does not implement ISpawnPositionResolver.",
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
    }
}
