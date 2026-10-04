using System.Collections.Generic;
using UnityEngine;

public enum LootType
{
    Weapon,
    Ammo,
    Medkit,
    Armor,
    Grenade,
    Drink,
    Supply      // air drop: top loot
}

/// <summary>Spawns supply crates in houses and around the island; walking into one picks it up.</summary>
public class LootSystem : MonoBehaviour
{
    private class Crate
    {
        public int id;
        public GameObject obj;
        public LootType type;
        public Vector3 basePos;
    }

    /// <summary>A crate as the server sends it to the phones.</summary>
    public struct CrateInfo
    {
        public int id;
        public LootType type;
        public Vector3 position;
    }

    private readonly List<Crate> crates = new List<Crate>();
    private const float PickupDistance = 1.6f;
    private int nextId = 1;
    /// <summary>Online: crates we asked the server for, given only when it says they are ours.</summary>
    private readonly Dictionary<int, LootType> pendingNet = new Dictionary<int, LootType>();

    public void SpawnLoot(int outdoorCount)
    {
        Clear();

        // Inside buildings first (better odds of weapons), then scattered outside.
        foreach (var spot in World.LootSpots)
            SpawnCrate(spot, RollType(true));

        for (int i = 0; i < outdoorCount; i++)
        {
            Vector3 p = World.RandomOpenPoint(Vector3.zero, World.IslandRadius - 6f);
            SpawnCrate(p - Vector3.up * 0.65f, RollType(false));
        }
    }

    private static LootType RollType(bool indoor)
    {
        float r = Random.value;
        if (indoor)
        {
            if (r < 0.4f) return LootType.Weapon;
            if (r < 0.58f) return LootType.Ammo;
            if (r < 0.7f) return LootType.Armor;
            if (r < 0.8f) return LootType.Medkit;
            if (r < 0.9f) return LootType.Grenade;
            return LootType.Drink;
        }
        if (r < 0.25f) return LootType.Weapon;
        if (r < 0.5f) return LootType.Ammo;
        if (r < 0.65f) return LootType.Medkit;
        if (r < 0.78f) return LootType.Armor;
        if (r < 0.88f) return LootType.Grenade;
        return LootType.Drink;
    }

