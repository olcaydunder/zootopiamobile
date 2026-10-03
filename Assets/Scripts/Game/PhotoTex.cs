using System.Collections.Generic;
using UnityEngine;

/// <summary>Photo-scanned CC0 textures from Poly Haven (Resources/Textures, see ASSETS.md).</summary>
public static class PhotoTex
{
    private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

    /// <summary>name like "asphalt" + "_diff" or "_nor". Null if missing.</summary>
    public static Texture2D Get(string name)
    {
        Texture2D t;
        if (!cache.TryGetValue(name, out t))
        {
            t = Resources.Load<Texture2D>("Textures/" + name);
            cache[name] = t;
        }
        return t;
    }

    public static bool Available
    {
        get { return Get("asphalt_diff") != null && Get("grass_diff") != null && Get("plaster_diff") != null; }
    }
}
