using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders skins in a grid using a prefab item.
/// Attach this to a GameObject with a GridLayoutGroup, and assign itemPrefab.
/// </summary>
public class SkinsGridView : MonoBehaviour
{
    [Header("UI")]
    public GridLayoutGroup grid;
    public SkinsGridItem itemPrefab;

    [Header("Ownership")]
    public Color notOwnedBorder = new Color32(180, 60, 60, 255);
    public Color ownedBorder = new Color32(80, 160, 240, 255);
    public Color activeBorder = new Color32(60, 200, 120, 255);

    private readonly List<SkinsGridItem> _items = new();

    private void Awake()
    {
        if (grid == null)
            grid = GetComponent<GridLayoutGroup>();
    }

    private void OnEnable()
    {
        if (SkinsService.Instance != null)
        {
            SkinsService.Instance.OnSkinsUpdated += Rebuild;
        }
        Rebuild();
    }

    private void OnDisable()
    {
        if (SkinsService.Instance != null)
        {
            SkinsService.Instance.OnSkinsUpdated -= Rebuild;
        }
    }

    public void Rebuild()
    {
        if (itemPrefab == null || SkinsService.Instance == null)
            return;

        Clear();
        var skins = SkinsService.Instance.Skins;
        if (skins == null || skins.Count == 0)
            return;

        var activeSkinId = PlayerManager.Instance?.GetSavedSkinId();
        foreach (var s in skins)
        {
            var item = Instantiate(itemPrefab, grid != null ? grid.transform : transform);
            item.notOwnedBorder = notOwnedBorder;
            item.ownedBorder = ownedBorder;
            item.activeBorder = activeBorder;

            var state = ResolveOwnershipState(s.SkinId, activeSkinId);
            item.Configure(s, state);
            _items.Add(item);
        }
    }

    private SkinOwnershipState ResolveOwnershipState(string skinId, string activeSkinId)
    {
        // Placeholder ownership logic:
        // - If skinId == activeSkinId => Active
        // - Else if Player has skin (not implemented; treat as NotOwned unless it's active)
        if (!string.IsNullOrEmpty(activeSkinId) && skinId == activeSkinId)
            return SkinOwnershipState.Active;

        // TODO: integrate real ownership from your inventory; for now mark others as NotOwned.
        return SkinOwnershipState.NotOwned;
    }

    private void Clear()
    {
        foreach (var it in _items)
        {
            if (it != null)
                Destroy(it.gameObject);
        }
        _items.Clear();
    }
}
