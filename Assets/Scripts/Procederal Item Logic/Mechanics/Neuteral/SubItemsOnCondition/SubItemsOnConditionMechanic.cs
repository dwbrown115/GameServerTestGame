using System;
using System.Collections.Generic;
using Game.Procederal;
using Game.Procederal.Api;
using Game.Procederal.Core;
using Mechanics;
using UnityEngine;

namespace Mechanics.Neuteral
{
    /// <summary>
    /// Watches for configured conditions (e.g., mob contact) and spawns new items through the
    /// ProcederalItemGenerator when they occur.
    /// </summary>
    [DisallowMultipleComponent]
    public class SubItemsOnConditionMechanic : MonoBehaviour, IMechanic, IPrimaryHitModifier
    {
        public enum ConditionKind
        {
            None = 0,
            MobContact,
            OnDamage,
        }

        public enum TargetKind
        {
            Mob = 0,
            Player,
            Any,
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
            public TargetKind target = TargetKind.Mob;
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

        [SerializeField]
        private bool _logRuleEvaluations = false;

        [SerializeField]
        private bool _logRuleSkips = false;

        private readonly List<RuleRuntime> _runtimeStates = new List<RuleRuntime>();
        private readonly HashSet<Collider2D> _activeMobColliders = new HashSet<Collider2D>();

        private MechanicContext _ctx;
        private ProcederalItemGenerator _generator;

        private void OnEnable()
        {
            ResetRuntimeStates();
        }

        private void OnDisable()
        {
            _activeMobColliders.Clear();
        }

        public bool debugLogs
        {
            get => _debugLogs;
            set => _debugLogs = value;
        }

        public bool logRuleEvaluations
        {
            get => _logRuleEvaluations;
            set => _logRuleEvaluations = value;
        }

        public bool logRuleSkips
        {
            get => _logRuleSkips;
            set => _logRuleSkips = value;
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
            ResetRuntimeStates();
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
                target = rule.target,
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
                case "damage":
                case "ondamage":
                case "primaryhit":
                case "hit":
                    return ConditionKind.OnDamage;
                default:
                    return ConditionKind.None;
            }
        }

        public static TargetKind ParseTarget(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return TargetKind.Mob;
            switch (value.Trim().ToLowerInvariant())
            {
                case "player":
                case "hero":
                case "owner":
                    return TargetKind.Player;
                case "any":
                case "all":
                    return TargetKind.Any;
                case "mob":
                case "enemy":
                default:
                    return TargetKind.Mob;
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
                target = ParseTarget(spec.target),
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
                    ExecuteRules(ConditionKind.MobContact, spawnPos, other.transform);
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
                    if (MatchesTarget(rule.target, other.transform))
                        return true;
                    break;
                }
            }
            return false;
        }

        private void ExecuteRules(
            ConditionKind condition,
            Vector3 spawnPos,
            Transform spawnTarget = null
        )
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
                LogRuleEvaluation(
                    i,
                    rule,
                    $"Evaluating condition {condition} at time {now:F2} (spawnOnce={rule.spawnOnce}, cooldown={rule.cooldownSeconds:F2}s)."
                );
                if (rule.spawnOnce && state.hasTriggered)
                {
                    LogRuleSkip(i, rule, "spawnOnce already triggered; skipping.");
                    continue;
                }
                if (state.nextAllowedTime > 0f && now < state.nextAllowedTime)
                {
                    LogRuleSkip(
                        i,
                        rule,
                        $"cooldown active (nextAllowed={state.nextAllowedTime:F2}, now={now:F2})."
                    );
                    continue;
                }

