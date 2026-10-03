using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared look for the menus: dark translucent panels with a yellow accent edge.</summary>
public static class Theme
{
    public static readonly Color Panel = new Color(0.07f, 0.08f, 0.1f, 0.82f);
    public static readonly Color PanelLight = new Color(0.16f, 0.18f, 0.22f, 0.9f);
    public static readonly Color Accent = new Color(1f, 0.82f, 0.18f, 1f);
    public static readonly Color AccentDark = new Color(0.55f, 0.42f, 0.05f, 1f);
    public static readonly Color Red = new Color(0.86f, 0.22f, 0.2f, 0.95f);
    public static readonly Color Selected = new Color(0.95f, 0.95f, 0.95f, 0.95f);
    public static readonly Color TextDim = new Color(1f, 1f, 1f, 0.65f);
    public static readonly Color Good = new Color(0.35f, 0.9f, 0.45f, 1f);
    public static readonly Color Bad = new Color(0.95f, 0.3f, 0.25f, 1f);

    public static Color Rarity(string rarity)
    {
        switch (rarity)
        {
            case "Nadir": return new Color(0.25f, 0.55f, 1f);
            case "Epik": return new Color(0.7f, 0.35f, 1f);
            case "Efsanevi": return new Color(1f, 0.6f, 0.15f);
            case "Mitik": return new Color(1f, 0.25f, 0.3f);
            default: return new Color(0.6f, 0.62f, 0.66f);
        }
    }

    /// <summary>Panel with an optional accent strip on its left edge.</summary>
    public static Image Box(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color, bool accent)
    {
        var img = UIUtil.CreateImage(parent, name, anchor, pos, size, color, false);
        if (accent)
        {
            var strip = UIUtil.CreateImage(img.transform, "Accent", new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(8f, size.y), Accent, false);
            strip.raycastTarget = false;
        }
        return img;
    }

    /// <summary>Left-aligned text inside a box (with padding).</summary>
    public static Text Label(Transform parent, string text, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color color, bool bold)
    {
        var t = UIUtil.CreateText(parent, text, new Vector2(0.5f, 0.5f), pos, size, fontSize, align);
        t.color = color;
        if (bold)
            t.fontStyle = FontStyle.Bold;
        return t;
    }
}
