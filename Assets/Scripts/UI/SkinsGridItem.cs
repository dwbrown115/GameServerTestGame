using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum SkinOwnershipState
{
    NotOwned,
    Owned,
    Active,
}

/// <summary>
/// Visualizes a single skin item: border, color swatch, and price text.
/// </summary>
public class SkinsGridItem : MonoBehaviour
{
    [Header("Refs")]
    public Image borderImage; // Optional: solid border image
    public Outline borderOutline; // Optional: outline effect as border
    public Image colorSwatch;
    public TMP_Text priceText;

    [Header("Colors")]
    public Color notOwnedBorder = new Color32(180, 60, 60, 255);
    public Color ownedBorder = new Color32(80, 160, 240, 255);
    public Color activeBorder = new Color32(60, 200, 120, 255);

    public void Configure(SkinsService.SkinItem skin, SkinOwnershipState state)
    {
        // Color swatch
        if (colorSwatch != null)
        {
            var hex = skin.HexValue;
            if (!string.IsNullOrEmpty(hex) && !hex.StartsWith("#"))
                hex = "#" + hex;
            if (ColorUtility.TryParseHtmlString(hex, out var col))
            {
                colorSwatch.color = col;
            }
            else
            {
                colorSwatch.color = Color.gray; // fallback
            }
        }

        // Price
        if (priceText != null)
        {
            priceText.text = skin.Price.ToString();
        }

        // Border color
        var c = state switch
        {
            SkinOwnershipState.Active => activeBorder,
            SkinOwnershipState.Owned => ownedBorder,
            _ => notOwnedBorder,
        };
        if (borderImage != null)
            borderImage.color = c;
        if (borderOutline != null)
            borderOutline.effectColor = c;
    }
}
