using Game.Procederal.Core;
using UnityEngine;

namespace Mechanics.Chaos
{
    /// Modifier: On mob hit, either bounce in a new random direction or get destroyed.
    /// Each bounce increases the destroy chance. If idle (no interactions) for a duration, auto-destroy.
    [DisallowMultipleComponent]
    public class BounceMechanic : MonoBehaviour, IMechanic
    {
        [Header("Bounce Settings")]
        [Tooltip("Initial chance (0..1) that the payload is destroyed instead of bouncing on hit.")]
        [Range(0f, 1f)]
        public float baseDestroyChance = 0.2f;

        [Tooltip("Amount added to destroy chance after each successful bounce.")]
        [Range(0f, 1f)]
        public float destroyChanceIncreasePerBounce = 0.1f;

        [Tooltip("Seconds of no interactions before auto-destroy.")]
        public float idleLifetime = 2f;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private int _bounces;
        private float _idleTimer;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            _bounces = 0;
            _idleTimer = 0f;
        }

        public void Tick(float dt)
        {
            _idleTimer += dt;
            if (idleLifetime > 0f && _idleTimer >= idleLifetime)
            {
                if (debugLogs)
                    Debug.Log("[Bounce] Auto-destroy by idle lifetime", this);
                if (_ctx?.Payload != null)
                    MechanicLifecycleUtility.Release(_ctx.Payload.gameObject);
                else
                    MechanicLifecycleUtility.Release(gameObject);
            }
        }

        /// Call when a mob hit occurs. Decides whether to destroy or bounce, and outputs new direction.
        public bool TryHandleHit(out bool shouldDestroy, out Vector2 newDirection)
        {
            _idleTimer = 0f; // reset idle on interaction
            float chance = Mathf.Clamp01(
                baseDestroyChance + destroyChanceIncreasePerBounce * _bounces
            );
            float roll = Random.value;
            if (debugLogs)
                Debug.Log(
                    $"[Bounce] hit: bounces={_bounces} chance={chance:F2} roll={roll:F2}",
                    this
                );
            if (roll < chance)
            {
                shouldDestroy = true;
                newDirection = Vector2.zero;
                return true;
            }
            // Bounce: pick a new random unit direction
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 1e-4f)
                dir = Vector2.right;
            newDirection = dir.normalized;
            _bounces++;
            shouldDestroy = false;
            return true;
        }

        // --- Beam integration (decoupled) ---------------------------------------------------------
        public struct BeamHitSummary
        {
            public int totalDamage;
            public int headDamage;
            public int tailDamage;
            public bool headHit;
            public bool usingAnchoredTail;
        }

        public struct RedirectDecision
        {
            public bool hasDecision; // true if redirect or destroy chosen
            public bool destroy; // destroy payload
            public bool spawnNewSegment; // beam should create a new segment (anchored segmented tail)
            public Vector2 newDirection; // valid if !destroy
        }

        /// Beam-specific hook: decide destruction or redirection based on a beam hit summary.
        /// Returns true if a decision was produced.
        public bool TryHandleBeamHit(in BeamHitSummary hit, out RedirectDecision decision)
        {
            decision = default;
            if (hit.totalDamage <= 0)
                return false; // nothing to react to

            // Reuse existing probability model; treat any damage tick as a single interaction.
            _idleTimer = 0f;
            float chance = Mathf.Clamp01(
                baseDestroyChance + destroyChanceIncreasePerBounce * _bounces
            );
            float roll = Random.value;
            bool destroy = roll < chance;
            if (debugLogs)
            {
                Debug.Log(
                    $"[Bounce] beamHit total={hit.totalDamage} head={hit.headDamage} tail={hit.tailDamage} chance={chance:F2} roll={roll:F2} destroy={destroy}",
                    this
                );
            }
            if (destroy)
            {
                decision = new RedirectDecision
                {
                    hasDecision = true,
                    destroy = true,
                    spawnNewSegment = false,
                    newDirection = Vector2.zero,
                };
                return true;
            }
            // Choose new direction; random for now (same logic as TryHandleHit). Could bias off head direction later.
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 1e-4f)
                dir = Vector2.right;
            _bounces++;
            decision = new RedirectDecision
            {
                hasDecision = true,
                destroy = false,
                spawnNewSegment = hit.usingAnchoredTail, // anchored tail beams expect segmentation
                newDirection = dir.normalized,
            };
            return true;
        }
    }
}
