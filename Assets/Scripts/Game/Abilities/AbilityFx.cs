using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared bits for ability objects: translucent materials, a quick expanding flash, and a registry so
/// everything an ability spawned is cleared at the end of a round.
/// </summary>
public static class AbilityFx
{
    private static readonly Dictionary<Color, Material> glass = new Dictionary<Color, Material>();
    private static readonly List<GameObject> spawned = new List<GameObject>();

    /// <summary>Unlit, see-through material (Sprites/Default) in the given colour.</summary>
    public static Material Glass(Color c)
    {
        Material m;
        if (!glass.TryGetValue(c, out m) || m == null)
        {
            m = UIUtil.UnlitMaterial(c);
            glass[c] = m;
        }
        return m;
    }

    public static void Track(GameObject go)
    {
        spawned.Add(go);
    }

    public static void ClearAll()
    {
        foreach (var go in spawned)
            if (go != null)
                Object.Destroy(go);
        spawned.Clear();
    }

    public static Transform Primitive(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    /// <summary>A sphere that grows and fades in <paramref name="seconds"/> (teleport, impacts).</summary>
    public static void Flash(Vector3 pos, Color color, float size, float seconds)
    {
        var go = new GameObject("AbilityFlash");
        go.transform.position = pos;
        var f = go.AddComponent<FlashFx>();
        f.Init(color, size, seconds);
        Track(go);
    }
}

/// <summary>Expanding, fading sphere.</summary>
public class FlashFx : MonoBehaviour
{
    private Material mat;
    private Color color;
    private float size, life, t;
    private Transform ball;

    public void Init(Color c, float s, float seconds)
    {
        color = c;
        size = s;
        life = Mathf.Max(0.05f, seconds);
        mat = UIUtil.UnlitMaterial(c);
        ball = AbilityFx.Primitive(transform, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.2f, mat);
    }

    private void OnDestroy()
    {
        if (mat != null)
            Destroy(mat);
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / life);
        ball.localScale = Vector3.one * Mathf.Lerp(0.2f, size, 1f - (1f - k) * (1f - k));
        Color c = color;
        c.a = color.a * (1f - k);
        mat.color = c;
        if (k >= 1f)
            Destroy(gameObject);
    }
}

/// <summary>Turns a character see-through (Gölge's stealth) and back.</summary>
public class GhostState : MonoBehaviour
{
    private readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
    private Material ghost;

    public static void Set(GameObject root, bool on, float alpha)
    {
        var g = root.GetComponent<GhostState>();
        if (g == null)
        {
            if (!on)
                return;
            g = root.AddComponent<GhostState>();
        }
        if (on)
            g.Apply(alpha);
        else
            g.Restore();
    }

    private void Apply(float alpha)
    {
        Restore();
        if (ghost == null)
            ghost = UIUtil.UnlitMaterial(new Color(0.12f, 0.1f, 0.18f, alpha));
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r is LineRenderer || r is ParticleSystemRenderer || r is SpriteRenderer)
                continue;
            originals[r] = r.sharedMaterials;
            var mats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
                mats[i] = ghost;
            r.sharedMaterials = mats;
        }
    }

    public void Restore()
    {
        foreach (var kv in originals)
            if (kv.Key != null && kv.Key.sharedMaterial == ghost)   // a renderer that changed meanwhile (new gun) keeps its new material
                kv.Key.sharedMaterials = kv.Value;
        originals.Clear();
    }

    private void OnDestroy()
    {
        if (ghost != null)
            Destroy(ghost);
    }
}
