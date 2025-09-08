using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds a skins grid programmatically at runtime (no prefabs).
/// Attach this to a RectTransform under a Canvas; it will create a GridLayoutGroup on itself
/// and populate child items when SkinsService provides data.
/// </summary>
[ExecuteAlways]
public class ProgrammaticSkinsGrid : MonoBehaviour
{
    private const string LOG_TAG = "[SkinsGrid]";

    [Header("Layout / Auto Fit")]
    [Min(1)]
    public int columns = 3; // how many cells per row
    public Vector2 spacing = new Vector2(12, 12);

    [Tooltip("Padding applied to the Grid container (pixels).")]
    public float paddingLeft = 12;
    public float paddingRight = 12;
    public float paddingTop = 12;
    public float paddingBottom = 12;

    [Tooltip("If true, cells are square. If false, height is derived from width and Cell Aspect.")]
    public bool squareCells = true;

    [Tooltip("Width / Height ratio when not using square cells (e.g., 1.78 for 16:9).")]
    public float cellAspect = 1f;

    [Header("Item Appearance")]
    [Range(0.2f, 0.9f)]
    public float swatchHeightRatio = 0.65f;
    public float borderThickness = 2f;
    public float priceFontSize = 20f;
    public TMP_FontAsset priceFont; // optional; if null uses default TMP font

    [Header("Background")]
    public Color backgroundColor = new Color32(240, 240, 240, 255);

    [Header("State Label")]
    public bool showStateLabel = true;
    public float stateLabelFontSize = 16f;
    public TMP_FontAsset stateLabelFont; // optional; if null uses default TMP font
    public FontStyles stateLabelFontStyle = FontStyles.Normal;

    [Tooltip("Extra vertical space between the price and state label (pixels).")]
    public float priceToStateLabelPadding = 4f;

    [Tooltip("Padding between the bottom of the cell and the state label (pixels).")]
    public float labelBottomPadding = 0f;

    [Tooltip("Vertical gap between the divider and the price (pixels).")]
    public float priceFromDividerPadding = 2f;

    [Header("Divider")]
    public bool showDivider = true;
    public float dividerThickness = 1f;
    public float dividerHorizontalPadding = 6f;
    public Color dividerColor = new Color(0f, 0f, 0f, 0.2f);

    [Header("Border Colors")]
    public Color notOwnedBorder = new Color32(180, 60, 60, 255);
    public Color ownedBorder = new Color32(80, 160, 240, 255);
    public Color activeBorder = new Color32(60, 200, 120, 255);

    private GridLayoutGroup _grid;
    private Vector2 _lastComputedCellSize;
    private readonly List<GameObject> _spawned = new();
    private readonly Dictionary<string, ProgrammaticSkinItemState> _itemsBySkinId = new();
    private ProgrammaticSkinItemState _activeItem;

    private void Awake()
    {
        EnsureGrid();
        ApplyGridSettings();
    }

    private void OnEnable()
    {
        if (SkinsService.Instance != null)
        {
            SkinsService.Instance.OnSkinsUpdated += Rebuild;
            Debug.Log(LOG_TAG + " Subscribed to SkinsService.OnSkinsUpdated");
        }
        if (UserAssetsManager.Instance != null)
        {
            UserAssetsManager.Instance.OnAssetsUpdated += Rebuild;
            Debug.Log(LOG_TAG + " Subscribed to UserAssetsManager.OnAssetsUpdated");
        }
        ApplyGridSettings();
        if (Application.isPlaying)
        {
            // If we have a user but no owned skins loaded yet, request them now (no blocking spinner).
            if (PlayerManager.Instance != null)
            {
                var userId = PlayerManager.Instance.GetUserId();
                var owned = PlayerManager.Instance.GetOwnedSkins();
                if (!string.IsNullOrEmpty(userId) && owned == null)
                {
                    Debug.Log(LOG_TAG + " Owned skins not loaded yet; requesting UserAssets...");
                    if (UserAssetsManager.Instance != null)
                        UserAssetsManager.Instance.TryFetchUserAssets();
                }
                else
                {
                    Debug.Log(LOG_TAG + " Owned skins already present or userId missing.");
                }
            }
            Rebuild();
        }
    }

    private void OnDisable()
    {
        if (SkinsService.Instance != null)
        {
            SkinsService.Instance.OnSkinsUpdated -= Rebuild;
        }
        if (UserAssetsManager.Instance != null)
        {
            UserAssetsManager.Instance.OnAssetsUpdated -= Rebuild;
        }
        if (Application.isPlaying)
        {
            Clear();
        }
    }

