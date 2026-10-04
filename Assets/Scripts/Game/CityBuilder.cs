using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the real Çekmeköy streets from MapData: apartment blocks with window façades, flat or tiled
/// roofs, enterable ground floors (loot), the mosque, the clinic with its sign, asphalt roads with
/// markings, street lights and trees. Everything static is merged into a few big meshes per map chunk
/// and material, with one mesh collider per chunk, so it stays cheap on phones.
/// </summary>
public static class CityBuilder
{
    public static string LastError;

    private const float ChunkSize = 176f;
    private const float FloorHeight = 3f;
    private const float ShellHeight = 3f;

    private class Batch
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();
    }

    private static Dictionary<long, Batch> batches;
    private static Dictionary<int, Batch> colliders;
    private static List<Material> materials;
    private static Dictionary<Mesh, Vector3[]> meshVerts;
    private static Dictionary<Mesh, int[]> meshTris;
    private static Dictionary<Mesh, Vector2[]> meshUvs;

    // material indices
    private static int[] facadeMats, shellMats;
    private static int metalMat, plinthMat, roofMat, tileRoofMat, floorMat, glassMat, stoneMat, domeMat, woodMat,
        whiteMat, signMat, signBoardMat, asphaltLinedMat, asphaltMat, poleMat, lampMat, trunkMat, cypressMat;
    private static int[] pineMats, leafMats;
    private static Mesh cube, cylinder, sphere;
    private static Mesh[] blobs;

    private static readonly Color[] FacadeColors =
    {
        new Color(0.93f, 0.89f, 0.80f), new Color(0.95f, 0.93f, 0.88f), new Color(0.92f, 0.82f, 0.62f),
        new Color(0.90f, 0.72f, 0.62f), new Color(0.78f, 0.80f, 0.80f), new Color(0.80f, 0.84f, 0.88f),
        new Color(0.86f, 0.78f, 0.70f)
    };

    private static Transform doorParent;
    private static Material doorMat;

    public static void Build(Transform parent)
    {
        LastError = null;
        if (!MapData.Loaded)
            return;

        doorParent = new GameObject("Doors").transform;
        doorParent.SetParent(parent, false);
        doorMat = MaterialCache.Lit(new Color(0.45f, 0.31f, 0.2f));
        batches = new Dictionary<long, Batch>();
        colliders = new Dictionary<int, Batch>();
        materials = new List<Material>();
        meshVerts = new Dictionary<Mesh, Vector3[]>();
        meshTris = new Dictionary<Mesh, int[]>();
        meshUvs = new Dictionary<Mesh, Vector2[]>();
        CreateMaterials();

        int failed = 0;
        foreach (var b in MapData.Buildings)
        {
            try
            {
                BuildBuilding(b);
            }
            catch (System.Exception e)
            {
                failed++;
                if (LastError == null)
                    LastError = "bina: " + e.Message;
            }
        }

        try
        {
            BuildRoads();
        }
        catch (System.Exception e)
        {
            LastError = "yol: " + e.Message;
        }

        try
        {
            BuildTrees(parent);
        }
        catch (System.Exception e)
        {
            LastError = "ağaç: " + e.Message;
        }

        Flush(parent);
        if (failed > 0)
            Debug.LogWarning("ZM şehir: " + failed + " bina kurulamadı");

        batches = null;
        colliders = null;
        meshVerts = null;
        meshTris = null;
        meshUvs = null;
    }

    // ---------------------------------------------------------------- materials & textures

    private static int Mat(Material m)
    {
        materials.Add(m);
        return materials.Count - 1;
    }

    private static Material NewMat(Color color, Texture2D tex)
    {
        var m = new Material(MaterialCache.Lit(Color.white));
        m.color = color;
        if (tex != null)
            m.mainTexture = tex;
        return m;
    }

    private static void CreateMaterials()
    {
        var facade = FacadeTexture();
        var metal = MetalTexture();
        facadeMats = new int[FacadeColors.Length];
        shellMats = new int[FacadeColors.Length];
        for (int i = 0; i < FacadeColors.Length; i++)
        {
            facadeMats[i] = Mat(NewMat(FacadeColors[i], facade));
            shellMats[i] = Mat(NewMat(FacadeColors[i] * 0.97f, NoiseTexture(64, 0.9f, 1f, 11 + i)));
        }
        metalMat = Mat(NewMat(new Color(0.72f, 0.76f, 0.8f), metal));
        plinthMat = Mat(NewMat(new Color(0.55f, 0.53f, 0.5f), NoiseTexture(64, 0.75f, 1f, 3)));
        roofMat = Mat(NewMat(new Color(0.58f, 0.57f, 0.55f), NoiseTexture(64, 0.7f, 1f, 5)));
        tileRoofMat = Mat(NewMat(new Color(0.78f, 0.36f, 0.24f), TileTexture()));
        floorMat = Mat(NewMat(new Color(0.75f, 0.72f, 0.68f), FloorTexture()));
        glassMat = Mat(NewMat(new Color(0.22f, 0.3f, 0.38f), null));
        stoneMat = Mat(NewMat(new Color(0.9f, 0.88f, 0.82f), NoiseTexture(64, 0.85f, 1f, 7)));
        domeMat = Mat(NewMat(new Color(0.5f, 0.54f, 0.58f), null));
        woodMat = Mat(NewMat(new Color(0.45f, 0.3f, 0.18f), null));
        whiteMat = Mat(NewMat(new Color(0.95f, 0.95f, 0.95f), null));
        signBoardMat = Mat(NewMat(new Color(0.08f, 0.35f, 0.2f), null));
        signMat = Mat(NewMat(Color.white, SignTexture()));
        asphaltLinedMat = Mat(NewMat(Color.white, AsphaltTexture(true)));
        asphaltMat = Mat(NewMat(Color.white, AsphaltTexture(false)));
        poleMat = Mat(NewMat(new Color(0.42f, 0.44f, 0.46f), null));
        lampMat = Mat(NewMat(new Color(0.95f, 0.93f, 0.8f), null));
        trunkMat = Mat(NewMat(new Color(0.36f, 0.24f, 0.13f), null));
        cypressMat = Mat(NewMat(new Color(0.1f, 0.28f, 0.15f), null));
        pineMats = new[]
        {
            Mat(NewMat(new Color(0.12f, 0.36f, 0.2f), null)), Mat(NewMat(new Color(0.15f, 0.42f, 0.22f), null)),
            Mat(NewMat(new Color(0.1f, 0.3f, 0.17f), null))
        };
        leafMats = new[]
        {
            Mat(NewMat(new Color(0.3f, 0.55f, 0.2f), null)), Mat(NewMat(new Color(0.38f, 0.6f, 0.22f), null)),
            Mat(NewMat(new Color(0.26f, 0.48f, 0.18f), null)), Mat(NewMat(new Color(0.5f, 0.58f, 0.22f), null))
        };

        if (PhotoTex.Available)
            UsePhotoTextures();

        cube = PrimitiveMesh(PrimitiveType.Cube);
        cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
        sphere = PrimitiveMesh(PrimitiveType.Sphere);
        blobs = new Mesh[6];
        for (int i = 0; i < blobs.Length; i++)
            blobs[i] = MeshUtil.Blob(300 + i, 0.25f);
    }

    private static bool photo;
    private static bool balconies;
    private static int railMat;

    /// <summary>Swaps the procedural materials for photo-scanned ones (plaster, metal, concrete,
    /// roof tiles, paving, bark, asphalt) with real windows, glass reflections and road markings.</summary>
    private static void UsePhotoTextures()
    {
        var facadeBase = Resources.Load<Material>("ZootopiaFacade");
        var roadBase = Resources.Load<Material>("ZootopiaRoad");
        if (facadeBase == null || roadBase == null)
            return;
        photo = true;
        balconies = PlayerPrefs.GetInt("zm_quality", 1) > 0;

        var windows = WindowMask(false);
        var glass = GlassMask(false);
        var industrial = WindowMask(true);
        var industrialGlass = GlassMask(true);
        var blank = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var clear = new Color32[16];
        blank.SetPixels32(clear);
        blank.Apply();

        System.Func<Texture2D, Texture2D, Color, Texture2D, Texture2D, Vector2, Material> facade = (baseTex, baseNrm, tint, win, gl, scale) =>
        {
            var m = new Material(facadeBase);
            m.SetTexture("_BaseTex", baseTex);
            if (baseNrm != null)
                m.SetTexture("_BaseNrm", baseNrm);
            m.SetColor("_Color", tint);
            m.SetTexture("_WindowTex", win);
            m.SetTexture("_GlassTex", gl);
            m.SetTextureScale("_WindowTex", new Vector2(0.5f, 0.5f));
            m.SetVector("_BaseScale", new Vector4(scale.x, scale.y, 0f, 0f));
            return m;
        };

        var plaster = PhotoTex.Get("plaster_diff");
        var plasterN = PhotoTex.Get("plaster_nor");
        for (int i = 0; i < FacadeColors.Length; i++)
        {
            materials[facadeMats[i]] = facade(plaster, plasterN, FacadeColors[i], windows, glass, new Vector2(2.8f, 2.6f));
            var shell = NewMat(FacadeColors[i] * 1.05f, plaster);
            shell.mainTextureScale = new Vector2(0.6f, 0.6f);
            materials[shellMats[i]] = shell;
        }
        var metalTex = PhotoTex.Get("metal_diff") ?? plaster;
        materials[metalMat] = facade(metalTex, PhotoTex.Get("metal_nor"), new Color(0.82f, 0.86f, 0.9f), industrial, industrialGlass, new Vector2(2f, 1.2f));
        materials[stoneMat] = facade(plaster, plasterN, new Color(0.97f, 0.95f, 0.9f), blank, blank, new Vector2(2f, 2f));

        var concrete = PhotoTex.Get("concrete_diff");
        if (concrete != null)
        {
            materials[plinthMat] = NewMat(new Color(0.85f, 0.83f, 0.8f), concrete);
            materials[roofMat] = NewMat(new Color(0.8f, 0.8f, 0.78f), concrete);
        }
        var tiles = PhotoTex.Get("rooftiles_diff");
        if (tiles != null)
            materials[tileRoofMat] = NewMat(new Color(1f, 0.92f, 0.88f), tiles);
        var paving = PhotoTex.Get("paving_diff");
        if (paving != null)
            materials[floorMat] = NewMat(Color.white, paving);
        var bark = PhotoTex.Get("bark_diff");
        if (bark != null)
            materials[trunkMat] = NewMat(new Color(0.9f, 0.85f, 0.8f), bark);

        System.Func<bool, Material> road = lined =>
        {
            var m = new Material(roadBase);
            m.SetTexture("_AsphaltTex", PhotoTex.Get("asphalt_diff"));
            var n = PhotoTex.Get("asphalt_nor");
            if (n != null)
                m.SetTexture("_AsphaltNrm", n);
            m.SetTexture("_MainTex", RoadMarkings(lined));
            m.SetFloat("_AsphaltTiling", 0.2f);
            return m;
        };
        materials[asphaltLinedMat] = road(true);
        materials[asphaltMat] = road(false);
        railMat = Mat(NewMat(new Color(0.22f, 0.23f, 0.25f), null));
    }

    /// <summary>2 bays x 2 storeys of windows (RGB colour, A mask): plain glass, curtains, blinds, lit room.</summary>
    private static Texture2D WindowMask(bool industrial)
    {
        const int s = 256;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                int cell = (x * 2 / s) + (y * 2 / s) * 2;
                float u = (x % (s / 2) + 0.5f) / (s / 2), v = (y % (s / 2) + 0.5f) / (s / 2);
                Color c = new Color(0f, 0f, 0f, 0f);
                if (industrial)
                {
                    if (v > 0.6f && v < 0.8f && u > 0.04f && u < 0.96f)
                        c = (u * 8f) % 1f < 0.06f ? new Color(0.35f, 0.37f, 0.4f, 1f) : new Color(0.14f, 0.18f, 0.22f, 1f);
                    else if (v < 0.03f)
                        c = new Color(0.4f, 0.42f, 0.44f, 0.6f);
                }
                else
                {
                    if (v < 0.045f)
                        c = new Color(0.66f, 0.64f, 0.6f, 0.55f);                       // floor slab band
                    bool frame = u > 0.24f && u < 0.76f && v > 0.28f && v < 0.86f;
                    bool glassArea = u > 0.27f && u < 0.73f && v > 0.31f && v < 0.83f;
                    if (frame)
                        c = new Color(0.93f, 0.93f, 0.91f, 1f);
                    if (glassArea)
                    {
                        float shade = Mathf.Lerp(0.1f, 0.2f, v);
                        c = new Color(shade * 0.8f, shade * 0.95f, shade * 1.15f, 1f);
                        if (cell == 1 && (u < 0.37f || u > 0.63f))
                            c = new Color(0.78f, 0.7f, 0.56f, 1f);                       // curtains
                        else if (cell == 2 && ((int)(v * 60f)) % 2 == 0)
                            c = new Color(0.72f, 0.72f, 0.7f, 1f);                       // blinds
                        else if (cell == 3 && v < 0.6f)
                            c = new Color(0.42f, 0.36f, 0.26f, 1f);                      // warm room
                        if (Mathf.Abs(u - 0.5f) < 0.012f)
                            c = new Color(0.93f, 0.93f, 0.91f, 1f);                      // mullion
                    }
                    if (u > 0.22f && u < 0.78f && v > 0.25f && v < 0.28f)
                        c = new Color(0.7f, 0.7f, 0.68f, 1f);                            // sill
                    if (u > 0.25f && u < 0.75f && v > 0.86f && v < 0.92f)
                        c = new Color(0.55f, 0.55f, 0.55f, 1f);                          // shutter box
                }
                px[y * s + x] = c;
            }
        }
        return MakeTexture(s, s, px, true);
    }

    /// <summary>Where the window mask is reflective glass (R).</summary>
    private static Texture2D GlassMask(bool industrial)
    {
        const int s = 128;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                int cell = (x * 2 / s) + (y * 2 / s) * 2;
                float u = (x % (s / 2) + 0.5f) / (s / 2), v = (y % (s / 2) + 0.5f) / (s / 2);
                float g = 0f;
                if (industrial)
                    g = v > 0.6f && v < 0.8f && u > 0.04f && u < 0.96f ? 0.8f : 0f;
                else if (u > 0.27f && u < 0.73f && v > 0.31f && v < 0.83f && Mathf.Abs(u - 0.5f) >= 0.012f)
                {
                    g = 1f;
                    if (cell == 1 && (u < 0.37f || u > 0.63f)) g = 0.1f;
                    if (cell == 2) g = 0.35f;
                    if (cell == 3 && v < 0.6f) g = 0.4f;
                }
                byte b = (byte)(g * 255f);
                px[y * s + x] = new Color32(b, b, b, 255);
            }
        }
        return MakeTexture(s, s, px, true);
    }

    /// <summary>Road paint and kerbs (RGB colour, A mask); u across the road, v along (8 m per tile).</summary>
    private static Texture2D RoadMarkings(bool lined)
    {
        const int w = 64, h = 128;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float edge = Mathf.Min(u, 1f - u);
                Color c = new Color(0f, 0f, 0f, 0f);
                if (edge < 0.012f)
                    c = new Color(0.2f, 0.2f, 0.2f, 0.45f);                  // gutter shadow
                else if (lined && edge > 0.05f && edge < 0.07f)
                    c = new Color(0.92f, 0.92f, 0.88f, 0.9f);                // edge line
                else if (lined && Mathf.Abs(u - 0.5f) < 0.013f && v < 0.5f)
                    c = new Color(0.95f, 0.95f, 0.9f, 0.9f);                 // centre dash
                px[y * w + x] = c;
            }
        }
        return MakeTexture(w, h, px, true);
    }

    private static readonly Dictionary<PrimitiveType, Mesh> primitives = new Dictionary<PrimitiveType, Mesh>();

    private static Mesh PrimitiveMesh(PrimitiveType type)
    {
        Mesh m;
        if (primitives.TryGetValue(type, out m) && m != null)
            return m;
        var go = GameObject.CreatePrimitive(type);
        m = go.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(go);
        primitives[type] = m;
        return m;
    }

    private static Texture2D MakeTexture(int w, int h, Color32[] px, bool repeat)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        tex.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        tex.SetPixels32(px);
        tex.Apply(true, true);
        return tex;
    }

    private static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int n = x * 374761393 + y * 668265263 + seed * 2147483647;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
        }
    }

    private static Texture2D NoiseTexture(int size, float min, float max, int seed)
    {
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Hash(x, y, seed) * 0.5f + Hash(x / 4, y / 4, seed + 1) * 0.5f;
                byte b = (byte)(Mathf.Lerp(min, max, n) * 255f);
                px[y * size + x] = new Color32(b, b, b, 255);
            }
        return MakeTexture(size, size, px, true);
    }

    /// <summary>One window bay (3.2 m wide, one 3 m storey): plaster wall, framed window, sill, floor band.</summary>
    private static Texture2D FacadeTexture()
    {
        const int s = 128;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float u = (x + 0.5f) / s, v = (y + 0.5f) / s;
                float n = 0.94f + Hash(x, y, 1) * 0.06f;
                Color c = new Color(n, n, n);
                if (v < 0.05f)
                    c *= 0.82f;                                   // floor slab band
                bool inFrame = u > 0.26f && u < 0.74f && v > 0.3f && v < 0.84f;
                bool inGlass = u > 0.29f && u < 0.71f && v > 0.33f && v < 0.81f;
                if (inFrame)
                    c = new Color(0.92f, 0.92f, 0.9f);
                if (inGlass)
                {
                    float refl = Mathf.Lerp(0.18f, 0.34f, v) + (u + v > 1.15f && u + v < 1.25f ? 0.12f : 0f);
                    c = new Color(refl * 0.8f, refl * 0.95f, refl * 1.15f);
                    if (Mathf.Abs(u - 0.5f) < 0.012f)
                        c = new Color(0.9f, 0.9f, 0.88f);         // mullion
                }
                if (u > 0.24f && u < 0.76f && v > 0.27f && v < 0.3f)
                    c = new Color(0.7f, 0.7f, 0.68f);             // sill
                px[y * s + x] = c;
            }
        }
        return MakeTexture(s, s, px, true);
    }

    private static Texture2D MetalTexture()
    {
        const int s = 64;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float rib = 0.82f + 0.18f * Mathf.Abs(Mathf.Sin(x / (float)s * Mathf.PI * 8f));
                float v = (y + 0.5f) / s;
                bool strip = v > 0.62f && v < 0.78f && (x % 16) > 2;
                float n = rib * (0.95f + Hash(x, y, 9) * 0.05f);
                px[y * s + x] = strip ? new Color(0.25f, 0.32f, 0.38f) : new Color(n, n, n);
            }
        return MakeTexture(s, s, px, true);
    }

    private static Texture2D TileTexture()
    {
        const int s = 64;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                int row = y / 8;
                float wave = Mathf.Abs(Mathf.Sin(((x + (row % 2) * 4) / 8f) * Mathf.PI));
                float shade = 0.72f + 0.28f * wave;
                if (y % 8 == 0)
                    shade *= 0.7f;
                shade *= 0.92f + Hash(x / 8, row, 4) * 0.12f;
                px[y * s + x] = new Color(shade, shade, shade);
            }
        return MakeTexture(s, s, px, true);
    }

    private static Texture2D FloorTexture()
    {
        const int s = 64;
        var px = new Color32[s * s];
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                bool grout = x % 32 < 1 || y % 32 < 1;
                float n = grout ? 0.6f : 0.9f + Hash(x / 32, y / 32, 6) * 0.1f;
                px[y * s + x] = new Color(n, n, n);
            }
        return MakeTexture(s, s, px, true);
    }

    /// <summary>Asphalt strip: u across the road (0..1), v along it (one tile = 8 m).</summary>
    private static Texture2D AsphaltTexture(bool lined)
    {
        const int w = 64, h = 128;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float n = 0.26f + Hash(x, y, 2) * 0.06f + Hash(x / 6, y / 6, 3) * 0.04f;
                Color c = new Color(n, n, n * 1.04f);
                float edge = Mathf.Min(u, 1f - u);
                if (edge < 0.03f)
                    c = new Color(0.55f, 0.54f, 0.52f);          // kerb
                else if (lined && edge > 0.045f && edge < 0.065f)
                    c = new Color(0.86f, 0.86f, 0.82f);          // edge line
                else if (lined && Mathf.Abs(u - 0.5f) < 0.012f && v < 0.5f)
                    c = new Color(0.88f, 0.88f, 0.84f);          // centre dash
                px[y * w + x] = c;
            }
        }
        return MakeTexture(w, h, px, true);
    }

    // 5x7 pixel glyphs for the clinic sign.
    private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        { 'Z', new[] { "11111", "00001", "00010", "00100", "01000", "10000", "11111" } },
        { 'O', new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" } },
        { 'T', new[] { "11111", "00100", "00100", "00100", "00100", "00100", "00100" } },
        { 'P', new[] { "11110", "10001", "10001", "11110", "10000", "10000", "10000" } },
        { 'I', new[] { "01110", "00100", "00100", "00100", "00100", "00100", "01110" } },
        { 'A', new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" } },
        { 'V', new[] { "10001", "10001", "10001", "10001", "10001", "01010", "00100" } },
        { 'E', new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" } },
        { 'R', new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" } },
        { 'N', new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" } },
        { 'K', new[] { "10001", "10010", "10100", "11000", "10100", "10010", "10001" } },
        { 'L', new[] { "10000", "10000", "10000", "10000", "10000", "10000", "11111" } },
        { 'G', new[] { "01110", "10001", "10000", "10111", "10001", "10001", "01110" } },
        { '7', new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" } },
        { '2', new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" } },
        { '4', new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" } },
        { '/', new[] { "00001", "00010", "00010", "00100", "01000", "01000", "10000" } },
        { ' ', new[] { "00000", "00000", "00000", "00000", "00000", "00000", "00000" } },
    };

    private static void DrawText(Color32[] px, int texW, int texH, string text, int x0, int y0, int scale, Color32 color)
    {
        int x = x0;
        foreach (char raw in text)
        {
            char ch = raw;
            bool dotAbove = false, breve = false;
            if (ch == 'İ') { ch = 'I'; dotAbove = true; }
            if (ch == 'Ğ') { ch = 'G'; breve = true; }
            string[] g;
            if (!Glyphs.TryGetValue(ch, out g))
                g = Glyphs[' '];
            for (int row = 0; row < 7; row++)
                for (int col = 0; col < 5; col++)
                    if (g[row][col] == '1')
                        FillRect(px, texW, texH, x + col * scale, y0 + (6 - row) * scale, scale, scale, color);
            if (dotAbove)
                FillRect(px, texW, texH, x + 2 * scale, y0 + 8 * scale, scale, scale, color);
            if (breve)
                FillRect(px, texW, texH, x + scale, y0 + 8 * scale, scale * 3, scale, color);
            x += 6 * scale;
        }
    }

    private static int TextWidth(string text, int scale)
    {
        return text.Length * 6 * scale - scale;
    }

    private static void FillRect(Color32[] px, int w, int h, int x0, int y0, int rw, int rh, Color32 c)
    {
        for (int y = y0; y < y0 + rh; y++)
            for (int x = x0; x < x0 + rw; x++)
                if (x >= 0 && y >= 0 && x < w && y < h)
                    px[y * w + x] = c;
    }

    /// <summary>"ZOOTOPIA / VETERİNER KLİNİĞİ 7/24" on green, with a white medical cross.</summary>
    private static Texture2D SignTexture()
    {
        const int w = 512, h = 128;
        var px = new Color32[w * h];
        Color32 bg = new Color32(22, 110, 60, 255);
        Color32 white = new Color32(250, 250, 245, 255);
        for (int i = 0; i < px.Length; i++)
            px[i] = bg;
        FillRect(px, w, h, 4, 4, w - 8, 3, white);
        FillRect(px, w, h, 4, h - 7, w - 8, 3, white);
        // cross
        FillRect(px, w, h, 30, 52, 60, 24, white);
        FillRect(px, w, h, 48, 34, 24, 60, white);
        string l1 = "ZOOTOPIA";
        string l2 = "VETERİNER KLİNİĞİ 7/24";
        DrawText(px, w, h, l1, 110 + (390 - TextWidth(l1, 7)) / 2, 62, 7, white);
        DrawText(px, w, h, l2, 110 + (390 - TextWidth(l2, 3)) / 2, 18, 3, white);
        return MakeTexture(w, h, px, false);
    }

    // ---------------------------------------------------------------- batching

    private static int ChunkOf(float x, float z)
    {
        int n = Mathf.CeilToInt(World.MapSize / ChunkSize);
        int cx = Mathf.Clamp((int)((x + World.MapSize * 0.5f) / ChunkSize), 0, n - 1);
        int cz = Mathf.Clamp((int)((z + World.MapSize * 0.5f) / ChunkSize), 0, n - 1);
        return cz * n + cx;
    }

    private static Batch Get(int chunk, int mat)
    {
        long key = (long)chunk * 1000 + mat;
        Batch b;
        if (!batches.TryGetValue(key, out b))
        {
            b = new Batch();
            batches[key] = b;
        }
        return b;
    }

    private static Batch Col(int chunk)
    {
        Batch b;
        if (!colliders.TryGetValue(chunk, out b))
        {
            b = new Batch();
            colliders[chunk] = b;
        }
        return b;
    }

    /// <summary>Quad a-b-c-d (in order around the face); faces the side of the normal hint n.</summary>
    private static void Quad(Batch b, Vector3 a, Vector3 bb, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 n)
    {
        int i = b.v.Count;
        b.v.Add(a); b.v.Add(bb); b.v.Add(c); b.v.Add(d);
        b.uv.Add(ua); b.uv.Add(ub); b.uv.Add(uc); b.uv.Add(ud);
        if (Vector3.Dot(Vector3.Cross(bb - a, c - a), n) >= 0f)
        {
            b.t.Add(i); b.t.Add(i + 1); b.t.Add(i + 2);
            b.t.Add(i); b.t.Add(i + 2); b.t.Add(i + 3);
        }
        else
        {
            b.t.Add(i); b.t.Add(i + 2); b.t.Add(i + 1);
            b.t.Add(i); b.t.Add(i + 3); b.t.Add(i + 2);
        }
    }

    private static void Face(int chunk, int mat, bool solid, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 n)
    {
        Quad(Get(chunk, mat), a, b, c, d, ua, ub, uc, ud, n);
        if (solid)
            Quad(Col(chunk), a, b, c, d, ua, ub, uc, ud, n);
    }

    private static void Tri(Batch b, Vector3 a, Vector3 bb, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 n)
    {
        int i = b.v.Count;
        b.v.Add(a); b.v.Add(bb); b.v.Add(c);
        b.uv.Add(ua); b.uv.Add(ub); b.uv.Add(uc);
        if (Vector3.Dot(Vector3.Cross(bb - a, c - a), n) >= 0f)
        {
            b.t.Add(i); b.t.Add(i + 1); b.t.Add(i + 2);
        }
        else
        {
            b.t.Add(i); b.t.Add(i + 2); b.t.Add(i + 1);
        }
    }

    private static void AddMesh(int chunk, int mat, Mesh mesh, Matrix4x4 m, bool solid)
    {
        Vector3[] verts;
        if (!meshVerts.TryGetValue(mesh, out verts))
        {
            verts = mesh.vertices;
            meshVerts[mesh] = verts;
            meshTris[mesh] = mesh.triangles;
            var uv = mesh.uv;
            meshUvs[mesh] = uv != null && uv.Length == verts.Length ? uv : new Vector2[verts.Length];
        }
        int[] tris = meshTris[mesh];
        Vector2[] uvs = meshUvs[mesh];
        AppendMesh(Get(chunk, mat), verts, tris, uvs, m);
        if (solid)
            AppendMesh(Col(chunk), verts, tris, uvs, m);
    }

    private static void AppendMesh(Batch b, Vector3[] verts, int[] tris, Vector2[] uvs, Matrix4x4 m)
    {
        int start = b.v.Count;
        for (int i = 0; i < verts.Length; i++)
        {
            b.v.Add(m.MultiplyPoint3x4(verts[i]));
            b.uv.Add(uvs[i]);
        }
        for (int i = 0; i < tris.Length; i++)
            b.t.Add(start + tris[i]);
    }

    /// <summary>Axis-aligned box in a building's local frame (origin o, long axis L, short axis S).</summary>
    private static void Box(int chunk, int mat, bool solid, Vector3 o, Vector3 L, Vector3 S, Vector3 center, Vector3 half, float uvScale)
    {
        Vector3 c = o + L * center.x + Vector3.up * center.y + S * center.z;
        Vector3 ex = L * half.x, ey = Vector3.up * half.y, ez = S * half.z;
        float sx = half.x * 2f * uvScale, sy = half.y * 2f * uvScale, sz = half.z * 2f * uvScale;
        // +L / -L
        Face(chunk, mat, solid, c + ex - ey - ez, c + ex - ey + ez, c + ex + ey + ez, c + ex + ey - ez,
            new Vector2(0, 0), new Vector2(sz, 0), new Vector2(sz, sy), new Vector2(0, sy), L);
        Face(chunk, mat, solid, c - ex - ey + ez, c - ex - ey - ez, c - ex + ey - ez, c - ex + ey + ez,
            new Vector2(0, 0), new Vector2(sz, 0), new Vector2(sz, sy), new Vector2(0, sy), -L);
        // +S / -S
        Face(chunk, mat, solid, c - ex - ey + ez, c + ex - ey + ez, c + ex + ey + ez, c - ex + ey + ez,
            new Vector2(0, 0), new Vector2(sx, 0), new Vector2(sx, sy), new Vector2(0, sy), S);
        Face(chunk, mat, solid, c + ex - ey - ez, c - ex - ey - ez, c - ex + ey - ez, c + ex + ey - ez,
            new Vector2(0, 0), new Vector2(sx, 0), new Vector2(sx, sy), new Vector2(0, sy), -S);
        // top / bottom
        Face(chunk, mat, solid, c - ex + ey - ez, c + ex + ey - ez, c + ex + ey + ez, c - ex + ey + ez,
            new Vector2(0, 0), new Vector2(sx, 0), new Vector2(sx, sz), new Vector2(0, sz), Vector3.up);
        Face(chunk, mat, solid, c - ex - ey + ez, c + ex - ey + ez, c + ex - ey - ez, c - ex - ey - ez,
            new Vector2(0, 0), new Vector2(sx, 0), new Vector2(sx, sz), new Vector2(0, sz), Vector3.down);
    }

    private static void Flush(Transform parent)
    {
        var city = new GameObject("City").transform;
        city.SetParent(parent, false);
        foreach (var kv in batches)
        {
            Batch b = kv.Value;
            if (b.t.Count == 0)
                continue;
            int mat = (int)(kv.Key % 1000);
            var mesh = new Mesh { name = "City_" + kv.Key };
            if (b.v.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(b.v);
            mesh.SetUVs(0, b.uv);
            mesh.SetTriangles(b.t, 0);
            mesh.RecalculateNormals();
            if (photo)
                mesh.RecalculateTangents();   // normal-mapped façades and roads
            mesh.RecalculateBounds();
            var go = new GameObject("CityMesh");
            go.transform.SetParent(city, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = materials[mat];
            bool flat = mat == asphaltMat || mat == asphaltLinedMat || mat == floorMat || mat == signMat;
            mr.shadowCastingMode = flat ? ShadowCastingMode.Off : ShadowCastingMode.On;
        }
        foreach (var kv in colliders)
        {
            Batch b = kv.Value;
            if (b.t.Count == 0)
                continue;
            var mesh = new Mesh { name = "CityCollider_" + kv.Key };
            if (b.v.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(b.v);
            mesh.SetTriangles(b.t, 0);
            mesh.RecalculateBounds();
            var go = new GameObject("CityCollider");
            go.transform.SetParent(city, false);
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
    }

    // ---------------------------------------------------------------- buildings

    private static int PickColor(MapData.Building b, int salt)
    {
        int h = Mathf.Abs(Mathf.RoundToInt(b.obbCenter.x * 13.7f) * 31 + Mathf.RoundToInt(b.obbCenter.y * 7.3f) + salt);
        return h % FacadeColors.Length;
    }

    private static void BuildBuilding(MapData.Building b)
    {
        Vector2[] o = b.outline;
        if (o == null || o.Length < 3)
            return;

        // Make sure the outline runs counter-clockwise (seen from above), so (dz, -dx) points outward.
        float area = 0f;
        for (int i = 0; i < o.Length; i++)
        {
            Vector2 p = o[i], q = o[(i + 1) % o.Length];
            area += p.x * q.y - q.x * p.y;
        }
        bool ccw = area > 0f;

        float minG = float.MaxValue, maxG = float.MinValue;
        foreach (var p in o)
        {
            float h = World.HeightAt(p.x, p.y);
            minG = Mathf.Min(minG, h);
            maxG = Mathf.Max(maxG, h);
        }
        float hc = World.HeightAt(b.obbCenter.x, b.obbCenter.y);
        minG = Mathf.Min(minG, hc);
        maxG = Mathf.Max(maxG, hc);

        float y0 = maxG + 0.15f;
        float yb = minG - 0.6f;
        float floorH = b.kind == MapData.KindIndustrial ? 4.5f : FloorHeight;
        float yTop = y0 + b.levels * floorH;
        int chunk = ChunkOf(b.obbCenter.x, b.obbCenter.y);

        bool enterable = (b.flags & MapData.FlagEnterable) != 0;
        bool hip = (b.flags & MapData.FlagHipRoof) != 0;
        int colorIdx = PickColor(b, 0);
        int wallMat = b.kind == MapData.KindIndustrial || b.kind == MapData.KindShed ? metalMat
            : b.kind == MapData.KindMosque ? stoneMat : facadeMats[colorIdx];

        // OBB frame
        float ang = b.obbAngle * Mathf.Deg2Rad;
        Vector3 L = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
        Vector3 S = new Vector3(-L.z, 0f, L.x);
        float hw = b.obbWidth * 0.5f, hd = b.obbDepth * 0.5f;

        if (enterable)
        {
            BuildEnterable(b, chunk, L, S, hw, hd, y0, yb, floorH, yTop, colorIdx, hip);
            return;
        }

        float parapet = hip ? 0f : (b.kind == MapData.KindShed ? 0f : 0.6f);
        Walls(chunk, o, ccw, yb, y0, yTop + parapet, y0, floorH, wallMat, true);

        if (hip)
            HipRoof(chunk, new Vector3(b.obbCenter.x, yTop, b.obbCenter.y), L, S, hw + 0.35f, hd + 0.35f);
        else
            FlatRoof(chunk, o, b.roofTriangles, ccw, yTop, parapet);

        if (photo && balconies && b.kind == MapData.KindApartment && b.levels >= 3)
            Balconies(chunk, o, ccw, y0, b.levels, colorIdx);

        if (b.kind == MapData.KindMosque)
            MosqueExtras(chunk, b, L, S, hw, hd, y0, yTop);
    }

    /// <summary>Balconies (slab + railing) on every other bay of the two longest walls, from the first floor up.</summary>
    private static void Balconies(int chunk, Vector2[] o, bool ccw, float y0, int levels, int colorIdx)
    {
        int n = o.Length;
        int best = -1, second = -1;
        float bestLen = 0f, secondLen = 0f;
        for (int i = 0; i < n; i++)
        {
            float len = (o[(i + 1) % n] - o[i]).magnitude;
            if (len > bestLen) { second = best; secondLen = bestLen; best = i; bestLen = len; }
            else if (len > secondLen) { second = i; secondLen = len; }
        }
        foreach (int e in new[] { best, second })
        {
            if (e < 0)
                continue;
            Vector2 a = o[e], c = o[(e + 1) % n];
            Vector2 d = c - a;
            float len = d.magnitude;
            if (len < 9f)
                continue;
            Vector3 along = new Vector3(d.x, 0f, d.y) / len;
            Vector3 outward = ccw ? new Vector3(d.y, 0f, -d.x) / len : new Vector3(-d.y, 0f, d.x) / len;
            Vector2 mid = (a + c) * 0.5f + new Vector2(outward.x, outward.z) * 2.5f;
            if (World.IsBlocked(mid.x, mid.y))
                continue;   // a neighbour's wall right there (terraced houses)
            float bays = Mathf.Max(1f, Mathf.Round(len / 3.2f));
            float bayLen = len / bays;
            for (int k = (e % 2); k < bays; k += 2)
            {
                Vector3 basePos = new Vector3(a.x, 0f, a.y) + along * (bayLen * (k + 0.5f));
                for (int f = 1; f < levels; f++)
                {
                    Vector3 p = basePos + Vector3.up * (y0 + f * FloorHeight);
                    // slab
                    Box(chunk, plinthMat, false, p, along, outward, new Vector3(0f, -0.07f, 0.5f), new Vector3(bayLen * 0.4f, 0.08f, 0.5f), 0.5f);
                    // railing (front)
                    Box(chunk, railMat, false, p, along, outward, new Vector3(0f, 0.5f, 0.97f), new Vector3(bayLen * 0.4f, 0.5f, 0.025f), 1f);
                }
            }
        }
    }

    /// <summary>Outer walls of a polygon: concrete plinth below the ground floor, façade above it.</summary>
    private static void Walls(int chunk, Vector2[] o, bool ccw, float yb, float y0, float yWallTop, float facadeBase, float floorH, int wallMat, bool solid)
    {
        int n = o.Length;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = o[i], c = o[(i + 1) % n];
            Vector2 d = c - a;
            float len = d.magnitude;
            if (len < 0.05f)
                continue;
            Vector3 outward = ccw ? new Vector3(d.y, 0f, -d.x) / len : new Vector3(-d.y, 0f, d.x) / len;
            Vector3 A = new Vector3(a.x, 0f, a.y), C = new Vector3(c.x, 0f, c.y);

            if (y0 - yb > 0.02f)
            {
                Face(chunk, plinthMat, solid, A + Vector3.up * yb, C + Vector3.up * yb, C + Vector3.up * y0, A + Vector3.up * y0,
                    new Vector2(0f, 0f), new Vector2(len * 0.5f, 0f), new Vector2(len * 0.5f, (y0 - yb) * 0.5f), new Vector2(0f, (y0 - yb) * 0.5f), outward);
            }
            float bays = Mathf.Max(1f, Mathf.Round(len / 3.2f));
            if (wallMat == metalMat || wallMat == stoneMat)
                bays = len / 3f;
            float v0 = (y0 - facadeBase) / floorH, v1 = (yWallTop - facadeBase) / floorH;
            Face(chunk, wallMat, solid, A + Vector3.up * y0, C + Vector3.up * y0, C + Vector3.up * yWallTop, A + Vector3.up * yWallTop,
                new Vector2(0f, v0), new Vector2(bays, v0), new Vector2(bays, v1), new Vector2(0f, v1), outward);
        }
    }

    private static void FlatRoof(int chunk, Vector2[] o, int[] tris, bool ccw, float y, float parapet)
    {
        var batch = Get(chunk, roofMat);
        var col = Col(chunk);
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            int ia = tris[i], ib = tris[i + 1], ic = tris[i + 2];
            if (ia >= o.Length || ib >= o.Length || ic >= o.Length)
                continue;
            Vector3 a = new Vector3(o[ia].x, y, o[ia].y), b = new Vector3(o[ib].x, y, o[ib].y), c = new Vector3(o[ic].x, y, o[ic].y);
            Vector2 ua = new Vector2(a.x, a.z) * 0.25f, ub = new Vector2(b.x, b.z) * 0.25f, uc = new Vector2(c.x, c.z) * 0.25f;
            Tri(batch, a, b, c, ua, ub, uc, Vector3.up);
            Tri(col, a, b, c, ua, ub, uc, Vector3.up);
        }
        if (parapet <= 0f)
            return;
        // Inner faces of the parapet (the outer faces are the façade).
        int n = o.Length;
        for (int i = 0; i < n; i++)
        {
            Vector2 a2 = o[i], c2 = o[(i + 1) % n];
            Vector2 d = c2 - a2;
            float len = d.magnitude;
            if (len < 0.05f)
                continue;
            Vector3 inward = ccw ? new Vector3(-d.y, 0f, d.x) / len : new Vector3(d.y, 0f, -d.x) / len;
            Vector3 A = new Vector3(a2.x, y, a2.y), C = new Vector3(c2.x, y, c2.y);
            Face(chunk, roofMat, false, A, C, C + Vector3.up * parapet, A + Vector3.up * parapet,
                Vector2.zero, new Vector2(len * 0.25f, 0f), new Vector2(len * 0.25f, 0.15f), new Vector2(0f, 0.15f), inward);
        }
    }

    private static void HipRoof(int chunk, Vector3 c, Vector3 L, Vector3 S, float hw, float hd)
    {
        float rise = hd * 0.55f;
        float ridge = Mathf.Max(0f, hw - hd);
        Vector3 up = Vector3.up * rise;
        Vector3 c00 = c - L * hw - S * hd, c10 = c + L * hw - S * hd, c11 = c + L * hw + S * hd, c01 = c - L * hw + S * hd;
        Vector3 r0 = c - L * ridge + up, r1 = c + L * ridge + up;
        float slope = Mathf.Sqrt(hd * hd + rise * rise);
        float k = 0.5f;   // tile texture scale (2 m per tile repeat)
        // long sides (trapezoids)
        Face(chunk, tileRoofMat, true, c00, c10, r1, r0,
            new Vector2(-hw * k, 0f), new Vector2(hw * k, 0f), new Vector2(ridge * k, slope * k), new Vector2(-ridge * k, slope * k), -S + Vector3.up);
        Face(chunk, tileRoofMat, true, c11, c01, r0, r1,
            new Vector2(-hw * k, 0f), new Vector2(hw * k, 0f), new Vector2(ridge * k, slope * k), new Vector2(-ridge * k, slope * k), S + Vector3.up);
        // hipped ends (triangles)
        float slopeEnd = Mathf.Sqrt((hw - ridge) * (hw - ridge) + rise * rise);
        Tri(Get(chunk, tileRoofMat), c10, c11, r1, new Vector2(-hd * k, 0f), new Vector2(hd * k, 0f), new Vector2(0f, slopeEnd * k), L + Vector3.up);
        Tri(Get(chunk, tileRoofMat), c01, c00, r0, new Vector2(-hd * k, 0f), new Vector2(hd * k, 0f), new Vector2(0f, slopeEnd * k), -L + Vector3.up);
        Tri(Col(chunk), c10, c11, r1, Vector2.zero, Vector2.zero, Vector2.zero, L + Vector3.up);
        Tri(Col(chunk), c01, c00, r0, Vector2.zero, Vector2.zero, Vector2.zero, -L + Vector3.up);
        // eaves underside so the overhang isn't see-through from below
        Face(chunk, roofMat, false, c00, c10, c11, c01, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.down);
    }

    private static void MosqueExtras(int chunk, MapData.Building b, Vector3 L, Vector3 S, float hw, float hd, float y0, float yTop)
    {
        Vector3 c = new Vector3(b.obbCenter.x, yTop, b.obbCenter.y);
        float r = Mathf.Min(hw, hd) * 0.75f;
        // drum + dome
        AddMesh(chunk, stoneMat, cylinder, Matrix4x4.TRS(c + Vector3.up * (r * 0.2f), Quaternion.identity, new Vector3(r * 1.9f, r * 0.2f, r * 1.9f)), true);
        AddMesh(chunk, domeMat, sphere, Matrix4x4.TRS(c + Vector3.up * (r * 0.4f), Quaternion.identity, new Vector3(r * 1.9f, r * 1.6f, r * 1.9f)), true);
        AddMesh(chunk, domeMat, MeshUtil.Cone, Matrix4x4.TRS(c + Vector3.up * (r * 1.15f), Quaternion.identity, new Vector3(0.3f, 1.4f, 0.3f)), false);
        // minaret at a corner
        Vector3 basePos = new Vector3(b.obbCenter.x, y0, b.obbCenter.y) + L * (hw + 1.6f) + S * (hd - 1.4f);
        float ground = World.HeightAt(basePos.x, basePos.z) - 0.3f;
        float h = (yTop - y0) + 22f;
        basePos.y = ground;
        AddMesh(chunk, stoneMat, cylinder, Matrix4x4.TRS(basePos + Vector3.up * (h * 0.5f), Quaternion.identity, new Vector3(2.2f, h * 0.5f, 2.2f)), true);
        AddMesh(chunk, stoneMat, cylinder, Matrix4x4.TRS(basePos + Vector3.up * (h * 0.78f), Quaternion.identity, new Vector3(3.4f, 0.15f, 3.4f)), true);
        AddMesh(chunk, domeMat, MeshUtil.Cone, Matrix4x4.TRS(basePos + Vector3.up * h, Quaternion.identity, new Vector3(2.4f, 6f, 2.4f)), false);
    }

    /// <summary>
    /// Rectangular building whose ground floor you can walk into (two doors, windows, loot),
    /// with the upper storeys as a solid block above.
    /// </summary>
    private static void BuildEnterable(MapData.Building b, int chunk, Vector3 L, Vector3 S, float hw, float hd,
        float y0, float yb, float floorH, float yTop, int colorIdx, bool hip)
    {
        // Use the OBB corners for the floor level too.
        Vector3 c0 = new Vector3(b.obbCenter.x, 0f, b.obbCenter.y);
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = c0 + L * ((i & 1) == 0 ? -hw : hw) + S * ((i & 2) == 0 ? -hd : hd);
            float h = World.HeightAt(corner.x, corner.z);
            y0 = Mathf.Max(y0, h + 0.15f);
            yb = Mathf.Min(yb, h - 0.6f);
        }
        yTop = y0 + b.levels * floorH;
        Vector3 o = new Vector3(b.obbCenter.x, y0, b.obbCenter.y);
        const float t = 0.25f;
        const float door = 1.7f, doorH = 2.3f;
        int shell = shellMats[colorIdx];
        bool clinic = (b.flags & MapData.FlagClinic) != 0;
        if (clinic)
            shell = whiteMat;

        // Foundation + floor
        Box(chunk, plinthMat, true, o, L, S, new Vector3(0f, (yb - y0) * 0.5f, 0f), new Vector3(hw, (y0 - yb) * 0.5f, hd), 0.5f);
        Face(chunk, floorMat, false, o + L * -hw + S * -hd + Vector3.up * 0.02f, o + L * hw + S * -hd + Vector3.up * 0.02f,
            o + L * hw + S * hd + Vector3.up * 0.02f, o + L * -hw + S * hd + Vector3.up * 0.02f,
            Vector2.zero, new Vector2(hw, 0f), new Vector2(hw, hd), new Vector2(0f, hd), Vector3.up);

        // Long walls with a centred door
        float sideLen = hw - door * 0.5f;
        for (int s = -1; s <= 1; s += 2)
        {
            float z = s * (hd - t * 0.5f);
            Box(chunk, shell, true, o, L, S, new Vector3(-(hw + door * 0.5f) * 0.5f, ShellHeight * 0.5f, z), new Vector3(sideLen * 0.5f, ShellHeight * 0.5f, t * 0.5f), 0.4f);
            Box(chunk, shell, true, o, L, S, new Vector3((hw + door * 0.5f) * 0.5f, ShellHeight * 0.5f, z), new Vector3(sideLen * 0.5f, ShellHeight * 0.5f, t * 0.5f), 0.4f);
            Box(chunk, shell, true, o, L, S, new Vector3(0f, (doorH + ShellHeight) * 0.5f, z), new Vector3(door * 0.5f, (ShellHeight - doorH) * 0.5f, t * 0.5f), 0.4f);
            // the door itself (opens with KAPI; bots push it open)
            Door.Create(doorParent, o + L * (-door * 0.5f) + S * z, L, door, doorH, doorMat);
            // shop windows either side of the door (glass panes on the outside)
            for (int w = -1; w <= 1; w += 2)
            {
                float x = w * (door * 0.5f + sideLen * 0.5f);
                float paneW = Mathf.Min(2.4f, sideLen - 0.8f);
                if (paneW > 0.6f)
                    Box(chunk, glassMat, false, o, L, S, new Vector3(x, 1.55f, s * (hd + 0.02f)), new Vector3(paneW * 0.5f, 0.7f, 0.03f), 1f);
            }
        }
        // Steps down from each door to the ground outside (floors sit level with the highest corner).
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 outside = o + S * (s * (hd + 1.2f));
            if (World.IsBlocked(outside.x, outside.z))
                continue;
            float rise = y0 - World.HeightAt(outside.x, outside.z);
            if (rise <= 0.22f)
                continue;
            int steps = Mathf.Min(12, Mathf.CeilToInt(rise / 0.22f));
            float stepH = rise / steps;
            const float tread = 0.32f;
            for (int k = 0; k < steps; k++)
            {
                float top = -k * stepH;                          // relative to the floor
                float bottom = -rise - 0.4f;
                float zc = s * (hd + tread * (k + 0.5f));
                Box(chunk, plinthMat, true, o, L, S, new Vector3(0f, (top + bottom) * 0.5f, zc),
                    new Vector3(door * 0.5f + 0.3f, (top - bottom) * 0.5f, tread * 0.5f), 0.5f);
            }
        }

        // Short walls
        for (int s = -1; s <= 1; s += 2)
            Box(chunk, shell, true, o, L, S, new Vector3(s * (hw - t * 0.5f), ShellHeight * 0.5f, 0f), new Vector3(t * 0.5f, ShellHeight * 0.5f, hd - t), 0.4f);

        // Ceiling / upper block
        if (b.levels > 1)
        {
            Vector2[] rect = RectOutline(b.obbCenter, L, S, hw, hd);
            float yU = y0 + ShellHeight;
            int wallMat = clinic ? facadeMats[1] : facadeMats[colorIdx];
            Walls(chunk, rect, true, yU, yU, yTop + (hip ? 0f : 0.6f), y0, floorH, wallMat, true);
            // ceiling of the ground floor (seen from inside)
            Face(chunk, roofMat, true, o + L * -hw + S * -hd + Vector3.up * ShellHeight, o + L * hw + S * -hd + Vector3.up * ShellHeight,
                o + L * hw + S * hd + Vector3.up * ShellHeight, o + L * -hw + S * hd + Vector3.up * ShellHeight,
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, Vector3.down);
            if (hip)
                HipRoof(chunk, new Vector3(b.obbCenter.x, yTop, b.obbCenter.y), L, S, hw + 0.35f, hd + 0.35f);
            else
                FlatRoof(chunk, rect, new[] { 0, 1, 2, 0, 2, 3 }, true, yTop, 0.6f);
        }
        else
        {
            Box(chunk, roofMat, true, o, L, S, new Vector3(0f, ShellHeight + 0.15f, 0f), new Vector3(hw + 0.15f, 0.15f, hd + 0.15f), 0.25f);
        }

        // Interior: a counter and some shelves for cover
        Box(chunk, woodMat, true, o, L, S, new Vector3(-hw * 0.45f, 0.5f, hd * 0.3f), new Vector3(Mathf.Min(1.4f, hw * 0.3f), 0.5f, 0.4f), 1f);
        Box(chunk, woodMat, true, o, L, S, new Vector3(hw - 0.6f, 0.9f, 0f), new Vector3(0.25f, 0.9f, Mathf.Min(2.2f, hd * 0.6f)), 1f);

        Vector3 center = new Vector3(b.obbCenter.x, y0, b.obbCenter.y);
        World.AddHouse(center, hw, hd);
        int loot = clinic ? 4 : (b.obbWidth * b.obbDepth > 150f ? 3 : 2);
        for (int i = 0; i < loot; i++)
        {
            float lx = Mathf.Lerp(-hw + 1.2f, hw - 1.2f, (i + 0.5f) / loot);
            float lz = (i % 2 == 0 ? -1f : 1f) * (hd * 0.35f);
            Vector3 p = o + L * lx + S * lz;
            World.LootSpots.Add(new Vector3(p.x, y0 + 0.3f, p.z));
        }

        if (clinic)
            ClinicSign(chunk, b, L, S, hw, hd, y0);
    }

    private static Vector2[] RectOutline(Vector2 c, Vector3 L, Vector3 S, float hw, float hd)
    {
        Vector2 l = new Vector2(L.x, L.z), s = new Vector2(S.x, S.z);
        // counter-clockwise seen from above: (-,-) → (+,-) → (+,+) → (-,+) when S is L rotated +90°
        return new[] { c - l * hw - s * hd, c + l * hw - s * hd, c + l * hw + s * hd, c - l * hw + s * hd };
    }

    private static void ClinicSign(int chunk, MapData.Building b, Vector3 L, Vector3 S, float hw, float hd, float y0)
    {
        Vector2 dir2 = MapData.LobbyPoint - b.obbCenter;
        if (dir2.sqrMagnitude < 0.01f)
            dir2 = new Vector2(S.x, S.z);
        dir2.Normalize();
        // Snap to the OBB side facing the lobby / street.
        float dl = Vector2.Dot(dir2, new Vector2(L.x, L.z)), ds = Vector2.Dot(dir2, new Vector2(S.x, S.z));
        Vector3 n;
        float dist;
        float along;
        Vector3 alongAxis;
        if (Mathf.Abs(dl) * hd > Mathf.Abs(ds) * hw)
        {
            n = L * Mathf.Sign(dl); dist = hw; along = hd; alongAxis = S;
        }
        else
        {
            n = S * Mathf.Sign(ds); dist = hd; along = hw; alongAxis = L;
        }
        float signW = Mathf.Min(7f, along * 2f - 0.6f);
        float signH = signW / 4f;
        Vector3 c = new Vector3(b.obbCenter.x, y0 + 2.4f + signH * 0.5f, b.obbCenter.y) + n * (dist + 0.12f);
        Vector3 right = Vector3.Cross(n, Vector3.up).normalized;   // viewer's right when standing outside facing the wall
        Vector3 hx = right * (signW * 0.5f), hy = Vector3.up * (signH * 0.5f);
        // backing board
        Box(chunk, signBoardMat, false, c - n * 0.06f, alongAxis, n, Vector3.zero,
            new Vector3(signW * 0.5f + 0.1f, signH * 0.5f + 0.1f, 0.06f), 1f);
        Face(chunk, signMat, false, c - hx - hy + n * 0.01f, c + hx - hy + n * 0.01f, c + hx + hy + n * 0.01f, c - hx + hy + n * 0.01f,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), n);
    }

    // ---------------------------------------------------------------- roads

    private static void BuildRoads()
    {
        int index = 0;
        foreach (var road in MapData.Roads)
        {
            index++;
            if (road.kind >= 4 || road.points.Length < 2)
                continue;
            int mat = road.kind <= 1 ? asphaltLinedMat : asphaltMat;
            float lift = 0.07f + (3 - road.kind) * 0.012f + (index % 7) * 0.001f;
            var pts = Resample(road.points, 3f);
            float hw = road.width * 0.5f;
            float totalLen = 0f;
            for (int k = 1; k < pts.Count; k++)
                totalLen += (pts[k] - pts[k - 1]).magnitude;
            float dist = 0f;
            Vector3 prevL = Vector3.zero, prevR = Vector3.zero;
            float prevV = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 tan = pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)];
                if (tan.sqrMagnitude < 1e-6f)
                    tan = Vector2.right;
                tan.Normalize();
                Vector2 left = new Vector2(-tan.y, tan.x);
                if (i > 0)
                    dist += (pts[i] - pts[i - 1]).magnitude;
                Vector2 pl = pts[i] + left * hw, pr = pts[i] - left * hw;
                Vector3 L3 = new Vector3(pl.x, World.HeightAt(pl.x, pl.y) + lift, pl.y);
                Vector3 R3 = new Vector3(pr.x, World.HeightAt(pr.x, pr.y) + lift, pr.y);
                float mid = Mathf.Max(World.HeightAt(pts[i].x, pts[i].y), (L3.y + R3.y) * 0.5f - lift) + lift;
                L3.y = Mathf.Max(L3.y, mid - 0.25f);
                R3.y = Mathf.Max(R3.y, mid - 0.25f);
                float v = dist / 8f;
                if (i > 0)
                {
                    Vector2 m = (pts[i] + pts[i - 1]) * 0.5f;
                    // No painted lines where roads meet (the ends of each way are usually junctions).
                    bool nearEnd = dist < hw + 4f || totalLen - dist < hw + 4f;
                    int quadMat = nearEnd ? asphaltMat : mat;
                    Quad(Get(ChunkOf(m.x, m.y), quadMat), prevL, L3, R3, prevR,
                        new Vector2(0f, prevV), new Vector2(0f, v), new Vector2(1f, v), new Vector2(1f, prevV), Vector3.up);
                }
                prevL = L3; prevR = R3; prevV = v;
            }

            // Street lights along the bigger roads
            if (road.kind <= 1)
            {
                float next = 12f;
                float walked = 0f;
                for (int i = 1; i < pts.Count; i++)
                {
                    float seg = (pts[i] - pts[i - 1]).magnitude;
                    walked += seg;
                    if (walked < next)
                        continue;
                    next += 38f;
                    Vector2 tan = (pts[i] - pts[i - 1]).normalized;
                    Vector2 left = new Vector2(-tan.y, tan.x);
                    Vector2 pp = pts[i] + left * (hw + 1.1f);
                    if (World.IsBlocked(pp.x, pp.y))
                        continue;
                    StreetLight(new Vector3(pp.x, World.HeightAt(pp.x, pp.y), pp.y), new Vector3(-left.x, 0f, -left.y));
                }
            }

            // Parked cars on side streets
            if (road.kind == 2 && World.VehicleSpots.Count < 10 && pts.Count > 12 && (index * 7919) % 3 == 0)
            {
                int i = pts.Count / 2;
                Vector2 tan = (pts[i + 1] - pts[i]).normalized;
                Vector2 right = new Vector2(tan.y, -tan.x);
                Vector2 sp = pts[i] + right * Mathf.Max(0f, hw - 1.4f);
                bool farEnough = sp.magnitude > 30f;
                foreach (var v in World.VehicleSpots)
                    if ((new Vector2(v.x, v.z) - sp).magnitude < 55f)
                        farEnough = false;
                if (farEnough && !World.IsBlocked(sp.x, sp.y))
                {
                    World.VehicleSpots.Add(new Vector3(sp.x, World.HeightAt(sp.x, sp.y), sp.y));
                    World.VehicleYaws.Add(Mathf.Atan2(tan.x, tan.y) * Mathf.Rad2Deg);
                }
            }
        }
    }

    private static List<Vector2> Resample(Vector2[] pts, float step)
    {
        var res = new List<Vector2>();
        res.Add(pts[0]);
        for (int i = 1; i < pts.Length; i++)
        {
            Vector2 a = pts[i - 1], b = pts[i];
            float len = (b - a).magnitude;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
            for (int k = 1; k <= n; k++)
                res.Add(Vector2.Lerp(a, b, (float)k / n));
        }
        return res;
    }

    private static void StreetLight(Vector3 p, Vector3 towardRoad)
    {
        int chunk = ChunkOf(p.x, p.z);
        Quaternion rot = Quaternion.LookRotation(towardRoad);
        AddMesh(chunk, poleMat, cylinder, Matrix4x4.TRS(p + Vector3.up * 4f, Quaternion.identity, new Vector3(0.16f, 4f, 0.16f)), true);
        AddMesh(chunk, poleMat, cube, Matrix4x4.TRS(p + Vector3.up * 7.85f + towardRoad * 0.8f, rot, new Vector3(0.1f, 0.1f, 1.6f)), false);
        AddMesh(chunk, lampMat, cube, Matrix4x4.TRS(p + Vector3.up * 7.75f + towardRoad * 1.55f, rot, new Vector3(0.32f, 0.12f, 0.6f)), false);
    }

    // ---------------------------------------------------------------- trees

    private static void BuildTrees(Transform parent)
    {
        var trunks = new GameObject("TreeTrunks").transform;
        trunks.SetParent(parent, false);
        int i = 0;
        foreach (var tr in MapData.Trees)
        {
            i++;
            float x = tr.position.x, z = tr.position.y;
            if (World.IsBlocked(x, z))
                continue;
            float g = World.HeightAt(x, z) - 0.1f;
            if (g < 0.8f)
                continue;
            int chunk = ChunkOf(x, z);
            float r = Hash(i, 3, 17);
            Vector3 p = new Vector3(x, g, z);
            float h;
            if (tr.kind == 0)
            {
                h = 6f + r * 5f;
                AddMesh(chunk, trunkMat, cube, Matrix4x4.TRS(p + Vector3.up * (h * 0.2f), Quaternion.identity, new Vector3(0.35f, h * 0.4f, 0.35f)), false);
                int m = pineMats[i % pineMats.Length];
                float rad = h * 0.38f;
                for (int k = 0; k < 3; k++)
                {
                    float s = rad * (1f - k * 0.25f) * 2f;
                    AddMesh(chunk, m, MeshUtil.Cone, Matrix4x4.TRS(p + Vector3.up * (h * 0.25f + k * h * 0.22f),
                        Quaternion.Euler(0f, r * 360f + k * 40f, 0f), new Vector3(s, h * 0.42f, s)), false);
                }
            }
            else if (tr.kind == 2)
            {
                h = 7f + r * 4f;
                AddMesh(chunk, trunkMat, cube, Matrix4x4.TRS(p + Vector3.up * 0.6f, Quaternion.identity, new Vector3(0.3f, 1.2f, 0.3f)), false);
                AddMesh(chunk, cypressMat, MeshUtil.Cone, Matrix4x4.TRS(p + Vector3.up * 0.8f, Quaternion.identity, new Vector3(1.6f, h, 1.6f)), false);
            }
            else
            {
                h = 4f + r * 3f;
                AddMesh(chunk, trunkMat, cube, Matrix4x4.TRS(p + Vector3.up * (h * 0.35f), Quaternion.identity, new Vector3(0.4f, h * 0.7f, 0.4f)), false);
                int m = leafMats[(i * 7) % leafMats.Length];
                int clumps = 2 + (i % 2);
                for (int k = 0; k < clumps; k++)
                {
                    float s = 2.6f + Hash(i, k, 5) * 1.3f;
                    Vector3 off = new Vector3(Hash(i, k, 6) * 1.6f - 0.8f, h * 0.8f + Hash(i, k, 7), Hash(i, k, 8) * 1.6f - 0.8f);
                    AddMesh(chunk, m, blobs[(i + k) % blobs.Length], Matrix4x4.TRS(p + off, Quaternion.Euler(0f, k * 70f, 0f), new Vector3(s, s * 0.8f, s)), false);
                }
            }

            var trunk = new GameObject("Trunk");
            trunk.transform.SetParent(trunks, false);
            trunk.transform.position = p + Vector3.up * 1.5f;
            var cap = trunk.AddComponent<CapsuleCollider>();
            cap.radius = 0.25f;
            cap.height = 3f;
        }
    }
}
