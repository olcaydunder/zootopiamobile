using UnityEngine;

/// <summary>
/// The lobby showroom: a dark curved backdrop with LED strips (Zootopia/Stage shader, drawn procedurally so it stays
/// sharp at any resolution), a glossy floor, a pedestal with a light ring and two stage lights. Built high above the
/// map so the character is shown on a clean stage rather than in a street; only active while the lobby is on screen.
/// </summary>
public static class LobbyStage
{
    public static readonly Vector3 Centre = new Vector3(0f, 900f, 0f);
    /// <summary>The character faces -Z (towards the camera); the backdrop's bright middle is behind it at +Z.</summary>
    public const float Yaw = 180f;
    private const float PedestalTop = 0.2f;
    private const float WallRadius = 9f, WallHeight = 11f;

    private static GameObject root;

    /// <summary>Where the lobby character stands (on top of the pedestal).</summary>
    public static Vector3 Spot { get { return Centre + Vector3.up * PedestalTop; } }

    public static void SetActive(bool on)
    {
        if (on)
            Ensure();
        if (root != null && root.activeSelf != on)
            root.SetActive(on);
    }

    private static void Ensure()
    {
        if (root != null)
            return;
        root = new GameObject("LobbyStage");
        root.transform.position = Centre;
        var t = root.transform;

        var glow = new Color(0.16f, 0.5f, 0.95f);
        var amber = new Color(1f, 0.7f, 0.2f);

        // Backdrop wall all around (the camera is inside it) and a dark ceiling.
        var wallMat = StageMaterial();
        var wall = Part(t, "Wall", Cylinder(64, WallRadius, WallHeight), wallMat, Vector3.zero);
        wall.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        wall.receiveShadows = false;
        var ceiling = Part(t, "Ceiling", Disc(64, 0f, WallRadius), wallMat, new Vector3(0f, WallHeight, 0f));
        ceiling.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ceiling.receiveShadows = false;

        // Glossy dark floor (receives the character's shadow) with a box collider to stand on.
        var floorMat = new Material(MaterialCache.Lit(new Color(0.055f, 0.06f, 0.075f)));
        floorMat.SetFloat("_Glossiness", 0.82f);
        floorMat.SetFloat("_Metallic", 0.25f);
        var floor = Part(t, "Floor", Disc(64, 0f, WallRadius), floorMat, Vector3.zero);
        floor.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var floorCol = floor.gameObject.AddComponent<BoxCollider>();
        floorCol.center = new Vector3(0f, -0.1f, 0f);
        floorCol.size = new Vector3(WallRadius * 2f, 0.2f, WallRadius * 2f);

        // Thin light rings on the floor around the pedestal.
        var ringMat = UIUtil.UnlitMaterial(new Color(glow.r, glow.g, glow.b, 0.55f));
        Part(t, "FloorRing1", Disc(72, 2.15f, 2.2f), ringMat, new Vector3(0f, 0.004f, 0f)).shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Part(t, "FloorRing2", Disc(96, 4.4f, 4.43f), UIUtil.UnlitMaterial(new Color(glow.r, glow.g, glow.b, 0.3f)), new Vector3(0f, 0.004f, 0f))
            .shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Pedestal: dark metal drum, a cyan ring on top and an amber band round its edge.
        var pedMat = new Material(MaterialCache.Lit(new Color(0.11f, 0.12f, 0.15f)));
        pedMat.SetFloat("_Glossiness", 0.65f);
        pedMat.SetFloat("_Metallic", 0.5f);
        var ped = Part(t, "Pedestal", Cylinder(48, 1.25f, PedestalTop, true), pedMat, Vector3.zero);
        var pedCol = ped.gameObject.AddComponent<BoxCollider>();
        pedCol.center = new Vector3(0f, PedestalTop * 0.5f, 0f);
        pedCol.size = new Vector3(2.4f, PedestalTop, 2.4f);
        Part(t, "PedestalRing", Disc(72, 1.08f, 1.15f), UIUtil.UnlitMaterial(new Color(glow.r, glow.g, glow.b, 0.9f)), new Vector3(0f, PedestalTop + 0.003f, 0f))
            .shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Part(t, "PedestalBand", Cylinder(48, 1.262f, 0.025f), UIUtil.UnlitMaterial(new Color(amber.r, amber.g, amber.b, 0.95f)), new Vector3(0f, PedestalTop - 0.06f, 0f))
            .shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Stage lights: a warm key light in front of the character and a cool rim light behind it.
        Lamp(t, "KeyLight", new Vector3(-1.6f, 3.2f, -3.2f), new Color(1f, 0.95f, 0.88f), 1.35f, 10f);
        Lamp(t, "RimLight", new Vector3(0.4f, 2.6f, 2.2f), new Color(0.45f, 0.7f, 1f), 1.8f, 5.5f);
        Lamp(t, "FillLight", new Vector3(2.4f, 1.2f, -2.2f), new Color(0.75f, 0.85f, 1f), 0.5f, 6f);
    }

    private static Material StageMaterial()
    {
        var src = Resources.Load<Material>("ZootopiaStage");
        Material m = src != null ? new Material(src) : new Material(MaterialCache.Lit(new Color(0.05f, 0.07f, 0.11f)));
        m.SetVector("_Center", new Vector4(Centre.x, Centre.y, Centre.z, 0f));
        return m;
    }

    private static MeshRenderer Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        return mr;
    }

    private static void Lamp(Transform parent, string name, Vector3 localPos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        l.renderMode = LightRenderMode.ForcePixel;
        l.cullingMask = ~(1 << GunsmithScreen.PreviewLayer);
    }

    /// <summary>Open (or capped) vertical cylinder from y = 0 to <paramref name="height"/>, normals outwards.</summary>
    private static Mesh Cylinder(int segments, float radius, float height, bool capTop = false)
    {
        var verts = new System.Collections.Generic.List<Vector3>();
        var normals = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            var n = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            verts.Add(n * radius);
            verts.Add(n * radius + Vector3.up * height);
            normals.Add(n);
            normals.Add(n);
        }
        for (int i = 0; i < segments; i++)
        {
            int b = i * 2;
            tris.AddRange(new[] { b, b + 3, b + 1, b, b + 2, b + 3 });   // clockwise seen from outside
        }
        if (capTop)
        {
            int c = verts.Count;
            verts.Add(Vector3.up * height);
            normals.Add(Vector3.up);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                verts.Add(new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius + Vector3.up * height);
                normals.Add(Vector3.up);
            }
            for (int i = 0; i < segments; i++)
                tris.AddRange(new[] { c, c + 1 + i, c + 2 + i });
        }
        var mesh = new Mesh { name = "StageCylinder" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Flat ring (or disc when <paramref name="inner"/> is 0) facing up, at y = 0.</summary>
    private static Mesh Disc(int segments, float inner, float outer)
    {
        var verts = new Vector3[(segments + 1) * 2];
        var normals = new Vector3[verts.Length];
        var tris = new int[segments * 6];
        for (int i = 0; i <= segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            verts[i * 2] = d * inner;
            verts[i * 2 + 1] = d * outer;
            normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
        }
        for (int i = 0; i < segments; i++)
        {
            int b = i * 2, k = i * 6;
            tris[k] = b; tris[k + 1] = b + 1; tris[k + 2] = b + 3;
            tris[k + 3] = b; tris[k + 4] = b + 3; tris[k + 5] = b + 2;
        }
        var mesh = new Mesh { name = "StageDisc" };
        mesh.vertices = verts;
        mesh.normals = normals;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }
}
