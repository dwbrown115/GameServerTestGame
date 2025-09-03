using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[Serializable]
public class LeaderboardEntry
{
    // Optional server id (not displayed directly)
    public string Id; // If server sends 'id'
    public string Username;
    public int PlayerHighestScore;
}

[Serializable]
public class LeaderboardApiResponse
{
    [JsonProperty("response_type")]
    public string ResponseType;

    [JsonProperty("payload")]
    public List<LeaderboardEntry> Payload;
}

/// <summary>
/// View component that renders cached leaderboard data from LeaderboardService.
/// Place on the Leaderboard scene. Does not perform network operations.
/// </summary>
public class LeaderboardManager : MonoBehaviour
{
    // Base (unscaled) heights; actual runtime heights scale with resolution/font.
    [Header("Row Scaling")]
    [SerializeField]
    private float baseHeaderHeight = 38f;

    [SerializeField]
    private float baseRowHeight = 36f;

    [SerializeField]
    private float minRowHeight = 20f;

    [SerializeField]
    private float maxRowHeight = 120f;
    private float _currentHeaderHeight;
    private float _currentRowHeight;

    [Header("Column Layout")]
    [Tooltip("Fixed width for Rank column.")]
    [SerializeField]
    private float rankColumnWidth = 80f;

    [Tooltip("Minimum (and starting) width for Player column; it will stretch.")]
    [SerializeField]
    private float playerColumnMinWidth = 320f;

    [Tooltip("Fixed width for Score column (right side).")]
    [SerializeField]
    private float scoreColumnWidth = 160f;

    [Tooltip("Horizontal spacing between columns (header & rows).")]
    [SerializeField]
    private float columnSpacing = 12f;

    [Tooltip("Auto-fit header text widths and propagate to rows.")]
    [SerializeField]
    private bool dynamicColumnWidths = true;

    [Tooltip("Extra padding added to measured header text width (pixels).")]
    [SerializeField]
    private float headerFitPadding = 24f;

    [Tooltip("Minimum width clamp for dynamically sized Rank column.")]
    [SerializeField]
    private float minRankWidth = 60f;

    [Tooltip("Minimum width clamp for dynamically sized Player column.")]
    [SerializeField]
    private float minPlayerWidth = 220f;

    [Tooltip("Minimum width clamp for dynamically sized Score column.")]
    [SerializeField]
    private float minScoreWidth = 120f;

    private float _legacyHeaderHeight = 0f; // kept for backward compatibility; no longer drives layout
    private RectTransform _headerRect;

    [Header("Font Scaling")]
    [Tooltip("Base header font size at reference resolution.")]
    [SerializeField]
    private float baseHeaderFontSize = 22f;

    [Tooltip("Base row font size at reference resolution.")]
    [SerializeField]
    private float baseRowFontSize = 20f;

    [Tooltip("Reference resolution used to compute scale (width, height).")]
    [SerializeField]
    private Vector2 referenceResolution = new Vector2(1920f, 1080f);

    [Tooltip("Additional user multiplier applied after resolution scaling.")]
    [SerializeField]
    [Range(0.25f, 4f)]
    private float fontScaleMultiplier = 1f;

    [Tooltip("Clamp minimum final font size to avoid unreadable text.")]
    [SerializeField]
    private float minReadableFontSize = 10f;

    [Tooltip("Clamp maximum final font size to avoid layout breakage.")]
    [SerializeField]
    private float maxReadableFontSize = 64f;

    private readonly List<TMP_Text> _headerTexts = new List<TMP_Text>();
    private int _lastScreenW = -1;
    private int _lastScreenH = -1;
    private float _lastAppliedMultiplier = -1f;

    [Tooltip("Max entries to render (0 = unlimited)."), SerializeField]
    private int maxEntries = 0;

    [Header("UI References")]
    [Tooltip(
        "Parent container (e.g., a Content object under a Scroll View) where leaderboard rows will be instantiated."
    )]
    [SerializeField]
    private Transform entriesParent;

