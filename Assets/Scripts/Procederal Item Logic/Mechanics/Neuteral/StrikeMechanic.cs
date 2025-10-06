using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Instantly damages a random enemy at a fixed interval.
    /// Visualization is optional (a black rectangle sprite by default).
    [DisallowMultipleComponent]
    public class StrikeMechanic : MonoBehaviour, IMechanic
    {
        [Header("Strike Settings")]
        [Min(0f)]
        public float interval = 0.5f;
        public int damagePerInterval = 5;

        [Tooltip(
            "If true, only colliders tagged 'Mob' (or with a parent tagged 'Mob') will be damaged."
        )]
        public bool requireMobTag = true;

        [Tooltip("If true, skip owner hierarchy when picking targets.")]
        public bool excludeOwner = true;

        [Header("Visualization")]
        public bool showVisualization = true;
        public Color vizColor = Color.black;
        public int vizSortingOrder = 0;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private float _timer;
        private Sprite _strikeSprite;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            _timer = 0f;
            // Cache a unit square sprite for strike flashes
            _strikeSprite = Game.Procederal.ProcederalItemGenerator.GetUnitSquareSprite();
            GameOverController.OnCountdownFinished += Stop;
        }

        private bool _stopped;

        private void Stop()
        {
            _stopped = true;
        }

        public void Tick(float dt)
        {
            if (_stopped)
                return;
            _timer += dt;
            if (_timer < Mathf.Max(0.01f, interval))
                return;
            _timer = 0f;

            var target = PickRandomEnemy();
            if (target == null)
                return;

            var dmg = target.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive)
                return;

            Vector2 hitPoint = target.position;
            Vector2 hitNormal = Vector2.zero;
            dmg.TakeDamage(damagePerInterval, hitPoint, hitNormal);
            if (debugLogs)
                Debug.Log($"[StrikeMechanic] Damaged {target.name} for {damagePerInterval}", this);

            // Generic modifier dispatch: notify any IPrimaryHitModifier components (no strike-specific coupling)
            DispatchHit(target, hitPoint, hitNormal, damagePerInterval);

            // Visualization: spawn a brief sprite on the enemy to show the strike
            if (showVisualization && _strikeSprite != null)
            {
                SpawnStrikeFlashOnTarget(target);
            }
        }

        private Transform PickRandomEnemy()
        {
            var mobs = GameObject.FindGameObjectsWithTag("Mob");
            if (mobs == null || mobs.Length == 0)
                return null;
            // Optionally filter out owner hierarchy
            Transform ownerT = _ctx != null ? _ctx.Owner : null;
            // Try a few random picks to avoid O(n) search each tick
            for (int tries = 0; tries < 3; tries++)
            {
                int idx = Random.Range(0, mobs.Length);
                var t = mobs[idx]?.transform;
                if (t == null)
                    continue;
                if (excludeOwner && ownerT != null)
                {
                    if (t == ownerT || t.IsChildOf(ownerT) || ownerT.IsChildOf(t))
                        continue;
                }
                // Verify IDamageable exists and alive
                var dmg = t.GetComponentInParent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                    return t;
            }
            // Fallback: linear search
            foreach (var go in mobs)
            {
                if (go == null)
                    continue;
                var t = go.transform;
                if (excludeOwner && ownerT != null)
                {
                    if (t == ownerT || t.IsChildOf(ownerT) || ownerT.IsChildOf(t))
                        continue;
                }
                var dmg = t.GetComponentInParent<IDamageable>();
                if (dmg != null && dmg.IsAlive)
                    return t;
            }
            return null;
        }

        private void SpawnStrikeFlashOnTarget(Transform target)
        {
            if (target == null)
                return;
            var go = new GameObject("StrikeFlash");
            go.transform.SetParent(target, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(1.25f, 0.5f, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _strikeSprite;
            sr.color = vizColor;
            sr.sortingOrder = vizSortingOrder;
            go.AddComponent<_AutoDestroy>().seconds = Mathf.Max(0.05f, interval * 0.25f);
        }

        private void OnDestroy()
        {
            GameOverController.OnCountdownFinished -= Stop;
        }

        // --- Generic primary hit dispatch (uses IPrimaryHitModifier) -----------------------
        private static System.Collections.Generic.List<Mechanics.IPrimaryHitModifier> _reusableList =
            new System.Collections.Generic.List<Mechanics.IPrimaryHitModifier>(8);

        private void DispatchHit(Transform target, Vector2 hitPoint, Vector2 hitNormal, int dmg)
        {
            _reusableList.Clear();
            GetComponents(_reusableList);
            var info = new Mechanics.PrimaryHitInfo(target, hitPoint, hitNormal, dmg, this);
            for (int i = 0; i < _reusableList.Count; i++)
            {
                var mod = _reusableList[i];
                if (mod == null)
                    continue;
                try
                {
                    mod.OnPrimaryHit(in info);
                }
                catch (System.Exception ex)
                {
                    if (debugLogs)
                        Debug.LogWarning(
                            $"[StrikeMechanic] Modifier exception: {ex.Message}",
                            this
                        );
                }
            }
        }

        private class _AutoDestroy : MonoBehaviour
        {
            public float seconds = 0.1f;
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
