using System.Collections.Generic;
using UnityEngine;

public enum AttachmentSlot
{
    Muzzle = 0,      // Namlu
    Optic = 1,       // Nişangah
    Underbarrel = 2, // Alt namlu
    Magazine = 3,    // Şarjör
    Stock = 4        // Dipçik
}

public class AttachmentDef
{
    public string id;
    public string name;
    public AttachmentSlot slot;
    public int price;
    public string note;
    // Percent changes (+10 = 10% better for that stat)
    public float damage, fireRate, accuracy, range, mobility, control;
    public float magazine;   // +50 = 50% bigger magazine
    public float reload;     // +20 = 20% faster reload
    public float zoom = 1f;  // aim-down-sights FOV multiplier (smaller = more zoom)
    public bool suppressor;
    public WeaponType[] only; // null = every weapon
}

public class CamoDef
{
    public string id;
    public string name;
    public int price;
    public string rarity;       // Sıradan, Nadir, Epik, Efsanevi, Mitik
    public Color a, b, c;       // pattern colours
    public Color glow;          // emission colour (alpha = strength)
    public int pattern;         // 0 blotches, 1 stripes, 2 digital, 3 veins, 4 solid metal, 5 nebula
    public float gloss;
}

/// <summary>
/// Gunsmith data: attachment and camo catalogue, what the player owns and has equipped per weapon,
/// and how a loadout changes a weapon's stats. Used by the lobby gunsmith and every weapon the player picks up.
/// </summary>
public static class Gunsmith
{
    public static readonly string[] SlotNames = { "Namlu", "Nişangah", "Alt Namlu", "Şarjör", "Dipçik" };
    public static readonly WeaponType[] Weapons = { WeaponType.Rifle, WeaponType.SMG, WeaponType.Shotgun, WeaponType.Sniper, WeaponType.Pistol };
    public static readonly string[] CategoryNames = { "TAARRUZ", "HAFİF MAKİNELİ", "POMPALI", "KESKİN NİŞANCI", "TABANCA" };

    public static readonly List<AttachmentDef> Attachments = new List<AttachmentDef>
    {
        // Muzzle
        new AttachmentDef { id = "sup", name = "Susturucu", slot = AttachmentSlot.Muzzle, price = 250, suppressor = true, range = -10, control = 5, note = "Sessiz atış, namlu alevi yok" },
        new AttachmentDef { id = "comp", name = "Kompansatör", slot = AttachmentSlot.Muzzle, price = 200, control = 20, mobility = -3, accuracy = -3, note = "Dikey sekmeyi azaltır" },
        new AttachmentDef { id = "brake", name = "Namlu Freni", slot = AttachmentSlot.Muzzle, price = 300, control = 12, accuracy = 8, range = 5, mobility = -4 },
        new AttachmentDef { id = "long", name = "Uzun Namlu", slot = AttachmentSlot.Muzzle, price = 350, range = 25, damage = 6, mobility = -8, note = "Uzak mesafe hasarı" },
        // Optic
        new AttachmentDef { id = "red", name = "Kırmızı Nokta", slot = AttachmentSlot.Optic, price = 150, accuracy = 6, zoom = 0.9f },
        new AttachmentDef { id = "holo", name = "Holografik", slot = AttachmentSlot.Optic, price = 200, accuracy = 8, zoom = 0.85f },
        new AttachmentDef { id = "x3", name = "3x Dürbün", slot = AttachmentSlot.Optic, price = 300, accuracy = 12, zoom = 0.6f, mobility = -5, only = new[] { WeaponType.Rifle, WeaponType.SMG, WeaponType.Sniper } },
        new AttachmentDef { id = "x6", name = "6x Dürbün", slot = AttachmentSlot.Optic, price = 450, accuracy = 18, zoom = 0.38f, mobility = -8, only = new[] { WeaponType.Rifle, WeaponType.Sniper } },
        // Underbarrel
        new AttachmentDef { id = "vgrip", name = "Dikey Tutamak", slot = AttachmentSlot.Underbarrel, price = 200, control = 15, mobility = -3 },
        new AttachmentDef { id = "agrip", name = "Açılı Tutamak", slot = AttachmentSlot.Underbarrel, price = 200, accuracy = 10, control = 6, mobility = -3 },
        new AttachmentDef { id = "laser", name = "Taktik Lazer", slot = AttachmentSlot.Underbarrel, price = 250, accuracy = 14, mobility = 4, note = "Kalçadan atış isabeti" },
        // Magazine
        new AttachmentDef { id = "ext", name = "Uzatılmış Şarjör", slot = AttachmentSlot.Magazine, price = 250, magazine = 50, reload = -12, mobility = -2 },
        new AttachmentDef { id = "fast", name = "Hızlı Şarjör", slot = AttachmentSlot.Magazine, price = 250, reload = 30 },
        new AttachmentDef { id = "drum", name = "Davul Şarjör", slot = AttachmentSlot.Magazine, price = 400, magazine = 100, reload = -30, mobility = -8, only = new[] { WeaponType.Rifle, WeaponType.SMG, WeaponType.Shotgun } },
        // Stock
        new AttachmentDef { id = "light", name = "Hafif Dipçik", slot = AttachmentSlot.Stock, price = 200, mobility = 10, control = -6 },
        new AttachmentDef { id = "tac", name = "Taktik Dipçik", slot = AttachmentSlot.Stock, price = 250, control = 12, accuracy = 4, mobility = -4 },
        new AttachmentDef { id = "nostock", name = "Dipçiksiz", slot = AttachmentSlot.Stock, price = 150, mobility = 18, control = -15, accuracy = -6, only = new[] { WeaponType.SMG, WeaponType.Shotgun, WeaponType.Rifle } },
    };

