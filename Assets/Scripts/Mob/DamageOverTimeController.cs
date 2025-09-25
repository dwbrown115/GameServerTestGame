using System.Collections.Generic;
using UnityEngine;

namespace Mob
{
    /// Component that manages stacking damage-over-time effects on a mob.
    [DisallowMultipleComponent]
    public class DamageOverTimeController : MonoBehaviour
    {
        private class Stack
        {
            public string effectId;
            public int damagePerTick;
            public float tickInterval;
            public float remaining;
            public float timeToNextTick;
            public Mechanics.Corruption.DrainMechanic drainOwner; // optional
            public GameObject vizNode; // optional visual node under Debuffs
        }

        [Header("Debug")]
        public bool debugLogs = true; // default on for troubleshooting

        private readonly List<Stack> _stacks = new();
        private IDamageable _damageable;
        private Transform _debuffsRoot;

        private void Awake()
        {
            _damageable = GetComponentInParent<IDamageable>() ?? GetComponent<IDamageable>();
            EnsureDebuffsRoot();
            if (debugLogs)
            {
                var dmgGo = (_damageable as Component)?.gameObject;
                Debug.Log(
                    $"[DoT][Awake] On '{gameObject.name}'. Damageable={(dmgGo != null ? dmgGo.name : "null")} DebuffsRoot={(_debuffsRoot != null ? _debuffsRoot.name + " (parent=" + _debuffsRoot.parent?.name + ")" : "null")}"
                );
            }
        }

        private void Update()
        {
            if (_damageable == null || !_damageable.IsAlive)
            {
                _stacks.Clear();
                enabled = false;
                return;
            }
            float dt = Time.deltaTime;
            for (int i = _stacks.Count - 1; i >= 0; i--)
            {
                var s = _stacks[i];
                s.remaining -= dt;
                s.timeToNextTick -= dt;
                if (s.timeToNextTick <= 0f)
                {
                    s.timeToNextTick += Mathf.Max(0.01f, s.tickInterval);
                    if (s.damagePerTick > 0)
                    {
                        Vector2 hitPoint = (Vector2)transform.position;
                        _damageable.TakeDamage(s.damagePerTick, hitPoint, Vector2.zero);
                        if (s.drainOwner != null)
                        {
                            s.drainOwner.ReportDamage(s.damagePerTick);
                        }
                        if (debugLogs)
                            Debug.Log(
                                $"[DoT] Tick {s.effectId} -> {gameObject.name} for {s.damagePerTick}"
                            );
                    }
                }
                if (s.remaining <= 0f)
                {
                    if (s.vizNode != null)
                    {
                        Destroy(s.vizNode);
                    }
                    if (debugLogs)
                        Debug.Log($"[DoT] Expired stack {s.effectId} on {gameObject.name}");
                    _stacks.RemoveAt(i);
                }
            }
            if (_stacks.Count == 0)
            {
                enabled = false;
                if (debugLogs)
                    Debug.Log($"[DoT] No stacks left on {gameObject.name}; controller disabled");
            }
        }

        /// Apply a DoT stack or refresh existing one based on allowStacking.
        public bool ApplyStack(
            int damagePerTick,
            float tickInterval,
            float duration,
            string effectId,
            bool allowStacking,
            Mechanics.Corruption.DrainMechanic drainOwner
        )
        {
            if (damagePerTick <= 0 || tickInterval <= 0f || duration <= 0f)
                return false;

            if (!allowStacking)
            {
                // Refresh or replace single stack for this effectId
                for (int i = 0; i < _stacks.Count; i++)
                {
                    if (_stacks[i].effectId == effectId)
                    {
                        _stacks[i].damagePerTick = damagePerTick;
                        _stacks[i].tickInterval = tickInterval;
                        _stacks[i].remaining = duration;
                        _stacks[i].timeToNextTick = tickInterval; // reset cadence
                        _stacks[i].drainOwner = drainOwner;
                        enabled = true;
                        if (debugLogs)
                            Debug.Log(
                                $"[DoT] Refreshed stack id={effectId} on {gameObject.name} (dmg/tick={damagePerTick}, interval={tickInterval}, duration={duration})"
                            );
                        return true;
                    }
                }
            }

            // Add new stack
            var stack = new Stack
            {
                effectId = effectId,
                damagePerTick = damagePerTick,
                tickInterval = tickInterval,
                remaining = duration,
                timeToNextTick = tickInterval,
                drainOwner = drainOwner,
            };
            // Create a child under Debuffs to represent this stack (can be extended for icons/VFX later)
            EnsureDebuffsRoot();
            if (_debuffsRoot != null)
            {
                var node = new GameObject($"DoT_{effectId}");
                node.transform.SetParent(_debuffsRoot, false);
                node.transform.localPosition = Vector3.zero;
                stack.vizNode = node;
                if (debugLogs)
                    Debug.Log(
                        $"[DoT] Created viz node '{node.name}' under Debuffs on '{_debuffsRoot.parent?.name}'"
                    );
            }
            _stacks.Add(stack);
            enabled = true;
            if (debugLogs)
                Debug.Log(
                    $"[DoT] Added stack id={effectId} to {gameObject.name} (dmg/tick={damagePerTick}, interval={tickInterval}, duration={duration}, stacking={allowStacking})"
                );
            return true;
        }

        private void EnsureDebuffsRoot()
        {
            if (_debuffsRoot != null)
                return;

            // Determine the mob root: prefer the ancestor tagged 'Mob', otherwise use the top of this object's hierarchy.
            Transform mobRoot = null;
            var t = transform;
            while (t != null)
            {
                if (t.CompareTag("Mob"))
                {
                    mobRoot = t;
                    break;
                }
                t = t.parent;
            }
            if (mobRoot == null)
                mobRoot = transform.root;

            // Look for existing 'Debuffs' under mob root
            var existing = mobRoot.Find("Debuffs");
            if (existing != null)
            {
                _debuffsRoot = existing;
                if (debugLogs)
                    Debug.Log($"[DoT] Found existing 'Debuffs' under '{mobRoot.name}'");
                return;
            }
            // Create an empty 'Debuffs' container under the mob root
            var go = new GameObject("Debuffs");
            _debuffsRoot = go.transform;
            _debuffsRoot.SetParent(mobRoot, false);
            _debuffsRoot.localPosition = Vector3.zero;
            if (debugLogs)
                Debug.Log($"[DoT] Created 'Debuffs' under '{mobRoot.name}'");
        }
    }
}
