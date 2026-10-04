using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vehicle and parachute camouflage catalogues, what the player owns and has equipped.
/// Weapon camos live in Gunsmith.Camos; all three share CamoDef and the camo pattern generator
/// (WeaponDressing.Pattern). Ids are unique across the three lists (v_ / p_ prefixes).
/// </summary>
public static class Cosmetics
{
    public static readonly List<CamoDef> VehicleCamos = new List<CamoDef>
    {
        new CamoDef { id = "", name = "Fabrika Boyası", price = 0, rarity = "Sıradan" },
        new CamoDef { id = "v_forest", name = "Orman", price = 200, rarity = "Sıradan", a = new Color(0.22f, 0.3f, 0.16f), b = new Color(0.35f, 0.4f, 0.22f), c = new Color(0.16f, 0.13f, 0.09f), pattern = 0, gloss = 0.2f },
        new CamoDef { id = "v_desert", name = "Çöl", price = 200, rarity = "Sıradan", a = new Color(0.76f, 0.64f, 0.44f), b = new Color(0.6f, 0.47f, 0.3f), c = new Color(0.86f, 0.78f, 0.6f), pattern = 0, gloss = 0.2f },
        new CamoDef { id = "v_urban", name = "Kentsel", price = 200, rarity = "Sıradan", a = new Color(0.45f, 0.47f, 0.5f), b = new Color(0.25f, 0.27f, 0.3f), c = new Color(0.68f, 0.7f, 0.72f), pattern = 2, gloss = 0.3f },
        new CamoDef { id = "v_snow", name = "Kar", price = 200, rarity = "Sıradan", a = new Color(0.9f, 0.92f, 0.94f), b = new Color(0.62f, 0.66f, 0.7f), c = new Color(0.78f, 0.8f, 0.83f), pattern = 0, gloss = 0.25f },
        new CamoDef { id = "v_tiger", name = "Kaplan", price = 400, rarity = "Nadir", a = new Color(0.9f, 0.45f, 0.08f), b = new Color(0.06f, 0.05f, 0.05f), c = new Color(0.95f, 0.6f, 0.15f), pattern = 1, gloss = 0.45f },
        new CamoDef { id = "v_navy", name = "Lacivert Dijital", price = 400, rarity = "Nadir", a = new Color(0.12f, 0.17f, 0.3f), b = new Color(0.22f, 0.3f, 0.48f), c = new Color(0.06f, 0.08f, 0.15f), pattern = 2, gloss = 0.4f },
        new CamoDef { id = "v_zebra", name = "Zebra", price = 400, rarity = "Nadir", a = new Color(0.92f, 0.92f, 0.9f), b = new Color(0.06f, 0.06f, 0.06f), c = new Color(0.85f, 0.85f, 0.82f), pattern = 1, gloss = 0.4f },
        new CamoDef { id = "v_race", name = "Yarış Şeridi", price = 450, rarity = "Nadir", a = new Color(0.85f, 0.1f, 0.1f), b = new Color(0.95f, 0.95f, 0.95f), c = new Color(0.1f, 0.1f, 0.12f), pattern = 6, gloss = 0.7f },
        new CamoDef { id = "v_carbon", name = "Karbon", price = 800, rarity = "Epik", a = new Color(0.12f, 0.12f, 0.13f), b = new Color(0.05f, 0.05f, 0.06f), c = new Color(0.3f, 0.3f, 0.33f), pattern = 4, gloss = 0.9f, glow = new Color(0.6f, 0.7f, 0.9f, 0.1f) },
        new CamoDef { id = "v_emerald", name = "Zümrüt", price = 900, rarity = "Epik", a = new Color(0.03f, 0.18f, 0.1f), b = new Color(0.05f, 0.35f, 0.2f), c = new Color(0.2f, 1f, 0.55f), pattern = 3, gloss = 0.8f, glow = new Color(0.2f, 1f, 0.5f, 0.9f) },
        new CamoDef { id = "v_copper", name = "Bakır", price = 800, rarity = "Epik", a = new Color(0.85f, 0.5f, 0.3f), b = new Color(0.6f, 0.3f, 0.15f), c = new Color(1f, 0.7f, 0.5f), pattern = 4, gloss = 0.9f },
        new CamoDef { id = "v_lava", name = "Lav", price = 1600, rarity = "Efsanevi", a = new Color(0.08f, 0.05f, 0.04f), b = new Color(0.25f, 0.06f, 0.02f), c = new Color(1f, 0.45f, 0.05f), pattern = 3, gloss = 0.6f, glow = new Color(1f, 0.4f, 0.05f, 1.8f) },
        new CamoDef { id = "v_aurora", name = "Kutup Işığı", price = 1700, rarity = "Efsanevi", a = new Color(0.03f, 0.1f, 0.18f), b = new Color(0.15f, 0.9f, 0.6f), c = new Color(0.02f, 0.04f, 0.1f), pattern = 5, gloss = 0.8f, glow = new Color(0.2f, 1f, 0.7f, 1.4f) },
        new CamoDef { id = "v_galaxy", name = "Galaksi", price = 2600, rarity = "Mitik", a = new Color(0.05f, 0.05f, 0.2f), b = new Color(0.3f, 0.5f, 1f), c = new Color(0.02f, 0.01f, 0.06f), pattern = 5, gloss = 0.9f, glow = new Color(0.4f, 0.6f, 1f, 1.6f) },
    };

