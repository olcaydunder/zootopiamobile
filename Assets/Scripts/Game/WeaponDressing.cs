using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dresses a gun model for the player: camo (procedural tileable patterns on a triplanar shader,
/// some with animated glow) and visible attachments (suppressor, scopes, grips, magazines, stocks).
/// </summary>
public static class WeaponDressing
{
    private static readonly Dictionary<string, Texture2D> patterns = new Dictionary<string, Texture2D>();
    private static readonly Dictionary<string, Material> camoMaterials = new Dictionary<string, Material>();
    private static Material camoBase;

    // ----- Pattern textures -----

    private static float[] TileNoise(int size, int cells, System.Random rng)
    {
        var lattice = new float[cells * cells];
        for (int i = 0; i < lattice.Length; i++)
            lattice[i] = (float)rng.NextDouble();
        var result = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            float fy = (float)y / size * cells;
            int y0 = Mathf.FloorToInt(fy);
            float ty = Mathf.SmoothStep(0f, 1f, fy - y0);
            for (int x = 0; x < size; x++)
            {
                float fx = (float)x / size * cells;
                int x0 = Mathf.FloorToInt(fx);
                float tx = Mathf.SmoothStep(0f, 1f, fx - x0);
                float a = lattice[(y0 % cells) * cells + x0 % cells];
                float b = lattice[(y0 % cells) * cells + (x0 + 1) % cells];
                float c = lattice[((y0 + 1) % cells) * cells + x0 % cells];
                float d = lattice[((y0 + 1) % cells) * cells + (x0 + 1) % cells];
                result[y * size + x] = Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
            }
        }
        return result;
    }

    private static float[] Fbm(int size, System.Random rng, params int[] octaves)
    {
        var sum = new float[size * size];
        float amp = 1f, norm = 0f;
        foreach (int cells in octaves)
        {
            var n = TileNoise(size, cells, rng);
            for (int i = 0; i < sum.Length; i++)
                sum[i] += n[i] * amp;
            norm += amp;
            amp *= 0.5f;
        }
        for (int i = 0; i < sum.Length; i++)
            sum[i] /= norm;
        return sum;
    }

    /// <summary>1 inside a paw print (pad + four toes), tiled 4 times per texture, soft edge.</summary>
    private static float PawMask(int x, int y, int size)
    {
        float best = 0f;
        float k = size / 256f;
        for (int p = 0; p < 4; p++)
        {
            float cx = (p == 0 ? 64f : p == 1 ? 192f : p == 2 ? 96f : 210f) * k;
            float cy = (p == 0 ? 60f : p == 1 ? 100f : p == 2 ? 190f : 222f) * k;
            float ang = (p * 47f - 30f) * Mathf.Deg2Rad;
            float dx = Mathf.Repeat(x - cx + size * 0.5f, size) - size * 0.5f;
            float dy = Mathf.Repeat(y - cy + size * 0.5f, size) - size * 0.5f;
            float rx = (dx * Mathf.Cos(ang) + dy * Mathf.Sin(ang)) / k;
            float ry = (-dx * Mathf.Sin(ang) + dy * Mathf.Cos(ang)) / k;
            // main pad
            float pad = 1f - (rx * rx / (22f * 22f) + (ry + 4f) * (ry + 4f) / (17f * 17f));
            float m = Mathf.Clamp01(pad * 6f);
            // toes
            for (int t = 0; t < 4; t++)
            {
                float tx = t == 0 ? -24f : t == 1 ? -9f : t == 2 ? 9f : 24f;
                float ty = t == 0 || t == 3 ? 20f : 31f;
                float d = ((rx - tx) * (rx - tx) + (ry - ty) * (ry - ty)) / (8.5f * 8.5f);
                m = Mathf.Max(m, Mathf.Clamp01((1f - d) * 6f));
            }
            best = Mathf.Max(best, m);
        }
        return best;
    }

    public static Texture2D Pattern(CamoDef camo)
    {
        Texture2D tex;
        if (patterns.TryGetValue(camo.id, out tex) && tex != null)
            return tex;

        const int size = 256;
        var rng = new System.Random(camo.id.GetHashCode());
        var px = new Color32[size * size];
        float[] n1 = Fbm(size, rng, 4, 8, 16);
        float[] n2 = Fbm(size, rng, 6, 12, 24);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                Color col;
                float glow = 0f;
                switch (camo.pattern)
                {
                    case 1: // tiger stripes
                    {
                        float s = Mathf.Sin((x / (float)size) * Mathf.PI * 2f * 6f + n1[i] * 9f);
                        col = s > 0.55f ? camo.b : (s > 0.35f ? Color.Lerp(camo.a, camo.c, n2[i]) : camo.a);
                        break;
                    }
                    case 2: // digital
                    {
                        int cx = x / 8, cy = y / 8;
                        float v = n1[(cy * 8) * size + cx * 8];
                        float w = n2[(cy * 8) * size + cx * 8];
                        col = v > 0.58f ? camo.b : (w > 0.55f ? camo.c : camo.a);
                        break;
                    }
                    case 3: // glowing veins / cracks
                    {
                        float ridge = 1f - Mathf.Abs(n1[i] * 2f - 1f);
                        float ridge2 = 1f - Mathf.Abs(n2[i] * 2f - 1f);
                        float vein = Mathf.Clamp01((Mathf.Max(ridge, ridge2 * 0.9f) - 0.86f) * 9f);
                        col = Color.Lerp(Color.Lerp(camo.a, camo.b, n2[i]), camo.c, vein);
                        glow = vein;
                        break;
                    }
                    case 4: // polished metal
                    {
                        float brushed = Mathf.PerlinNoise(x * 0.02f, y * 0.9f) * 0.25f;
                        col = Color.Lerp(camo.b, camo.a, n1[i] * 0.8f + brushed);
                        col = Color.Lerp(col, camo.c, Mathf.Clamp01((n2[i] - 0.62f) * 4f));
                        glow = Mathf.Clamp01((n2[i] - 0.7f) * 5f);
                        break;
                    }
                    case 5: // nebula with stars
                    {
                        float t = n1[i];
                        col = t < 0.5f ? Color.Lerp(camo.c, camo.a, t * 2f) : Color.Lerp(camo.a, camo.b, (t - 0.5f) * 2f);
                        float swirl = Mathf.Clamp01((n2[i] - 0.55f) * 3f);
                        col = Color.Lerp(col, camo.b * 1.2f, swirl * 0.6f);
                        glow = swirl * 0.8f;
                        if (rng.NextDouble() < 0.004)
                        {
                            col = Color.white;
                            glow = 1f;
                        }
                        break;
                    }
                    case 6: // bold stripes (racing / parachute panels), thin dark separators
                    {
                        float u = x / (float)size * 4f;
                        float f = u - Mathf.Floor(u);
                        int band = Mathf.FloorToInt(u);
                        col = band % 2 == 0 ? camo.a : camo.b;
                        if (f < 0.04f || f > 0.96f)
                            col = camo.c;
                        break;
                    }
                    case 7: // checker
                    {
                        bool odd = ((x / 32) + (y / 32)) % 2 == 1;
                        col = odd ? camo.b : camo.a;
                        col = Color.Lerp(col, camo.c, (n2[i] - 0.5f) * 0.15f);
                        break;
                    }
                    case 8: // soft vertical gradient
                    {
                        float t = Mathf.Clamp01(y / (float)size + (n1[i] - 0.5f) * 0.2f);
                        col = Color.Lerp(camo.a, camo.b, t);
                        col = Color.Lerp(col, camo.c, Mathf.Clamp01((0.25f - t) * 2f) * 0.5f);
                        break;
                    }
                    case 9: // paw prints
                    {
                        float paw = PawMask(x, y, size);
                        col = Color.Lerp(Color.Lerp(camo.a, camo.c, n1[i] * 0.5f), camo.b, paw);
                        glow = paw * 0.6f;
                        break;
                    }
                    case 10: // flames rising from the bottom
                    {
                        float v = y / (float)size;
                        float edge = 0.45f + (n1[(i + size * 37) % (size * size)] - 0.5f) * 0.6f + Mathf.Sin(x / (float)size * Mathf.PI * 2f * 3f) * 0.08f;
                        float heat = Mathf.Clamp01((edge - v) * 4f);
                        col = Color.Lerp(camo.a, Color.Lerp(camo.b, camo.c, heat), Mathf.Clamp01(heat * 1.5f));
                        glow = heat;
                        break;
                    }
                    default: // blotches
                    {
                        float v = n1[i];
                        col = v > 0.6f ? camo.b : (n2[i] > 0.6f ? camo.c : camo.a);
                        break;
                    }
                }
                px[i] = new Color(col.r, col.g, col.b, glow);
            }
        }

        tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = camo.pattern == 2 ? FilterMode.Point : FilterMode.Trilinear;
        tex.anisoLevel = 4;
        tex.SetPixels32(px);
        tex.Apply(true, true);
        patterns[camo.id] = tex;
        return tex;
    }

    // ----- Applying to a gun -----

    public static void Dress(GameObject gun, WeaponData data, Transform weaponRoot)
    {
        if (gun == null || data == null)
            return;
        ApplyCamo(gun, data.camo);
        AddAttachments(gun, data, weaponRoot);
    }

    private static void ApplyCamo(GameObject gun, string camoId)
    {
        if (string.IsNullOrEmpty(camoId))
            return;
        var camo = Gunsmith.FindCamo(camoId);
        if (camo == null || string.IsNullOrEmpty(camo.id))
            return;
        if (camoBase == null)
            camoBase = Resources.Load<Material>("ZootopiaCamo");
        if (camoBase == null)
            return;

        foreach (var r in gun.GetComponentsInChildren<Renderer>())
        {
            var mf = r.GetComponent<MeshFilter>();
            float extent = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.size.magnitude : 1f;
            string key = camo.id + "_" + Mathf.RoundToInt(extent * 100f);
            Material mat;
            if (!camoMaterials.TryGetValue(key, out mat) || mat == null)
            {
                mat = new Material(camoBase);
                mat.SetTexture("_PatternTex", Pattern(camo));
                mat.SetFloat("_Scale", 2.2f / Mathf.Max(0.001f, extent));   // ~2 repeats along the gun, whatever the units
                mat.SetColor("_GlowColor", camo.glow);
                mat.SetFloat("_Shininess", Mathf.Lerp(0.15f, 0.9f, camo.gloss));
                mat.SetFloat("_Gloss", camo.gloss);
                mat.SetFloat("_Flow", camo.pattern == 5 ? 0.02f : 0f);
                camoMaterials[key] = mat;
            }

            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                // Keep near-black details (grips, rails) so the gun still reads clearly.
                Color c = mats[i] != null ? mats[i].color : Color.grey;
                float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
                if (lum > 0.07f || camo.pattern == 4)
                    mats[i] = mat;
            }
            r.sharedMaterials = mats;
        }
    }

    private static Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Color color, bool unlit)
    {
        var go = GameObject.CreatePrimitive(type);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = "Attachment";
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localRotation = Quaternion.Euler(euler);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = unlit ? UIUtil.UnlitMaterial(color) : MaterialCache.Lit(color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    /// <summary>Gun bounds in the weapon root's local space (+Z forward = muzzle).</summary>
    private static Bounds LocalBounds(GameObject gun, Transform root)
    {
        bool first = true;
        Bounds b = new Bounds();
        foreach (var mf in gun.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.gameObject.name == "Attachment")
                continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (first)
                {
                    b = new Bounds(p, Vector3.zero);
                    first = false;
                }
                else
                {
                    b.Encapsulate(p);
                }
            }
        }
        return b;
    }

    private static void AddAttachments(GameObject gun, WeaponData data, Transform root)
    {
        if (data.attachments == null)
            return;
        Bounds b = LocalBounds(gun, root);
        if (b.size.z < 0.05f)
            return;

        var holder = new GameObject("Attachments").transform;
        holder.SetParent(root, false);
        holder.gameObject.layer = root.gameObject.layer;

        float len = b.size.z;
        float top = b.max.y;
        float barrelY = b.max.y - b.size.y * 0.22f;
        float front = b.max.z;
        float back = b.min.z;
        float midZ = b.center.z;
        Color dark = new Color(0.1f, 0.1f, 0.11f);
        Color metal = new Color(0.32f, 0.33f, 0.35f);

        foreach (var id in data.attachments)
        {
            var a = Gunsmith.FindAttachment(id);
            if (a == null)
                continue;
            switch (a.id)
            {
                case "sup":
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, barrelY, front + len * 0.1f), new Vector3(0.05f, len * 0.1f, 0.05f), new Vector3(90f, 0f, 0f), dark, false);
                    break;
                case "comp":
                case "brake":
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, barrelY, front + 0.035f), new Vector3(0.045f, 0.035f, 0.045f), new Vector3(90f, 0f, 0f), metal, false);
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, barrelY + 0.02f, front + 0.035f), new Vector3(0.012f, 0.015f, 0.05f), Vector3.zero, dark, false);
                    break;
                case "long":
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, barrelY, front + len * 0.08f), new Vector3(0.025f, len * 0.08f, 0.025f), new Vector3(90f, 0f, 0f), metal, false);
                    break;
                case "red":
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, top + 0.025f, midZ), new Vector3(0.04f, 0.04f, 0.06f), Vector3.zero, dark, false);
                    Part(holder, PrimitiveType.Sphere, new Vector3(0f, top + 0.03f, midZ + 0.031f), new Vector3(0.012f, 0.012f, 0.004f), Vector3.zero, new Color(1f, 0.1f, 0.1f), true);
                    break;
                case "holo":
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, top + 0.012f, midZ), new Vector3(0.045f, 0.02f, 0.08f), Vector3.zero, dark, false);
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, top + 0.045f, midZ + 0.03f), new Vector3(0.045f, 0.05f, 0.006f), Vector3.zero, new Color(0.3f, 0.9f, 1f, 0.6f), true);
                    break;
                case "x3":
                case "x6":
                {
                    float sl = a.id == "x6" ? 0.26f : 0.18f;
                    float sr = a.id == "x6" ? 0.045f : 0.038f;
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, top + sr + 0.01f, midZ), new Vector3(sr, sl * 0.5f, sr), new Vector3(90f, 0f, 0f), dark, false);
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, top + sr + 0.01f, midZ + sl * 0.5f), new Vector3(sr * 1.25f, 0.015f, sr * 1.25f), new Vector3(90f, 0f, 0f), dark, false);
                    Part(holder, PrimitiveType.Sphere, new Vector3(0f, top + sr + 0.01f, midZ + sl * 0.5f + 0.012f), new Vector3(sr * 1.6f, sr * 1.6f, 0.004f), Vector3.zero, new Color(0.25f, 0.55f, 0.9f), true);
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, top + 0.006f, midZ), new Vector3(0.02f, 0.02f, sl * 0.6f), Vector3.zero, metal, false);
                    break;
                }
                case "vgrip":
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, b.min.y + b.size.y * 0.35f - 0.06f, midZ + len * 0.22f), new Vector3(0.03f, 0.06f, 0.03f), Vector3.zero, dark, false);
                    break;
                case "agrip":
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, b.min.y + b.size.y * 0.35f - 0.035f, midZ + len * 0.2f), new Vector3(0.03f, 0.05f, 0.09f), new Vector3(-30f, 0f, 0f), dark, false);
                    break;
                case "laser":
                    Part(holder, PrimitiveType.Cube, new Vector3(0.03f, barrelY - 0.02f, midZ + len * 0.25f), new Vector3(0.03f, 0.03f, 0.07f), Vector3.zero, dark, false);
                    Part(holder, PrimitiveType.Sphere, new Vector3(0.03f, barrelY - 0.02f, midZ + len * 0.25f + 0.036f), new Vector3(0.012f, 0.012f, 0.004f), Vector3.zero, new Color(0.2f, 1f, 0.3f), true);
                    break;
                case "ext":
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, b.min.y - 0.03f, midZ + len * 0.05f), new Vector3(0.035f, 0.08f, 0.05f), new Vector3(12f, 0f, 0f), dark, false);
                    break;
                case "fast":
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, b.min.y - 0.015f, midZ + len * 0.05f), new Vector3(0.04f, 0.035f, 0.055f), Vector3.zero, new Color(0.75f, 0.55f, 0.15f), false);
                    break;
                case "drum":
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, b.min.y - 0.04f, midZ + len * 0.05f), new Vector3(0.11f, 0.035f, 0.11f), new Vector3(0f, 0f, 90f), dark, false);
                    break;
                case "light":
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, barrelY - 0.02f, back - 0.06f), new Vector3(0.02f, 0.08f, 0.14f), Vector3.zero, metal, false);
                    break;
                case "tac":
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, barrelY - 0.03f, back - 0.08f), new Vector3(0.04f, 0.1f, 0.18f), Vector3.zero, dark, false);
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, barrelY - 0.03f, back - 0.17f), new Vector3(0.042f, 0.12f, 0.03f), Vector3.zero, new Color(0.2f, 0.2f, 0.2f), false);
                    break;
            }
        }
    }

    public static void Clear(Transform weaponRoot)
    {
        var old = weaponRoot.Find("Attachments");
        if (old != null)
        {
            old.name = "AttachmentsOld";
            old.gameObject.SetActive(false);
            Object.Destroy(old.gameObject);
        }
    }
}
