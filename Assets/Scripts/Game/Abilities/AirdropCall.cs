using UnityEngine;

/// <summary>
/// Hava İkmali Jetonu: orange smoke marks the spot, and after a short wait a supply crate drifts down on a
/// parachute. When it lands it becomes a pick-up with top loot (LootSystem.SpawnSupplyCrate).
/// </summary>
public class AirdropCall : MonoBehaviour
{
    public const float Delay = 15f;
    public const float DropHeight = 110f;
    public const float FallSpeed = 7f;

    public static readonly System.Collections.Generic.List<AirdropCall> Active = new System.Collections.Generic.List<AirdropCall>();

    private Vector3 ground;
    private float arriveAt;
    private float nextSmoke;
    private Transform crate;

    /// <summary>The spot it will land (for the minimap).</summary>
    public Vector3 Target { get { return ground; } }

    public static AirdropCall Call(Vector3 position)
    {
        RaycastHit hit;
        float y = Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.point.y : World.GroundHeight(position.x, position.z);
        var go = new GameObject("AirdropCall");
        var a = go.AddComponent<AirdropCall>();
        a.ground = new Vector3(position.x, y, position.z);
        a.arriveAt = Time.time + Delay;
        AbilityFx.Track(go);
        Active.Add(a);
        return a;
    }

    private void OnDestroy()
    {
        Active.Remove(this);
    }

    private void Update()
    {
        if (Time.time >= nextSmoke)
        {
            nextSmoke = Time.time + 0.12f;
            Effects.Smoke(ground + Vector3.up * 0.2f, new Color(1f, 0.45f, 0.1f, 0.75f));
        }
        float timeToLand = DropHeight / FallSpeed;
        if (crate == null && Time.time >= arriveAt - timeToLand)
            BuildCrate();
        if (crate == null)
            return;
        Vector3 p = crate.position - Vector3.up * FallSpeed * Time.deltaTime;
        crate.position = p;
        crate.Rotate(0f, 20f * Time.deltaTime, 0f);
        if (p.y <= ground.y + 0.3f)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.lootSystem != null)
                gm.lootSystem.SpawnSupplyCrate(ground);
            Effects.Dust(ground, 14);
            Sfx.PlayAt(SoundBank.Land, ground, 1f, 0.6f);
            Destroy(gameObject);
        }
    }

    private void BuildCrate()
    {
        crate = new GameObject("FallingSupply").transform;
        crate.SetParent(transform, false);
        crate.position = ground + Vector3.up * DropHeight;
        BuildSupplyModel(crate);
        // Parachute
        var canopy = AbilityFx.Primitive(crate, PrimitiveType.Sphere, new Vector3(0f, 3.4f, 0f), new Vector3(3.6f, 1.1f, 3.6f), MaterialCache.Lit(new Color(1f, 0.5f, 0.12f)));
        canopy.name = "Canopy";
        for (int i = 0; i < 4; i++)
        {
            float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
            Vector3 top = new Vector3(sx * 1.4f, 3.2f, sz * 1.4f), bottom = new Vector3(sx * 0.4f, 0.6f, sz * 0.4f);
            var line = AbilityFx.Primitive(crate, PrimitiveType.Cube, (top + bottom) * 0.5f, new Vector3(0.03f, Vector3.Distance(top, bottom), 0.03f), MaterialCache.Lit(new Color(0.9f, 0.9f, 0.85f)));
            line.localRotation = Quaternion.FromToRotation(Vector3.up, top - bottom);
        }
        Sfx.PlayAt(SoundBank.Whoosh, crate.position, 1f, 0.5f);
    }

    /// <summary>Orange-red supply crate with white bands and a paw emblem (also used for the landed pick-up).</summary>
    public static void BuildSupplyModel(Transform parent)
    {
        var red = MaterialCache.Lit(new Color(0.85f, 0.25f, 0.12f));
        var white = MaterialCache.Lit(new Color(0.95f, 0.93f, 0.88f));
        var dark = MaterialCache.Lit(new Color(0.15f, 0.12f, 0.1f));
        AbilityFx.Primitive(parent, PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.1f, 0.9f, 1.1f), red);
        AbilityFx.Primitive(parent, PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.14f, 0.12f, 1.14f), white);
        AbilityFx.Primitive(parent, PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(0.14f, 0.94f, 1.14f), white);
        AbilityFx.Primitive(parent, PrimitiveType.Cube, new Vector3(0f, 0.92f, 0f), new Vector3(1.16f, 0.06f, 1.16f), dark);
        AbilityFx.Primitive(parent, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(1.16f, 0.06f, 1.16f), dark);
        // Paw emblem on two sides
        for (int s = -1; s <= 1; s += 2)
        {
            AbilityFx.Primitive(parent, PrimitiveType.Sphere, new Vector3(0.3f, 0.62f, s * 0.56f), new Vector3(0.2f, 0.16f, 0.02f), white);
            for (int t = 0; t < 3; t++)
                AbilityFx.Primitive(parent, PrimitiveType.Sphere, new Vector3(0.3f + (t - 1) * 0.1f, 0.76f, s * 0.56f), new Vector3(0.07f, 0.07f, 0.02f), white);
        }
    }
}
