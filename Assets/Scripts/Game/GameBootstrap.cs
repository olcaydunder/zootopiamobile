using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

/// <summary>
/// Entry point. Created automatically when any scene loads, so the game needs no editor setup:
/// builds the island, managers, UI, player, then opens the lobby.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance;

    public const float IslandRadius = 55f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (FindObjectOfType<GameBootstrap>() == null)
            new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Input.simulateMouseWithTouches = false;
        Input.multiTouchEnabled = true;

        EnsureEventSystem();
        SetupLighting();
        BuildWorld();

        var manager = new GameObject("GameManager").AddComponent<GameManager>();
        var player = new GameObject("Player").AddComponent<PlayerController>();
        manager.RegisterPlayer(player);
        manager.JoinLobby();
    }

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;
        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    private static void SetupLighting()
    {
        if (FindObjectOfType<Light>() == null)
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.None; // cheap on low-end phones
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);
    }

    // ----- World -----

    private void BuildWorld()
    {
        var world = new GameObject("World").transform;

        // Ocean (no collider: falling in is fatal).
        var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ocean.name = "Ocean";
        Destroy(ocean.GetComponent<Collider>());
        ocean.transform.SetParent(world, false);
        ocean.transform.position = new Vector3(0f, -1.6f, 0f);
        ocean.transform.localScale = new Vector3(60f, 1f, 60f);
        ocean.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.12f, 0.42f, 0.68f));

        // Beach ring, then grass on top.
        CreateDisc(world, "Beach", IslandRadius + 5f, -0.08f, new Color(0.86f, 0.78f, 0.55f));
        CreateDisc(world, "Grass", IslandRadius, 0f, new Color(0.33f, 0.6f, 0.28f));

        var rng = new System.Random(1923);

        for (int i = 0; i < 12; i++)
            CreateHouse(world, RandomPoint(rng, 8f, IslandRadius - 8f), (float)rng.NextDouble() * 90f, rng);

        for (int i = 0; i < 80; i++)
            CreateTree(world, RandomPoint(rng, 6f, IslandRadius - 2f), rng);

        for (int i = 0; i < 35; i++)
            CreateRock(world, RandomPoint(rng, 6f, IslandRadius - 2f), rng);
    }

    private static Vector3 RandomPoint(System.Random rng, float minR, float maxR)
    {
        double angle = rng.NextDouble() * Mathf.PI * 2f;
        float r = Mathf.Sqrt(Mathf.Lerp(minR * minR, maxR * maxR, (float)rng.NextDouble()));
        return new Vector3(Mathf.Cos((float)angle) * r, 0f, Mathf.Sin((float)angle) * r);
    }

    private static void CreateDisc(Transform parent, string discName, float radius, float topY, Color color)
    {
        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = discName;
        disc.transform.SetParent(parent, false);
        // Unity's cylinder is 2 units tall and 1 unit wide.
        disc.transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
        disc.transform.position = new Vector3(0f, topY - 1f, 0f);
        disc.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(color);

        // The default capsule collider would make a dome; use the real cylinder shape.
        DestroyImmediate(disc.GetComponent<Collider>());
        var mc = disc.AddComponent<MeshCollider>();
        mc.sharedMesh = disc.GetComponent<MeshFilter>().sharedMesh;
    }

    private static void CreateTree(Transform parent, Vector3 pos, System.Random rng)
    {
        float h = 2.5f + (float)rng.NextDouble() * 2f;

        var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "Tree";
        trunk.transform.SetParent(parent, false);
        trunk.transform.position = pos + Vector3.up * (h * 0.5f);
        trunk.transform.localScale = new Vector3(0.45f, h * 0.5f, 0.45f);
        trunk.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.36f, 0.24f, 0.13f));

        var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        canopy.name = "Canopy";
        canopy.transform.SetParent(trunk.transform, true);
        canopy.transform.position = pos + Vector3.up * (h + 0.6f);
        float s = 2.2f + (float)rng.NextDouble() * 1.2f;
        canopy.transform.localScale = new Vector3(s / 0.45f, s / (h * 0.5f), s / 0.45f);
        float g = 0.45f + (float)rng.NextDouble() * 0.2f;
        canopy.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.12f, g, 0.18f));
    }

    private static void CreateRock(Transform parent, Vector3 pos, System.Random rng)
    {
        var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.name = "Rock";
        rock.transform.SetParent(parent, false);
        float w = 1.2f + (float)rng.NextDouble() * 2.2f;
        float h = 0.8f + (float)rng.NextDouble() * 1.4f;
        rock.transform.position = pos + Vector3.up * (h * 0.3f);
        rock.transform.localScale = new Vector3(w, h, w * (0.7f + (float)rng.NextDouble() * 0.6f));
        rock.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
        float v = 0.45f + (float)rng.NextDouble() * 0.15f;
        rock.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(v, v, v * 1.05f));
    }

    private static void CreateHouse(Transform parent, Vector3 pos, float yaw, System.Random rng)
    {
        var house = new GameObject("House").transform;
        house.SetParent(parent, false);
        house.position = pos;
        house.rotation = Quaternion.Euler(0f, yaw, 0f);

        Color wall = Color.Lerp(new Color(0.85f, 0.8f, 0.7f), new Color(0.7f, 0.55f, 0.45f), (float)rng.NextDouble());
        const float size = 6f;
        const float height = 3f;
        const float thick = 0.3f;
        const float door = 1.6f;

        // Back and side walls.
        CreateBox(house, new Vector3(0f, height * 0.5f, size * 0.5f), new Vector3(size, height, thick), wall);
        CreateBox(house, new Vector3(-size * 0.5f, height * 0.5f, 0f), new Vector3(thick, height, size), wall);
        CreateBox(house, new Vector3(size * 0.5f, height * 0.5f, 0f), new Vector3(thick, height, size), wall);

        // Front wall with a doorway in the middle.
        float side = (size - door) * 0.5f;
        CreateBox(house, new Vector3(-(door + side) * 0.5f, height * 0.5f, -size * 0.5f), new Vector3(side, height, thick), wall);
        CreateBox(house, new Vector3((door + side) * 0.5f, height * 0.5f, -size * 0.5f), new Vector3(side, height, thick), wall);
        CreateBox(house, new Vector3(0f, height - 0.4f, -size * 0.5f), new Vector3(door, 0.8f, thick), wall);

        // Roof.
        CreateBox(house, new Vector3(0f, height + 0.15f, 0f), new Vector3(size + 0.6f, 0.3f, size + 0.6f), new Color(0.55f, 0.2f, 0.15f));
    }

    private static void CreateBox(Transform parent, Vector3 localPos, Vector3 scale, Color color)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPos;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(color);
    }
}
