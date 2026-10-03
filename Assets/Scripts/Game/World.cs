using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the whole island from code: hilly terrain with a painted colour map, animated sea,
/// sky, sun, fog, trees, rocks, bushes and houses. Also exposes height queries, loot/vehicle spots
/// and a minimap texture for the rest of the game.
/// </summary>
public static class World
{
    public const float IslandRadius = 85f;     // average coastline distance from the centre
    public const float MapSize = 260f;         // terrain mesh width (water beyond)
    private const float SeedX = 31.7f;
    private const float SeedZ = 87.3f;
    private const int GridRes = 512;           // colour-map / minimap sampling grid
    private const int MeshRes = 200;           // terrain mesh resolution

    public static Transform Root { get; private set; }
    public static Texture2D MinimapTexture { get; private set; }
    public static readonly List<Vector3> LootSpots = new List<Vector3>();
    public static readonly List<Vector3> VehicleSpots = new List<Vector3>();
    public static readonly List<Vector3> HouseCenters = new List<Vector3>();
    private static readonly List<Vector4> houseFootprints = new List<Vector4>(); // x, z, halfWidth, halfDepth

    private static float[] heights;
    private static Light sun;

    public static readonly Color SkyHorizon = new Color(0.75f, 0.86f, 0.95f);
    public static readonly Color WaterShallow = new Color(0.2f, 0.62f, 0.72f);
    public static readonly Color WaterDeep = new Color(0.06f, 0.27f, 0.47f);

    // ----- Height queries -----

    public static float HeightAt(float x, float z)
    {
        float r = Mathf.Sqrt(x * x + z * z);
        float angle = Mathf.Atan2(z, x);
        float coastNoise = Mathf.PerlinNoise(Mathf.Cos(angle) * 1.6f + SeedX, Mathf.Sin(angle) * 1.6f + SeedZ) - 0.5f;
        float coast = IslandRadius + coastNoise * 22f;

        float land = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(coast - 16f, coast + 6f, r));
        float n = Fbm(x * 0.012f + SeedX, z * 0.012f + SeedZ);
        float hills = Mathf.Max(0f, n - 0.38f) * 30f;

        float h = Mathf.Lerp(-6f, 1.4f + hills, land);