    private void EnsureGrid()
    {
        _grid = GetComponent<GridLayoutGroup>();
        if (_grid == null)
        {
            _grid = gameObject.AddComponent<GridLayoutGroup>();
        }
        // Static settings
        _grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        _grid.childAlignment = TextAnchor.UpperLeft;
        _grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _grid.constraintCount = Mathf.Max(1, columns);
    }

    private void ApplyGridSettings()
    {
        EnsureGrid();

        // Update constraint in case columns changed
        _grid.constraintCount = Mathf.Max(1, columns);

        // Spacing
        _grid.spacing = spacing;

        // Padding
        _grid.padding = new RectOffset(
            Mathf.Max(0, Mathf.RoundToInt(paddingLeft)),
            Mathf.Max(0, Mathf.RoundToInt(paddingRight)),
            Mathf.Max(0, Mathf.RoundToInt(paddingTop)),
            Mathf.Max(0, Mathf.RoundToInt(paddingBottom))
        );

        // Compute cell size to fit container width
        var rt = transform as RectTransform;
        if (rt == null)
            return;
        var rect = rt.rect;
        float totalWidth = rect.width;
        if (totalWidth <= 0)
            return;

        float availWidth = totalWidth - _grid.padding.left - _grid.padding.right;
        float totalSpacingX = spacing.x * Mathf.Max(0, columns - 1);
        float cellW = (availWidth - totalSpacingX) / Mathf.Max(1, columns);
        cellW = Mathf.Max(0, cellW);
        float cellH;
        if (squareCells)
        {
            cellH = cellW;
        }
        else
        {
            float aspect = Mathf.Max(0.01f, cellAspect);
            // width / height = aspect => height = width / aspect
            cellH = cellW / aspect;
        }

        var computed = new Vector2(cellW, cellH);
        _grid.cellSize = computed;
        _lastComputedCellSize = computed;
    }

    public void Rebuild()
    {
        Debug.Log(LOG_TAG + " Rebuild() start");
        ApplyGridSettings();
        Clear();
        // Ensure data readiness:
        if (SkinsService.Instance == null)
        {
            Debug.LogWarning(LOG_TAG + " SkinsService.Instance is null; aborting rebuild.");
            return;
        }
        var skins = SkinsService.Instance.Skins;
        Debug.Log(LOG_TAG + $" Skins available: {(skins != null ? skins.Count : 0)}");
        if (skins == null || skins.Count == 0)
        {
            // Waiting for skins payload
            if (SkinShopManager.Instance != null)
                SkinShopManager.Instance.ShowLoading("Loading Shop");
            Debug.Log(LOG_TAG + " No skins to display yet; showing loading (if present).");
            return;
        }

        var activeSkinId = PlayerManager.Instance?.GetSavedSkinId();
        var ownedArr = PlayerManager.Instance?.GetOwnedSkins();
        var userId = PlayerManager.Instance?.GetUserId();
        Debug.Log(
            LOG_TAG
                + $" userId={(string.IsNullOrEmpty(userId) ? "<none>" : userId)} activeSkinId={(string.IsNullOrEmpty(activeSkinId) ? "<none>" : activeSkinId)} ownedCount={(ownedArr == null ? 0 : ownedArr.Length)}"
        );
        if (ownedArr != null && ownedArr.Length > 0)
        {
            for (int i = 0; i < ownedArr.Length; i++)
            {
                Debug.Log(LOG_TAG + $" owned[{i}]={ownedArr[i]}");
            }
        }
        HashSet<string> ownedSet = null;
        if (ownedArr != null && ownedArr.Length > 0)
        {
            ownedSet = new HashSet<string>(ownedArr);
        }
        foreach (var s in skins)
        {
            bool owned = ownedSet != null && ownedSet.Contains(s.SkinId);
            bool activeMatch = !string.IsNullOrEmpty(activeSkinId) && s.SkinId == activeSkinId;
            var initialState = ResolveOwnershipState(s.SkinId, activeSkinId, ownedSet);
            Debug.Log(
                LOG_TAG
                    + $" Skin={s.SkinId} price={s.Price} owned={owned} activeMatch={activeMatch} -> state={initialState}"
            );
            var itemGO = CreateItem(s, initialState);
            Debug.Log(LOG_TAG + $" Created item for {s.SkinId} with state {initialState}");
            _spawned.Add(itemGO);
        }

        // No loading modal here; grid renders immediately and will update if assets arrive later.
    }