    public static readonly List<CamoDef> Camos = new List<CamoDef>
    {
        new CamoDef { id = "", name = "Varsayılan", price = 0, rarity = "Sıradan" },
        new CamoDef { id = "forest", name = "Orman", price = 150, rarity = "Sıradan", a = new Color(0.22f, 0.3f, 0.16f), b = new Color(0.35f, 0.4f, 0.22f), c = new Color(0.16f, 0.13f, 0.09f), pattern = 0, gloss = 0.2f },
        new CamoDef { id = "desert", name = "Çöl", price = 150, rarity = "Sıradan", a = new Color(0.76f, 0.64f, 0.44f), b = new Color(0.6f, 0.47f, 0.3f), c = new Color(0.86f, 0.78f, 0.6f), pattern = 0, gloss = 0.2f },
        new CamoDef { id = "urban", name = "Kentsel Dijital", price = 250, rarity = "Nadir", a = new Color(0.45f, 0.47f, 0.5f), b = new Color(0.25f, 0.27f, 0.3f), c = new Color(0.68f, 0.7f, 0.72f), pattern = 2, gloss = 0.3f },
        new CamoDef { id = "tiger", name = "Gece Kaplanı", price = 400, rarity = "Nadir", a = new Color(0.9f, 0.45f, 0.08f), b = new Color(0.06f, 0.05f, 0.05f), c = new Color(0.95f, 0.6f, 0.15f), pattern = 1, gloss = 0.45f },
        new CamoDef { id = "gold", name = "Kraliyet Altını", price = 800, rarity = "Epik", a = new Color(1f, 0.78f, 0.3f), b = new Color(0.85f, 0.6f, 0.15f), c = new Color(1f, 0.88f, 0.5f), pattern = 4, gloss = 0.95f, glow = new Color(1f, 0.75f, 0.3f, 0.15f) },
        new CamoDef { id = "ice", name = "Buz Kristali", price = 900, rarity = "Epik", a = new Color(0.85f, 0.95f, 1f), b = new Color(0.45f, 0.75f, 0.95f), c = new Color(0.2f, 0.4f, 0.7f), pattern = 3, gloss = 0.85f, glow = new Color(0.4f, 0.85f, 1f, 0.9f) },
        new CamoDef { id = "dragon", name = "Kızıl Ejder", price = 1500, rarity = "Efsanevi", a = new Color(0.08f, 0.07f, 0.09f), b = new Color(0.2f, 0.05f, 0.06f), c = new Color(0.55f, 0.04f, 0.06f), pattern = 3, gloss = 0.7f, glow = new Color(1f, 0.15f, 0.08f, 1.6f) },
        new CamoDef { id = "nebula", name = "Mor Nebula", price = 2000, rarity = "Mitik", a = new Color(0.2f, 0.06f, 0.35f), b = new Color(0.65f, 0.15f, 0.7f), c = new Color(0.1f, 0.05f, 0.2f), pattern = 5, gloss = 0.8f, glow = new Color(0.9f, 0.35f, 1f, 1.4f) },
        // Sıradan
        new CamoDef { id = "snow", name = "Kar Örtüsü", price = 150, rarity = "Sıradan", a = new Color(0.9f, 0.92f, 0.94f), b = new Color(0.62f, 0.66f, 0.7f), c = new Color(0.78f, 0.8f, 0.83f), pattern = 0, gloss = 0.2f },
        new CamoDef { id = "jungle", name = "Cengel", price = 150, rarity = "Sıradan", a = new Color(0.12f, 0.24f, 0.12f), b = new Color(0.3f, 0.42f, 0.14f), c = new Color(0.07f, 0.1f, 0.06f), pattern = 0, gloss = 0.2f },
        new CamoDef { id = "slate", name = "Arduvaz", price = 150, rarity = "Sıradan", a = new Color(0.3f, 0.33f, 0.36f), b = new Color(0.2f, 0.22f, 0.25f), c = new Color(0.42f, 0.45f, 0.48f), pattern = 2, gloss = 0.25f },
        new CamoDef { id = "dune", name = "Kum Fırtınası", price = 150, rarity = "Sıradan", a = new Color(0.82f, 0.7f, 0.5f), b = new Color(0.7f, 0.56f, 0.36f), c = new Color(0.55f, 0.42f, 0.28f), pattern = 2, gloss = 0.2f },
        // Nadir
        new CamoDef { id = "navy", name = "Lacivert Dijital", price = 250, rarity = "Nadir", a = new Color(0.12f, 0.17f, 0.3f), b = new Color(0.22f, 0.3f, 0.48f), c = new Color(0.06f, 0.08f, 0.15f), pattern = 2, gloss = 0.35f },
        new CamoDef { id = "rust", name = "Pas", price = 250, rarity = "Nadir", a = new Color(0.45f, 0.22f, 0.1f), b = new Color(0.65f, 0.35f, 0.15f), c = new Color(0.25f, 0.13f, 0.07f), pattern = 0, gloss = 0.15f },
        new CamoDef { id = "zebra", name = "Zebra", price = 300, rarity = "Nadir", a = new Color(0.92f, 0.92f, 0.9f), b = new Color(0.06f, 0.06f, 0.06f), c = new Color(0.85f, 0.85f, 0.82f), pattern = 1, gloss = 0.4f },
        new CamoDef { id = "olive", name = "Zeytin Dijital", price = 250, rarity = "Nadir", a = new Color(0.36f, 0.38f, 0.2f), b = new Color(0.25f, 0.26f, 0.13f), c = new Color(0.5f, 0.5f, 0.3f), pattern = 2, gloss = 0.3f },
        new CamoDef { id = "crimson", name = "Kızıl Çizgi", price = 300, rarity = "Nadir", a = new Color(0.12f, 0.1f, 0.1f), b = new Color(0.75f, 0.1f, 0.12f), c = new Color(0.25f, 0.06f, 0.07f), pattern = 1, gloss = 0.5f },
        new CamoDef { id = "arctic", name = "Arktik Dijital", price = 300, rarity = "Nadir", a = new Color(0.85f, 0.9f, 0.95f), b = new Color(0.5f, 0.65f, 0.8f), c = new Color(0.3f, 0.4f, 0.55f), pattern = 2, gloss = 0.4f },
        // Epik
        new CamoDef { id = "carbon", name = "Karbon Fiber", price = 700, rarity = "Epik", a = new Color(0.12f, 0.12f, 0.13f), b = new Color(0.05f, 0.05f, 0.06f), c = new Color(0.3f, 0.3f, 0.33f), pattern = 4, gloss = 0.9f, glow = new Color(0.6f, 0.7f, 0.9f, 0.1f) },
        new CamoDef { id = "emerald", name = "Zümrüt", price = 850, rarity = "Epik", a = new Color(0.03f, 0.18f, 0.1f), b = new Color(0.05f, 0.35f, 0.2f), c = new Color(0.2f, 1f, 0.55f), pattern = 3, gloss = 0.8f, glow = new Color(0.2f, 1f, 0.5f, 0.9f) },
        new CamoDef { id = "toxic", name = "Zehir", price = 850, rarity = "Epik", a = new Color(0.1f, 0.12f, 0.05f), b = new Color(0.2f, 0.25f, 0.05f), c = new Color(0.65f, 1f, 0.1f), pattern = 3, gloss = 0.6f, glow = new Color(0.6f, 1f, 0.1f, 1f) },
        new CamoDef { id = "copper", name = "Bakır", price = 700, rarity = "Epik", a = new Color(0.85f, 0.5f, 0.3f), b = new Color(0.6f, 0.3f, 0.15f), c = new Color(1f, 0.7f, 0.5f), pattern = 4, gloss = 0.9f, glow = new Color(1f, 0.55f, 0.3f, 0.12f) },
        new CamoDef { id = "sunset", name = "Gün Batımı", price = 900, rarity = "Epik", a = new Color(0.9f, 0.35f, 0.2f), b = new Color(1f, 0.7f, 0.25f), c = new Color(0.35f, 0.1f, 0.35f), pattern = 5, gloss = 0.7f, glow = new Color(1f, 0.5f, 0.2f, 0.7f) },
        // Efsanevi
        new CamoDef { id = "lava", name = "Lav Akıntısı", price = 1500, rarity = "Efsanevi", a = new Color(0.08f, 0.05f, 0.04f), b = new Color(0.25f, 0.06f, 0.02f), c = new Color(1f, 0.45f, 0.05f), pattern = 3, gloss = 0.6f, glow = new Color(1f, 0.4f, 0.05f, 1.8f) },
        new CamoDef { id = "obsidian", name = "Obsidyen", price = 1500, rarity = "Efsanevi", a = new Color(0.06f, 0.04f, 0.1f), b = new Color(0.3f, 0.1f, 0.45f), c = new Color(0.02f, 0.02f, 0.04f), pattern = 5, gloss = 0.95f, glow = new Color(0.6f, 0.3f, 1f, 1.1f) },
        new CamoDef { id = "aurora", name = "Kutup Işığı", price = 1600, rarity = "Efsanevi", a = new Color(0.03f, 0.1f, 0.18f), b = new Color(0.15f, 0.9f, 0.6f), c = new Color(0.02f, 0.04f, 0.1f), pattern = 5, gloss = 0.8f, glow = new Color(0.2f, 1f, 0.7f, 1.4f) },
        // Mitik
        new CamoDef { id = "galaxy", name = "Galaksi", price = 2500, rarity = "Mitik", a = new Color(0.05f, 0.05f, 0.2f), b = new Color(0.3f, 0.5f, 1f), c = new Color(0.02f, 0.01f, 0.06f), pattern = 5, gloss = 0.9f, glow = new Color(0.4f, 0.6f, 1f, 1.6f) },
        new CamoDef { id = "phoenix", name = "Anka Kuşu", price = 2500, rarity = "Mitik", a = new Color(0.3f, 0.04f, 0.02f), b = new Color(0.7f, 0.15f, 0.03f), c = new Color(1f, 0.8f, 0.2f), pattern = 3, gloss = 0.85f, glow = new Color(1f, 0.65f, 0.15f, 2f) },
        new CamoDef { id = "pati", name = "Altın Pati", price = 3000, rarity = "Mitik", a = new Color(1f, 0.8f, 0.35f), b = new Color(0.75f, 0.5f, 0.1f), c = new Color(1f, 0.95f, 0.7f), pattern = 9, gloss = 0.95f, glow = new Color(1f, 0.8f, 0.3f, 0.6f) },
    };

