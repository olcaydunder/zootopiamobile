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
                    case 11: // rainbow prism: flowing hue bands with a pearly sheen
                    {
                        float hue = (x / (float)size + y / (float)size * 0.35f + n1[i] * 0.45f) % 1f;
                        col = Color.HSVToRGB(hue, 0.62f, 1f);
                        col = Color.Lerp(col, Color.white, Mathf.Clamp01((n2[i] - 0.62f) * 2.5f) * 0.55f);
                        glow = 0.25f + Mathf.Clamp01((n2[i] - 0.6f) * 3f) * 0.5f;
                        break;
                    }
                    case 12: // neon waves on a dark base, cyan to magenta across the gun
                    {
                        float u = x / (float)size, v = y / (float)size;
                        float wv = Mathf.Sin((v * 7f + Mathf.Sin(u * Mathf.PI * 4f) * 0.35f + n1[i] * 0.8f) * Mathf.PI * 2f);
                        float lineK = Mathf.Clamp01((Mathf.Abs(wv) - 0.9f) * 12f);
                        col = Color.Lerp(camo.a, camo.a * 1.8f, n2[i] * 0.5f);
                        col = Color.Lerp(col, Color.Lerp(camo.b, camo.c, Mathf.PingPong(u * 2f, 1f)), lineK);
                        glow = lineK;
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

    /// <param name="scale">How much the gun model was rescaled to its real size (1 = as modelled).</param>
    public static void Dress(GameObject gun, WeaponData data, Transform weaponRoot, float scale = 1f)
    {
        if (gun == null || data == null)
            return;
        ApplyCamo(gun, data.camo);
        if (data.attachments == null)
            return;
        var anchors = GunAnchors.For(data);
        if (anchors != null)
            AddAttachments(anchors, data, weaponRoot, scale);
        else
            AddAttachmentsByBounds(gun, data, weaponRoot);
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
            var mat = CamoMaterial(camo, extent, 2.2f);
            if (mat == null)
                return;

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

    /// <summary>
    /// Shared camo material for a mesh of the given size (object-space bounds diagonal):
    /// about <paramref name="repeats"/> pattern repeats across it, whatever the model's units.
    /// Used for guns, vehicles and parachutes. Null if the camo shader is missing.
    /// </summary>
    public static Material CamoMaterial(CamoDef camo, float extent, float repeats)
    {
        if (camo == null || string.IsNullOrEmpty(camo.id))
            return null;
        if (camoBase == null)
            camoBase = Resources.Load<Material>("ZootopiaCamo");
        if (camoBase == null)
            return null;
        string key = camo.id + "_" + Mathf.RoundToInt(extent * 100f) + "_" + Mathf.RoundToInt(repeats * 10f);
        Material mat;
        if (!camoMaterials.TryGetValue(key, out mat) || mat == null)
        {
            mat = new Material(camoBase);
            mat.SetTexture("_PatternTex", Pattern(camo));
            mat.SetFloat("_Scale", repeats / Mathf.Max(0.001f, extent));
            mat.SetColor("_GlowColor", camo.glow);
            mat.SetFloat("_Shininess", Mathf.Lerp(0.15f, 0.9f, camo.gloss));
            mat.SetFloat("_Gloss", camo.gloss);
            mat.SetFloat("_Flow", camo.pattern == 5 ? 0.02f : 0f);
            camoMaterials[key] = mat;
        }
        return mat;
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
        r.sharedMaterial = unlit ? AbilityFx.Glass(color) : MaterialCache.Lit(color);   // cached, no leak per rebuild
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

    /// <summary>Fallback for a gun model without measured anchors: places parts from its bounds.</summary>
    private static void AddAttachmentsByBounds(GameObject gun, WeaponData data, Transform root)
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

        // Muzzle devices go on the end of a longer barrel, if one is fitted.
        string barrel = data.attachments.Length > (int)AttachmentSlot.Barrel ? data.attachments[(int)AttachmentSlot.Barrel] : null;
        float muzzleFront = front + (barrel == "long" ? len * 0.16f : barrel == "sniperb" ? len * 0.24f + 0.03f : 0f);

        foreach (var id in data.attachments)
        {
            var a = Gunsmith.FindAttachment(id);
            if (a == null)
                continue;
            switch (a.id)
            {
                case "sup":
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, barrelY, muzzleFront + len * 0.1f), new Vector3(0.05f, len * 0.1f, 0.05f), new Vector3(90f, 0f, 0f), dark, false);
                    break;
                case "comp":
                case "brake":
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, barrelY, muzzleFront + 0.035f), new Vector3(0.045f, 0.035f, 0.045f), new Vector3(90f, 0f, 0f), metal, false);
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, barrelY + 0.02f, muzzleFront + 0.035f), new Vector3(0.012f, 0.015f, 0.05f), Vector3.zero, dark, false);
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
                case "x4":
                case "x6":
                case "x8":
                {
                    float sl = a.id == "x8" ? 0.32f : a.id == "x6" ? 0.26f : a.id == "x4" ? 0.21f : 0.18f;
                    float sr = a.id == "x8" ? 0.05f : a.id == "x6" ? 0.045f : a.id == "x4" ? 0.04f : 0.038f;
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
                case "x2":
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, top + 0.02f, midZ), new Vector3(0.05f, 0.035f, 0.09f), Vector3.zero, dark, false);
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, top + 0.05f, midZ + 0.035f), new Vector3(0.05f, 0.045f, 0.008f), Vector3.zero, new Color(0.9f, 0.5f, 0.2f, 0.7f), true);
                    break;
                case "flash":
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, barrelY, muzzleFront + 0.045f), new Vector3(0.04f, 0.045f, 0.04f), new Vector3(90f, 0f, 0f), dark, false);
                    break;
                case "hsup":
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, barrelY, muzzleFront + len * 0.13f), new Vector3(0.065f, len * 0.13f, 0.065f), new Vector3(90f, 0f, 0f), dark, false);
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, barrelY, muzzleFront + len * 0.02f), new Vector3(0.07f, 0.012f, 0.07f), new Vector3(90f, 0f, 0f), metal, false);
                    break;
                case "heavy":
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, barrelY, front - len * 0.06f), new Vector3(0.045f, len * 0.1f, 0.045f), new Vector3(90f, 0f, 0f), dark, false);
                    break;
                case "lightb":
                    for (int k = 0; k < 3; k++)
                        Part(holder, PrimitiveType.Cylinder, new Vector3(0f, barrelY, front - len * (0.04f + k * 0.05f)), new Vector3(0.04f, 0.008f, 0.04f), new Vector3(90f, 0f, 0f), metal, false);
                    break;
                case "sniperb":
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0f, barrelY, front + len * 0.12f), new Vector3(0.022f, len * 0.12f, 0.022f), new Vector3(90f, 0f, 0f), metal, false);
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, barrelY, front + len * 0.24f), new Vector3(0.05f, 0.03f, 0.06f), Vector3.zero, dark, false);
                    break;
                case "rlaser":
                case "glaser":
                {
                    Color beam = a.id == "rlaser" ? new Color(1f, 0.15f, 0.1f) : new Color(0.2f, 1f, 0.3f);
                    Part(holder, PrimitiveType.Cube, new Vector3(-0.03f, barrelY - 0.015f, midZ + len * 0.28f), new Vector3(0.028f, 0.028f, 0.06f), Vector3.zero, dark, false);
                    Part(holder, PrimitiveType.Sphere, new Vector3(-0.03f, barrelY - 0.015f, midZ + len * 0.28f + 0.032f), new Vector3(0.012f, 0.012f, 0.004f), Vector3.zero, beam, true);
                    break;
                }
                case "hgrip":
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, b.min.y + b.size.y * 0.35f - 0.025f, midZ + len * 0.22f), new Vector3(0.03f, 0.035f, 0.06f), Vector3.zero, dark, false);
                    break;
                case "tgrip":
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, b.min.y + b.size.y * 0.35f - 0.05f, midZ + len * 0.22f), new Vector3(0.035f, 0.08f, 0.04f), new Vector3(-10f, 0f, 0f), dark, false);
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, b.min.y + b.size.y * 0.35f - 0.09f, midZ + len * 0.23f), new Vector3(0.04f, 0.015f, 0.05f), Vector3.zero, metal, false);
                    break;
                case "bipod":
                    Part(holder, PrimitiveType.Cylinder, new Vector3(-0.025f, b.min.y + b.size.y * 0.35f - 0.07f, midZ + len * 0.3f), new Vector3(0.012f, 0.07f, 0.012f), new Vector3(0f, 0f, -12f), metal, false);
                    Part(holder, PrimitiveType.Cylinder, new Vector3(0.025f, b.min.y + b.size.y * 0.35f - 0.07f, midZ + len * 0.3f), new Vector3(0.012f, 0.07f, 0.012f), new Vector3(0f, 0f, 12f), metal, false);
                    break;
                case "fastext":
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, b.min.y - 0.035f, midZ + len * 0.05f), new Vector3(0.035f, 0.09f, 0.05f), new Vector3(12f, 0f, 0f), new Color(0.75f, 0.55f, 0.15f), false);
                    break;
                case "fold":
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, barrelY - 0.01f, back - 0.07f), new Vector3(0.015f, 0.015f, 0.14f), Vector3.zero, metal, false);
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, barrelY - 0.05f, back - 0.07f), new Vector3(0.015f, 0.015f, 0.14f), Vector3.zero, metal, false);
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, barrelY - 0.03f, back - 0.14f), new Vector3(0.02f, 0.07f, 0.015f), Vector3.zero, dark, false);
                    break;
                case "hstock":
                    Part(holder, PrimitiveType.Cube, new Vector3(0f, barrelY - 0.035f, back - 0.1f), new Vector3(0.05f, 0.12f, 0.22f), Vector3.zero, dark, false);
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

    // ----- Attachments fitted to the measured gun (GunAnchors) -----

    private static readonly Color Dark = new Color(0.1f, 0.1f, 0.11f), Metal = new Color(0.32f, 0.33f, 0.35f),
        Polymer = new Color(0.17f, 0.17f, 0.18f), Rubber = new Color(0.05f, 0.05f, 0.05f), Gold = new Color(0.75f, 0.55f, 0.15f),
        TapeColor = new Color(0.33f, 0.34f, 0.27f), Grain = new Color(0.2f, 0.19f, 0.17f), GlassBlue = new Color(0.25f, 0.55f, 0.9f, 0.85f),
        GlassCyan = new Color(0.3f, 0.9f, 1f, 0.6f), GlassAmber = new Color(0.95f, 0.55f, 0.2f, 0.7f), LaserRed = new Color(1f, 0.1f, 0.1f),
        LaserGreen = new Color(0.2f, 1f, 0.3f);

    /// <summary>Builds attachment parts in a frame (a child transform), all flush with the gun.</summary>
    private class Kit
    {
        public int layer;

        public Transform Frame(Transform parent, Vector3 pos, Vector3 euler, float scale = 1f)
        {
            var t = new GameObject("Frame").transform;
            t.gameObject.layer = layer;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(euler);
            t.localScale = Vector3.one * scale;
            return t;
        }

        public void Box(Transform f, Vector3 pos, Vector3 size, Color c)
        {
            Part(f, PrimitiveType.Cube, pos, size, Vector3.zero, c, false);
        }

        public void Box(Transform f, Vector3 pos, Vector3 size, Color c, Vector3 euler)
        {
            Part(f, PrimitiveType.Cube, pos, size, euler, c, false);
        }

        public void Glass(Transform f, Vector3 pos, Vector3 size, Color c)
        {
            Part(f, PrimitiveType.Cube, pos, size, Vector3.zero, c, true);
        }

        /// <summary>Cylinder along Z (the barrel direction).</summary>
        public void Tube(Transform f, Vector3 pos, float r, float length, Color c)
        {
            Part(f, PrimitiveType.Cylinder, pos, new Vector3(2f * r, length * 0.5f, 2f * r), new Vector3(90f, 0f, 0f), c, false);
        }

        public void TubeY(Transform f, Vector3 pos, float r, float length, Color c)
        {
            Part(f, PrimitiveType.Cylinder, pos, new Vector3(2f * r, length * 0.5f, 2f * r), Vector3.zero, c, false);
        }

        public void TubeX(Transform f, Vector3 pos, float r, float length, Color c)
        {
            Part(f, PrimitiveType.Cylinder, pos, new Vector3(2f * r, length * 0.5f, 2f * r), new Vector3(0f, 0f, 90f), c, false);
        }

        /// <summary>Thin disc facing along Z (lenses, bores).</summary>
        public void Disc(Transform f, Vector3 pos, float r, Color c, bool unlit = true)
        {
            Part(f, PrimitiveType.Cylinder, pos, new Vector3(2f * r, 0.001f, 2f * r), new Vector3(90f, 0f, 0f), c, unlit);
        }

        /// <summary>Round bar from one point to another.</summary>
        public void Rod(Transform f, Vector3 a, Vector3 b, float r, Color c)
        {
            Vector3 d = b - a;
            var t = Part(f, PrimitiveType.Cylinder, (a + b) * 0.5f, new Vector3(2f * r, d.magnitude * 0.5f, 2f * r), Vector3.zero, c, false);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        }
    }

    private static readonly string[] ScopeIds = { "x3", "x4", "x6", "x8" };

    private static void Optic(Kit k, Transform f, string id, float w, float railLen)
    {
        switch (id)
        {
            case "red":
                k.Box(f, new Vector3(0f, 0.005f, 0f), new Vector3(w, 0.010f, 0.045f), Dark);
                k.Tube(f, new Vector3(0f, 0.024f, 0.002f), 0.015f, 0.042f, Dark);
                k.Tube(f, new Vector3(0f, 0.024f, 0.022f), 0.0165f, 0.006f, Metal);
                k.Disc(f, new Vector3(0f, 0.024f, 0.0255f), 0.012f, new Color(0.6f, 0.3f, 0.3f, 0.7f));
                k.Disc(f, new Vector3(0f, 0.024f, -0.0195f), 0.012f, new Color(0.3f, 0.35f, 0.45f, 0.7f));
                Part(f, PrimitiveType.Sphere, new Vector3(0f, 0.024f, 0.018f), new Vector3(0.004f, 0.004f, 0.001f), Vector3.zero, LaserRed, true);
                break;
            case "holo":
                k.Box(f, new Vector3(0f, 0.007f, 0f), new Vector3(w * 1.05f, 0.014f, 0.075f), Dark);
                k.Box(f, new Vector3(-0.0175f, 0.031f, 0.012f), new Vector3(0.005f, 0.034f, 0.04f), Dark);
                k.Box(f, new Vector3(0.0175f, 0.031f, 0.012f), new Vector3(0.005f, 0.034f, 0.04f), Dark);
                k.Box(f, new Vector3(0f, 0.0495f, 0.012f), new Vector3(0.04f, 0.005f, 0.042f), Dark);
                k.Glass(f, new Vector3(0f, 0.031f, 0.016f), new Vector3(0.03f, 0.03f, 0.002f), GlassCyan);
                k.Box(f, new Vector3(0f, 0.022f, -0.026f), new Vector3(0.03f, 0.016f, 0.022f), Dark);
                break;
            case "x2":
                k.Box(f, new Vector3(0f, 0.006f, 0f), new Vector3(w, 0.012f, 0.06f), Dark);
                k.Box(f, new Vector3(0f, 0.03f, -0.006f), new Vector3(0.036f, 0.036f, 0.06f), Dark);
                k.Box(f, new Vector3(0f, 0.03f, 0.029f), new Vector3(0.04f, 0.04f, 0.012f), Dark);
                k.Glass(f, new Vector3(0f, 0.03f, 0.0355f), new Vector3(0.03f, 0.03f, 0.002f), GlassAmber);
                break;
            default:
            {
                float L = id == "x8" ? 0.3f : id == "x6" ? 0.25f : id == "x4" ? 0.2f : 0.17f;
                float R = id == "x8" ? 0.022f : id == "x6" ? 0.02f : id == "x4" ? 0.018f : 0.016f;
                const float h = 0.012f;
                float ty = h + R + 0.004f;
                float rz = Mathf.Min(L * 0.2f, Mathf.Max(0.02f, railLen * 0.5f - 0.012f));
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = s * rz;
                    k.Box(f, new Vector3(0f, h * 0.5f, z), new Vector3(w * 0.95f, h, 0.014f), Dark);
                    k.Tube(f, new Vector3(0f, ty, z), R * 0.8f + 0.004f, 0.014f, Dark);
                    k.Box(f, new Vector3(0f, (h + ty) * 0.5f, z), new Vector3(0.012f, ty - h, 0.014f), Dark);
                }
                k.Tube(f, new Vector3(0f, ty, 0f), R * 0.8f, L * 0.56f, Dark);
                k.Tube(f, new Vector3(0f, ty, L * 0.36f), R * 1.25f, L * 0.24f, Dark);
                k.Tube(f, new Vector3(0f, ty, L * 0.2f), R * 1.02f, L * 0.1f, Dark);
                k.Tube(f, new Vector3(0f, ty, -L * 0.38f), R * 1.05f, L * 0.2f, Dark);
                k.TubeY(f, new Vector3(0f, ty + R * 0.8f + 0.007f, 0f), 0.009f, 0.014f, Metal);
                k.TubeX(f, new Vector3(R * 0.8f + 0.007f, ty, 0f), 0.009f, 0.014f, Metal);
                k.Disc(f, new Vector3(0f, ty, L * 0.48f + 0.001f), R * 1.12f, GlassBlue);
                k.Disc(f, new Vector3(0f, ty, -L * 0.48f - 0.001f), R * 0.9f, new Color(0.2f, 0.3f, 0.4f, 0.85f));
                break;
            }
        }
    }

    private static bool Has(string[] ids, params string[] any)
    {
        foreach (var id in ids)
            foreach (var a in any)
                if (id == a)
                    return true;
        return false;
    }

    /// <summary>Attachments fitted to the measured gun: on the rail, in line with the bore, under the handguard,
    /// below the magazine (or a magazine where the gun has none), on or behind the stock.</summary>
    private static void AddAttachments(GunAnchors A, WeaponData data, Transform root, float scale)
    {
        var holder = new GameObject("Attachments").transform;
        holder.SetParent(root, false);
        holder.gameObject.layer = root.gameObject.layer;
        holder.localScale = Vector3.one * scale;
        var k = new Kit { layer = root.gameObject.layer };
        var ids = data.attachments;
        WeaponType type = data.weaponType;
        bool pistol = type == WeaponType.Pistol;
        float c = A.cx, br = A.barrelR, my = A.muzzleY;

        string barrel = null;
        foreach (var id in ids)
            if (id == "long" || id == "sniperb" || id == "heavy" || id == "lightb" || id == "short")
                barrel = id;
        float ext = barrel == "long" ? (pistol ? 0.06f : type == WeaponType.SMG ? 0.08f : 0.10f) : barrel == "sniperb" ? 0.14f : 0f;
        float mz = A.muzzleZ + ext;
        float w = Mathf.Clamp(2f * A.w, 0.022f, 0.034f);
        float bareStart = A.barrelStart, bareLen = A.muzzleZ - 0.015f - bareStart;
        bool muzzleDevice = Has(ids, "sup", "hsup", "comp", "brake", "flash");

        foreach (var id in ids)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            switch (id)
            {
                // Barrels
                case "long":
                case "sniperb":
                    k.Tube(holder, new Vector3(c, my, A.muzzleZ + ext * 0.5f - 0.004f), br * (id == "sniperb" ? 1.05f : 0.95f), ext + 0.008f, Metal);
                    k.Tube(holder, new Vector3(c, my, A.muzzleZ + 0.002f), br * 1.3f, 0.012f, Dark);
                    if (id == "sniperb" && !muzzleDevice)
                    {
                        float r = br * 1.8f;
                        k.Tube(holder, new Vector3(c, my, mz + 0.023f), r, 0.05f, Dark);
                        for (int s = -1; s <= 1; s += 2)
                            for (int n = 0; n < 2; n++)
                                k.Box(holder, new Vector3(c + s * r * 0.92f, my, mz + 0.012f + n * 0.02f), new Vector3(0.005f, r * 1.1f, 0.01f), Rubber);
                    }
                    break;
                case "heavy":
                    if (pistol)
                        k.Box(holder, new Vector3(c, my - br - 0.012f, A.muzzleZ - 0.045f), new Vector3(0.026f, 0.018f, 0.07f), Dark);
                    else if (bareLen > 0.04f)
                        k.Tube(holder, new Vector3(c, my, bareStart + bareLen * 0.5f), br * 1.55f, bareLen, Dark);
                    break;
                case "lightb":
                    if (!pistol && bareLen > 0.04f)
                        for (int n = 0; n < 3; n++)
                            k.Tube(holder, new Vector3(c, my, bareStart + bareLen * (0.25f + 0.25f * n)), br * 1.3f, 0.006f, Metal);
                    break;

                // Muzzle devices (on the end of the barrel, in line with the bore)
                case "sup":
                {
                    float r = Mathf.Clamp(br * 2f, 0.015f, 0.03f);
                    float L = pistol ? 0.12f : type == WeaponType.SMG ? 0.14f : 0.17f;
                    float z0 = mz - 0.01f;
                    k.Tube(holder, new Vector3(c, my, z0 + L * 0.5f), r, L, Dark);
                    k.Tube(holder, new Vector3(c, my, z0 + L - 0.004f), r * 1.03f, 0.008f, Metal);
                    k.Tube(holder, new Vector3(c, my, z0 + 0.014f), r * 1.03f, 0.006f, Metal);
                    k.Disc(holder, new Vector3(c, my, z0 + L + 0.0006f), r * 0.3f, Rubber, false);
                    break;
                }
                case "hsup":
                {
                    float r = Mathf.Clamp(br * 2.4f, 0.019f, 0.034f);
                    float L = pistol ? 0.15f : 0.22f;
                    float z0 = mz - 0.01f;
                    k.Tube(holder, new Vector3(c, my, z0 + L * 0.5f), r, L, Dark);
                    k.Tube(holder, new Vector3(c, my, z0 + L * 0.08f), r * 1.04f, 0.01f, Metal);
                    k.Tube(holder, new Vector3(c, my, z0 + L * 0.5f), r * 1.04f, 0.01f, Metal);
                    k.Tube(holder, new Vector3(c, my, z0 + L - 0.005f), r * 1.04f, 0.01f, Metal);
                    k.Disc(holder, new Vector3(c, my, z0 + L + 0.0006f), r * 0.3f, Rubber, false);
                    break;
                }
                case "comp":
                {
                    float r = Mathf.Max(br * 1.6f, 0.012f);
                    k.Tube(holder, new Vector3(c, my, mz - 0.004f + 0.025f), r, 0.05f, Metal);
                    for (int n = 0; n < 2; n++)
                        k.Box(holder, new Vector3(c, my + r * 0.86f, mz + 0.012f + n * 0.018f), new Vector3(r * 0.9f, 0.006f, 0.008f), Rubber);
                    break;
                }
                case "brake":
                {
                    float r = Mathf.Max(br * 1.7f, 0.013f);
                    k.Tube(holder, new Vector3(c, my, mz - 0.004f + 0.0275f), r, 0.055f, Metal);
                    for (int s = -1; s <= 1; s += 2)
                        for (int n = 0; n < 2; n++)
                            k.Box(holder, new Vector3(c + s * r * 0.9f, my, mz + 0.014f + n * 0.02f), new Vector3(0.006f, r * 1.1f, 0.01f), Rubber);
                    break;
                }
                case "flash":
                {
                    float r = Mathf.Max(br * 1.5f, 0.011f);
                    const float L = 0.045f;
                    k.Tube(holder, new Vector3(c, my, mz - 0.004f + L * 0.5f), r, L, Dark);
                    for (int n = 0; n < 4; n++)
                    {
                        float a = (45f + 90f * n) * Mathf.Deg2Rad;
                        k.Box(holder, new Vector3(c + Mathf.Cos(a) * r * 0.95f, my + Mathf.Sin(a) * r * 0.95f, mz + L * 0.62f),
                            new Vector3(0.004f, 0.004f, L * 0.55f), Rubber, new Vector3(0f, 0f, 45f + 90f * n));
                    }
                    break;
                }

                // Optics: on the rail; a gun with its own scope gets a sunshade, or a small sight canted on the side
                case "red":
                case "holo":
                case "x2":
                case "x3":
                case "x4":
                case "x6":
                case "x8":
                {
                    bool magnified = System.Array.IndexOf(ScopeIds, id) >= 0;
                    if (A.scope)
                    {
                        if (magnified)
                        {
                            float sun = 0.03f + 0.006f * (id == "x3" ? 3 : id == "x4" ? 4 : id == "x6" ? 6 : 8);
                            k.Tube(holder, new Vector3(c, A.scopeY, A.scopeFront + sun * 0.5f - 0.002f), A.scopeR * 1.08f, sun, Dark);
                            k.Disc(holder, new Vector3(c, A.scopeY, A.scopeFront + sun), A.scopeR * 0.95f, GlassBlue);
                        }
                        else
                        {
                            Optic(k, k.Frame(holder, new Vector3(c + 0.016f, A.railY - 0.002f, A.railZ), new Vector3(0f, 0f, -45f), 0.85f), id, 0.02f, A.railLen);
                        }
                    }
                    else
                    {
                        float s = pistol ? 0.8f : 1f;
                        Optic(k, k.Frame(holder, new Vector3(c, A.railY, A.railZ), Vector3.zero, s), id, w / s, A.railLen);
                    }
                    break;
                }

                // Under the handguard
                case "vgrip":
                case "agrip":
                case "hgrip":
                case "tgrip":
                case "bipod":
                {
                    var u = k.Frame(holder, new Vector3(c, A.underY, A.underZ), Vector3.zero);
                    k.Box(u, new Vector3(0f, -0.005f, 0f), new Vector3(w * 0.95f, 0.011f, 0.034f), Dark);
                    if (id == "vgrip")
                    {
                        k.TubeY(u, new Vector3(0f, -0.054f, 0f), 0.015f, 0.088f, Polymer);
                        k.TubeY(u, new Vector3(0f, -0.099f, 0f), 0.0165f, 0.006f, Dark);
                    }
                    else if (id == "agrip")
                    {
                        k.Box(u, new Vector3(0f, -0.012f, 0f), new Vector3(0.026f, 0.012f, 0.075f), Polymer);
                        k.Box(u, new Vector3(0f, -0.026f, -0.008f), new Vector3(0.024f, 0.02f, 0.058f), Polymer, new Vector3(-14f, 0f, 0f));
                    }
                    else if (id == "hgrip")
                    {
                        k.TubeY(u, new Vector3(0f, -0.033f, 0f), 0.015f, 0.046f, Polymer);
                    }
                    else if (id == "tgrip")
                    {
                        k.TubeY(u, new Vector3(0f, -0.056f, 0f), 0.016f, 0.09f, Polymer);
                        k.Box(u, new Vector3(0f, -0.098f, 0.01f), new Vector3(0.03f, 0.008f, 0.04f), Dark);
                    }
                    else
                    {
                        // Folded bipod: legs forward along the handguard.
                        k.Box(u, new Vector3(0f, -0.016f, 0f), new Vector3(0.032f, 0.016f, 0.03f), Dark);
                        for (int s = -1; s <= 1; s += 2)
                        {
                            k.Tube(u, new Vector3(s * 0.011f, -0.022f, 0.085f), 0.0055f, 0.17f, Metal);
                            k.Box(u, new Vector3(s * 0.011f, -0.022f, 0.172f), new Vector3(0.012f, 0.012f, 0.012f), Rubber);
                        }
                    }
                    break;
                }

                // Lasers: on the right of the handguard (pistols: under the barrel)
                case "laser":
                case "rlaser":
                case "glaser":
                {
                    Color beam = id == "rlaser" ? LaserRed : LaserGreen;
                    Transform lf;
                    if (pistol)
                    {
                        lf = k.Frame(holder, new Vector3(c, A.underY - 0.012f, A.underZ + 0.01f), Vector3.zero);
                    }
                    else
                    {
                        lf = k.Frame(holder, new Vector3(c + A.sideX + 0.011f, A.sideY, A.sideZ), Vector3.zero);
                        k.Box(lf, new Vector3(-0.009f, 0f, 0f), new Vector3(0.008f, 0.012f, 0.03f), Dark);
                    }
                    k.Box(lf, Vector3.zero, new Vector3(0.02f, 0.022f, 0.055f), Dark);
                    k.Disc(lf, new Vector3(0f, 0.003f, 0.0281f), 0.005f, beam);
                    k.Disc(lf, new Vector3(0f, -0.005f, 0.0281f), 0.004f, new Color(0.85f, 0.85f, 0.8f));
                    break;
                }

                // Magazines: continue the gun's own magazine (same lean); guns without one get a box magazine
                case "ext":
                case "fast":
                case "fastext":
                case "drum":
                {
                    float dz = A.magBotZ - A.magTopZ, dy = A.magBotY - A.magTopY;
                    float len0 = Mathf.Sqrt(dz * dz + dy * dy);
                    float lean = Mathf.Atan2(-dz, -dy) * Mathf.Rad2Deg;
                    float th = A.magThick, dp = A.magDepth;
                    if (!A.hasMag)
                    {
                        var top = k.Frame(holder, new Vector3(c, A.magTopY, A.magTopZ), new Vector3(lean, 0f, 0f));
                        k.Box(top, new Vector3(0f, -len0 * 0.5f, 0f), new Vector3(th, len0, dp), Dark);
                    }
                    var m = k.Frame(holder, new Vector3(c, A.magBotY, A.magBotZ), new Vector3(lean, 0f, 0f));
                    if (id == "ext" || id == "fastext")
                    {
                        float L = id == "fastext" ? 0.05f : 0.056f;
                        k.Box(m, new Vector3(0f, -L * 0.5f, 0f), new Vector3(th * 0.98f, L, dp * 0.97f), Dark);
                        k.Box(m, new Vector3(0f, -L - 0.003f, 0f), new Vector3(th * 1.1f, 0.006f, dp * 1.06f), id == "fastext" ? Gold : Polymer);
                        if (id == "fastext")
                            k.Box(m, new Vector3(0f, -L - 0.012f, -dp * 0.25f), new Vector3(th * 0.55f, 0.014f, 0.007f), Gold);
                    }
                    else if (id == "fast")
                    {
                        k.Box(m, new Vector3(0f, -0.004f, 0f), new Vector3(th * 1.08f, 0.008f, dp * 1.04f), Gold);
                        k.Box(m, new Vector3(0f, -0.014f, -dp * 0.25f), new Vector3(th * 0.55f, 0.014f, 0.007f), Gold);
                    }
                    else
                    {
                        float r = type == WeaponType.SMG ? 0.06f : 0.068f;
                        k.TubeX(m, new Vector3(0f, -0.028f, dp * 0.1f), r, th * 2.6f, Dark);
                        k.TubeX(m, new Vector3(th * 1.3f + 0.001f, -0.028f, dp * 0.1f), r * 0.8f, 0.004f, Metal);
                        k.TubeX(m, new Vector3(0f, -0.028f, dp * 0.1f), r * 0.25f, th * 2.8f, Metal);
                    }
                    break;
                }

                // Stocks: on the gun's own stock (butt pad, cheek riser), or a stock attached behind the receiver
                case "light":
                case "tac":
                case "fold":
                case "hstock":
                    if (A.hasStock)
                    {
                        float bh = A.buttHi - A.buttLo, by = (A.buttLo + A.buttHi) * 0.5f;
                        float t = id == "tac" ? 0.02f : id == "hstock" ? 0.03f : 0.012f;
                        k.Box(holder, new Vector3(c, by, A.buttZ - t * 0.5f + 0.001f), new Vector3(A.stockW * 1.06f, bh * 1.02f, t), Rubber);
                        if (id == "tac" || id == "hstock")
                        {
                            float h = id == "tac" ? 0.016f : 0.024f, cl = A.combZ1 - A.combZ0;
                            float slope = Mathf.Atan2(A.combY1 - A.combY0, cl) * Mathf.Rad2Deg;
                            var f = k.Frame(holder, new Vector3(c, (A.combY0 + A.combY1) * 0.5f, (A.combZ0 + A.combZ1) * 0.5f), new Vector3(-slope, 0f, 0f));
                            k.Box(f, new Vector3(0f, h * 0.5f - 0.002f, -cl * 0.05f), new Vector3(A.stockW * 0.85f, h, cl * 0.75f), Polymer);
                        }
                        if (id == "fold")
                            k.TubeY(holder, new Vector3(c + A.stockW * 0.5f + 0.005f, A.combY1 - 0.03f, A.combZ1 + 0.012f), 0.008f, 0.045f, Metal);
                    }
                    else
                    {
                        float ay = A.attachY, az = A.attachZ;
                        float Ls = pistol ? 0.17f : 0.23f, ph = pistol ? 0.1f : 0.12f, sw = Mathf.Max(A.stockW, 0.03f);
                        float top = ay + 0.012f, bot = top - ph;
                        if (id == "fold")
                        {
                            // Folded along the right side.
                            float sx = c + A.w + 0.012f;
                            k.TubeY(holder, new Vector3(c + A.w + 0.006f, ay, az + 0.004f), 0.008f, 0.03f, Metal);
                            k.Tube(holder, new Vector3(sx, ay, az + Ls * 0.5f), 0.007f, Ls, Metal);
                            if (!pistol)
                                k.Rod(holder, new Vector3(sx, ay - 0.035f, az + 0.012f), new Vector3(sx, bot + 0.015f, az + Ls), 0.006f, Metal);
                            k.Box(holder, new Vector3(sx + sw * 0.5f - 0.004f, top - ph * 0.5f, az + Ls), new Vector3(sw, ph, 0.014f), Rubber);
                        }
                        else if (id == "hstock")
                        {
                            k.Box(holder, new Vector3(c, ay - ph * 0.3f, az - Ls * 0.5f + 0.006f), new Vector3(sw * (pistol ? 0.7f : 0.9f), ph * 0.62f, Ls + 0.012f), Polymer);
                            k.Box(holder, new Vector3(c, top - ph * 0.5f, az - Ls), new Vector3(sw * 1.05f, ph, 0.022f), Rubber);
                        }
                        else
                        {
                            k.Tube(holder, new Vector3(c, ay, az - Ls * 0.5f + 0.006f), 0.007f, Ls + 0.012f, Metal);
                            k.Box(holder, new Vector3(c, ay, az - 0.006f), new Vector3(sw * 0.7f, 0.03f, 0.016f), Dark);
                            if (id == "tac")
                            {
                                k.Rod(holder, new Vector3(c, ay - 0.035f, az + 0.01f), new Vector3(c, bot + 0.015f, az - Ls), 0.006f, Metal);
                                k.Box(holder, new Vector3(c, ay + 0.011f, az - Ls * 0.6f), new Vector3(sw * 0.8f, 0.016f, 0.08f), Polymer);
                            }
                            k.Box(holder, new Vector3(c, top - ph * 0.5f, az - Ls), new Vector3(sw, ph, 0.014f), Rubber);
                        }
                    }
                    break;

                // Grip wraps
                case "rubber":
                case "tape":
                case "granular":
                {
                    var g = k.Frame(holder, new Vector3(c, A.gripY, A.gripZ), new Vector3(A.gripTilt, 0f, 0f));
                    if (id == "tape")
                    {
                        for (int n = -1; n <= 1; n++)
                            k.Box(g, new Vector3(0f, n * A.gripH * 0.3f, 0f), new Vector3(A.gripW * 1.14f, A.gripH * 0.17f, A.gripD * 1.12f), TapeColor);
                    }
                    else
                    {
                        k.Box(g, Vector3.zero, new Vector3(A.gripW * 1.08f, A.gripH * 0.7f, A.gripD * 0.95f), id == "rubber" ? Rubber : Grain);
                    }
                    break;
                }
            }
        }
    }

    /// <summary>Where the muzzle ends (weapon space), after a longer barrel or a muzzle device: flash and tracers start here.</summary>
    public static Vector3 MuzzleTip(WeaponData data)
    {
        var A = GunAnchors.For(data);
        if (A == null)
            return new Vector3(0f, 0.04f, ModelLibrary.MuzzleDistance(data.weaponType) + 0.08f);
        float z = A.muzzleZ;
        bool pistol = data.weaponType == WeaponType.Pistol;
        string[] ids = data.attachments ?? new string[0];
        bool device = Has(ids, "sup", "hsup", "comp", "brake", "flash");
        if (Has(ids, "long"))
            z += pistol ? 0.06f : data.weaponType == WeaponType.SMG ? 0.08f : 0.10f;
        else if (Has(ids, "sniperb"))
            z += 0.14f + (device ? 0f : 0.048f);
        if (Has(ids, "sup"))
            z += (pistol ? 0.12f : data.weaponType == WeaponType.SMG ? 0.14f : 0.17f) - 0.01f;
        else if (Has(ids, "hsup"))
            z += (pistol ? 0.15f : 0.22f) - 0.01f;
        else if (Has(ids, "comp"))
            z += 0.046f;
        else if (Has(ids, "brake"))
            z += 0.051f;
        else if (Has(ids, "flash"))
            z += 0.041f;
        return new Vector3(A.cx, A.muzzleY, z + 0.03f);
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