    public static readonly List<CamoDef> ParachuteCamos = new List<CamoDef>
    {
        new CamoDef { id = "", name = "Klasik", price = 0, rarity = "Sıradan" },
        new CamoDef { id = "p_redwhite", name = "Kırmızı Beyaz", price = 150, rarity = "Sıradan", a = new Color(0.85f, 0.12f, 0.12f), b = new Color(0.95f, 0.95f, 0.95f), c = new Color(0.7f, 0.1f, 0.1f), pattern = 6, gloss = 0.2f },
        new CamoDef { id = "p_olive", name = "Haki", price = 150, rarity = "Sıradan", a = new Color(0.36f, 0.38f, 0.2f), b = new Color(0.55f, 0.5f, 0.32f), c = new Color(0.25f, 0.26f, 0.13f), pattern = 6, gloss = 0.1f },
        new CamoDef { id = "p_sky", name = "Gökyüzü", price = 150, rarity = "Sıradan", a = new Color(0.25f, 0.55f, 0.9f), b = new Color(0.85f, 0.95f, 1f), c = new Color(0.15f, 0.35f, 0.7f), pattern = 8, gloss = 0.2f },
        new CamoDef { id = "p_check", name = "Dama", price = 300, rarity = "Nadir", a = new Color(0.08f, 0.08f, 0.08f), b = new Color(1f, 0.82f, 0.1f), c = new Color(0.2f, 0.2f, 0.2f), pattern = 7, gloss = 0.3f },
        new CamoDef { id = "p_sunset", name = "Gün Batımı", price = 300, rarity = "Nadir", a = new Color(1f, 0.55f, 0.2f), b = new Color(0.5f, 0.15f, 0.55f), c = new Color(1f, 0.85f, 0.4f), pattern = 8, gloss = 0.3f },
        new CamoDef { id = "p_navy", name = "Denizci", price = 300, rarity = "Nadir", a = new Color(0.1f, 0.15f, 0.35f), b = new Color(0.95f, 0.95f, 0.95f), c = new Color(0.8f, 0.15f, 0.15f), pattern = 6, gloss = 0.3f },
        new CamoDef { id = "p_paw", name = "Pati İzi", price = 700, rarity = "Epik", a = new Color(0.1f, 0.55f, 0.55f), b = new Color(1f, 1f, 1f), c = new Color(0.05f, 0.35f, 0.35f), pattern = 9, gloss = 0.4f, glow = new Color(0.6f, 1f, 1f, 0.3f) },
        new CamoDef { id = "p_tiger", name = "Kaplan", price = 700, rarity = "Epik", a = new Color(0.9f, 0.45f, 0.08f), b = new Color(0.06f, 0.05f, 0.05f), c = new Color(0.95f, 0.6f, 0.15f), pattern = 1, gloss = 0.4f },
        new CamoDef { id = "p_digital", name = "Neon Dijital", price = 750, rarity = "Epik", a = new Color(0.05f, 0.05f, 0.1f), b = new Color(0.1f, 0.9f, 1f), c = new Color(0.9f, 0.1f, 0.8f), pattern = 2, gloss = 0.5f, glow = new Color(0.2f, 0.9f, 1f, 0.8f) },
        new CamoDef { id = "p_flame", name = "Alev", price = 1400, rarity = "Efsanevi", a = new Color(0.1f, 0.05f, 0.05f), b = new Color(1f, 0.35f, 0.05f), c = new Color(1f, 0.85f, 0.2f), pattern = 10, gloss = 0.5f, glow = new Color(1f, 0.5f, 0.1f, 1.4f) },
        new CamoDef { id = "p_gold", name = "Kraliyet Altını", price = 1500, rarity = "Efsanevi", a = new Color(1f, 0.78f, 0.3f), b = new Color(0.85f, 0.6f, 0.15f), c = new Color(1f, 0.88f, 0.5f), pattern = 4, gloss = 0.95f, glow = new Color(1f, 0.75f, 0.3f, 0.3f) },
        new CamoDef { id = "p_nebula", name = "Nebula", price = 2400, rarity = "Mitik", a = new Color(0.2f, 0.06f, 0.35f), b = new Color(0.65f, 0.15f, 0.7f), c = new Color(0.1f, 0.05f, 0.2f), pattern = 5, gloss = 0.8f, glow = new Color(0.9f, 0.35f, 1f, 1.4f) },
        new CamoDef { id = "p_phoenix", name = "Anka Kanadı", price = 2600, rarity = "Mitik", a = new Color(0.4f, 0.05f, 0.02f), b = new Color(1f, 0.4f, 0.05f), c = new Color(1f, 0.9f, 0.4f), pattern = 10, gloss = 0.7f, glow = new Color(1f, 0.6f, 0.15f, 2f) },
        new CamoDef { id = "p_rainbow", name = "Gökkuşağı", price = 2400, rarity = "Mitik", a = new Color(0.95f, 0.25f, 0.3f), b = new Color(0.25f, 0.6f, 1f), c = new Color(1f, 0.85f, 0.2f), pattern = 6, gloss = 0.5f, glow = new Color(1f, 1f, 1f, 0.25f) },
    };