    /// <summary>Icon (Resources/UI/Icons) for an attachment slot.</summary>
    public static string SlotIcon(AttachmentSlot slot)
    {
        switch (slot)
        {
            case AttachmentSlot.Optic: return "slot_optic";
            case AttachmentSlot.Underbarrel: return "slot_underbarrel";
            case AttachmentSlot.Magazine: return "slot_magazine";
            case AttachmentSlot.Stock: return "slot_stock";
            default: return "slot_muzzle";
        }
    }

    public static AttachmentDef FindAttachment(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (var a in Attachments)
            if (a.id == id)
                return a;
        return null;
    }

    public static CamoDef FindCamo(string id)
    {
        foreach (var c in Camos)
            if (c.id == (id ?? ""))
                return c;
        return Camos[0];
    }

    public static bool Fits(AttachmentDef a, WeaponType w)
    {
        if (a.only == null)
            return true;
        foreach (var t in a.only)
            if (t == w)
                return true;
        return false;
    }

    public static List<AttachmentDef> Options(WeaponType w, AttachmentSlot slot)
    {
        var list = new List<AttachmentDef>();
        foreach (var a in Attachments)
            if (a.slot == slot && Fits(a, w))
                list.Add(a);
        return list;
    }

    // ----- Ownership & loadouts (saved on the device) -----