                bool spawned = SpawnFromRule(rule, spawnPos, spawnTarget, i);
                if (spawned)
                {
                    state.hasTriggered = state.hasTriggered || rule.spawnOnce;
                    state.nextAllowedTime =
                        rule.cooldownSeconds > 0f ? now + rule.cooldownSeconds : 0f;
                    _runtimeStates[i] = state;
                    if (rule.cooldownSeconds > 0f)
                    {
                        LogRuleEvaluation(
                            i,
                            rule,
                            $"Cooldown set. nextAllowed={state.nextAllowedTime:F2}."
                        );
                    }
                    if (rule.spawnOnce)
                    {
                        LogRuleEvaluation(i, rule, "Marked as triggered (spawnOnce).");
                    }
                }
            }
        }

        private bool SpawnFromRule(
            Rule rule,
            Vector3 spawnPos,
            Transform spawnTarget,
            int ruleIndex
        )
        {
            if (rule == null)
                return false;

            if (!MatchesTarget(rule.target, spawnTarget))
            {
                LogRuleSkip(
                    ruleIndex,
                    rule,
                    $"target mismatch (expected {rule.target}, actual={FormatTarget(spawnTarget)})."
                );
                return false;
            }

            var generator = ResolveGenerator();
            if (generator == null)
            {
                LogRuleSkip(ruleIndex, rule, "No ProcederalItemGenerator assigned.");
                if (_debugLogs || rule.debugLogs)
                {
                    Debug.LogWarning(
                        "[SubItemsOnCondition] No ProcederalItemGenerator assigned; cannot spawn.",
                        this
                    );
                }
                return false;
            }

            if (string.IsNullOrWhiteSpace(rule.primary))
            {
                LogRuleSkip(ruleIndex, rule, "Primary mechanic name missing.");
                return false;
            }

            Vector3 spawnAnchor = spawnPos;
            if (spawnTarget != null)
            {
                var targetPos = spawnTarget.position;
                spawnAnchor.x = targetPos.x;
                spawnAnchor.y = targetPos.y;
            }

            int attempts = Mathf.Max(1, rule.spawnCount);
            int spawnedCount = 0;
            for (int i = 0; i < attempts; i++)
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

                var spawned = generator.Create(instruction, parms, transform, spawnAnchor);
                if (spawned == null)
                {
                    LogRuleSkip(ruleIndex, rule, "Generator returned null instance.");
                    continue;
                }

                if (spawned.transform.parent == transform)
                    spawned.transform.SetParent(null, worldPositionStays: true);

                spawned.transform.position = spawnAnchor;
                var intervalSpawner =
                    spawned.GetComponent<Game.Procederal.Api.GenericIntervalSpawner>();
                if (intervalSpawner != null)
                {
                    intervalSpawner.owner = spawned.transform;
                    intervalSpawner.parentSpawnedToSpawner = false;
                }

                spawnedCount++;
            }

            if (spawnedCount > 0)
            {
                LogRuleEvaluation(
                    ruleIndex,
                    rule,
                    $"Spawned {spawnedCount} item(s) at {spawnAnchor}."
                );
                return true;
            }

            LogRuleSkip(ruleIndex, rule, "Generator produced no payloads.");
            return false;
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

        private void LogRuleEvaluation(int ruleIndex, Rule rule, string message)
        {
            if (!ShouldLogRuleEvaluation(rule))
                return;
            Debug.Log(
                $"[SubItemsOnCondition] Rule#{ruleIndex} ({rule?.primary ?? "<null>"}): {message}",
                this
            );
        }

        private void LogRuleSkip(int ruleIndex, Rule rule, string reason)
        {
            if (!ShouldLogRuleSkip(rule))
                return;
            Debug.Log(
                $"[SubItemsOnCondition] Rule#{ruleIndex} ({rule?.primary ?? "<null>"}) skipped: {reason}",
                this
            );
        }

        private bool ShouldLogRuleEvaluation(Rule rule)
        {
            return _logRuleEvaluations || _debugLogs || (rule != null && rule.debugLogs);
        }

        private bool ShouldLogRuleSkip(Rule rule)
        {
            return _logRuleSkips || _debugLogs || (rule != null && rule.debugLogs);
        }

        private static string FormatTarget(Transform t)
        {
            return t != null ? t.name : "<null>";
        }

        public void OnPrimaryHit(in PrimaryHitInfo info)
        {
            if (!HasRuleFor(ConditionKind.OnDamage))
                return;

            Vector3 spawnPos = transform.position;
            Transform target = info.target;
            bool hasTarget = target != null;

            if (hasTarget)
            {
                spawnPos = target.position;
            }
            else
            {
                var hitPoint = info.hitPoint;
                if (!float.IsNaN(hitPoint.x) && !float.IsNaN(hitPoint.y))
                {
                    spawnPos = new Vector3(hitPoint.x, hitPoint.y, spawnPos.z);
                }
            }

            ExecuteRules(ConditionKind.OnDamage, spawnPos, target);
        }

        private bool HasRuleFor(ConditionKind kind)
        {
            if (_rules == null || _rules.Count == 0)
                return false;
            for (int i = 0; i < _rules.Count; i++)
            {
                var rule = _rules[i];
                if (rule != null && rule.condition == kind)
                    return true;
            }
            return false;
        }

        private void ResetRuntimeStates()
        {
            _runtimeStates.Clear();
            _activeMobColliders.Clear();
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

        private static bool HasPlayerTag(Transform t)
        {
            while (t != null)
            {
                if (t.CompareTag("Player"))
                    return true;
                t = t.parent;
            }
            return false;
        }

        private static bool MatchesTarget(TargetKind kind, Transform candidate)
        {
            switch (kind)
            {
                case TargetKind.Any:
                    return true;
                case TargetKind.Player:
                    return candidate != null && HasPlayerTag(candidate);
                case TargetKind.Mob:
                default:
                    return candidate != null && HasMobTag(candidate);
            }
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
