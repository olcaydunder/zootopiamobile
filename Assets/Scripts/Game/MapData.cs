using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// The real-world map the match is played on (MapCatalog.Current: Ekşioğlu, Senir, Fırat Üniversitesi), baked
/// by Tools/build_map.py from elevation data and OpenStreetMap: height grid, ground types, buildings, roads, trees.
/// Frame: metres, +x east, +z north, origin at the middle of the play area.
/// </summary>
public static class MapData
{
    public const byte GroundGrass = 0, GroundPark = 1, GroundDirt = 2, GroundUrban = 3, GroundIndustrial = 4,
        GroundForest = 5, GroundCemetery = 6, GroundPitch = 7, GroundPool = 8, GroundSidewalk = 9,
        GroundParking = 10, GroundRoad = 11, GroundBuilding = 12;

    public const int KindApartment = 0, KindIndustrial = 1, KindPublic = 2, KindMosque = 3, KindCommercial = 4, KindHouse = 5, KindShed = 6;
    public const int FlagEnterable = 1, FlagClinic = 2, FlagHipRoof = 4;

    public class Building
    {
        public int kind, levels, flags;
        public Vector2 obbCenter;
        public float obbWidth, obbDepth, obbAngle;   // angle (degrees) of the long side, counter-clockwise from +x
        public Vector2[] outline;
        public int[] roofTriangles;
    }

    public class Road
    {
        public int kind;      // 0 motorway/trunk, 1 primary/secondary, 2 street, 3 service, 4 footway
        public float width;
        public Vector2[] points;
    }

    public struct Tree
    {
        public Vector2 position;
        public int kind;      // 0 pine, 1 broadleaf, 2 cypress
    }

    public static bool Loaded { get; private set; }
    public static float MapSize, PlayHalf, Coast;
    public static Vector2 LobbyPoint;
    /// <summary>Highest terrain point inside the play area (0 when no map is loaded).</summary>
    public static float MaxHeight { get; private set; }
    public static float LobbyYaw;
    public static readonly List<Building> Buildings = new List<Building>();
    public static readonly List<Road> Roads = new List<Road>();
    public static readonly List<Tree> Trees = new List<Tree>();

    private static float[] heights;
    private static int heightRes;
    private static byte[] ground;
    private static int groundRes;
    private static bool tried;

