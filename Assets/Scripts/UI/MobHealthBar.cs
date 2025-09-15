using UnityEngine;

/// Attach to a Mob (same GameObject as MobHealth). Creates a small world-space 2D health bar above the mob.
[DisallowMultipleComponent]
public class MobHealthBar : MonoBehaviour
{
    [Header("Appearance")]
    [Min(0.05f)]
    public float width = 1.0f;

    [Min(0.02f)]
    public float height = 0.12f;
    public float yOffset = 1.0f;
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
    public Color fillColor = new Color(0.2f, 0.9f, 0.2f, 0.95f);
    public bool hideWhenFull = false;
    public int sortingOrder = 100; // draw above sprites

    private MobHealth _health;
    private Transform _barRoot;
    private Transform _fillTf;
    private SpriteRenderer _bgSr;
    private SpriteRenderer _fillSr;
    private Sprite _whiteSprite;

    private void Awake()
    {
        _health = GetComponent<MobHealth>();
        if (_health == null)
        {
            Debug.LogWarning(
                "MobHealthBar: No MobHealth found on the same GameObject. Disabling.",
                this
            );
            enabled = false;
            return;
        }

        _whiteSprite = GenerateWhiteSprite();
        BuildBar();
        // Initialize to full; will update on first OnHealthChanged broadcast
        SetRatio(1f);
    }

    private void OnEnable()
    {
        if (_health != null)
        {
            _health.OnHealthChanged += OnHealthChanged;
            _health.OnDied += OnDied;
            // Initialize with current values if available
            _health.GetHealth(out int current, out int max);
            OnHealthChanged(current, max);
        }
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnHealthChanged -= OnHealthChanged;
            _health.OnDied -= OnDied;
        }
    }

    private void BuildBar()
    {
        _barRoot = new GameObject("MobHealthBar").transform;
        _barRoot.SetParent(transform, false);
        _barRoot.localPosition = new Vector3(0f, yOffset, 0f);

        // Background
        var bg = new GameObject("BG");
        bg.transform.SetParent(_barRoot, false);
        _bgSr = bg.AddComponent<SpriteRenderer>();
        _bgSr.sprite = _whiteSprite;
        _bgSr.color = backgroundColor;
        _bgSr.sortingOrder = sortingOrder;
        bg.transform.localScale = new Vector3(width, height, 1f);

        // Fill (left-aligned scaling)
        var fill = new GameObject("Fill");
        fill.transform.SetParent(_barRoot, false);
        _fillSr = fill.AddComponent<SpriteRenderer>();
        _fillSr.sprite = _whiteSprite;
        _fillSr.color = fillColor;
        _fillSr.sortingOrder = sortingOrder + 1;
        _fillTf = fill.transform;
        _fillTf.localScale = new Vector3(width, height, 1f);
        _fillTf.localPosition = Vector3.zero; // will be adjusted by SetRatio
    }

    private void OnHealthChanged(int current, int max)
    {
        float ratio = max > 0 ? Mathf.Clamp01((float)current / max) : 1f;
        SetRatio(ratio);
    }

    private void OnDied()
    {
        if (_barRoot != null)
            Destroy(_barRoot.gameObject);
    }

    private void SetRatio(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        if (_barRoot == null || _fillTf == null)
            return;

        if (hideWhenFull)
            _barRoot.gameObject.SetActive(ratio < 0.999f);

        // Scale X based on ratio, keep left edge aligned by shifting center
        float w = Mathf.Max(0.001f, width);
        float scaledW = w * ratio;
        _fillTf.localScale = new Vector3(scaledW, height, 1f);
        float delta = (w - scaledW) * 0.5f;
        _fillTf.localPosition = new Vector3(-delta, 0f, 0f);
    }

    private Sprite GenerateWhiteSprite()
    {
        // 16x16 white texture, 1 world unit wide at scale 1 when pixelsPerUnit=16
        int size = 16;
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;
        var colors = new Color[size * size];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply(false, false);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