    [Tooltip("Optional loading panel while fetching.")]
    [SerializeField]
    private GameObject loadingPanel;

    [Tooltip("Optional error text element.")]
    [SerializeField]
    private TMP_Text errorText;

    [Header("Generated Runtime Styles")]
    [Tooltip("Background color for normal rows.")]
    [SerializeField]
    private Color baseRowColor = new Color(0.10f, 0.10f, 0.14f, 0.85f);

    [Tooltip("Background color alternate for striped rows.")]
    [SerializeField]
    private Color altRowColor = new Color(0.16f, 0.16f, 0.22f, 0.95f);

    [Tooltip("Highlight color for the local player row.")]
    [SerializeField]
    private Color selfHighlightColor = new Color(0.20f, 0.55f, 0.25f, 1f);

    [Tooltip("Highlight color for 1st place (full alpha to avoid dulling).")]
    [SerializeField]
    private Color top1Color = new Color(1f, 0.84f, 0f, 1f); // #FFD600

    [Tooltip("Highlight color for 2nd place.")]
    [SerializeField]
    private Color top2Color = new Color(0.80f, 0.80f, 0.80f, 1f); // silver

    [Tooltip("Highlight color for 3rd place.")]
    [SerializeField]
    private Color top3Color = new Color(0.90f, 0.55f, 0.25f, 1f); // bronze

    private readonly List<LeaderboardRowView> _activeRows = new List<LeaderboardRowView>();
    private readonly Queue<LeaderboardRowView> _pool = new Queue<LeaderboardRowView>();
    private bool _headerBuilt;