    private static string Owned { get { return PlayerPrefs.GetString("zm_gs_owned", ""); } }

    public static bool OwnsAttachment(string id) { return ("," + Owned + ",").Contains("," + id + ","); }
    public static bool OwnsCamo(string id) { return string.IsNullOrEmpty(id) || ("," + PlayerPrefs.GetString("zm_gs_camos", "") + ",").Contains("," + id + ","); }

    public static bool Buy(ProfileData profile, string key, int price, bool camo)
    {
        if (camo ? OwnsCamo(key) : OwnsAttachment(key))
            return true;
        if (profile.coins < price)
            return false;
        profile.coins -= price;
        string pref = camo ? "zm_gs_camos" : "zm_gs_owned";
        string current = PlayerPrefs.GetString(pref, "");
        PlayerPrefs.SetString(pref, current.Length == 0 ? key : current + "," + key);
        profile.Save();
        return true;
    }

    /// <summary>Equipped loadout for a weapon type: 5 attachment ids + camo id.</summary>
    public static string[] Loadout(WeaponType w)
    {
        string raw = PlayerPrefs.GetString("zm_gs_" + w, ",,,,,");
        var parts = raw.Split(',');
        var result = new string[6];
        for (int i = 0; i < 6; i++)
            result[i] = i < parts.Length ? parts[i] : "";
        return result;
    }

