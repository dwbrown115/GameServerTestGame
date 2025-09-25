using System.Collections.Generic;
using UnityEngine;

namespace Mechanics.Chaos
{
    /// Radial damage on demand with distance falloff. Affects both players and mobs.
    /// Attach to a payload (projectile/beam/etc), then call TriggerExplosion(epicenter).
    public class ExplosionMechanic : MonoBehaviour, IMechanic
    {
        [Header("Explosion Settings")]
        [Tooltip("Explosion radius in world units.")]
        [Min(0f)]
        public float radius = 3f;

        [Tooltip(
            "Damage dealt at the epicenter (distance=0). Falls off linearly to minDamageFraction at edge."
        )]
        [Min(0)]
        public int baseDamage = 20;

        [Tooltip(
            "Minimum fraction of baseDamage at radius edge. 0 = no damage at the edge; 1 = full damage everywhere."
        )]
        [Range(0f, 1f)]
        public float minDamageFraction = 0.1f;

        [Tooltip("If true, skip damaging the owner hierarchy.")]
        public bool excludeOwner = true;

        [Tooltip("Layer mask of colliders to consider for explosion.")]
        public LayerMask explosionLayers = ~0;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private readonly List<Collider2D> _overlaps = new();
        private static Material _lineMat;

        [Header("Visualization")]
        [Tooltip("Show a red circle outline when the explosion triggers.")]
        public bool showVisualization = true;

        [Min(0.01f)]
        public float vizDuration = 0.2f;

        [Range(16, 256)]
        public int vizSegments = 64;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
        }

        public void Tick(float dt)
        {
            // no per-frame behavior required
        }

        public int TriggerExplosion(Vector2 epicenter)
        {
            int layerMask = explosionLayers.value;
            _overlaps.Clear();
            Physics2D.OverlapCircle(
                epicenter,
                Mathf.Max(0f, radius),
                new ContactFilter2D
                {
                    useLayerMask = true,
                    layerMask = layerMask,
                    useTriggers = true,
                },
                _overlaps
            );
            if (_overlaps.Count == 0)
                return 0;

            float r = Mathf.Max(0.0001f, radius);
            float minFrac = Mathf.Clamp01(minDamageFraction);
            int totalDamage = 0;

            for (int i = 0; i < _overlaps.Count; i++)
            {
                var c = _overlaps[i];
                if (c == null)
                    continue;

                if (excludeOwner && IsOwnerRelated(c))
                    continue;

                var dmg = c.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive)
                    continue;

                // distance falloff
                float dist = Vector2.Distance(epicenter, c.bounds.ClosestPoint(epicenter));
                float t = Mathf.Clamp01(dist / r); // 0 at center, 1 at edge
                float frac = Mathf.Lerp(1f, minFrac, t);
                int dealt = Mathf.RoundToInt(baseDamage * frac);
                if (dealt <= 0)
                    continue;

                Vector2 hitNormal = (c.transform.position - (Vector3)epicenter).normalized;
                dmg.TakeDamage(dealt, epicenter, hitNormal);
                totalDamage += dealt;

                if (debugLogs)
                    Debug.Log(
                        $"[ExplosionMechanic] Damaged {c.name} for {dealt} (dist={dist:F2})",
                        this
                    );
            }

            // Report to Drain if present on owner
            if (totalDamage > 0 && _ctx != null && _ctx.Owner != null)
            {
                var drain = _ctx.Owner.GetComponentInChildren<Mechanics.Corruption.DrainMechanic>();
                if (drain != null)
                    drain.ReportDamage(totalDamage);
            }

            if (showVisualization)
            {
                TryVisualizeExplosion(epicenter, Mathf.Max(0f, radius));
            }

            if (debugLogs)
                Debug.Log($"[ExplosionMechanic] Total damage={totalDamage}", this);
            return totalDamage;
        }

        private bool IsOwnerRelated(Collider2D c)
        {
            if (_ctx == null || _ctx.Owner == null || c == null)
                return false;
            var o = _ctx.Owner;
            if (c.transform == o || c.transform.IsChildOf(o) || o.IsChildOf(c.transform))
                return true;
            if (c.attachedRigidbody != null)
            {
                var rt = c.attachedRigidbody.transform;
                if (rt == o || rt.IsChildOf(o) || o.IsChildOf(rt))
                    return true;
            }
            return c.transform.root == o.root;
        }

        private void TryVisualizeExplosion(Vector2 epicenter, float r)
        {
            // Compute a 1-screen-pixel line width in world units
            float width = 0.02f;
            var cam = Camera.main;
            if (cam != null && cam.orthographic && Screen.height > 0)
            {
                float worldHeight = cam.orthographicSize * 2f;
                width = worldHeight / Screen.height; // 1 pixel in world units
            }

            var go = new GameObject("ExplosionViz");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = Mathf.Clamp(vizSegments, 16, 256);
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = Color.red;
            lr.endColor = Color.red;
            lr.sortingOrder = 1000;
            if (_lineMat == null)
            {
                var sh = Shader.Find("Sprites/Default");
                _lineMat = sh != null ? new Material(sh) : null;
            }
            if (_lineMat != null)
                lr.sharedMaterial = _lineMat;

            // Place points around the circle
            Vector3 center = new Vector3(epicenter.x, epicenter.y, 0f);
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float ang = t * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * r + center.x;
                float y = Mathf.Sin(ang) * r + center.y;
                lr.SetPosition(i, new Vector3(x, y, 0f));
            }

            // Auto-destroy viz
            Destroy(go, Mathf.Max(0.01f, vizDuration));
        }
    }
}
