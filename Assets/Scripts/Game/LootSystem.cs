using System.Collections.Generic;
using UnityEngine;

public enum LootType
{
    Weapon,
    Ammo,
    Medkit,
    Armor,
    Grenade,
    Drink
}

/// <summary>Spawns supply crates in houses and around the island; walking into one picks it up.</summary>
public class LootSystem : MonoBehaviour
{
    private class Crate
    {
        public GameObject obj;
        public LootType type;
        public Vector3 basePos;
    }

    private readonly List<Crate> crates = new List<Crate>();
    private const float PickupDistance = 1.6f;

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
        SpawnCrate(p, Random.value < 0.55f ? LootType.Weapon : (Random.value < 0.5f ? LootType.Ammo : LootType.Medkit));
    }

    private void SpawnCrate(Vector3 position, LootType type)
    {
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

        crates.Add(new Crate { obj = crate, type = type, basePos = position });
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

            default:
                if (player.armor >= player.maxArmor)
                    return null;
                player.armor = Mathf.Min(player.maxArmor, player.armor + 50f);
                return "+50 zırh";
        }
    }
}