    private GameObject CreateItem(SkinsService.SkinItem skin, SkinOwnershipState state)
    {
        // Root
        var itemGO = new GameObject($"SkinItem_{skin.SkinId}", typeof(RectTransform));
        itemGO.transform.SetParent(transform, false);
        var rt = (RectTransform)itemGO.transform;
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0.5f, 0.5f);
        // Let GridLayoutGroup control size; keep sizeDelta in sync for clarity
        rt.sizeDelta = _grid != null ? _grid.cellSize : _lastComputedCellSize;

        // Background/Border
        var bg = itemGO.AddComponent<Image>();
        var outline = itemGO.AddComponent<Outline>();
        outline.useGraphicAlpha = false;
        outline.effectDistance = new Vector2(borderThickness, -borderThickness);
        bg.color = backgroundColor;
        // Add button for interaction
        var btn = itemGO.AddComponent<Button>();
        btn.targetGraphic = bg;

        // Inner container (for swatch + price)
        var inner = new GameObject("Inner", typeof(RectTransform));
        inner.transform.SetParent(itemGO.transform, false);
        var innerRt = (RectTransform)inner.transform;
        innerRt.anchorMin = new Vector2(0, 0);
        innerRt.anchorMax = new Vector2(1, 1);
        innerRt.offsetMin = new Vector2(6, 6);
        innerRt.offsetMax = new Vector2(-6, -6);

        // Swatch
        var swatch = new GameObject("Swatch", typeof(RectTransform));
        swatch.transform.SetParent(inner.transform, false);
        var swRt = (RectTransform)swatch.transform;
        swRt.anchorMin = new Vector2(0, 1);
        swRt.anchorMax = new Vector2(1, 1);
        swRt.pivot = new Vector2(0.5f, 1);
        float baseCellH = (_grid != null ? _grid.cellSize.y : _lastComputedCellSize.y);
        // Reserve space for bottom UI: label bottom padding + label + gap + price + (divider + gap)
        float priceHeight = Mathf.Ceil(priceFontSize + 6f);
        float stateHeight = showStateLabel ? Mathf.Ceil(stateLabelFontSize + 6f) : 0f;
        float labelBottomGap = showStateLabel ? Mathf.Max(0f, labelBottomPadding) : 0f;
        float priceLabelGap = showStateLabel ? Mathf.Max(0f, priceToStateLabelPadding) : 0f;
        float dividerH = showDivider ? Mathf.Max(1f, dividerThickness) : 0f;
        float dividerGap = showDivider ? Mathf.Max(0f, priceFromDividerPadding) : 0f;
        float reservedBottom =
            labelBottomGap + stateHeight + priceLabelGap + priceHeight + dividerGap + dividerH;
        float availableForSwatch = Mathf.Max(0f, baseCellH - 12f - reservedBottom);
        float swatchHeight = Mathf.Clamp01(swatchHeightRatio) * availableForSwatch;
        swRt.sizeDelta = new Vector2(0, swatchHeight);
        swRt.anchoredPosition = new Vector2(0, 0);
        var swImg = swatch.AddComponent<Image>();
        var hex = skin.HexValue;
        if (!string.IsNullOrEmpty(hex) && !hex.StartsWith("#"))
            hex = "#" + hex;
        if (ColorUtility.TryParseHtmlString(hex, out var col))
            swImg.color = col;
        else
            swImg.color = Color.gray;

        // Divider between swatch and bottom text blocks
        if (showDivider)
        {
            var divider = new GameObject("Divider", typeof(RectTransform));
            divider.transform.SetParent(inner.transform, false);
            var dRt = (RectTransform)divider.transform;
            dRt.anchorMin = new Vector2(0, 0);
            dRt.anchorMax = new Vector2(1, 0);
            dRt.pivot = new Vector2(0.5f, 0);
            dRt.sizeDelta = new Vector2(0, dividerH);
            dRt.anchoredPosition = new Vector2(
                0,
                labelBottomGap + stateHeight + priceLabelGap + priceHeight + dividerGap
            );
            dRt.offsetMin = new Vector2(dividerHorizontalPadding, dRt.offsetMin.y);
            dRt.offsetMax = new Vector2(-dividerHorizontalPadding, dRt.offsetMax.y);
            var dImg = divider.AddComponent<Image>();
            dImg.color = dividerColor;
        }

