using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dense swaying grass around the camera, drawn with GPU instancing (no GameObjects).
/// Clumps are generated per 8 m cell on grassy terrain and only cells near the camera are drawn.
/// </summary>
public class Grass : MonoBehaviour
{
    private const float CellSize = 8f;
    private const int BatchSize = 1023;

    private static Grass instance;
    private static int quality = 1;

    private Mesh clump;
    private Material material;
    private readonly Dictionary<long, Matrix4x4[]> cells = new Dictionary<long, Matrix4x4[]>();
    private readonly List<Matrix4x4[]> batches = new List<Matrix4x4[]>();
    private readonly List<Matrix4x4[]> pool = new List<Matrix4x4[]>();
    private readonly List<int> batchCounts = new List<int>();
    private Vector3 lastBuildPos = new Vector3(9999f, 0f, 9999f);
    private float radius = 30f;
    private float density = 0.45f;   // clumps per square metre

    public static void Create()
    {
        if (instance != null)
            return;
        var baseMat = Resources.Load<Material>("ZootopiaGrass");
        if (baseMat == null || !SystemInfo.supportsInstancing)
            return;
        instance = new GameObject("Grass").AddComponent<Grass>();
        instance.material = new Material(baseMat);
        instance.material.enableInstancing = true;
        instance.clump = BuildClump();
        SetQuality(quality);
    }

    public static void SetQuality(int q)
    {
        quality = q;
        if (instance == null)
            return;
        instance.enabled = q > 0;
        instance.radius = q >= 3 ? 55f : (q == 2 ? 42f : 30f);
        instance.density = q >= 3 ? 1f : (q == 2 ? 0.7f : 0.45f);
        instance.material.SetFloat("_FadeStart", instance.radius - 9f);
        instance.material.SetFloat("_FadeEnd", instance.radius);
        instance.cells.Clear();
        instance.lastBuildPos = new Vector3(9999f, 0f, 9999f);
    }

    /// <summary>A clump of 7 tapered blades, ~0.5 m tall; uv.y = 0 at the root, 1 at the tip.</summary>
    private static Mesh BuildClump()
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        var rng = new System.Random(5);
        for (int b = 0; b < 7; b++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = (float)rng.NextDouble() * 0.25f;
            Vector3 root = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            float yaw = (float)rng.NextDouble() * Mathf.PI * 2f;
            Vector3 side = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * 0.035f;
            Vector3 lean = new Vector3(Mathf.Sin(yaw), 0f, -Mathf.Cos(yaw)) * (0.05f + (float)rng.NextDouble() * 0.12f);
            float h = 0.35f + (float)rng.NextDouble() * 0.3f;

            int i0 = verts.Count;
            verts.Add(root - side); uvs.Add(new Vector2(0f, 0f));
            verts.Add(root + side); uvs.Add(new Vector2(1f, 0f));
            verts.Add(root - side * 0.6f + Vector3.up * h * 0.55f + lean * 0.4f); uvs.Add(new Vector2(0f, 0.55f));
            verts.Add(root + side * 0.6f + Vector3.up * h * 0.55f + lean * 0.4f); uvs.Add(new Vector2(1f, 0.55f));
            verts.Add(root + Vector3.up * h + lean); uvs.Add(new Vector2(0.5f, 1f));
            tris.AddRange(new[] { i0, i0 + 2, i0 + 1, i0 + 1, i0 + 2, i0 + 3, i0 + 2, i0 + 4, i0 + 3 });
        }
        var mesh = new Mesh { name = "GrassClump" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        // Generous bounds so wind sway never gets culled early.
        mesh.bounds = new Bounds(new Vector3(0f, 0.4f, 0f), new Vector3(1.5f, 1.2f, 1.5f));
        return mesh;
    }

    private static long Key(int cx, int cz)
    {
        return ((long)cx << 32) ^ (uint)cz;
    }

    private Matrix4x4[] GetCell(int cx, int cz)
    {
        long key = Key(cx, cz);
        Matrix4x4[] cell;
        if (cells.TryGetValue(key, out cell))
            return cell;

        var rng = new System.Random(cx * 73856093 ^ cz * 19349663);
        int target = Mathf.RoundToInt(CellSize * CellSize * density);
        var list = new List<Matrix4x4>(target);
        for (int i = 0; i < target; i++)
        {
            float x = (cx + (float)rng.NextDouble()) * CellSize;
            float z = (cz + (float)rng.NextDouble()) * CellSize;
            float h = World.HeightAt(x, z);
            if (h < 1.6f || !World.GrassAllowed(x, z))
                continue;
            float dx = World.HeightAt(x + 0.5f, z) - World.HeightAt(x - 0.5f, z);
            float dz = World.HeightAt(x, z + 0.5f) - World.HeightAt(x, z - 0.5f);
            if (dx * dx + dz * dz > 0.5f)
                continue;   // too steep (rocky slopes)
            // Patchy meadows rather than a uniform carpet.
            if (Mathf.PerlinNoise(x * 0.06f + 40f, z * 0.06f + 13f) < 0.32f)
                continue;
            float s = 0.75f + (float)rng.NextDouble() * 0.7f;
            list.Add(Matrix4x4.TRS(new Vector3(x, h - 0.03f, z), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), new Vector3(s, s * (0.8f + (float)rng.NextDouble() * 0.5f), s)));
        }
        cell = list.ToArray();
        cells[key] = cell;
        return cell;
    }

    private void Rebuild(Vector3 center)
    {
        lastBuildPos = center;
        batches.Clear();
        batchCounts.Clear();

        int minX = Mathf.FloorToInt((center.x - radius) / CellSize);
        int maxX = Mathf.FloorToInt((center.x + radius) / CellSize);
        int minZ = Mathf.FloorToInt((center.z - radius) / CellSize);
        int maxZ = Mathf.FloorToInt((center.z + radius) / CellSize);
        float r2 = (radius + CellSize) * (radius + CellSize);

        Matrix4x4[] current = null;
        int count = 0;
        for (int cz = minZ; cz <= maxZ; cz++)
        {
            for (int cx = minX; cx <= maxX; cx++)
            {
                float ccx = (cx + 0.5f) * CellSize - center.x;
                float ccz = (cz + 0.5f) * CellSize - center.z;
                if (ccx * ccx + ccz * ccz > r2)
                    continue;
                foreach (var m in GetCell(cx, cz))
                {
                    if (current == null || count == BatchSize)
                    {
                        if (current != null)
                            batchCounts.Add(count);
                        if (pool.Count <= batches.Count)
                            pool.Add(new Matrix4x4[BatchSize]);   // reused between rebuilds
                        current = pool[batches.Count];
                        batches.Add(current);
                        count = 0;
                    }
                    current[count++] = m;
                }
            }
        }
        if (current != null)
            batchCounts.Add(count);

        // Forget far-away cells so memory stays small.
        if (cells.Count > 400)
            cells.Clear();
    }

    private void Update()
    {
        var cam = Camera.main;
        if (cam == null || clump == null)
            return;

        Vector3 pos = cam.transform.position;
        // No grass from high up (plane / skydiving): too far to see and saves time.
        if (pos.y - World.HeightAt(pos.x, pos.z) > 60f)
            return;

        Vector3 d = pos - lastBuildPos;
        d.y = 0f;
        if (d.sqrMagnitude > 16f)
            Rebuild(pos);

        for (int i = 0; i < batches.Count; i++)
            Graphics.DrawMeshInstanced(clump, 0, material, batches[i], batchCounts[i], null, UnityEngine.Rendering.ShadowCastingMode.Off, true);
    }
}