    /// <summary>Loads the baked files once. Returns false if they are missing (the game then falls back to the old island).</summary>
    public static bool Load()
    {
        if (tried)
            return Loaded;
        tried = true;
        try
        {
            string dir = "Map/" + MapCatalog.Current + "/";
            var feat = Resources.Load<TextAsset>(dir + "features");
            var h = Resources.Load<TextAsset>(dir + "height");
            var g = Resources.Load<TextAsset>(dir + "ground");
            if (feat == null || h == null || g == null)
            {
                Debug.LogWarning("ZM harita: Map dosyaları bulunamadı, yedek ada kullanılıyor");
                return false;
            }

            using (var r = new BinaryReader(new MemoryStream(feat.bytes)))
            {
                string magic = new string(r.ReadChars(4));
                int version = r.ReadInt32();
                if (magic != "ZMAP" || version != 1)
                    throw new IOException("bad map header " + magic + " " + version);
                MapSize = r.ReadSingle();
                PlayHalf = r.ReadSingle();
                Coast = r.ReadSingle();
                heightRes = r.ReadInt32();
                groundRes = r.ReadInt32();
                LobbyPoint = new Vector2(r.ReadSingle(), r.ReadSingle());
                LobbyYaw = r.ReadSingle();
                r.ReadSingle();   // lobby height (recomputed from the grid)

                int nb = r.ReadInt32();
                for (int i = 0; i < nb; i++)
                {
                    var b = new Building();
                    b.kind = r.ReadByte();
                    b.levels = r.ReadByte();
                    b.flags = r.ReadByte();
                    r.ReadByte();
                    b.obbCenter = new Vector2(r.ReadSingle(), r.ReadSingle());
                    b.obbWidth = r.ReadSingle();
                    b.obbDepth = r.ReadSingle();
                    b.obbAngle = r.ReadSingle();
                    int np = r.ReadInt32();
                    b.outline = new Vector2[np];
                    for (int p = 0; p < np; p++)
                        b.outline[p] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    int nt = r.ReadInt32();
                    b.roofTriangles = new int[nt];
                    for (int t = 0; t < nt; t++)
                        b.roofTriangles[t] = r.ReadUInt16();
                    Buildings.Add(b);
                }

                int nr = r.ReadInt32();
                for (int i = 0; i < nr; i++)
                {
                    var road = new Road();
                    road.kind = r.ReadByte();
                    road.width = r.ReadSingle();
                    int np = r.ReadInt32();
                    road.points = new Vector2[np];
                    for (int p = 0; p < np; p++)
                        road.points[p] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    Roads.Add(road);
                }

                int ntr = r.ReadInt32();
                for (int i = 0; i < ntr; i++)
                {
                    var tr = new Tree();
                    tr.position = new Vector2(r.ReadSingle(), r.ReadSingle());
                    tr.kind = r.ReadByte();
                    Trees.Add(tr);
                }
            }

            byte[] hb = h.bytes;
            if (hb.Length != heightRes * heightRes * 2)
                throw new IOException("height size " + hb.Length);
            heights = new float[heightRes * heightRes];
            float max = 0f;
            float cellSize = MapSize / (heightRes - 1);
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = (hb[i * 2] | (hb[i * 2 + 1] << 8)) * 0.01f - 10f;
                float gx = (i % heightRes) * cellSize - MapSize * 0.5f, gz = (i / heightRes) * cellSize - MapSize * 0.5f;
                if (Mathf.Abs(gx) <= PlayHalf && Mathf.Abs(gz) <= PlayHalf)
                    max = Mathf.Max(max, heights[i]);
            }
            MaxHeight = max;

            ground = g.bytes;
            if (ground.Length != groundRes * groundRes)
                throw new IOException("ground size " + ground.Length);

            Loaded = true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("ZM harita yüklenemedi: " + e.Message);
            Buildings.Clear();
            Roads.Clear();
            Trees.Clear();
            Loaded = false;
        }
        return Loaded;
    }

    /// <summary>Forgets the loaded map (before the world is rebuilt for another one).</summary>
    public static void Unload()
    {
        tried = false;
        Loaded = false;
        Buildings.Clear();
        Roads.Clear();
        Trees.Clear();
        heights = null;
        ground = null;
        heightRes = 0;
        groundRes = 0;
        MaxHeight = 0f;
    }

    public static int HeightRes { get { return heightRes; } }

    /// <summary>Height grid node (clamped).</summary>
    public static float Node(int x, int z)
    {
        x = x < 0 ? 0 : (x >= heightRes ? heightRes - 1 : x);
        z = z < 0 ? 0 : (z >= heightRes ? heightRes - 1 : z);
        return heights[z * heightRes + x];
    }

    /// <summary>Terrain height, interpolated the same way the terrain mesh triangles are.</summary>
    public static float Height(float x, float z)
    {
        float cell = MapSize / (heightRes - 1);
        float fx = (x + MapSize * 0.5f) / cell;
        float fz = (z + MapSize * 0.5f) / cell;
        if (fx < 0f || fz < 0f || fx > heightRes - 1 || fz > heightRes - 1)
            return -6.5f;
        int ix = Mathf.Min(heightRes - 2, (int)fx);
        int iz = Mathf.Min(heightRes - 2, (int)fz);
        float tx = fx - ix;
        float tz = fz - iz;
        float h00 = heights[iz * heightRes + ix];
        float h10 = heights[iz * heightRes + ix + 1];
        float h01 = heights[(iz + 1) * heightRes + ix];
        float h11 = heights[(iz + 1) * heightRes + ix + 1];
        // Terrain mesh quads are split along the (1,0)-(0,1) diagonal (see World.BuildTerrain).
        if (tx + tz <= 1f)
            return h00 + (h10 - h00) * tx + (h01 - h00) * tz;
        return h11 + (h01 - h11) * (1f - tx) + (h10 - h11) * (1f - tz);
    }

    public static byte Ground(float x, float z)
    {
        int i = (int)((x + MapSize * 0.5f) / MapSize * groundRes);
        int j = (int)((z + MapSize * 0.5f) / MapSize * groundRes);
        if (i < 0 || j < 0 || i >= groundRes || j >= groundRes)
            return GroundGrass;
        return ground[j * groundRes + i];
    }

    public static int GroundRes { get { return groundRes; } }

    public static byte GroundCell(int i, int j)
    {
        i = i < 0 ? 0 : (i >= groundRes ? groundRes - 1 : i);
        j = j < 0 ? 0 : (j >= groundRes ? groundRes - 1 : j);
        return ground[j * groundRes + i];
    }
}