        // Price (bottom, above state label if present)
        var price = new GameObject("Price", typeof(RectTransform));
        price.transform.SetParent(inner.transform, false);
        var prRt = (RectTransform)price.transform;
        prRt.anchorMin = new Vector2(0, 0);
        prRt.anchorMax = new Vector2(1, 0);
        prRt.pivot = new Vector2(0.5f, 0);
        prRt.sizeDelta = new Vector2(0, priceHeight);
        prRt.anchoredPosition = new Vector2(0, labelBottomGap + stateHeight + priceLabelGap);
        var tmp = price.AddComponent<TextMeshProUGUI>();
        if (priceFont != null)
            tmp.font = priceFont;
        tmp.fontSize = priceFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text = skin.Price.ToString();
        tmp.textWrappingMode = TextWrappingModes.NoWrap;

        // State label (bottom, centered)
        if (showStateLabel)
        {
            var labelGO = new GameObject("StateLabel", typeof(RectTransform));
            labelGO.transform.SetParent(inner.transform, false);
            var labRt = (RectTransform)labelGO.transform;
            labRt.anchorMin = new Vector2(0, 0);
            labRt.anchorMax = new Vector2(1, 0);
            labRt.pivot = new Vector2(0.5f, 0);
            labRt.sizeDelta = new Vector2(0, stateHeight);
            labRt.anchoredPosition = new Vector2(0, labelBottomGap);
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            if (stateLabelFont != null)
                label.font = stateLabelFont;
            label.fontSize = stateLabelFontSize;
            label.fontStyle = stateLabelFontStyle;
            label.alignment = TextAlignmentOptions.Center;
            // text/color set after state component is assigned
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        // Attach local state component and initialize
        var stateComp = itemGO.AddComponent<ProgrammaticSkinItemState>();
        stateComp.skinId = skin.SkinId;
        stateComp.outline = outline;
        stateComp.background = bg;
        stateComp.stateLabel = showStateLabel
            ? inner.transform.Find("StateLabel").GetComponent<TextMeshProUGUI>()
            : null;
        _itemsBySkinId[skin.SkinId] = stateComp;
        SetItemState(stateComp, state);

        // Track active reference
        if (state == SkinOwnershipState.Active)
            _activeItem = stateComp;

        // Click behavior (UI-only)
        btn.onClick.AddListener(() => HandleItemClicked(stateComp));

        return itemGO;
    }

    private Color StateToBorderColor(SkinOwnershipState state)
    {
        switch (state)
        {
            case SkinOwnershipState.Active:
                return activeBorder;
            case SkinOwnershipState.Owned:
                return ownedBorder;
            default:
                return notOwnedBorder;
        }
    }

    private string StateToLabel(SkinOwnershipState state)
    {
        switch (state)
        {
            case SkinOwnershipState.Active:
                return "Active";
            case SkinOwnershipState.Owned:
                return "Owned";
            default:
                return "Not Owned";
        }
    }

    private SkinOwnershipState ResolveOwnershipState(
        string skinId,
        string activeSkinId,
        HashSet<string> ownedSet
    )
    {
        if (!string.IsNullOrEmpty(activeSkinId) && skinId == activeSkinId)
            return SkinOwnershipState.Active;
        if (ownedSet != null && ownedSet.Contains(skinId))
            return SkinOwnershipState.Owned;
        return SkinOwnershipState.NotOwned;
    }

    private void SetItemState(ProgrammaticSkinItemState item, SkinOwnershipState state)
    {
        item.isActive = state == SkinOwnershipState.Active;
        item.isOwned = state == SkinOwnershipState.Owned || state == SkinOwnershipState.Active;
        item.isNotOwned = state == SkinOwnershipState.NotOwned;
        Debug.Log(
            LOG_TAG
                + $" SetItemState skinId={item.skinId} -> isOwned={item.isOwned} isActive={item.isActive} isNotOwned={item.isNotOwned}"
        );

        var color = StateToBorderColor(state);
        if (item.outline != null)
            item.outline.effectColor = color;
        if (item.stateLabel != null)
        {
            item.stateLabel.text = StateToLabel(state);
            item.stateLabel.color = color;
        }
    }

