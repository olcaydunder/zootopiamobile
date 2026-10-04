using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// The player's custom on-screen button layout: an offset from the default position and a size
/// multiplier for each touch control, saved in PlayerPrefs ("zm_hud_layout").
/// Missing entries mean "default", so new buttons added later keep working with old saves.
/// </summary>
public static class HudLayout
{
    public const string PrefKey = "zm_hud_layout";
    public const float MinScale = 0.6f;
    public const float MaxScale = 1.6f;

    public struct Entry
    {
        public Vector2 offset;
        public float scale;
    }

    private static Dictionary<string, Entry> entries;

    private static Dictionary<string, Entry> All
    {
        get
        {
            if (entries == null)
                Load();
            return entries;
        }
    }

    public static Entry Get(string key)
    {
        Entry e;
        if (All.TryGetValue(key, out e))
            return e;
        e.offset = Vector2.zero;
        e.scale = 1f;
        return e;
    }

    public static void Set(string key, Vector2 offset, float scale)
    {
        scale = Mathf.Clamp(scale, MinScale, MaxScale);
        if (offset.sqrMagnitude < 0.25f && Mathf.Abs(scale - 1f) < 0.01f)
        {
            All.Remove(key);
            return;
        }
        Entry e;
        e.offset = offset;
        e.scale = scale;
        All[key] = e;
    }

    public static void ResetAll()
    {
        All.Clear();
    }

    public static void Load()
    {
        entries = new Dictionary<string, Entry>();
        string raw = PlayerPrefs.GetString(PrefKey, "");
        if (string.IsNullOrEmpty(raw))
            return;
        foreach (string part in raw.Split(';'))
        {
            int eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            string[] v = part.Substring(eq + 1).Split(',');
            float x, y, s;
            if (v.Length != 3 ||
                !float.TryParse(v[0], NumberStyles.Float, CultureInfo.InvariantCulture, out x) ||
                !float.TryParse(v[1], NumberStyles.Float, CultureInfo.InvariantCulture, out y) ||
                !float.TryParse(v[2], NumberStyles.Float, CultureInfo.InvariantCulture, out s))
                continue;
            Entry e;
            e.offset = new Vector2(x, y);
            e.scale = Mathf.Clamp(s, MinScale, MaxScale);
            entries[part.Substring(0, eq)] = e;
        }
    }

    public static void Save()
    {
        var sb = new StringBuilder();
        foreach (var kv in All)
        {
            if (sb.Length > 0)
                sb.Append(';');
            sb.Append(kv.Key).Append('=')
              .Append(kv.Value.offset.x.ToString("0.#", CultureInfo.InvariantCulture)).Append(',')
              .Append(kv.Value.offset.y.ToString("0.#", CultureInfo.InvariantCulture)).Append(',')
              .Append(kv.Value.scale.ToString("0.##", CultureInfo.InvariantCulture));
        }
        PlayerPrefs.SetString(PrefKey, sb.ToString());
        PlayerPrefs.Save();
    }
}
