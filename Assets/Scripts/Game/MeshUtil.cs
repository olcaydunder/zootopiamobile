using System.Collections.Generic;
using UnityEngine;

/// <summary>Procedural low-poly meshes (cones, rocks, grids, rings) so the world needs no model files.</summary>
public static class MeshUtil
{
    private static Mesh cone;
    private static Mesh openCylinder;

    /// <summary>Unit cone: base radius 0.5 at y=0, tip at y=1. Flat shaded.</summary>
    public static Mesh Cone
    {
        get
        {
            if (cone == null)
            {
                const int seg = 9;
                var verts = new List<Vector3>();
                var tris = new List<int>();
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2f / seg;
                    float a1 = (i + 1) * Mathf.PI * 2f / seg;
                    Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0f, Mathf.Sin(a0) * 0.5f);
                    Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0f, Mathf.Sin(a1) * 0.5f);
                    AddTri(verts, tris, p0, new Vector3(0f, 1f, 0f), p1);   // side
                    AddTri(verts, tris, p1, Vector3.zero, p0);               // bottom
                }
                cone = Build("Cone", verts, tris);
            }
            return cone;
        }
    }

    /// <summary>Open cylinder of radius 1 and height 1 (y 0..1), visible from both sides with a double-sided shader.</summary>
    public static Mesh OpenCylinder
    {
        get
        {
            if (openCylinder == null)
            {
                const int seg = 64;
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var tris = new List<int>();
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    float x = Mathf.Cos(a);
                    float z = Mathf.Sin(a);
                    verts.Add(new Vector3(x, 0f, z));
                    verts.Add(new Vector3(x, 1f, z));
                    uvs.Add(new Vector2((float)i / seg, 0f));
                    uvs.Add(new Vector2((float)i / seg, 1f));
                }
                for (int i = 0; i < seg; i++)
                {
                    int b = i * 2;
                    tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                    tris.Add(b + 2); tris.Add(b + 1); tris.Add(b + 3);
                }
                openCylinder = new Mesh { name = "OpenCylinder" };
                openCylinder.SetVertices(verts);
                openCylinder.SetUVs(0, uvs);
                openCylinder.SetTriangles(tris, 0);
                openCylinder.RecalculateNormals();
                openCylinder.RecalculateBounds();
            }
            return openCylinder;
        }
    }

    /// <summary>Lumpy faceted rock / bush blob, roughly radius 0.5.</summary>
    public static Mesh Blob(int seed, float roughness)
    {
        var rng = new System.Random(seed);
        // Icosphere-ish: subdivide an octahedron once for a pleasant low-poly look.
        var baseVerts = new List<Vector3>
        {
            Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back
        };
        var faces = new List<int[]>
        {
            new[] {0, 4, 3}, new[] {0, 3, 5}, new[] {0, 5, 2}, new[] {0, 2, 4},
            new[] {1, 3, 4}, new[] {1, 5, 3}, new[] {1, 2, 5}, new[] {1, 4, 2}
        };
        var mid = new Dictionary<long, int>();
        var newFaces = new List<int[]>();
        foreach (var f in faces)
        {
            int a = Mid(baseVerts, mid, f[0], f[1]);
            int b = Mid(baseVerts, mid, f[1], f[2]);
            int c = Mid(baseVerts, mid, f[2], f[0]);
            newFaces.Add(new[] { f[0], a, c });
            newFaces.Add(new[] { f[1], b, a });
            newFaces.Add(new[] { f[2], c, b });
            newFaces.Add(new[] { a, b, c });
        }

        var displaced = new Vector3[baseVerts.Count];
        for (int i = 0; i < baseVerts.Count; i++)
        {
            float k = 1f + ((float)rng.NextDouble() - 0.5f) * 2f * roughness;
            displaced[i] = baseVerts[i].normalized * 0.5f * k;
        }

        var verts = new List<Vector3>();
        var tris = new List<int>();
        foreach (var f in newFaces)
            AddTri(verts, tris, displaced[f[0]], displaced[f[1]], displaced[f[2]]);
        return Build("Blob", verts, tris);
    }

    /// <summary>Flat grid in the XZ plane, centred on the origin.</summary>
    public static Mesh Grid(float size, int resolution)
    {
        var verts = new List<Vector3>((resolution + 1) * (resolution + 1));
        var uvs = new List<Vector2>(verts.Capacity);
        var tris = new List<int>(resolution * resolution * 6);
        float step = size / resolution;
        float half = size * 0.5f;
        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                verts.Add(new Vector3(x * step - half, 0f, z * step - half));
                uvs.Add(new Vector2((float)x / resolution, (float)z / resolution));
            }
        }
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int i = z * (resolution + 1) + x;
                tris.Add(i); tris.Add(i + resolution + 1); tris.Add(i + 1);
                tris.Add(i + 1); tris.Add(i + resolution + 1); tris.Add(i + resolution + 2);
            }
        }
        var mesh = new Mesh { name = "Grid" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();   // the water shader uses tangent-space ripples
        mesh.RecalculateBounds();
        return mesh;
    }

    private static int Mid(List<Vector3> verts, Dictionary<long, int> cache, int a, int b)
    {
        long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
        int idx;
        if (cache.TryGetValue(key, out idx))
            return idx;
        verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
        idx = verts.Count - 1;
        cache[key] = idx;
        return idx;
    }

    private static void AddTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
    {
        tris.Add(verts.Count); verts.Add(a);
        tris.Add(verts.Count); verts.Add(b);
        tris.Add(verts.Count); verts.Add(c);
    }

    private static Mesh Build(string meshName, List<Vector3> verts, List<int> tris)
    {
        var mesh = new Mesh { name = meshName };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
