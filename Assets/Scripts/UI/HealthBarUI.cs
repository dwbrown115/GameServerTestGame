using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class HealthBarUI : MonoBehaviour
{
    [Header("Binding")]
    [Tooltip("PlayerHealth to observe. If not set, will try to find the Player by tag.")]
    public PlayerHealth playerHealth;

    [Header("Style")]
    public Vector2 barSize = new Vector2(220, 18);
    public Color backgroundColor = new Color(0f, 0f, 0f, 0.65f);
    public Color fillColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    public Color textColor = Color.white;
    public int fontSize = 18;
    public Vector2 margin = new Vector2(16, 16); // distance from bottom-left

    private RectTransform _root;
    private RectTransform _barBg;
    private RectTransform _barFill;
    private TextMeshProUGUI _label;

    private void Awake()
    {
        _root = GetComponent<RectTransform>();

        // Default anchoring to bottom-left of the canvas/parent
        _root.anchorMin = new Vector2(0f, 0f);
        _root.anchorMax = new Vector2(0f, 0f);
        _root.pivot = new Vector2(0f, 0f);
        _root.anchoredPosition = margin;

        BuildUI();
    }

    private void OnEnable()
    {
        if (playerHealth == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerHealth = player.GetComponent<PlayerHealth>();
            }
        }

        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += HandleHealthChanged;
            playerHealth.OnDied += HandleDied;
            // Initialize display with current values
            playerHealth.GetHealth(out var cur, out var mx);
            UpdateDisplay(cur, mx);
        }
        else
        {
            UpdateDisplay(0, 100);
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= HandleHealthChanged;
            playerHealth.OnDied -= HandleDied;
        }
    }

    private void HandleHealthChanged(int current, int max)
    {
        UpdateDisplay(current, max);
    }

    private void HandleDied()
    {
        if (playerHealth != null)
        {
            playerHealth.GetHealth(out var _, out var mx);
            UpdateDisplay(0, mx);
        }
        else
        {
            UpdateDisplay(0, 100);
        }
    }

    private void BuildUI()
    {
        // Clear existing children once (safe if this is re-run in edit mode)
        for (int i = _root.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(_root.GetChild(i).gameObject);
        }

        // Label
        var labelGO = new GameObject("HP_Label", typeof(RectTransform));
        labelGO.transform.SetParent(_root, false);
        var labelRt = labelGO.GetComponent<RectTransform>();
        // Keep label directly above the bar and span the bar width
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(0f, 0f);
        labelRt.pivot = new Vector2(0f, 0f);
        labelRt.anchoredPosition = new Vector2(0f, barSize.y + 6f);
        labelRt.sizeDelta = new Vector2(barSize.x, fontSize + 6f);
        _label = labelGO.AddComponent<TextMeshProUGUI>();
        _label.fontSize = fontSize;
        _label.color = textColor;
        _label.text = "HP: 0/0";
        _label.textWrappingMode = TextWrappingModes.NoWrap;
        _label.alignment = TextAlignmentOptions.Center;

        // Bar background
        var bgGO = new GameObject("HP_Bar_BG", typeof(RectTransform));
        bgGO.transform.SetParent(_root, false);
        _barBg = bgGO.GetComponent<RectTransform>();
        _barBg.anchorMin = new Vector2(0f, 0f);
        _barBg.anchorMax = new Vector2(0f, 0f);
        _barBg.pivot = new Vector2(0f, 0f);
        _barBg.sizeDelta = barSize;
        _barBg.anchoredPosition = Vector2.zero;
        var bgImage = bgGO.AddComponent<Image>();
        bgImage.color = backgroundColor;

        // Bar fill (use left-stretch technique via anchorMax.x to represent percent)
        var fillGO = new GameObject("HP_Bar_Fill", typeof(RectTransform));
        fillGO.transform.SetParent(bgGO.transform, false);
        _barFill = fillGO.GetComponent<RectTransform>();
        _barFill.anchorMin = new Vector2(0f, 0f);
        _barFill.anchorMax = new Vector2(0f, 1f);
        _barFill.pivot = new Vector2(0f, 0.5f);
        _barFill.offsetMin = new Vector2(2f, 2f);
        _barFill.offsetMax = new Vector2(-2f, -2f);
        var fillImage = fillGO.AddComponent<Image>();
        fillImage.color = fillColor;
    }

    private void UpdateDisplay(int current, int max)
    {
        current = Mathf.Max(0, current);
        max = Mathf.Max(1, max);
        float pct = Mathf.Clamp01((float)current / max);

        if (_label != null)
        {
            _label.text = $"HP: {current}/{max}";
        }

        if (_barFill != null)
        {
            // Adjust anchorMax.x based on percent to visually fill from left to right
            _barFill.anchorMax = new Vector2(pct, 1f);
        }
    }
}
