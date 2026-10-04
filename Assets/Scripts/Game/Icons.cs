using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI icons from Resources/UI/Icons (rank insignia, tokens, classes, attachment slots, vehicles…),
/// loaded once and turned into sprites.
/// </summary>
public static class Icons
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    /// <summary>Sprite for Resources/UI/Icons/&lt;name&gt;.png, or null if it is missing.</summary>
    public static Sprite Get(string name)
    {
        Sprite s;
        if (cache.TryGetValue(name, out s))
            return s;
        var tex = Resources.Load<Texture2D>("UI/Icons/" + name);
        if (tex != null)
        {
            tex.wrapMode = TextureWrapMode.Clamp;
            s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            s.name = name;
        }
        cache[name] = s;
        return s;
    }

    /// <summary>An Image showing an icon (keeps its aspect ratio). Falls back to a plain tinted box.</summary>
    public static UnityEngine.UI.Image Create(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var img = UIUtil.CreateImage(parent, "Icon_" + name, anchor, pos, size, Color.white, false);
        img.raycastTarget = false;
        var s = Get(name);
        if (s != null)
        {
            img.sprite = s;
            img.preserveAspect = true;
        }
        else
            img.color = new Color(1f, 1f, 1f, 0.15f);
        return img;
    }

    public static void Set(UnityEngine.UI.Image img, string name)
    {
        if (img == null)
            return;
        var s = Get(name);
        img.sprite = s;
        img.preserveAspect = true;
        img.color = s != null ? Color.white : new Color(1f, 1f, 1f, 0.15f);
    }

    public static string Rank(int rankIndex) { return "rank_" + rankIndex.ToString("00"); }
}