        // Keep the very centre gentle (lobby view).
        float centre = Mathf.Clamp01(Mathf.InverseLerp(16f, 5f, r));
        return Mathf.Lerp(h, 1.8f, centre);
    }

    private static float Fbm(float x, float z)
    {
        float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
        for (int i = 0; i < 4; i++)
        {
            sum += Mathf.PerlinNoise(x * freq, z * freq) * amp;
            norm += amp;
            amp *= 0.5f;
            freq *= 2.03f;
        }
        return sum / norm;
    }

    /// <summary>Highest solid surface under a point (roofs included).</summary>
    public static float GroundHeight(float x, float z)
    {
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(x, 250f, z), Vector3.down, out hit, 400f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return HeightAt(x, z);
    }

    /// <summary>True inside (or right next to) a building, where grass and props shouldn't go.</summary>
    public static bool IsBlocked(float x, float z)
    {
        for (int i = 0; i < houseFootprints.Count; i++)
        {
            Vector4 f = houseFootprints[i];
            float r = Mathf.Sqrt(f.z * f.z + f.w * f.w) + 0.6f;
            float dx = f.x - x;
            float dz = f.y - z;
            if (dx * dx + dz * dz < r * r)
                return true;
        }
        return false;
    }

    public static bool IsLand(float x, float z)
    {
        return HeightAt(x, z) > 0.6f;
    }

    /// <summary>Random spot on land with nothing solid around it. Returned y is at standing height (feet + 0.95).</summary>
    public static Vector3 RandomOpenPoint(Vector3 around, float radius)
    {
        Vector3 best = new Vector3(around.x, HeightAt(around.x, around.z) + 0.95f, around.z);
        for (int i = 0; i < 30; i++)
        {
            Vector2 p = Random.insideUnitCircle * radius;
            float x = around.x + p.x;
            float z = around.z + p.y;
            float h = HeightAt(x, z);
            if (h < 0.8f)
                continue;
            Vector3 pos = new Vector3(x, h + 0.95f, z);
            best = pos;
            if (!Physics.CheckSphere(pos + Vector3.up * 0.3f, 0.6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return pos;
        }
        return best;
    }

    // ----- Building -----

    public static void Build()
    {
        Root = new GameObject("World").transform;
        LootSpots.Clear();
        VehicleSpots.Clear();
        HouseCenters.Clear();
        houseFootprints.Clear();

        ComputeHeightGrid();
        SetupAtmosphere();
        BuildTerrain();
        BuildWater();

        var props = new GameObject("Props").transform;
        props.SetParent(Root, false);
        var rng = new System.Random(1923);
        BuildHouses(props, rng);
        BuildNature(props, rng);
        BuildMinimap();

        // Fewer draw calls: everything static is merged by material.
        StaticBatchingUtility.Combine(props.gameObject);

        // Model props (sandbags, barriers, containers...) live outside the batched root
        // because imported meshes are not CPU-readable.
        var cover = new GameObject("Cover").transform;
        cover.SetParent(Root, false);
        BuildCover(cover, rng);
    }

    private static void ComputeHeightGrid()
    {
        heights = new float[(GridRes + 1) * (GridRes + 1)];
        float step = MapSize / GridRes;
        float half = MapSize * 0.5f;
        for (int z = 0; z <= GridRes; z++)
            for (int x = 0; x <= GridRes; x++)
                heights[z * (GridRes + 1) + x] = HeightAt(x * step - half, z * step - half);
    }

    private static float GridHeight(int x, int z)
    {
        x = Mathf.Clamp(x, 0, GridRes);
        z = Mathf.Clamp(z, 0, GridRes);
        return heights[z * (GridRes + 1) + x];
    }

    private static void SetupAtmosphere()
    {
        sun = Object.FindObjectOfType<Light>();
        if (sun == null)
            sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.15f;
        sun.color = new Color(1f, 0.95f, 0.85f);
        sun.shadows = LightShadows.Hard;
        sun.shadowStrength = 0.65f;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        RenderSettings.sun = sun;

        var skyBase = Resources.Load<Material>("ZootopiaSky");
        if (skyBase != null)
        {
            var sky = new Material(skyBase);
            sky.SetVector("_SunDir", -sun.transform.forward);
            RenderSettings.skybox = sky;
        }

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.85f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.6f, 0.58f);
        RenderSettings.ambientGroundColor = new Color(0.32f, 0.3f, 0.26f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = SkyHorizon;
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 260f;

        QualitySettings.shadows = ShadowQuality.HardOnly;
        QualitySettings.shadowDistance = 45f;
        QualitySettings.shadowResolution = ShadowResolution.Medium;
        QualitySettings.shadowCascades = 1;
    }

    private static Material CloneResource(string name)
    {
        var baseMat = Resources.Load<Material>(name);
        if (baseMat != null)
            return new Material(baseMat);
        return new Material(MaterialCache.Lit(Color.white));
    }

    private static void BuildTerrain()
    {
        // Mesh
        var verts = new List<Vector3>((MeshRes + 1) * (MeshRes + 1));
        var uvs = new List<Vector2>(verts.Capacity);
        var tris = new List<int>(MeshRes * MeshRes * 6);
        float step = MapSize / MeshRes;
        float half = MapSize * 0.5f;
        for (int z = 0; z <= MeshRes; z++)
        {
            for (int x = 0; x <= MeshRes; x++)
            {
                float wx = x * step - half;
                float wz = z * step - half;
                verts.Add(new Vector3(wx, HeightAt(wx, wz), wz));
                uvs.Add(new Vector2((float)x / MeshRes, (float)z / MeshRes));
            }
        }
        for (int z = 0; z < MeshRes; z++)
        {
            for (int x = 0; x < MeshRes; x++)
            {
                int i = z * (MeshRes + 1) + x;
                tris.Add(i); tris.Add(i + MeshRes + 1); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + MeshRes + 1); tris.Add(i + MeshRes + 2);
            }
        }
        var mesh = new Mesh { name = "IslandTerrain" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();   // for the detail normal map
        mesh.RecalculateBounds();

        // Colour map painted from height, slope and noise.
        var colors = new Color32[GridRes * GridRes];
        float cell = MapSize / GridRes;
        for (int z = 0; z < GridRes; z++)
        {
            for (int x = 0; x < GridRes; x++)
            {
                float h = GridHeight(x, z);
                float dx = GridHeight(x + 1, z) - GridHeight(x - 1, z);
                float dz = GridHeight(x, z + 1) - GridHeight(x, z - 1);
                float slope = Mathf.Sqrt(dx * dx + dz * dz) / (2f * cell);
                float wx = x * cell - MapSize * 0.5f;
                float wz = z * cell - MapSize * 0.5f;
                colors[z * GridRes + x] = TerrainColor(h, slope, wx, wz);
            }
        }
        var colorMap = new Texture2D(GridRes, GridRes, TextureFormat.RGBA32, true);
        colorMap.wrapMode = TextureWrapMode.Clamp;
        colorMap.filterMode = FilterMode.Trilinear;
        colorMap.anisoLevel = 8;
        colorMap.SetPixels32(colors);
        colorMap.Apply(true, true);

        var mat = CloneResource("ZootopiaTerrain");
        mat.SetTexture("_MainTex", colorMap);
        var noise = DetailNoise();
        mat.SetTexture("_DetailTex", noise);
        mat.SetTexture("_DetailNormal", NormalFromHeight(noise));

        var terrain = new GameObject("Terrain");
        terrain.transform.SetParent(Root, false);
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = terrain.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private static Color32 TerrainColor(float h, float slope, float wx, float wz)
    {
        float n1 = Mathf.PerlinNoise(wx * 0.05f + 11f, wz * 0.05f + 7f);
        float n2 = Mathf.PerlinNoise(wx * 0.11f + 3f, wz * 0.11f + 19f);

        Color wetSand = new Color(0.62f, 0.56f, 0.4f);
        Color sand = new Color(0.86f, 0.79f, 0.58f);
        Color grassA = new Color(0.3f, 0.52f, 0.2f);
        Color grassB = new Color(0.42f, 0.6f, 0.24f);
        Color dryGrass = new Color(0.58f, 0.6f, 0.3f);
        Color dirt = new Color(0.47f, 0.36f, 0.24f);
        Color rock = new Color(0.48f, 0.47f, 0.45f);

        Color c;
        float beach = 1.1f + (n2 - 0.5f) * 0.8f;
        if (h < 0f)
            c = Color.Lerp(wetSand * 0.8f, wetSand, Mathf.InverseLerp(-4f, 0f, h));
        else if (h < beach)
            c = Color.Lerp(wetSand, sand, Mathf.InverseLerp(0f, 0.5f, h));
        else
        {
            c = Color.Lerp(grassA, grassB, n1);
            c = Color.Lerp(c, dryGrass, Mathf.Clamp01((n2 - 0.6f) * 3f));
            if (n1 > 0.68f)
                c = Color.Lerp(c, dirt, Mathf.Clamp01((n1 - 0.68f) * 6f));
            // Blend into sand just above the beach line.
            c = Color.Lerp(sand, c, Mathf.Clamp01((h - beach) * 2.5f));
        }

        c = Color.Lerp(c, rock, Mathf.Clamp01((slope - 0.55f) * 2.5f));
        return c;
    }

    private static Texture2D DetailNoise()
    {
        const int size = 128;
        var rng = new System.Random(77);
        var pixels = new Color32[size * size];
        float[] layer1 = TileNoise(size, 8, rng);
        float[] layer2 = TileNoise(size, 32, rng);
        for (int i = 0; i < pixels.Length; i++)
        {
            float v = layer1[i] * 0.45f + layer2[i] * 0.4f + (float)rng.NextDouble() * 0.15f;
            byte b = (byte)(Mathf.Clamp01(v) * 255f);
            pixels[i] = new Color32(b, b, b, 255);
        }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        tex.SetPixels32(pixels);
        tex.Apply(true, false);   // stays readable: the normal map is built from it
        return tex;
    }

    /// <summary>Tileable normal map from a greyscale height texture (RGB-encoded tangent-space normals).</summary>
    private static Texture2D NormalFromHeight(Texture2D height)
    {
        int w = height.width;
        int h = height.height;
        Color32[] src = height.GetPixels32();
        var dst = new Color32[src.Length];
        const float strength = 2.5f;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float l = src[y * w + (x - 1 + w) % w].r / 255f;
                float r = src[y * w + (x + 1) % w].r / 255f;
                float d = src[((y - 1 + h) % h) * w + x].r / 255f;
                float u = src[((y + 1) % h) * w + x].r / 255f;
                Vector3 n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                dst[y * w + x] = new Color32((byte)((n.x * 0.5f + 0.5f) * 255f), (byte)((n.y * 0.5f + 0.5f) * 255f), (byte)((n.z * 0.5f + 0.5f) * 255f), 255);
            }
        }
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Trilinear;
        tex.anisoLevel = 4;
        tex.SetPixels32(dst);
        tex.Apply(true, true);
        return tex;
    }

    /// <summary>Tileable smooth value noise in 0..1.</summary>
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
                float a = lattice[(y0 % cells) * cells + (x0 % cells)];
                float b = lattice[(y0 % cells) * cells + ((x0 + 1) % cells)];
                float c = lattice[((y0 + 1) % cells) * cells + (x0 % cells)];
                float d = lattice[((y0 + 1) % cells) * cells + ((x0 + 1) % cells)];
                result[y * size + x] = Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
            }
        }
        return result;
    }

    private static void BuildWater()
    {
        var water = new GameObject("Sea");
        water.transform.SetParent(Root, false);
        water.transform.position = new Vector3(0f, -0.15f, 0f);
        water.AddComponent<MeshFilter>().sharedMesh = MeshUtil.Grid(900f, 150);
        var mr = water.AddComponent<MeshRenderer>();
        var mat = CloneResource("ZootopiaWater");
        mat.SetFloat("_ShoreRadius", IslandRadius);
        mat.SetFloat("_MapSize", MapSize);
        mat.SetTexture("_FoamTex", FoamMask());
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
    }

    /// <summary>Bright where the sea is shallow next to the beach (for the water shader's foam).</summary>
    private static Texture2D FoamMask()
    {
        const int size = 256;
        var pixels = new Color32[size * size];
        float scale = (float)GridRes / size;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float h = GridHeight(Mathf.RoundToInt(x * scale), Mathf.RoundToInt(y * scale));
                float v = h > 0.1f ? 0f : Mathf.Clamp01(1f - (-h) / 1.6f);
                byte b = (byte)(v * 255f);
                pixels[y * size + x] = new Color32(b, b, b, 255);
            }
        }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(pixels);
        tex.Apply(true, true);
        return tex;
    }

    // ----- Props -----

    private static readonly Color[] PineGreens = { new Color(0.12f, 0.36f, 0.2f), new Color(0.15f, 0.42f, 0.22f), new Color(0.1f, 0.3f, 0.17f) };
    private static readonly Color[] LeafGreens = { new Color(0.3f, 0.55f, 0.2f), new Color(0.38f, 0.6f, 0.22f), new Color(0.26f, 0.48f, 0.18f), new Color(0.55f, 0.6f, 0.2f) };
    private static readonly Color[] RockGrays = { new Color(0.5f, 0.5f, 0.52f), new Color(0.42f, 0.42f, 0.44f), new Color(0.58f, 0.55f, 0.5f) };
    private static readonly Color[] WallColors = { new Color(0.86f, 0.8f, 0.68f), new Color(0.78f, 0.62f, 0.5f), new Color(0.7f, 0.72f, 0.66f), new Color(0.9f, 0.88f, 0.82f), new Color(0.62f, 0.5f, 0.42f) };
    private static readonly Color[] RoofColors = { new Color(0.6f, 0.22f, 0.16f), new Color(0.35f, 0.32f, 0.3f), new Color(0.25f, 0.35f, 0.45f) };
    private static readonly Color Trunk = new Color(0.36f, 0.24f, 0.13f);
    private static readonly Color Glass = new Color(0.25f, 0.35f, 0.45f);
    private static readonly Color Wood = new Color(0.45f, 0.3f, 0.18f);
    private static readonly Color Concrete = new Color(0.55f, 0.54f, 0.52f);

    private static float Rand(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    private static Vector3 RandomLandPoint(System.Random rng, float minR, float maxR)
    {
        for (int i = 0; i < 40; i++)
        {
            float a = Rand(rng, 0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Rand(rng, minR * minR, maxR * maxR));
            float x = Mathf.Cos(a) * r;
            float z = Mathf.Sin(a) * r;
            float h = HeightAt(x, z);
            if (h > 1.3f)
                return new Vector3(x, h, z);
        }
        return new Vector3(0f, HeightAt(0f, 0f), 0f);
    }

    private static float Slope(float x, float z)
    {
        float dx = HeightAt(x + 1f, z) - HeightAt(x - 1f, z);
        float dz = HeightAt(x, z + 1f) - HeightAt(x, z - 1f);
        return Mathf.Sqrt(dx * dx + dz * dz) * 0.5f;
    }

    private static bool NearHouse(Vector3 p, float distance)
    {
        foreach (var c in HouseCenters)
        {
            float dx = c.x - p.x;
            float dz = c.z - p.z;
            if (dx * dx + dz * dz < distance * distance)
                return true;
        }
        return false;
    }

    private static void BuildHouses(Transform parent, System.Random rng)
    {
        int houses = 0;
        int warehouses = 0;
        for (int attempt = 0; attempt < 400 && (houses < 16 || warehouses < 3); attempt++)
        {
            bool big = warehouses < 3 && attempt % 5 == 0;
            Vector3 p = RandomLandPoint(rng, 12f, IslandRadius - 14f);
            if (Slope(p.x, p.z) > 0.35f || NearHouse(p, big ? 22f : 16f))
                continue;

            float yaw = Mathf.Round(Rand(rng, 0f, 4f)) * 90f + Rand(rng, -12f, 12f);
            if (big)
            {
                CreateWarehouse(parent, p, yaw, rng);
                warehouses++;
            }
            else
            {
                CreateHouse(parent, p, yaw, rng);
                houses++;
            }
        }
    }

    private static GameObject Box(Transform parent, Vector3 localPos, Vector3 scale, Color color, bool collider)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (!collider)
            Object.DestroyImmediate(box.GetComponent<Collider>());
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPos;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(color);
        return box;
    }

    private static float FootprintMinMax(Vector3 c, float w, float d, float yaw, out float min)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        float max = float.MinValue;
        min = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = c + rot * new Vector3((i % 2 == 0 ? -1 : 1) * w * 0.5f, 0f, (i < 2 ? -1 : 1) * d * 0.5f);
            float h = HeightAt(corner.x, corner.z);
            max = Mathf.Max(max, h);
            min = Mathf.Min(min, h);
        }
        return max;
    }

    private static void CreateHouse(Transform parent, Vector3 pos, float yaw, System.Random rng)
    {
        float w = Mathf.Round(Rand(rng, 6f, 8.5f));
        float d = Mathf.Round(Rand(rng, 6f, 8f));
        const float h = 3f;
        const float t = 0.25f;
        const float door = 1.6f;
        Color wall = WallColors[rng.Next(WallColors.Length)];
        Color roof = RoofColors[rng.Next(RoofColors.Length)];

        float minH;
        float floorY = FootprintMinMax(pos, w, d, yaw, out minH) + 0.15f;

        var house = new GameObject("House").transform;
        house.SetParent(parent, false);
        house.position = new Vector3(pos.x, floorY, pos.z);
        house.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Foundation and floor
        float found = floorY - minH + 1f;
        Box(house, new Vector3(0f, -found * 0.5f, 0f), new Vector3(w + 0.2f, found, d + 0.2f), Concrete, true);
        Box(house, new Vector3(0f, 0.02f, 0f), new Vector3(w - 0.1f, 0.04f, d - 0.1f), Wood, false);

        // Back and side walls
        Box(house, new Vector3(0f, h * 0.5f, d * 0.5f), new Vector3(w, h, t), wall, true);
        Box(house, new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), wall, true);
        Box(house, new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), wall, true);

        // Front wall with doorway
        float side = (w - door) * 0.5f;
        Box(house, new Vector3(-(door + side) * 0.5f, h * 0.5f, -d * 0.5f), new Vector3(side, h, t), wall, true);
        Box(house, new Vector3((door + side) * 0.5f, h * 0.5f, -d * 0.5f), new Vector3(side, h, t), wall, true);
        Box(house, new Vector3(0f, h - 0.4f, -d * 0.5f), new Vector3(door, 0.8f, t), wall, true);
        // Door frame
        Box(house, new Vector3(-door * 0.5f - 0.06f, (h - 0.8f) * 0.5f, -d * 0.5f - 0.05f), new Vector3(0.12f, h - 0.8f, 0.12f), Wood, false);
        Box(house, new Vector3(door * 0.5f + 0.06f, (h - 0.8f) * 0.5f, -d * 0.5f - 0.05f), new Vector3(0.12f, h - 0.8f, 0.12f), Wood, false);

        // Windows (glass panes with sills, both sides)
        for (int s = -1; s <= 1; s += 2)
        {
            Box(house, new Vector3(s * (w * 0.5f + 0.02f), 1.6f, 0f), new Vector3(0.06f, 1f, 1.4f), Glass, false);
            Box(house, new Vector3(s * (w * 0.5f + 0.08f), 1.05f, 0f), new Vector3(0.12f, 0.08f, 1.6f), Wood, false);
        }
        Box(house, new Vector3(-w * 0.25f, 1.6f, d * 0.5f + 0.02f), new Vector3(1.2f, 1f, 0.06f), Glass, false);

        // Gable roof (two tilted slabs)
        const float pitch = 28f;
        float slab = (w * 0.5f + 0.4f) / Mathf.Cos(pitch * Mathf.Deg2Rad);
        float rise = (w * 0.25f) * Mathf.Tan(pitch * Mathf.Deg2Rad);
        for (int s = -1; s <= 1; s += 2)
        {
            var r = Box(house, new Vector3(s * w * 0.25f, h + rise + 0.1f, 0f), new Vector3(slab, 0.18f, d + 0.8f), roof, true);
            r.transform.localRotation = Quaternion.Euler(0f, 0f, -s * pitch);
        }
        // Gable ends
        Box(house, new Vector3(0f, h + rise * 0.5f, d * 0.5f), new Vector3(w * 0.5f, rise, t), wall, false);
        Box(house, new Vector3(0f, h + rise * 0.5f, -d * 0.5f), new Vector3(w * 0.5f, rise, t), wall, false);

        RegisterBuilding(house, w, d, 2, rng, floorY);
    }

    private static void CreateWarehouse(Transform parent, Vector3 pos, float yaw, System.Random rng)
    {
        float w = 14f;
        float d = 9f;
        const float h = 5f;
        const float t = 0.3f;
        const float door = 4f;
        Color metal = new Color(0.45f, 0.5f, 0.52f);
        Color trim = new Color(0.7f, 0.55f, 0.2f);

        float minH;
        float floorY = FootprintMinMax(pos, w, d, yaw, out minH) + 0.15f;

        var shed = new GameObject("Warehouse").transform;
        shed.SetParent(parent, false);
        shed.position = new Vector3(pos.x, floorY, pos.z);
        shed.rotation = Quaternion.Euler(0f, yaw, 0f);

        float found = floorY - minH + 1f;
        Box(shed, new Vector3(0f, -found * 0.5f, 0f), new Vector3(w + 0.2f, found, d + 0.2f), Concrete, true);
        Box(shed, new Vector3(0f, h * 0.5f, d * 0.5f), new Vector3(w, h, t), metal, true);
        Box(shed, new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), metal, true);
        Box(shed, new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), metal, true);

        float side = (w - door) * 0.5f;
        Box(shed, new Vector3(-(door + side) * 0.5f, h * 0.5f, -d * 0.5f), new Vector3(side, h, t), metal, true);
        Box(shed, new Vector3((door + side) * 0.5f, h * 0.5f, -d * 0.5f), new Vector3(side, h, t), metal, true);
        Box(shed, new Vector3(0f, h - 0.75f, -d * 0.5f), new Vector3(door, 1.5f, t), metal, true);
        Box(shed, new Vector3(0f, h + 0.1f, 0f), new Vector3(w + 0.6f, 0.2f, d + 0.6f), new Color(0.38f, 0.4f, 0.42f), true);
        Box(shed, new Vector3(0f, h - 0.2f, -d * 0.5f - 0.18f), new Vector3(w, 0.25f, 0.06f), trim, false);

        // Crates inside for cover
        for (int i = 0; i < 3; i++)
        {
            float cx = Rand(rng, -w * 0.35f, w * 0.35f);
            float cz = Rand(rng, -d * 0.2f, d * 0.3f);
            Box(shed, new Vector3(cx, 0.6f, cz), new Vector3(1.2f, 1.2f, 1.2f), Wood, true);
        }

        RegisterBuilding(shed, w, d, 4, rng, floorY);
    }

    private static void RegisterBuilding(Transform building, float w, float d, int lootCount, System.Random rng, float floorY)
    {
        HouseCenters.Add(building.position);
        houseFootprints.Add(new Vector4(building.position.x, building.position.z, w * 0.5f, d * 0.5f));

        for (int i = 0; i < lootCount; i++)
        {
            Vector3 local = new Vector3(Rand(rng, -w * 0.3f, w * 0.3f), 0f, Rand(rng, -d * 0.3f, d * 0.25f));
            Vector3 world = building.TransformPoint(local);
            LootSpots.Add(new Vector3(world.x, floorY + 0.3f, world.z));
        }

        // Parking spot in front of the door.
        if (VehicleSpots.Count < 8 && rng.NextDouble() < 0.5)
        {
            Vector3 spot = building.TransformPoint(new Vector3(w * 0.5f + 3.5f, 0f, -d * 0.5f - 3f));
            float sh = HeightAt(spot.x, spot.z);
            if (sh > 1f && Slope(spot.x, spot.z) < 0.4f)
                VehicleSpots.Add(new Vector3(spot.x, sh, spot.z));
        }
    }

    private static void BuildNature(Transform parent, System.Random rng)
    {
        var rockMeshes = new Mesh[4];
        for (int i = 0; i < rockMeshes.Length; i++)
            rockMeshes[i] = MeshUtil.Blob(100 + i, 0.35f);
        var bushMeshes = new Mesh[3];
        for (int i = 0; i < bushMeshes.Length; i++)
            bushMeshes[i] = MeshUtil.Blob(200 + i, 0.2f);

        for (int i = 0; i < 150; i++)
        {
            Vector3 p = RandomLandPoint(rng, 7f, IslandRadius + 5f);
            if (p.y < 1.4f || NearHouse(p, 9f))
                continue;
            if (rng.NextDouble() < 0.55)
                CreatePine(parent, p, rng);
            else
                CreateBroadleaf(parent, p, rng);
        }

        for (int i = 0; i < 60; i++)
        {
            Vector3 p = RandomLandPoint(rng, 7f, IslandRadius + 8f);
            if (NearHouse(p, 8f))
                continue;
            var rock = new GameObject("Rock");
            rock.transform.SetParent(parent, false);
            float s = Rand(rng, 0.8f, 3.2f);
            rock.transform.position = p + Vector3.up * (s * 0.15f);
            rock.transform.rotation = Quaternion.Euler(Rand(rng, -10f, 10f), Rand(rng, 0f, 360f), Rand(rng, -10f, 10f));
            rock.transform.localScale = new Vector3(s * Rand(rng, 0.9f, 1.4f), s * Rand(rng, 0.55f, 0.9f), s);
            Mesh m = rockMeshes[rng.Next(rockMeshes.Length)];
            rock.AddComponent<MeshFilter>().sharedMesh = m;
            rock.AddComponent<MeshRenderer>().sharedMaterial = MaterialCache.Lit(RockGrays[rng.Next(RockGrays.Length)]);
            var mc = rock.AddComponent<MeshCollider>();
            mc.sharedMesh = m;
            mc.convex = true;
        }

        // Bushes: cover you can hide in (no collider, bullets pass through).
        for (int i = 0; i < 90; i++)
        {
            Vector3 p = RandomLandPoint(rng, 6f, IslandRadius);
            if (NearHouse(p, 6f))
                continue;
            var bush = new GameObject("Bush");
            bush.transform.SetParent(parent, false);
            float s = Rand(rng, 0.9f, 1.6f);
            bush.transform.position = p + Vector3.up * (s * 0.3f);
            bush.transform.localScale = new Vector3(s * 1.3f, s, s * 1.2f);
            bush.AddComponent<MeshFilter>().sharedMesh = bushMeshes[rng.Next(bushMeshes.Length)];
            bush.AddComponent<MeshRenderer>().sharedMaterial = MaterialCache.Lit(LeafGreens[rng.Next(LeafGreens.Length)] * 0.85f);
        }
    }

    private static void CreatePine(Transform parent, Vector3 p, System.Random rng)
    {
        float h = Rand(rng, 5f, 9f);
        var tree = new GameObject("Pine").transform;
        tree.SetParent(parent, false);
        tree.position = p;

        var trunk = Box(tree, new Vector3(0f, h * 0.2f, 0f), new Vector3(0.35f, h * 0.4f, 0.35f), Trunk, false);
        trunk.AddComponent<CapsuleCollider>();

        Color green = PineGreens[rng.Next(PineGreens.Length)];
        float r = h * 0.38f;
        for (int i = 0; i < 3; i++)
        {
            var cone = new GameObject("Cone");
            cone.transform.SetParent(tree, false);
            float y = h * 0.25f + i * h * 0.22f;
            float s = r * (1f - i * 0.25f) * 2f;
            cone.transform.localPosition = new Vector3(0f, y, 0f);
            cone.transform.localScale = new Vector3(s, h * 0.42f, s);
            cone.transform.localRotation = Quaternion.Euler(0f, Rand(rng, 0f, 360f), 0f);
            cone.AddComponent<MeshFilter>().sharedMesh = MeshUtil.Cone;
            cone.AddComponent<MeshRenderer>().sharedMaterial = MaterialCache.Lit(green);
        }
    }

    private static void CreateBroadleaf(Transform parent, Vector3 p, System.Random rng)
    {
        float h = Rand(rng, 3.5f, 6f);
        var tree = new GameObject("Tree").transform;
        tree.SetParent(parent, false);
        tree.position = p;

        var trunk = Box(tree, new Vector3(0f, h * 0.35f, 0f), new Vector3(0.4f, h * 0.7f, 0.4f), Trunk, false);
        trunk.AddComponent<CapsuleCollider>();

        Color green = LeafGreens[rng.Next(LeafGreens.Length)];
        Mesh blob = MeshUtil.Blob(rng.Next(1000), 0.25f);
        int clumps = 2 + rng.Next(2);
        for (int i = 0; i < clumps; i++)
        {
            var leaf = new GameObject("Leaves");
            leaf.transform.SetParent(tree, false);
            float s = Rand(rng, 2.4f, 3.6f);
            leaf.transform.localPosition = new Vector3(Rand(rng, -0.8f, 0.8f), h * 0.8f + Rand(rng, 0f, 1f), Rand(rng, -0.8f, 0.8f));
            leaf.transform.localScale = new Vector3(s, s * 0.8f, s);
            leaf.AddComponent<MeshFilter>().sharedMesh = blob;
            leaf.AddComponent<MeshRenderer>().sharedMaterial = MaterialCache.Lit(green);
        }
    }

    // ----- Cover props (3D models) -----

    private static void BuildCover(Transform parent, System.Random rng)
    {
        if (ModelLibrary.Prefab(ModelLibrary.PropPath("Crate")) == null)
            return;

        // Small clutter next to buildings (outside their footprint).
        for (int h = 0; h < HouseCenters.Count; h++)
        {
            Vector3 c = HouseCenters[h];
            Vector4 f = houseFootprints[h];
            float outer = Mathf.Sqrt(f.z * f.z + f.w * f.w);   // half-diagonal
            int n = 1 + rng.Next(3);
            for (int i = 0; i < n; i++)
            {
                float a = Rand(rng, 0f, Mathf.PI * 2f);
                float r = Rand(rng, outer + 1.5f, outer + 4f);
                Vector3 p = new Vector3(c.x + Mathf.Cos(a) * r, 0f, c.z + Mathf.Sin(a) * r);
                if (!IsLand(p.x, p.z) || NearVehicleSpot(p, 4f))
                    continue;
                string name = rng.NextDouble() < 0.75
                    ? ModelLibrary.SmallProps[rng.Next(ModelLibrary.SmallProps.Length)]
                    : ModelLibrary.CoverProps[rng.Next(ModelLibrary.CoverProps.Length)];
                PlaceProp(parent, name, p, Rand(rng, 0f, 360f));
            }
        }

        // Cover in the open.
        int placed = 0;
        for (int attempt = 0; attempt < 200 && placed < 45; attempt++)
        {
            Vector3 p = RandomLandPoint(rng, 10f, IslandRadius - 6f);
            if (Slope(p.x, p.z) > 0.3f || NearHouse(p, 13f) || NearVehicleSpot(p, 6f))
                continue;
            string name = ModelLibrary.CoverProps[rng.Next(ModelLibrary.CoverProps.Length)];
            PlaceProp(parent, name, p, Rand(rng, 0f, 360f));
            placed++;

            // Sometimes a little cluster: crates or barrels beside the main piece.
            if (rng.NextDouble() < 0.35)
            {
                Vector3 q = p + new Vector3(Rand(rng, -3f, 3f), 0f, Rand(rng, -3f, 3f));
                PlaceProp(parent, ModelLibrary.SmallProps[rng.Next(ModelLibrary.SmallProps.Length)], q, Rand(rng, 0f, 360f));
            }
        }
    }

    private static bool NearVehicleSpot(Vector3 p, float distance)
    {
        foreach (var v in VehicleSpots)
        {
            float dx = v.x - p.x;
            float dz = v.z - p.z;
            if (dx * dx + dz * dz < distance * distance)
                return true;
        }
        return false;
    }

    private static void PlaceProp(Transform parent, string name, Vector3 p, float yaw)
    {
        var holder = new GameObject(name).transform;
        holder.SetParent(parent, false);
        holder.position = new Vector3(p.x, 0f, p.z);

        var model = ModelLibrary.Spawn(ModelLibrary.PropPath(name), holder);
        if (model == null)
        {
            Object.Destroy(holder.gameObject);
            return;
        }
        model.transform.localPosition = Vector3.zero;
        bool small = System.Array.IndexOf(ModelLibrary.SmallProps, name) >= 0;
        ModelLibrary.ShareMaterials(model, !small);

        // Guard against unit mix-ups (props are 0.5 - 5 m).
        Bounds b = ModelLibrary.RenderBounds(model);
        float biggest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (biggest > 30f)
            model.transform.localScale *= 0.01f;
        b = ModelLibrary.RenderBounds(model);

        // Sit on the lowest ground under the footprint so nothing floats on slopes.
        float ground = float.MaxValue;
        for (int i = 0; i < 4; i++)
        {
            float x = p.x + (i % 2 == 0 ? -0.5f : 0.5f) * b.size.x;
            float z = p.z + (i < 2 ? -0.5f : 0.5f) * b.size.z;
            ground = Mathf.Min(ground, HeightAt(x, z));
        }
        float baseOffset = b.min.y - holder.position.y;
        holder.position = new Vector3(p.x, ground - baseOffset - 0.03f, p.z);

        // Solid box collider for cover, computed before rotating.
        b = ModelLibrary.RenderBounds(model);
        var box = holder.gameObject.AddComponent<BoxCollider>();
        box.center = b.center - holder.position;
        box.size = b.size;
        holder.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // ----- Minimap -----

    private static void BuildMinimap()
    {
        const int size = 256;
        var pixels = new Color32[size * size];
        float scale = (float)GridRes / size;
        float cell = MapSize / GridRes;
        Color32 houseColor = new Color32(70, 62, 56, 255);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int gx = Mathf.Min(GridRes, Mathf.RoundToInt(x * scale));
                int gz = Mathf.Min(GridRes, Mathf.RoundToInt(y * scale));
                float h = GridHeight(gx, gz);
                float wx = gx * cell - MapSize * 0.5f;
                float wz = gz * cell - MapSize * 0.5f;
                Color c;
                if (h < 0.05f)
                    c = Color.Lerp(WaterShallow, WaterDeep, Mathf.InverseLerp(0f, -5f, h));
                else
                    c = (Color)TerrainColor(h, 0f, wx, wz) * (0.9f + Mathf.Clamp01(h / 15f) * 0.25f);
                pixels[y * size + x] = c;
            }
        }

        foreach (var f in houseFootprints)
        {
            int cx = Mathf.RoundToInt((f.x / MapSize + 0.5f) * size);
            int cz = Mathf.RoundToInt((f.y / MapSize + 0.5f) * size);
            int rx = Mathf.Max(1, Mathf.RoundToInt(f.z / MapSize * size));
            int rz = Mathf.Max(1, Mathf.RoundToInt(f.w / MapSize * size));
            for (int y = cz - rz; y <= cz + rz; y++)
                for (int x = cx - rx; x <= cx + rx; x++)
                    if (x >= 0 && y >= 0 && x < size && y < size)
                        pixels[y * size + x] = houseColor;
        }

        MinimapTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        MinimapTexture.wrapMode = TextureWrapMode.Clamp;
        MinimapTexture.filterMode = FilterMode.Bilinear;
        MinimapTexture.SetPixels32(pixels);
        MinimapTexture.Apply(false, true);
    }

    /// <summary>World position to 0..1 minimap coordinates.</summary>
    public static Vector2 ToMapUV(Vector3 world)
    {
        return new Vector2(world.x / MapSize + 0.5f, world.z / MapSize + 0.5f);
    }
}
