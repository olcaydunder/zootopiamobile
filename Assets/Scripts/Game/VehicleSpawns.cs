using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where vehicles appear each round: cars, ATVs, bikes and trucks on the road spots, boats on the sea
/// next to the shore, a helicopter on one of two helipads; the tank arrives later by air (TankDrop).
/// </summary>
public static class VehicleSpawns
{
    private static readonly VehicleKind[] RoadCycle =
    {
        VehicleKind.Offroad, VehicleKind.ATV, VehicleKind.Moto, VehicleKind.Offroad, VehicleKind.Truck,
        VehicleKind.ATV, VehicleKind.Moto, VehicleKind.Offroad, VehicleKind.Moto, VehicleKind.Truck
    };

    private static readonly List<GameObject> pads = new List<GameObject>();

    public static void SpawnAll(List<Vehicle> into)
    {
        ClearPads();
        // Road spots (city) / parking spots (island).
        for (int i = 0; i < World.VehicleSpots.Count; i++)
        {
            float yaw = i < World.VehicleYaws.Count ? World.VehicleYaws[i] : Random.Range(0f, 360f);
            into.Add(Vehicle.Spawn(RoadCycle[i % RoadCycle.Length], World.VehicleSpots[i], yaw));
        }
        // A few extra off-road bikes and quads in open ground.
        float half = MapData.Loaded ? MapData.PlayHalf : World.IslandRadius * 0.7f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 p = World.RandomOpenPoint(new Vector3(Random.Range(-half, half) * 0.7f, 0f, Random.Range(-half, half) * 0.7f), 40f);
            into.Add(Vehicle.Spawn(i % 2 == 0 ? VehicleKind.ATV : VehicleKind.Moto, p - Vector3.up * 0.95f, Random.Range(0f, 360f)));
        }
        // Boats along the shore.
        for (int i = 0; i < 4; i++)
        {
            Vector3 spot;
            float yaw;
            if (BoatSpot(i * 90f + Random.Range(-30f, 30f), out spot, out yaw))
                into.Add(Vehicle.Spawn(VehicleKind.Boat, spot, yaw));
        }
        // Two helipads, a helicopter on one of them.
        int heliPad = Random.Range(0, 2);
        for (int i = 0; i < 2; i++)
        {
            Vector3 c = new Vector3(Mathf.Cos(i * Mathf.PI + 0.7f), 0f, Mathf.Sin(i * Mathf.PI + 0.7f)) * half * 0.55f;
            Vector3 p = OpenPad(c);
            BuildPad(p);
            if (i == heliPad)
                into.Add(Vehicle.Spawn(VehicleKind.Heli, p, Random.Range(0f, 360f)));
        }
    }

    /// <summary>Walks outward from the land at an angle until the sea is deep enough for a boat.</summary>
    private static bool BoatSpot(float angleDeg, out Vector3 spot, out float yaw)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        float start = MapData.Loaded ? MapData.PlayHalf * 0.6f : World.IslandRadius * 0.6f;
        float end = World.MapSize * 0.5f - 12f;
        for (float r = start; r < end; r += 4f)
        {
            Vector3 p = dir * r;
            if (PlayerController.WaterDepthAt(p) > 2f && PlayerController.WaterDepthAt(p + dir * 6f) > 2f)
            {
                spot = new Vector3(p.x, PlayerController.WaterY, p.z) + dir * 3f;
                yaw = Quaternion.LookRotation(new Vector3(-dir.z, 0f, dir.x)).eulerAngles.y;   // parallel to the shore
                return true;
            }
        }
        spot = Vector3.zero;
        yaw = 0f;
        return false;
    }

    private static Vector3 OpenPad(Vector3 around)
    {
        Vector3 best = World.RandomOpenPoint(around, 50f);
        for (int i = 0; i < 25; i++)
        {
            Vector3 p = World.RandomOpenPoint(around, 60f);
            if (!Physics.CheckSphere(p + Vector3.up * 3.5f, 5.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return p - Vector3.up * 0.95f;
            best = p;
        }
        return best - Vector3.up * 0.95f;
    }

    private static void BuildPad(Vector3 ground)
    {
        var pad = new GameObject("Helipad");
        pad.transform.position = ground + Vector3.up * 0.04f;
        if (World.Root != null)
            pad.transform.SetParent(World.Root, true);
        var dark = MaterialCache.Lit(new Color(0.2f, 0.21f, 0.23f));
        var white = MaterialCache.Lit(new Color(0.92f, 0.92f, 0.9f));
        var yellow = MaterialCache.Lit(new Color(0.95f, 0.78f, 0.15f));
        AbilityFx.Primitive(pad.transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(12f, 0.05f, 12f), yellow);
        AbilityFx.Primitive(pad.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(11f, 0.05f, 11f), dark);
        AbilityFx.Primitive(pad.transform, PrimitiveType.Cube, new Vector3(-1.4f, 0.05f, 0f), new Vector3(0.6f, 0.03f, 4f), white);
        AbilityFx.Primitive(pad.transform, PrimitiveType.Cube, new Vector3(1.4f, 0.05f, 0f), new Vector3(0.6f, 0.03f, 4f), white);
        AbilityFx.Primitive(pad.transform, PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(2.4f, 0.03f, 0.6f), white);
        pads.Add(pad);
    }

    public static void ClearPads()
    {
        foreach (var p in pads)
            if (p != null)
                Object.Destroy(p);
        pads.Clear();
    }
}

/// <summary>Mid-match event: a tank comes down under three parachutes somewhere inside the safe zone.</summary>
public class TankDrop : MonoBehaviour
{
    public static readonly List<TankDrop> Active = new List<TankDrop>();
    private Vector3 ground;
    private Transform model;
    private float nextSmoke;
    private float yaw;
    private const float FallSpeed = 8f;

    public Vector3 Target { get { return ground; } }

    public static TankDrop Launch(Vector3 groundPoint)
    {
        var go = new GameObject("TankDrop");
        var d = go.AddComponent<TankDrop>();
        d.ground = groundPoint;
        d.model = new GameObject("FallingTank").transform;
        d.model.SetParent(go.transform, false);
        d.model.position = groundPoint + Vector3.up * 140f;
        d.yaw = Random.Range(0f, 360f);
        d.model.rotation = Quaternion.Euler(0f, d.yaw, 0f);
        var m = ModelLibrary.Spawn("Models/Vehicles/Tank", d.model);
        if (m != null)
            ModelLibrary.ShareMaterials(m, false);
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f * Mathf.Deg2Rad;
            Vector3 top = new Vector3(Mathf.Cos(a) * 3f, 9f, Mathf.Sin(a) * 3f);
            AbilityFx.Primitive(d.model, PrimitiveType.Sphere, top, new Vector3(5f, 1.4f, 5f), MaterialCache.Lit(new Color(0.75f, 0.68f, 0.5f)));
            var line = AbilityFx.Primitive(d.model, PrimitiveType.Cube, top * 0.5f + Vector3.up * 1.5f, new Vector3(0.04f, top.magnitude, 0.04f), MaterialCache.Lit(new Color(0.85f, 0.85f, 0.8f)));
            line.localRotation = Quaternion.FromToRotation(Vector3.up, top - Vector3.up * 2.5f);
        }
        AbilityFx.Track(go);
        Active.Add(d);
        return d;
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
            Effects.Smoke(ground + Vector3.up * 0.2f, new Color(0.9f, 0.2f, 0.15f, 0.75f));
        }
        Vector3 p = model.position - Vector3.up * FallSpeed * Time.deltaTime;
        model.position = p;
        if (p.y > ground.y + 0.2f)
            return;
        var gm = GameManager.Instance;
        if (gm != null)
            gm.vehicles.Add(Vehicle.Spawn(VehicleKind.Tank, ground, yaw));
        Effects.Dust(ground, 24);
        Sfx.PlayAt(SoundBank.Explosion, ground, 0.7f, 0.5f);
        Destroy(gameObject);
    }
}
