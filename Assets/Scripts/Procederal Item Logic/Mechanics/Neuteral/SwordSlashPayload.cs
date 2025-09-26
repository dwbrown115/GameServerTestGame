using UnityEngine;

namespace Mechanics.Neuteral
{
    /// Static crescent (annular sector) payload for Sword Slash. Does not move itself;
    /// pair with ProjectileMechanic for motion and damage handling.
    [DisallowMultipleComponent]
    public class SwordSlashPayload : MonoBehaviour, IMechanic
    {
        [Header("Crescent Shape")]
        [Min(0.01f)]
        public float outerRadius = 1.5f;

        [Tooltip("When edgeOnly=false, thickness of the crescent (outerRadius - innerRadius)")]
        [Min(0.01f)]
        public float width = 0.5f;

        [Tooltip("Span of the crescent in degrees")]
        [Range(5f, 360f)]
        public float arcLengthDeg = 120f;

        [Tooltip("Render/damage as a thin outer band only")]
        public bool edgeOnly = true;

        [Min(0.01f)]
        public float edgeThickness = 0.2f;

        [Header("Visualization")]
        public bool showVisualization = true;
        public Color vizColor = new Color(1f, 1f, 1f, 0.5f);
        public int vizSortingOrder = -40;

        [Header("Debug")]
        public bool debugLogs = false;

        private MechanicContext _ctx;
        private LineRenderer _line;
        private PolygonCollider2D _poly;

        public void Initialize(MechanicContext ctx)
        {
            _ctx = ctx;
            EnsureCollider();
            if (showVisualization)
                EnsureViz();
            UpdateGeometry();
        }

        public void Tick(float dt)
        {
            // Static geometry; nothing to do per-frame
        }

        private void EnsureCollider()
        {
            _poly = GetComponent<PolygonCollider2D>();
            if (_poly == null)
                _poly = gameObject.AddComponent<PolygonCollider2D>();
            _poly.isTrigger = true;
            // Relay triggers upward if desired by other systems
            if (GetComponent<PayloadTriggerRelay>() == null)
                gameObject.AddComponent<PayloadTriggerRelay>();
        }

        private void EnsureViz()
        {
            _line = GetComponent<LineRenderer>();
            if (_line == null)
                _line = gameObject.AddComponent<LineRenderer>();
            var mat = new Material(Shader.Find("Sprites/Default"));
            _line.sharedMaterial = mat;
            _line.useWorldSpace = false;
            _line.loop = false;
            _line.textureMode = LineTextureMode.Stretch;
            _line.sortingOrder = vizSortingOrder;
            _line.startColor = vizColor;
            _line.endColor = vizColor;
            _line.startWidth = Mathf.Max(0.01f, edgeOnly ? edgeThickness : 0.06f);
            _line.endWidth = _line.startWidth;
        }

        private void UpdateGeometry()
        {
            float baseAng = 0f; // orient via transform.rotation externally
            float inner = edgeOnly
                ? Mathf.Max(0f, outerRadius - Mathf.Max(0.01f, edgeThickness))
                : Mathf.Max(0.01f, outerRadius - width);
            float outer = Mathf.Max(outerRadius, inner + 0.01f);
            float span = Mathf.Clamp(arcLengthDeg, 5f, 360f);
            int segments = Mathf.Clamp(Mathf.CeilToInt(span / 6f), 3, 128);

            // Collider path as ring segment (outer arc then inner arc reversed)
            var pts = new Vector2[(segments + 1) * 2];
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float a = Mathf.Lerp(-span * 0.5f, span * 0.5f, t) + baseAng;
                float rad = a * Mathf.Deg2Rad;
                float cx = Mathf.Cos(rad);
                float cy = Mathf.Sin(rad);
                pts[i] = new Vector2(cx * outer, cy * outer);
            }
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float a = Mathf.Lerp(span * 0.5f, -span * 0.5f, t) + baseAng;
                float rad = a * Mathf.Deg2Rad;
                float cx = Mathf.Cos(rad);
                float cy = Mathf.Sin(rad);
                pts[(segments + 1) + i] = new Vector2(cx * inner, cy * inner);
            }
            if (_poly != null)
            {
                _poly.pathCount = 1;
                _poly.SetPath(0, pts);
            }

            if (showVisualization && _line != null)
            {
                // Draw outer arc only to emphasize outline
                _line.positionCount = segments + 1;
                for (int i = 0; i <= segments; i++)
                {
                    float t = (float)i / segments;
                    float a = Mathf.Lerp(-span * 0.5f, span * 0.5f, t) + baseAng;
                    float rad = a * Mathf.Deg2Rad;
                    float cx = Mathf.Cos(rad);
                    float cy = Mathf.Sin(rad);
                    _line.SetPosition(i, new Vector3(cx * outer, cy * outer, 0f));
                }
            }
        }
    }
}
