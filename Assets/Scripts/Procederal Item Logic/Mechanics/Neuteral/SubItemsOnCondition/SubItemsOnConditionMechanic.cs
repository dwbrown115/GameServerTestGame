using System;
using System.Collections.Generic;
using Game.Procederal;
using Game.Procederal.Api;
using Game.Procederal.Core;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// <summary>
    /// Watches for configured conditions (e.g., mob contact) and spawns new items through the
    /// ProcederalItemGenerator when they occur.
    /// </summary>
    [DisallowMultipleComponent]
    public class SubItemsOnConditionMechanic : MonoBehaviour, IMechanic
    {
        public enum ConditionKind
        {
            None = 0,
            MobContact,
        }

        [Serializable]
        public class Rule
        {
            public ConditionKind condition = ConditionKind.MobContact;
            public string primary = "Projectile";
            public List<string> secondary = new List<string>();
            public int spawnCount = 1;
            public bool spawnOnce = false;
            public float cooldownSeconds = 0f;
            public bool debugLogs = false;
        }

        private struct RuleRuntime
        {
            public float nextAllowedTime;
            public bool hasTriggered;
        }

        [Header("Rules")]
        [SerializeField]
        private List<Rule> _rules = new List<Rule>();

        [Header("Debug")]
        [SerializeField]
        private bool _debugLogs = false;

        private readonly List<RuleRuntime> _runtimeStates = new List<RuleRuntime>();
        private readonly HashSet<Collider2D> _activeMobColliders = new HashSet<Collider2D>();

        private MechanicContext _ctx;
        private ProcederalItemGenerator _generator;

        public bool debugLogs
        {
            get => _debugLogs;
            set => _debugLogs = value;
        }

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            EnsurePayloadRelay();
            ResolveGenerator();
        }

        public void Tick(float dt) { }

        public void SetGenerator(ProcederalItemGenerator generator)
        {
            _generator = generator;
        }

        public void ClearRules()
        {
            _rules.Clear();
            _runtimeStates.Clear();
        }

        public void AddRule(Rule rule)
        {
            if (rule == null)
                return;

            EnsurePayloadRelay();
            ResolveGenerator();
            var clone = CloneRule(rule);
            _rules.Add(clone);
            _runtimeStates.Add(new RuleRuntime { nextAllowedTime = 0f, hasTriggered = false });
        }

        public IReadOnlyList<Rule> Rules => _rules;

        public static Rule CloneRule(Rule rule)
        {
            if (rule == null)
                return null;
            return new Rule
            {
                condition = rule.condition,
                primary = string.IsNullOrWhiteSpace(rule.primary)
                    ? "Projectile"
                    : rule.primary.Trim(),
                secondary =
                    rule.secondary != null ? new List<string>(rule.secondary) : new List<string>(),
                spawnCount = Mathf.Max(1, rule.spawnCount),
                spawnOnce = rule.spawnOnce,
                cooldownSeconds = Mathf.Max(0f, rule.cooldownSeconds),
                debugLogs = rule.debugLogs,
            };
        }

        public static ConditionKind ParseCondition(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return ConditionKind.None;
            switch (value.Trim().ToLowerInvariant())
            {
                case "mob":
                case "mobcontact":
                case "contact":
                    return ConditionKind.MobContact;
                default:
                    return ConditionKind.None;
            }
        }

        public static Rule CreateRuleFromSpec(ItemParams.SpawnItemsOnConditionSpec spec)
        {
            if (spec == null)
                return null;
            var rule = new Rule
            {
                condition = ParseCondition(spec.condition),
                primary = string.IsNullOrWhiteSpace(spec.primary)
                    ? "Projectile"
                    : spec.primary.Trim(),
                spawnCount = Mathf.Max(1, spec.spawnCount),
                spawnOnce = spec.spawnOnce,
                cooldownSeconds = Mathf.Max(0f, spec.cooldownSeconds),
                debugLogs = spec.debugLogs,
            };
            if (spec.secondary != null && spec.secondary.Count > 0)
            {
                for (int i = 0; i < spec.secondary.Count; i++)
                {
                    var entry = spec.secondary[i];
                    if (string.IsNullOrWhiteSpace(entry))
                        continue;
                    rule.secondary.Add(entry.Trim());
                }
            }
            return rule;
        }

        private void EnsurePayloadRelay()
        {
            var relay = GetComponent<PayloadTriggerRelay>();
            if (relay == null)
                relay = gameObject.AddComponent<PayloadTriggerRelay>();
            if (relay != null && _debugLogs)
                relay.debugLogs = true;
        }

        private void OnPayloadTriggerEnter2D(Collider2D other)
        {
            if (other == null)
                return;

            if (_rules == null || _rules.Count == 0)
                return;

            if (ShouldHandleMobContact(other))
            {
                if (_activeMobColliders.Add(other))
                {
                    Vector3 spawnPos = ResolveSpawnPosition(other);
                    ExecuteRules(ConditionKind.MobContact, spawnPos);
                }
            }
        }

        private void OnPayloadTriggerExit2D(Collider2D other)
        {
            if (other == null)
                return;
            _activeMobColliders.Remove(other);
        }

        private bool ShouldHandleMobContact(Collider2D other)
        {
            if (other == null)
                return false;
            foreach (var rule in _rules)
            {
                if (rule != null && rule.condition == ConditionKind.MobContact)
                {
                    if (HasMobTag(other.transform))
                        return true;
                    break;
                }
            }
            return false;
        }

        private void ExecuteRules(ConditionKind condition, Vector3 spawnPos)
        {
            if (_rules == null || _runtimeStates == null)
                return;

            float now = Time.time;
            for (int i = 0; i < _rules.Count; i++)
            {
                var rule = _rules[i];
                if (rule == null || rule.condition != condition)
                    continue;

                if (i >= _runtimeStates.Count)
                    _runtimeStates.Add(new RuleRuntime());

                var state = _runtimeStates[i];
                if (rule.spawnOnce && state.hasTriggered)
                    continue;
                if (state.nextAllowedTime > 0f && now < state.nextAllowedTime)
                    continue;

                bool spawned = SpawnFromRule(rule, spawnPos);
                if (spawned)
                {
                    state.hasTriggered = state.hasTriggered || rule.spawnOnce;
                    state.nextAllowedTime =
                        rule.cooldownSeconds > 0f ? now + rule.cooldownSeconds : 0f;
                    _runtimeStates[i] = state;
                }
            }
        }

        private bool SpawnFromRule(Rule rule, Vector3 spawnPos)
        {
            var generator = ResolveGenerator();
            if (generator == null)
            {
                if (_debugLogs || (rule != null && rule.debugLogs))
                    Debug.LogWarning(
                        "[SubItemsOnCondition] No ProcederalItemGenerator assigned; cannot spawn.",
                        this
                    );
                return false;
            }

            if (rule == null || string.IsNullOrWhiteSpace(rule.primary))
                return false;

            Vector3 spawnAnchor = spawnPos;

            int count = Mathf.Max(1, rule.spawnCount);
            bool anySpawned = false;
            for (int i = 0; i < count; i++)
            {
                var instruction = new ItemInstruction
                {
                    primary = rule.primary,
                    secondary =
                        rule.secondary != null
                            ? new List<string>(rule.secondary)
                            : new List<string>(),
                    isolateSecondarySettings = true,
                };
                var parms = new ItemParams { debugLogs = rule.debugLogs || _debugLogs };

                var spawned = generator.Create(instruction, parms, transform);
                if (spawned == null)
                    continue;

                if (spawned.transform.parent == transform)
                    spawned.transform.localPosition = Vector3.zero;
                spawned.transform.position = spawnAnchor;
                if (spawned.transform.parent == transform)
                    spawned.transform.SetParent(null, worldPositionStays: true);
                var intervalSpawner =
                    spawned.GetComponent<Game.Procederal.Api.GenericIntervalSpawner>();
                if (intervalSpawner != null)
                {
                    intervalSpawner.owner = spawned.transform;
                    intervalSpawner.parentSpawnedToSpawner = false;
                }

                anySpawned = true;
            }

            if ((_debugLogs || (rule != null && rule.debugLogs)) && anySpawned)
            {
                Debug.Log(
                    $"[SubItemsOnCondition] Spawned {count} item(s) via rule (primary={rule.primary}).",
                    this
                );
            }

            return anySpawned;
        }

        private Vector3 ResolveSpawnPosition(Collider2D other)
        {
            Vector3 fallback = transform.position;
            if (_ctx != null)
            {
                if (_ctx.Payload != null)
                    fallback = _ctx.Payload.position;
                else if (_ctx.Owner != null)
                    fallback = _ctx.Owner.position;
            }

            if (other == null)
                return fallback;

            var boundsPoint = other.bounds.ClosestPoint(fallback);
            return new Vector3(boundsPoint.x, boundsPoint.y, fallback.z);
        }

        private static bool HasMobTag(Transform t)
        {
            while (t != null)
            {
                if (t.CompareTag("Mob"))
                    return true;
                t = t.parent;
            }
            return false;
        }

        private ProcederalItemGenerator ResolveGenerator()
        {
            if (_generator != null)
                return _generator;

            if (_ctx != null)
            {
                if (_ctx.Owner != null)
                {
                    _generator = _ctx.Owner.GetComponentInParent<ProcederalItemGenerator>();
                    if (_generator != null)
                        return _generator;
                }

                if (_ctx.Payload != null)
                {
                    _generator = _ctx.Payload.GetComponentInParent<ProcederalItemGenerator>();
                    if (_generator != null)
                        return _generator;
                }
            }

            _generator = GetComponentInParent<ProcederalItemGenerator>();
            return _generator;
        }
    }
}