    private void HandleItemClicked(ProgrammaticSkinItemState clicked)
    {
        // Send API requests according to clicked state
        string userId = PlayerManager.Instance != null ? PlayerManager.Instance.GetUserId() : null;
        if (string.IsNullOrEmpty(userId))
        {
            SetErrorText("No userId available.");
            return;
        }

        if (clicked.isNotOwned)
        {
            // Show loading modal for purchase
            if (SkinShopManager.Instance != null)
                SkinShopManager.Instance.ShowLoading("Processing Purchase");
            // Buy skin
            StartCoroutine(
                ShopApiClient.BuySkin(
                    userId,
                    clicked.skinId,
                    (res, err) =>
                    {
                        // Always hide loading when request completes
                        if (SkinShopManager.Instance != null)
                            SkinShopManager.Instance.HideLoading();
                        if (err != null)
                        {
                            SetErrorText(err);
                            return;
                        }
                        if (res.Status == "Bad")
                        {
                            SetErrorText(
                                string.IsNullOrEmpty(res.Message) ? "Purchase failed" : res.Message
                            );
                            return;
                        }
                        // Ok → mark as owned and refresh points/owned from server
                        SetItemState(clicked, SkinOwnershipState.Owned);
                        ClearErrorText();
                        if (UserAssetsManager.Instance != null)
                        {
                            UserAssetsManager.Instance.TryFetchUserAssets();
                        }
                    }
                )
            );
            return;
        }
        if (clicked.isOwned && !clicked.isActive)
        {
            // Show loading modal for equipping
            if (SkinShopManager.Instance != null)
                SkinShopManager.Instance.ShowLoading("Equipping Skin");
            // Set active
            StartCoroutine(
                ShopApiClient.SetActiveSkin(
                    userId,
                    clicked.skinId,
                    (res, err) =>
                    {
                        if (err != null)
                        {
                            if (SkinShopManager.Instance != null)
                                SkinShopManager.Instance.HideLoading();
                            SetErrorText(err);
                            return;
                        }
                        if (res.Status == "Bad")
                        {
                            if (SkinShopManager.Instance != null)
                                SkinShopManager.Instance.HideLoading();
                            SetErrorText(
                                string.IsNullOrEmpty(res.Message) ? "Equip failed" : res.Message
                            );
                            return;
                        }
                        // Ok -> make active locally
                        if (_activeItem != null && _activeItem != clicked)
                        {
                            SetItemState(
                                _activeItem,
                                _activeItem.isOwned
                                    ? SkinOwnershipState.Owned
                                    : SkinOwnershipState.NotOwned
                            );
                        }
                        _activeItem = clicked;
                        SetItemState(clicked, SkinOwnershipState.Active);
                        ClearErrorText();
                        // Update local active skin persistence from response if available
                        if (PlayerManager.Instance != null)
                        {
                            PlayerManager.Instance.SetActiveSkin(res.SkinId, res.HexValue);
                        }
                        // Follow-up GET to refresh active skin from server
                        StartCoroutine(
                            ShopApiClient.GetActiveSkin(
                                userId,
                                (res2, err2) =>
                                {
                                    // Hide loading after follow-up completes
                                    if (SkinShopManager.Instance != null)
                                        SkinShopManager.Instance.HideLoading();
                                    if (err2 != null)
                                    {
                                        SetErrorText(err2);
                                        return;
                                    }
                                    if (res2.Status == "Bad")
                                    {
                                        SetErrorText(
                                            string.IsNullOrEmpty(res2.Message)
                                                ? "Active skin check failed"
                                                : res2.Message
                                        );
                                        return;
                                    }
                                    // Update PlayerManager with latest values
                                    if (PlayerManager.Instance != null)
                                    {
                                        PlayerManager.Instance.SetActiveSkin(
                                            res2.SkinId,
                                            res2.HexValue
                                        );
                                    }
                                }
                            )
                        );
                    }
                )
            );
        }
        // If already active, no-op.
    }

    private void SetErrorText(string msg)
    {
        if (SkinShopManager.Instance != null && SkinShopManager.Instance.errorText != null)
        {
            SkinShopManager.Instance.errorText.text = msg;
        }
    }

    private void ClearErrorText()
    {
        if (SkinShopManager.Instance != null && SkinShopManager.Instance.errorText != null)
        {
            SkinShopManager.Instance.errorText.text = string.Empty;
        }
    }

    private void Clear()
    {
        foreach (var go in _spawned)
        {
            if (go != null)
                Destroy(go);
        }
        _spawned.Clear();
        _itemsBySkinId.Clear();
        _activeItem = null;
    }

    private void OnRectTransformDimensionsChange()
    {
        // Live update when the container is resized
        ApplyGridSettings();
    }

    private void OnValidate()
    {
        // Live update in the editor when values change
        columns = Mathf.Max(1, columns);
        cellAspect = Mathf.Max(0.01f, cellAspect);
        ApplyGridSettings();
    }
}