    public static CamoDef FindVehicleCamo(string id) { return Find(VehicleCamos, id); }
    public static CamoDef FindParachuteCamo(string id) { return Find(ParachuteCamos, id); }

    private static CamoDef Find(List<CamoDef> list, string id)
    {
        foreach (var c in list)
            if (c.id == (id ?? ""))
                return c;
        return list[0];
    }

    /// <summary>Any camo by id, whichever collection it belongs to (weapon, vehicle, parachute).</summary>
    public static CamoDef FindAny(string id)
    {
        if (string.IsNullOrEmpty(id))
            return Gunsmith.Camos[0];
        if (id.StartsWith("v_"))
            return FindVehicleCamo(id);
        if (id.StartsWith("p_"))
            return FindParachuteCamo(id);
        return Gunsmith.FindCamo(id);
    }

    // ----- Ownership & equipped (PlayerPrefs) -----

    private const string VehicleOwnedKey = "zm_vcamos", ParachuteOwnedKey = "zm_pcamos";
    private const string VehicleEquippedKey = "zm_vcamo", ParachuteEquippedKey = "zm_pcamo";

    public static bool OwnsVehicleCamo(string id) { return Owns(VehicleOwnedKey, id); }
    public static bool OwnsParachuteCamo(string id) { return Owns(ParachuteOwnedKey, id); }
    public static void AddVehicleCamo(string id) { Add(VehicleOwnedKey, id); }
    public static void AddParachuteCamo(string id) { Add(ParachuteOwnedKey, id); }

    public static string EquippedVehicleCamo
    {
        get { string id = PlayerPrefs.GetString(VehicleEquippedKey, ""); return OwnsVehicleCamo(id) ? id : ""; }
        set { PlayerPrefs.SetString(VehicleEquippedKey, value ?? ""); PlayerPrefs.Save(); }
    }

    public static string EquippedParachuteCamo
    {
        get { string id = PlayerPrefs.GetString(ParachuteEquippedKey, ""); return OwnsParachuteCamo(id) ? id : ""; }
        set { PlayerPrefs.SetString(ParachuteEquippedKey, value ?? ""); PlayerPrefs.Save(); }
    }

    private static bool Owns(string key, string id)
    {
        return string.IsNullOrEmpty(id) || ("," + PlayerPrefs.GetString(key, "") + ",").Contains("," + id + ",");
    }

    private static void Add(string key, string id)
    {
        if (Owns(key, id))
            return;
        string current = PlayerPrefs.GetString(key, "");
        PlayerPrefs.SetString(key, current.Length == 0 ? id : current + "," + id);
    }

    /// <summary>Weapon camos the player owns are stored by the gunsmith ("zm_gs_camos").</summary>
    public static void AddWeaponCamo(string id)
    {
        if (Gunsmith.OwnsCamo(id))
            return;
        string current = PlayerPrefs.GetString("zm_gs_camos", "");
        PlayerPrefs.SetString("zm_gs_camos", current.Length == 0 ? id : current + "," + id);
    }

    public static void AddAttachment(string id)
    {
        if (Gunsmith.OwnsAttachment(id))
            return;
        string current = PlayerPrefs.GetString("zm_gs_owned", "");
        PlayerPrefs.SetString("zm_gs_owned", current.Length == 0 ? id : current + "," + id);
    }

    /// <summary>What a duplicate of this rarity sells for.</summary>
    public static int SpareValue(string rarity)
    {
        switch (rarity)
        {
            case "Nadir": return 120;
            case "Epik": return 300;
            case "Efsanevi": return 700;
            case "Mitik": return 1500;
            default: return 50;
        }
    }
}
