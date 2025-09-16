using UnityEngine;

/// Utility methods for converting hex color strings into Unity Color values and applying them to renderers.
/// Supported formats:
/// - #RRGGBB
/// - #RRGGBBAA
/// - RRGGBB / RRGGBBAA (leading # optional)
/// - #RGB / #RGBA (shorthand, expands to #RRGGBB / #RRGGBBAA)
/// - RGB / RGBA (leading # optional)
public static class ColorUtils
{
    /// Try parse hex into a Unity Color. Returns false if invalid.
    public static bool TryParse(string hex, out Color color)
    {
        color = Color.white;
        string norm = NormalizeHex(hex);
        if (string.IsNullOrEmpty(norm))
            return false;
        return ColorUtility.TryParseHtmlString(norm, out color);
    }

    /// Try parse hex into a Unity Color32. Returns false if invalid.
    public static bool TryParse32(string hex, out Color32 color)
    {
        color = new Color32(255, 255, 255, 255);
        if (TryParse(hex, out var c))
        {
            color = (Color32)c;
            return true;
        }
        return false;
    }

    /// Parse hex string or return the provided default color if invalid.
    public static Color ParseOrDefault(string hex, Color defaultColor)
    {
        return TryParse(hex, out var c) ? c : defaultColor;
    }

    /// Apply hex color to a SpriteRenderer if valid. Returns true if applied.
    public static bool TryApply(SpriteRenderer sr, string hex)
    {
        if (sr == null)
            return false;
        if (TryParse(hex, out var c))
        {
            sr.color = c;
            return true;
        }
        return false;
    }

    /// Apply hex color to a SpriteRenderer; if invalid, use defaultColor.
    public static void ApplyOrDefault(SpriteRenderer sr, string hex, Color defaultColor)
    {
        if (sr == null)
            return;
        sr.color = ParseOrDefault(hex, defaultColor);
    }

    /// Returns a copy of color with the specified alpha (0..1).
    public static Color WithAlpha(Color c, float a)
    {
        c.a = Mathf.Clamp01(a);
        return c;
    }

    /// Normalize various hex formats to #RRGGBB or #RRGGBBAA for ColorUtility.
    private static string NormalizeHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            return null;
        string s = hex.Trim();
        if (s[0] != '#')
            s = "#" + s;
        // Remove any non-hex chars beyond leading #
        // Accept only lengths 4, 5, 7, 9 (including '#')
        if (s.Length == 4 || s.Length == 5)
        {
            // Expand #RGB or #RGBA -> #RRGGBB or #RRGGBBAA
            char r = s[1];
            char g = s[2];
            char b = s[3];
            if (s.Length == 4)
            {
                s = $"#{r}{r}{g}{g}{b}{b}";
            }
            else // 5
            {
                char a = s[4];
                s = $"#{r}{r}{g}{g}{b}{b}{a}{a}";
            }
        }
        if (s.Length == 7 || s.Length == 9)
            return s;
        // Unsupported length -> invalid
        return null;
    }
}