    public static void SetLoadout(WeaponType w, string[] loadout)
    {
        PlayerPrefs.SetString("zm_gs_" + w, string.Join(",", loadout));
        PlayerPrefs.Save();
    }

    public static void Equip(WeaponType w, AttachmentSlot slot, string id)
    {
        var l = Loadout(w);
        l[(int)slot] = id ?? "";
        SetLoadout(w, l);
    }

    public static void EquipCamo(WeaponType w, string camo)
    {
        var l = Loadout(w);
        l[5] = camo ?? "";
        SetLoadout(w, l);
    }

    // ----- Applying a loadout -----

    /// <summary>Returns a copy of the weapon with the player's saved attachments and camo applied.</summary>
    public static WeaponData Apply(WeaponData baseData)
    {
        var l = Loadout(baseData.weaponType);
        var d = baseData.Clone();
        d.modelSkin = ModelLibrary.SelectedGunSkin(d.weaponType);
        for (int i = 0; i < 5; i++)
        {
            var a = FindAttachment(l[i]);
            if (a == null || !Fits(a, d.weaponType) || !OwnsAttachment(a.id))
                continue;
            d.attachments[i] = a.id;
            d.damage *= 1f + a.damage / 100f;
            d.fireRate /= Mathf.Max(0.2f, 1f + a.fireRate / 100f);
            d.spread *= Mathf.Max(0.2f, 1f - a.accuracy / 100f);
            d.range *= 1f + a.range / 100f;
            d.mobilityMul *= Mathf.Max(0.1f, 1f + a.mobility / 100f);
            d.recoilMul *= Mathf.Max(0.2f, 1f - a.control / 100f);
            d.magazineSize = Mathf.Max(1, Mathf.RoundToInt(d.magazineSize * (1f + a.magazine / 100f)));
            d.reloadTime /= Mathf.Max(0.3f, 1f + a.reload / 100f);
            d.zoomMul *= Mathf.Max(0.1f, a.zoom);
            d.suppressed |= a.suppressor;
        }
        if (OwnsCamo(l[5]))
            d.camo = l[5];
        return d;
    }

    // ----- Stats shown in the gunsmith (0..100) -----

    public static readonly string[] StatNames = { "HASAR", "ATIŞ HIZI", "İSABET", "MOBİLİTE", "MENZİL", "KONTROL" };

    private static float BaseMobility(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.Pistol: return 92f;
            case WeaponType.SMG: return 82f;
            case WeaponType.Shotgun: return 72f;
            case WeaponType.Sniper: return 48f;
            default: return 66f;
        }
    }

    public static float[] Stats(WeaponData d)
    {
        int pellets = d.weaponType == WeaponType.Shotgun ? 8 : 1;
        float dmg = Mathf.Clamp(d.damage * pellets / 90f * 100f, 5f, 100f);
        float rate = Mathf.Clamp(60f / Mathf.Max(0.01f, d.fireRate) / 850f * 100f, 5f, 100f);
        float acc = Mathf.Clamp(100f - d.spread * 11f, 5f, 100f);
        float mob = Mathf.Clamp(BaseMobility(d.weaponType) * d.mobilityMul, 5f, 100f);
        float rng = Mathf.Clamp(d.range / 150f * 100f, 5f, 100f);
        float ctl = Mathf.Clamp(100f - d.Recoil * 22f, 5f, 100f);
        return new[] { dmg, rate, acc, mob, rng, ctl };
    }

    public static WeaponData BaseWeapon(WeaponType t)
    {
        switch (t)
        {
            case WeaponType.SMG: return WeaponData.CreateSMG();
            case WeaponType.Shotgun: return WeaponData.CreateShotgun();
            case WeaponType.Sniper: return WeaponData.CreateSniper();
            case WeaponType.Pistol: return WeaponData.CreatePistol();
            default: return WeaponData.CreateRifle();
        }
    }
}