    private void OnEnable()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(
                !LeaderboardService.Instance || !LeaderboardService.Instance.HasData
            );
        if (errorText != null)
            errorText.text = string.Empty;
        Subscribe();
        TryRender();
        RescaleAllFontsIfNeeded(force: true);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (LeaderboardService.Instance != null)
        {
            LeaderboardService.Instance.OnEntriesUpdated += HandleServiceUpdate;
            LeaderboardService.Instance.OnError += HandleServiceError;
        }
    }

    private void Unsubscribe()
    {
        if (LeaderboardService.Instance != null)
        {
            LeaderboardService.Instance.OnEntriesUpdated -= HandleServiceUpdate;
            LeaderboardService.Instance.OnError -= HandleServiceError;
        }
    }

    private void HandleServiceUpdate()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
        if (errorText != null)
            errorText.text = string.Empty;
        TryRender();
        RescaleAllFontsIfNeeded(force: true);
    }

    private void HandleServiceError(string message)
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
        if (errorText != null)
            errorText.text = message;
    }

    private void TryRender()
    {
        if (LeaderboardService.Instance == null)
            return;
        if (!LeaderboardService.Instance.HasData)
        {
            ClearExisting();
            return;
        }
        EnsureHeader();
        RenderLeaderboard(new List<LeaderboardEntry>(LeaderboardService.Instance.Entries));
    }

    private void ClearExisting()
    {
        for (int i = 0; i < _activeRows.Count; i++)
        {
            var row = _activeRows[i];
            if (row)
            {
                row.gameObject.SetActive(false);
                _pool.Enqueue(row);
            }
        }
        _activeRows.Clear();
    }

    private void RenderLeaderboard(List<LeaderboardEntry> entries)
    {
        ClearExisting(); // entries already sorted by service
        int count = 0;
        string selfName =
            PlayerManager.Instance != null ? PlayerManager.Instance.GetPlayerName() : null;
        foreach (var entry in entries)
        {
            if (maxEntries > 0 && count >= maxEntries)
                break;
            var row = GetRow();
            ConfigureRow(row, count, entry, selfName);
            _activeRows.Add(row);
            count++;
        }
    }

    private void EnsureHeader()
    {
        if (_headerBuilt)
            return;
        ConfigureParentLayoutGroup();
        // Build a non-pooled header row
        var go = new GameObject("LeaderboardHeader", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(entriesParent, false);
        rt.SetSiblingIndex(0);
        // Anchor & pivot at top stretch across width
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, baseHeaderHeight); // height only (width stretches)

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.07f, 0.12f, 0.95f);

        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childControlWidth = true; // allow LayoutElement widths to drive sizing
        layout.childControlHeight = true;
        layout.spacing = columnSpacing;
        layout.padding = new RectOffset(12, 12, 4, 4);

        // Ensure header background stretches full width inside a VerticalLayoutGroup
        var headerLE = go.AddComponent<LayoutElement>();
        headerLE.minHeight = baseHeaderHeight;
        headerLE.preferredHeight = baseHeaderHeight;
        headerLE.flexibleWidth = 1f;
        _legacyHeaderHeight = baseHeaderHeight;
        _headerRect = rt;

        CreateHeaderText(go.transform, "RANK", rankColumnWidth, TextAlignmentOptions.Center);
        // Player stretches (flexible) to fill remaining width
        CreateHeaderText(
            go.transform,
            "PLAYER",
            playerColumnMinWidth,
            TextAlignmentOptions.Left,
            flexible: 1
        );
        CreateHeaderText(go.transform, "SCORE", scoreColumnWidth, TextAlignmentOptions.Center);
        _headerBuilt = true;
        RescaleAllFontsIfNeeded(force: true);
    }

    private void CreateHeaderText(
        Transform parent,
        string label,
        float width,
        TextAlignmentOptions align,
        int flexible = 0
    )
    {
        var go = new GameObject(label, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = baseHeaderFontSize; // will be scaled later
        tmp.fontStyle = FontStyles.Bold;
        tmp.text = label;
        tmp.alignment = align;
        var layout = go.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = flexible;
        _headerTexts.Add(tmp);
    }

    private LeaderboardRowView GetRow()
    {
        LeaderboardRowView rowView = null;
        while (_pool.Count > 0 && (rowView == null || rowView.gameObject == null))
        {
            rowView = _pool.Dequeue();
        }
        if (rowView == null)
        {
            rowView = BuildRow();
        }
        rowView.gameObject.SetActive(true);
        return rowView;
    }

    private LeaderboardRowView BuildRow()
    {
        var go = new GameObject(
            "LeaderboardRow",
            typeof(RectTransform),
            typeof(LeaderboardRowView)
        );
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(entriesParent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f); // top pivot so vertical layout positions flush
        rt.sizeDelta = new Vector2(0, baseRowHeight);

        var bg = go.AddComponent<Image>();
        bg.color = baseRowColor;

        var layout = go.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;
        layout.childControlWidth = true; // honor LayoutElement widths
        layout.childControlHeight = true;
        layout.spacing = columnSpacing;
        layout.padding = new RectOffset(12, 12, 4, 4); // match header padding

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = baseRowHeight;
        le.preferredHeight = baseRowHeight;
        le.flexibleHeight = 0f;
        le.flexibleWidth = 1f;

        var rowView = go.GetComponent<LeaderboardRowView>();
        rowView.background = bg;
        rowView.rankText = CreateTMPChild(
            go.transform,
            "Rank",
            TextAlignmentOptions.Center,
            rankColumnWidth,
            flexible: 0
        );
        rowView.userText = CreateTMPChild(
            go.transform,
            "Username",
            TextAlignmentOptions.Left,
            playerColumnMinWidth,
            flexible: 1
        );
        rowView.scoreText = CreateTMPChild(
            go.transform,
            "Score",
            TextAlignmentOptions.Center,
            scoreColumnWidth,
            flexible: 0
        );
        // Apply scaling to newly created row texts & height
        ApplyScaledFont(rowView.rankText, baseRowFontSize);
        ApplyScaledFont(rowView.userText, baseRowFontSize);
        ApplyScaledFont(rowView.scoreText, baseRowFontSize);
        ApplyScaledRowHeight(rt);
        return rowView;
    }

    private TMP_Text CreateTMPChild(
        Transform parent,
        string label,
        TextAlignmentOptions alignment,
        float width,
        int flexible = 0
    )
    {
        var go = new GameObject(label, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = baseRowFontSize; // scaled after creation
        tmp.alignment = alignment;
        SetNoWrap(tmp);
        tmp.text = label;
        var layout = go.AddComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.flexibleWidth = flexible;

        var outline = go.AddComponent<Outline>();
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(1f, -1f);
        return tmp;
    }

    private float ComputeResolutionScale()
    {
        if (referenceResolution.x <= 0f || referenceResolution.y <= 0f)
            return 1f * fontScaleMultiplier;
        float wScale = Screen.width / referenceResolution.x;
        float hScale = Screen.height / referenceResolution.y;
        // Use smaller scale to reduce overflow likelihood
        float baseScale = Mathf.Min(wScale, hScale);
        return baseScale * fontScaleMultiplier;
    }

    private void ApplyScaledFont(TMP_Text txt, float baseSize)
    {
        if (!txt)
            return;
        float scale = ComputeResolutionScale();
        float finalSize = Mathf.Clamp(baseSize * scale, minReadableFontSize, maxReadableFontSize);
        if (!Mathf.Approximately(txt.fontSize, finalSize))
            txt.fontSize = finalSize;
    }

    private void RescaleAllFontsIfNeeded(bool force = false)
    {
        if (
            !force
            && _lastScreenW == Screen.width
            && _lastScreenH == Screen.height
            && Mathf.Approximately(_lastAppliedMultiplier, fontScaleMultiplier)
        )
        {
            return;
        }
        _lastScreenW = Screen.width;
        _lastScreenH = Screen.height;
        _lastAppliedMultiplier = fontScaleMultiplier;

        // Header texts
        for (int i = 0; i < _headerTexts.Count; i++)
        {
            ApplyScaledFont(_headerTexts[i], baseHeaderFontSize);
        }
        // Active rows
        for (int i = 0; i < _activeRows.Count; i++)
        {
            var r = _activeRows[i];
            if (!r)
                continue;
            ApplyScaledFont(r.rankText, baseRowFontSize);
            ApplyScaledFont(r.userText, baseRowFontSize);
            ApplyScaledFont(r.scoreText, baseRowFontSize);
        }
        if (dynamicColumnWidths)
        {
            RefreshDynamicColumnWidths();
        }
        UpdateScaledHeights();
        RepositionRows();
    }

    private void Update()
    {
        RescaleAllFontsIfNeeded();
    }

    private void UpdateScaledHeights()
    {
        float scale = ComputeResolutionScale();
        _currentHeaderHeight = Mathf.Clamp(baseHeaderHeight * scale, minRowHeight, maxRowHeight);
        _currentRowHeight = Mathf.Clamp(baseRowHeight * scale, minRowHeight, maxRowHeight);
        if (_headerRect)
        {
            _headerRect.sizeDelta = new Vector2(_headerRect.sizeDelta.x, _currentHeaderHeight);
            var le = _headerRect.GetComponent<LayoutElement>();
            if (le)
            {
                le.minHeight = _currentHeaderHeight;
                le.preferredHeight = _currentHeaderHeight;
            }
        }
        for (int i = 0; i < _activeRows.Count; i++)
        {
            var row = _activeRows[i];
            if (!row)
                continue;
            var rt = row.transform as RectTransform;
            if (rt)
                ApplyScaledRowHeight(rt);
        }
    }

    private void ApplyScaledRowHeight(RectTransform rt)
    {
        if (!rt)
            return;
        float h = _currentRowHeight > 0 ? _currentRowHeight : baseRowHeight;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
        var le = rt.GetComponent<LayoutElement>();
        if (le)
        {
            le.minHeight = h;
            le.preferredHeight = h;
        }
    }

    private void RepositionRows()
    {
        float headerH = _currentHeaderHeight > 0 ? _currentHeaderHeight : baseHeaderHeight;
        float rowH = _currentRowHeight > 0 ? _currentRowHeight : baseRowHeight;
        for (int i = 0; i < _activeRows.Count; i++)
        {
            var row = _activeRows[i];
            if (!row)
                continue;
            var rt = row.transform as RectTransform;
            if (!rt)
                continue;
            float y = -(headerH + (rowH * i));
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        }
    }

    /// <summary>
    /// Measure header text preferred widths (after font scaling) and adjust LayoutElement widths.
    /// Propagate the resulting column widths to all active rows so alignment remains consistent.
    /// Player column retains flexible width to occupy remaining space while honoring a minimum.
    /// </summary>
    private void RefreshDynamicColumnWidths()
    {
        if (!_headerBuilt || _headerTexts.Count < 3)
            return;

        // Expected order when created: 0=RANK 1=PLAYER 2=SCORE
        TMP_Text rankHeader = _headerTexts[0];
        TMP_Text playerHeader = _headerTexts[1];
        TMP_Text scoreHeader = _headerTexts[2];
        if (!rankHeader || !playerHeader || !scoreHeader)
            return;

        // Measure preferred widths (in local units/pixels) after scaling
        float rankW = Mathf.Max(minRankWidth, rankHeader.preferredWidth + headerFitPadding);
        float scoreW = Mathf.Max(minScoreWidth, scoreHeader.preferredWidth + headerFitPadding);
        // Player header measurement – we still allow it to flex but enforce a min based on its label or serialized minimum.
        float playerMin = Mathf.Max(minPlayerWidth, playerHeader.preferredWidth + headerFitPadding);

        // Apply to header layout elements
        ApplyHeaderWidth(rankHeader, rankW, flexible: 0);
        ApplyHeaderWidth(scoreHeader, scoreW, flexible: 0);
        ApplyHeaderWidth(playerHeader, playerMin, flexible: 1); // flexible fills remainder

        // Propagate to rows (rank & score fixed, player min width updated; player keeps flexible width via existing LayoutElement)
        for (int i = 0; i < _activeRows.Count; i++)
        {
            var row = _activeRows[i];
            if (!row)
                continue;
            SyncRowColumnWidth(row.rankText, rankW, 0);
            SyncRowColumnWidth(row.scoreText, scoreW, 0);
            SyncRowColumnWidth(row.userText, playerMin, 1, isPlayer: true);
        }
    }

    private void ApplyHeaderWidth(TMP_Text txt, float width, int flexible)
    {
        if (!txt)
            return;
        var le = txt.GetComponent<LayoutElement>();
        if (!le)
            return;
        le.minWidth = width;
        le.preferredWidth = width;
        le.flexibleWidth = flexible;
    }

    private void SyncRowColumnWidth(TMP_Text txt, float width, int flexible, bool isPlayer = false)
    {
        if (!txt)
            return;
        var le = txt.GetComponent<LayoutElement>();
        if (!le)
            return;
        // For player column keep flexible width = 1 so it stretches, but ensure minimum width matches header's min.
        if (isPlayer)
        {
            le.minWidth = width;
            le.preferredWidth = width; // Acts as a lower bound; stretch will expand beyond if space available
            le.flexibleWidth = 1;
        }
        else
        {
            le.minWidth = width;
            le.preferredWidth = width;
            le.flexibleWidth = flexible;
        }
    }

    private void ConfigureRow(
        LeaderboardRowView row,
        int index,
        LeaderboardEntry entry,
        string selfName
    )
    {
        string displayName = SanitizeDisplayName(entry);
        row.Set(index + 1, displayName, entry.PlayerHighestScore);

        // Manual vertical stacking (VerticalLayoutGroup disabled for deterministic positioning)
        var rtRow = row.transform as RectTransform;
        if (rtRow != null)
        {
            float headerH = _currentHeaderHeight > 0 ? _currentHeaderHeight : baseHeaderHeight;
            float rowH = _currentRowHeight > 0 ? _currentRowHeight : baseRowHeight;
            float y = -(headerH + (rowH * index));
            rtRow.anchoredPosition = new Vector2(rtRow.anchoredPosition.x, y);
        }
        var bg = row.background;
        if (bg)
        {
            // Rank highlight
            Color color;
            if (index == 0)
                color = top1Color;
            else if (index == 1)
                color = top2Color;
            else if (index == 2)
                color = top3Color;
            else
                color = (index % 2 == 1) ? altRowColor : baseRowColor;

            // Self highlight: only override if NOT a top-3 rank to preserve intended award colors.
            if (
                !string.IsNullOrEmpty(selfName)
                && !string.IsNullOrEmpty(entry.Username)
                && string.Equals(selfName, entry.Username, StringComparison.OrdinalIgnoreCase)
            )
            {
                if (index >= 3)
                {
                    color = selfHighlightColor; // full replace for non-podium rows
                }
                // else: leave podium color untouched (no blending to keep exact hex e.g. #FFD600)
            }
            bg.color = color;
        }
    }

    private string SanitizeDisplayName(LeaderboardEntry entry)
    {
        // Prefer Username; fallback to Id if Username missing.
        string name = entry.Username;
        if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(entry.Id))
            name = entry.Id;

        if (string.IsNullOrEmpty(name))
            return "<no name>";

        // If looks like a GUID or pure hex id, shorten/mask.
        if (LooksLikeGuid(name))
        {
            return "Player " + name.Substring(0, 8);
        }

        return name;
    }

    private bool LooksLikeGuid(string s)
    {
        if (s.Length == 36)
        {
            Guid g;
            return Guid.TryParse(s, out g);
        }
        // Hex-only long strings (>=24)
        if (s.Length >= 24)
        {
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool hex =
                    (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex)
                    return false;
            }
            return true;
        }
        return false;
    }

    // --- Wrapping Helper (avoids compile-time dependency on newer TMP enums) ---
    private static bool _wrapChecked;
    private static System.Reflection.PropertyInfo _wrappingProp;
    private static object _noWrapEnumValue;

    private static void SetNoWrap(TMP_Text text)
    {
        if (!_wrapChecked)
        {
            _wrapChecked = true;
            _wrappingProp = typeof(TMP_Text).GetProperty("textWrappingMode");
            if (_wrappingProp != null)
            {
                var enumType = _wrappingProp.PropertyType; // TMPro.TextWrappingMode
                try
                {
                    _noWrapEnumValue = System.Enum.Parse(enumType, "NoWrap");
                }
                catch
                {
                    _wrappingProp = null;
                }
            }
        }

        if (_wrappingProp != null && _noWrapEnumValue != null)
        {
            // Use modern API via reflection
            try
            {
                _wrappingProp.SetValue(text, _noWrapEnumValue);
            }
            catch
            {
                FallbackLegacy(text);
            }
        }
        else
        {
            FallbackLegacy(text);
        }
    }

    private static void FallbackLegacy(TMP_Text text)
    {
#pragma warning disable 618
        text.enableWordWrapping = false; // legacy path
#pragma warning restore 618
    }

    /// <summary>
    /// Adjust parent VerticalLayoutGroup (if present) so header sits at top and rows flow directly beneath.
    /// </summary>
    private void ConfigureParentLayoutGroup()
    {
        if (!entriesParent)
            return;
        var vlg = entriesParent.GetComponent<VerticalLayoutGroup>();
        if (vlg)
        {
            // Disable automatic layout so we can manually stack to avoid header overlap issues.
            vlg.enabled = false;
        }
        var contentSizeFitter = entriesParent.GetComponent<ContentSizeFitter>();
        if (contentSizeFitter)
        {
            // Typical ScrollView Content: vertical fit preferred size
            contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }
}