    /// <summary>Eliminated bots leave a crate behind.</summary>
    public void DropDeathCrate(Vector3 position)
    {
        // Short ray from the body down, so bots killed indoors drop loot on the floor, not the roof.
        RaycastHit hit;
        float ground = Physics.Raycast(position + Vector3.up * 0.5f, Vector3.down, out hit, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.point.y
            : World.HeightAt(position.x, position.z);
        Vector3 p = new Vector3(position.x, ground + 0.3f, position.z);
        LootType type = Random.value < 0.55f ? LootType.Weapon : (Random.value < 0.5f ? LootType.Ammo : LootType.Medkit);
        int id = SpawnCrate(p, type);
        NetGame.CrateAdded(id, type, p);
    }

    /// <summary>A landed air-drop crate (picked up like any crate, gives top loot).</summary>
    public void SpawnSupplyCrate(Vector3 ground)
    {
        var crate = new GameObject("Loot_Supply");
        crate.transform.SetParent(transform, false);
        crate.transform.position = ground + Vector3.up * 0.65f;   // the crates bob around basePos
        var model = new GameObject("Model").transform;
        model.SetParent(crate.transform, false);
        model.localPosition = new Vector3(0f, -0.65f, 0f);
        AirdropCall.BuildSupplyModel(model);
        var beacon = AbilityFx.Primitive(model, PrimitiveType.Cylinder, new Vector3(0f, 6f, 0f), new Vector3(0.25f, 5f, 0.25f), AbilityFx.Glass(new Color(1f, 0.5f, 0.15f, 0.3f)));
        beacon.name = "Beacon";
        // basePos is ~0.65 m above the ground for the pick-up height check, like the other crates.
        int id = nextId++;
        crates.Add(new Crate { id = id, obj = crate, type = LootType.Supply, basePos = ground + Vector3.up * 0.65f });
        NetGame.CrateAdded(id, LootType.Supply, ground + Vector3.up * 0.65f);
    }

    /// <summary>Landed supply crates still waiting to be opened (for the minimap).</summary>
    public void SupplyCrates(List<Vector3> into)
    {
        into.Clear();
        foreach (var c in crates)
            if (c.obj != null && c.type == LootType.Supply)
                into.Add(c.basePos);
    }

    private int SpawnCrate(Vector3 position, LootType type)
    {
        int id = nextId++;
        var crate = new GameObject("Loot_" + type);
        crate.transform.SetParent(transform, false);
        crate.transform.position = position;
        crate.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Color body = ColorFor(type);
        Color band = new Color(0.15f, 0.15f, 0.15f);
        // Armour shows the real gear: a plate-carrier vest or a tactical helmet lying on the ground.
        GameObject gear = type == LootType.Armor
            ? ModelLibrary.Spawn(ModelLibrary.PropPath(Random.value < 0.5f ? "ArmorVest" : "ArmorHelmet"), crate.transform)
            : null;
        var model = gear == null ? ModelLibrary.Spawn(ModelLibrary.PropPath("Crate"), crate.transform) : null;
        if (gear != null)
        {
            ModelLibrary.ShareMaterials(gear, false);
            Bounds b = ModelLibrary.RenderBounds(gear);
            float k = 0.5f / Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
            if (k < 0.4f || k > 2.5f)
                gear.transform.localScale *= k;
            b = ModelLibrary.RenderBounds(gear);
            gear.transform.localPosition = new Vector3(0f, -0.3f - (b.min.y - position.y), 0f);
        }
        else if (model != null)
        {
            // Wooden crate model (~0.8 m) shrunk to 0.55 m, with a coloured strap showing what is inside.
            model.transform.localScale *= 0.7f;
            Bounds b = ModelLibrary.RenderBounds(model);
            float k = 0.55f / Mathf.Max(0.01f, b.size.y);
            if (k < 0.4f || k > 2.5f)
                model.transform.localScale *= k;
            b = ModelLibrary.RenderBounds(model);
            model.transform.localPosition = new Vector3(0f, -0.3f - (b.min.y - position.y), 0f);
            ModelLibrary.ShareMaterials(model, false);
            Part(crate.transform, PrimitiveType.Cube, new Vector3(0f, 0.27f, 0f), new Vector3(0.6f, 0.04f, 0.16f), body);
        }
        else
        {
            Part(crate.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.7f, 0.42f, 0.5f), body);
            Part(crate.transform, PrimitiveType.Cube, new Vector3(0f, 0.22f, 0f), new Vector3(0.74f, 0.06f, 0.54f), body * 0.8f);
            Part(crate.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.12f, 0.44f, 0.52f), band);
        }
        // Small floating marker so loot is easy to spot.
        Part(crate.transform, PrimitiveType.Sphere, new Vector3(0f, 0.75f, 0f), Vector3.one * 0.16f, Color.Lerp(body, Color.white, 0.4f));

