using System.Collections.Generic;
using UnityEngine;

public enum LootType
{
    Weapon,
    Ammo,
    Medkit,
    Armor
}

/// <summary>Spawns supply crates around the island; walking into one picks it up.</summary>
public class LootSystem : MonoBehaviour
{
    private class Crate
    {
        public GameObject obj;
        public LootType type;
    }

    private readonly List<Crate> crates = new List<Crate>();
    private const float PickupDistance = 1.6f;

    public void SpawnLoot(int count)
    {
        Clear();

        for (int i = 0; i < count; i++)
        {
            float r = Random.value;
            LootType type = r < 0.35f ? LootType.Weapon : r < 0.6f ? LootType.Ammo : r < 0.8f ? LootType.Medkit : LootType.Armor;

            var crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crate.name = "Loot_" + type;
            crate.transform.SetParent(transform, false);
            Vector2 p = Random.insideUnitCircle * (GameBootstrap.IslandRadius - 5f);
            crate.transform.position = new Vector3(p.x, 0.3f, p.y);
            crate.transform.localScale = new Vector3(0.8f, 0.6f, 0.8f);
            crate.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 90f), 0f);
            crate.GetComponent<Renderer>().material.color = ColorFor(type);

            // Pick-ups shouldn't block movement or bullets.
            Destroy(crate.GetComponent<Collider>());

            crates.Add(new Crate { obj = crate, type = type });
        }
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
            case LootType.Weapon: return new Color(0.95f, 0.75f, 0.15f);
            case LootType.Ammo: return new Color(0.75f, 0.55f, 0.3f);
            case LootType.Medkit: return new Color(0.9f, 0.95f, 0.9f);
            default: return new Color(0.25f, 0.45f, 0.95f);
        }
    }

    private void Update()
    {
        // Gentle spin so crates are easier to spot.
        float spin = 45f * Time.deltaTime;
        foreach (var c in crates)
        {
            if (c.obj != null)
                c.obj.transform.Rotate(0f, spin, 0f, Space.World);
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

            Vector3 d = c.obj.transform.position - pos;
            d.y = 0f;
            if (d.magnitude > PickupDistance)
                continue;

            string message = Apply(player, c.type);
            if (message == null)
                continue; // e.g. medkits full: leave it for later

            if (ui != null)
                ui.Toast(message);
            Destroy(c.obj);
            crates.RemoveAt(i);
        }
    }

    private static int Tier(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Pistol: return 0;
            case WeaponType.SMG:
            case WeaponType.Shotgun: return 1;
            default: return 2;
        }
    }

    private static string Apply(PlayerController player, LootType type)
    {
        var weapon = player.currentWeapon;
        switch (type)
        {
            case LootType.Weapon:
                WeaponData found = WeaponData.CreateRandomLoot();
                if (weapon.weaponData != null &&
                    (weapon.weaponData.weaponType == found.weaponType || Tier(found.weaponType) < Tier(weapon.weaponData.weaponType)))
                {
                    weapon.AddAmmo(found.magazineSize * 2);
                    return "+" + (found.magazineSize * 2) + " mermi";
                }
                player.EquipWeapon(found);
                return found.weaponName + " alındı";

            case LootType.Ammo:
                int amount = weapon.weaponData != null ? weapon.weaponData.magazineSize * 2 : 30;
                weapon.AddAmmo(amount);
                return "+" + amount + " mermi";

            case LootType.Medkit:
                if (!player.inventory.AddMedkit())
                    return null;
                return "İlk yardım çantası alındı";

            default:
                if (player.armor >= player.maxArmor)
                    return null;
                player.armor = Mathf.Min(player.maxArmor, player.armor + 50f);
                return "+50 zırh";
        }
    }
}