        crates.Add(new Crate { id = id, obj = crate, type = type, basePos = position });
        return id;
    }

    // ----- Online -----

    /// <summary>Server: every crate on the map (sent to the phones at the start).</summary>
    public void GetCrates(List<CrateInfo> into)
    {
        into.Clear();
        foreach (var c in crates)
            if (c.obj != null)
                into.Add(new CrateInfo { id = c.id, type = c.type, position = c.basePos });
    }

    /// <summary>Phone: a crate the server told us about.</summary>
    public void SpawnNet(int id, LootType type, Vector3 position)
    {
        foreach (var c in crates)
            if (c.id == id)
                return;
        if (type == LootType.Supply)
        {
            SpawnSupplyCrate(position - Vector3.up * 0.65f);
            crates[crates.Count - 1].id = id;   // the server's number, not ours
            return;
        }
        SpawnCrate(position, type);
        crates[crates.Count - 1].id = id;
    }

    public bool CratePosition(int id, out Vector3 position)
    {
        foreach (var c in crates)
        {
            if (c.id == id && c.obj != null)
            {
                position = c.basePos;
                return true;
            }
        }
        position = Vector3.zero;
        return false;
    }

    /// <summary>Someone took this crate (server: a phone picked it up; phone: another player did).</summary>
    public void RemoveById(int id)
    {
        for (int i = crates.Count - 1; i >= 0; i--)
        {
            if (crates[i].id != id)
                continue;
            if (crates[i].obj != null)
                Destroy(crates[i].obj);
            crates.RemoveAt(i);
        }
    }

    private static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = MaterialCache.Lit(color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    public void Clear()
    {
        pendingNet.Clear();
        foreach (var c in crates)
        {
            if (c.obj != null)
                Destroy(c.obj);
        }
        crates.Clear();
    }

    private static Color ColorFor(LootType type)
    {
        switch (type)
        {
            case LootType.Weapon: return new Color(0.95f, 0.72f, 0.15f);
            case LootType.Ammo: return new Color(0.62f, 0.5f, 0.28f);
            case LootType.Medkit: return new Color(0.92f, 0.95f, 0.92f);
            case LootType.Armor: return new Color(0.25f, 0.45f, 0.95f);
            case LootType.Grenade: return new Color(0.3f, 0.45f, 0.22f);
            default: return new Color(0.95f, 0.4f, 0.75f);
        }
    }

    private void Update()
    {
        float t = Time.time;
        float spin = 45f * Time.deltaTime;
        foreach (var c in crates)
        {
            if (c.obj == null)
                continue;
            c.obj.transform.Rotate(0f, spin, 0f, Space.World);
            c.obj.transform.position = c.basePos + Vector3.up * (Mathf.Sin(t * 2f + c.basePos.x) * 0.05f);
        }
    }

    public void TryCollectLoot(PlayerController player)
    {
        if (player == null)
            return;

        var ui = GameManager.Instance != null ? GameManager.Instance.uiManager : null;
        Vector3 pos = player.transform.position;

        for (int i = crates.Count - 1; i >= 0; i--)
        {
            var c = crates[i];
            if (c.obj == null)
            {
                crates.RemoveAt(i);
                continue;
            }

            Vector3 d = c.basePos - pos;
            if (Mathf.Abs(d.y + 0.6f) > 1.4f)
                continue;
            d.y = 0f;
            if (d.magnitude > PickupDistance)
                continue;

            if (NetGame.InOnlineMatch)
            {
                // Two players can touch the same crate: the server decides who gets it.
                if (!CanTake(player, c.type))
                    continue;
                pendingNet[c.id] = c.type;
                NetGame.LootTaken(c.id);
                Destroy(c.obj);
                crates.RemoveAt(i);
                continue;
            }

            string message = Apply(player, c.type);
            if (message == null)
                continue; // e.g. medkits full: leave it for later

            if (ui != null)
                ui.Toast(message);
            Sfx.Play(SoundBank.Pickup, 0.45f);
            Destroy(c.obj);
            crates.RemoveAt(i);
        }
    }

    /// <summary>Whether picking this up would do anything (same rules as <see cref="Apply"/>).</summary>
    private static bool CanTake(PlayerController player, LootType type)
    {
        switch (type)
        {
            case LootType.Medkit: return player.inventory.medkits < Inventory.MaxMedkits;
            case LootType.Grenade: return player.inventory.grenades < Inventory.MaxGrenades;
            case LootType.Drink: return player.inventory.drinks < Inventory.MaxDrinks;
            case LootType.Armor: return player.armor < player.maxArmor;
            default: return true;
        }
    }

    /// <summary>Online: the server's answer to our pick-up. Only now the item is given.</summary>
    public void NetPickupResult(int id, bool ours, PlayerController player)
    {
        LootType type;
        if (!pendingNet.TryGetValue(id, out type))
            return;
        pendingNet.Remove(id);
        if (!ours || player == null || player.isDead)
            return;
        string message = Apply(player, type);
        if (message == null)
            return;
        var ui = GameManager.Instance != null ? GameManager.Instance.uiManager : null;
        if (ui != null)
            ui.Toast(message);
        Sfx.Play(SoundBank.Pickup, 0.45f);
    }

    private static string Apply(PlayerController player, LootType type)
    {
        switch (type)
        {
            case LootType.Weapon:
                return player.GiveWeapon(WeaponData.CreateRandomLoot());

            case LootType.Ammo:
                return player.GiveAmmo();

            case LootType.Medkit:
                if (!player.inventory.AddMedkit())
                    return null;
                return "İlk yardım çantası alındı";

            case LootType.Grenade:
                if (!player.inventory.AddGrenade())
                    return null;
                return "El bombası alındı";

            case LootType.Drink:
                if (!player.inventory.AddDrink())
                    return null;
                return "Enerji içeceği alındı";

            case LootType.Supply:
            {
                string gun = player.GiveWeapon(Random.value < 0.5f ? WeaponData.CreateSniper() : WeaponData.CreateRifle());
                player.armor = player.maxArmor;
                player.inventory.AddMedkit();
                player.inventory.AddMedkit();
                player.inventory.AddDrink();
                player.inventory.AddGrenade();
                player.GiveAmmo();
                return "İKMAL: " + gun + "  •  tam zırh  •  ilk yardım";
            }

            default:
                if (player.armor >= player.maxArmor)
                    return null;
                player.armor = Mathf.Min(player.maxArmor, player.armor + 50f);
                return "+50 zırh";
        }
    }
}
