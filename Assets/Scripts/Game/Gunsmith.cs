using System.Collections.Generic;
using UnityEngine;

public enum AttachmentSlot
{
    Muzzle = 0,      // Namlu ucu
    Optic = 1,       // Optik
    Underbarrel = 2, // Alt namlu
    Magazine = 3,    // Şarjör
    Stock = 4,       // Dipçik
    Barrel = 5,      // Namlu
    Laser = 6,       // Lazer
    RearGrip = 7,    // Arka tutamak
    Ammo = 8         // Mermi
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
    /// <summary>Indexed by AttachmentSlot.</summary>
    public static readonly string[] SlotNames = { "Namlu Ucu", "Optik", "Alt Namlu", "Şarjör", "Dipçik", "Namlu", "Lazer", "Arka Tutamak", "Mermi" };
    /// <summary>Order the slots are shown in the gunsmith.</summary>
    public static readonly AttachmentSlot[] SlotOrder =
    {
        AttachmentSlot.Muzzle, AttachmentSlot.Barrel, AttachmentSlot.Laser, AttachmentSlot.Optic, AttachmentSlot.Stock,
        AttachmentSlot.RearGrip, AttachmentSlot.Underbarrel, AttachmentSlot.Magazine, AttachmentSlot.Ammo
    };
    public const int SlotCount = 9;
    public const int MaxEquipped = 5;
    /// <summary>Index of the camo in a saved loadout (after the 9 attachment slots).</summary>
    public const int CamoIndex = SlotCount;
    public static readonly WeaponType[] Weapons = { WeaponType.Rifle, WeaponType.SMG, WeaponType.Shotgun, WeaponType.Sniper, WeaponType.Pistol };
    public static readonly string[] CategoryNames = { "TAARRUZ", "HAFİF MAKİNELİ", "POMPALI", "KESKİN NİŞANCI", "TABANCA" };

    private static readonly WeaponType[] LongGuns = { WeaponType.Rifle, WeaponType.SMG, WeaponType.Sniper };
    private static readonly WeaponType[] RifleSniper = { WeaponType.Rifle, WeaponType.Sniper };

    public static readonly List<AttachmentDef> Attachments = new List<AttachmentDef>
    {
        // Namlu ucu
        new AttachmentDef { id = "sup", name = "Susturucu", slot = AttachmentSlot.Muzzle, price = 250, suppressor = true, range = -10, control = 5, note = "Sessiz atış, namlu alevi yok" },
        new AttachmentDef { id = "comp", name = "Kompansatör", slot = AttachmentSlot.Muzzle, price = 200, control = 20, mobility = -3, accuracy = -3, note = "Dikey sekmeyi azaltır" },
        new AttachmentDef { id = "brake", name = "Namlu Freni", slot = AttachmentSlot.Muzzle, price = 300, control = 12, accuracy = 8, range = 5, mobility = -4 },
        new AttachmentDef { id = "flash", name = "Alev Gizleyici", slot = AttachmentSlot.Muzzle, price = 200, accuracy = 6, control = 6, note = "Namlu alevini küçültür" },
        new AttachmentDef { id = "hsup", name = "Ağır Susturucu", slot = AttachmentSlot.Muzzle, price = 450, suppressor = true, range = 8, control = 10, mobility = -8, note = "Sessiz, menzil kaybı yok" },
        // Namlu
        new AttachmentDef { id = "long", name = "Uzun Namlu", slot = AttachmentSlot.Barrel, price = 350, range = 25, damage = 6, mobility = -8, note = "Uzak mesafe hasarı" },
        new AttachmentDef { id = "short", name = "Kısa Namlu", slot = AttachmentSlot.Barrel, price = 250, mobility = 10, accuracy = 6, range = -12 },
        new AttachmentDef { id = "heavy", name = "Ağır Namlu", slot = AttachmentSlot.Barrel, price = 400, range = 15, control = 12, mobility = -12 },
        new AttachmentDef { id = "lightb", name = "Hafif Namlu", slot = AttachmentSlot.Barrel, price = 300, mobility = 12, fireRate = 4, control = -6 },
        new AttachmentDef { id = "sniperb", name = "Keskin Nişancı Namlusu", slot = AttachmentSlot.Barrel, price = 500, range = 35, damage = 10, mobility = -10, only = RifleSniper },
        // Lazer
        new AttachmentDef { id = "laser", name = "Taktik Lazer", slot = AttachmentSlot.Laser, price = 250, accuracy = 14, mobility = 4, note = "Kalçadan atış isabeti" },
        new AttachmentDef { id = "rlaser", name = "Kırmızı Lazer", slot = AttachmentSlot.Laser, price = 200, accuracy = 8, mobility = 6, note = "Nişana daha hızlı geçiş" },
        new AttachmentDef { id = "glaser", name = "Yeşil Lazer", slot = AttachmentSlot.Laser, price = 300, accuracy = 18, note = "En iyi kalçadan atış" },
        // Optik
        new AttachmentDef { id = "red", name = "Kırmızı Nokta", slot = AttachmentSlot.Optic, price = 150, accuracy = 6, zoom = 0.9f },
        new AttachmentDef { id = "holo", name = "Holografik", slot = AttachmentSlot.Optic, price = 200, accuracy = 8, zoom = 0.85f },
        new AttachmentDef { id = "x2", name = "2x Refleks", slot = AttachmentSlot.Optic, price = 250, accuracy = 10, zoom = 0.75f },
        new AttachmentDef { id = "x3", name = "3x Dürbün", slot = AttachmentSlot.Optic, price = 300, accuracy = 12, zoom = 0.6f, mobility = -5, only = LongGuns },
        new AttachmentDef { id = "x4", name = "4x Taktik", slot = AttachmentSlot.Optic, price = 380, accuracy = 14, zoom = 0.5f, mobility = -6, only = LongGuns },
        new AttachmentDef { id = "x6", name = "6x Dürbün", slot = AttachmentSlot.Optic, price = 450, accuracy = 18, zoom = 0.38f, mobility = -8, only = RifleSniper },
        new AttachmentDef { id = "x8", name = "8x Keskin", slot = AttachmentSlot.Optic, price = 600, accuracy = 22, zoom = 0.27f, mobility = -10, only = RifleSniper },
        // Dipçik
        new AttachmentDef { id = "light", name = "Hafif Dipçik", slot = AttachmentSlot.Stock, price = 200, mobility = 10, control = -6 },
        new AttachmentDef { id = "tac", name = "Taktik Dipçik", slot = AttachmentSlot.Stock, price = 250, control = 12, accuracy = 4, mobility = -4 },
        new AttachmentDef { id = "fold", name = "Katlanır Dipçik", slot = AttachmentSlot.Stock, price = 250, mobility = 14, accuracy = -4 },
        new AttachmentDef { id = "hstock", name = "Ağır Dipçik", slot = AttachmentSlot.Stock, price = 350, control = 18, accuracy = 6, mobility = -10 },
        new AttachmentDef { id = "nostock", name = "Dipçiksiz", slot = AttachmentSlot.Stock, price = 150, mobility = 18, control = -15, accuracy = -6, only = new[] { WeaponType.SMG, WeaponType.Shotgun, WeaponType.Rifle } },
        // Arka tutamak
        new AttachmentDef { id = "rubber", name = "Kauçuk Kaplama", slot = AttachmentSlot.RearGrip, price = 150, control = 8, accuracy = 3 },
        new AttachmentDef { id = "tape", name = "Bantlı Tutamak", slot = AttachmentSlot.RearGrip, price = 150, mobility = 6, accuracy = 4 },
        new AttachmentDef { id = "granular", name = "Granüllü Tutamak", slot = AttachmentSlot.RearGrip, price = 200, control = 6, mobility = 4 },
        // Alt namlu
        new AttachmentDef { id = "vgrip", name = "Dikey Tutamak", slot = AttachmentSlot.Underbarrel, price = 200, control = 15, mobility = -3 },
        new AttachmentDef { id = "agrip", name = "Açılı Tutamak", slot = AttachmentSlot.Underbarrel, price = 200, accuracy = 10, control = 6, mobility = -3 },
        new AttachmentDef { id = "hgrip", name = "Yarım Tutamak", slot = AttachmentSlot.Underbarrel, price = 220, control = 8, accuracy = 6 },
        new AttachmentDef { id = "tgrip", name = "Taktik Tutamak", slot = AttachmentSlot.Underbarrel, price = 280, control = 12, accuracy = 8, mobility = -5 },
        new AttachmentDef { id = "bipod", name = "İki Ayak", slot = AttachmentSlot.Underbarrel, price = 300, control = 22, accuracy = 10, mobility = -12, only = RifleSniper },
        // Şarjör
        new AttachmentDef { id = "ext", name = "Uzatılmış Şarjör", slot = AttachmentSlot.Magazine, price = 250, magazine = 50, reload = -12, mobility = -2 },
        new AttachmentDef { id = "fast", name = "Hızlı Şarjör", slot = AttachmentSlot.Magazine, price = 250, reload = 30 },
        new AttachmentDef { id = "fastext", name = "Hızlı Uzatılmış", slot = AttachmentSlot.Magazine, price = 450, magazine = 40, reload = 15, mobility = -3 },
        new AttachmentDef { id = "drum", name = "Davul Şarjör", slot = AttachmentSlot.Magazine, price = 400, magazine = 100, reload = -30, mobility = -8, only = new[] { WeaponType.Rifle, WeaponType.SMG, WeaponType.Shotgun } },
        // Mermi
        new AttachmentDef { id = "ap", name = "Zırh Delici", slot = AttachmentSlot.Ammo, price = 300, damage = 8, fireRate = -5, note = "Zırhlı hedeflere karşı" },
        new AttachmentDef { id = "hv", name = "Yüksek Hızlı", slot = AttachmentSlot.Ammo, price = 300, range = 20, accuracy = 5, damage = -3 },
        new AttachmentDef { id = "hammo", name = "Ağır Mermi", slot = AttachmentSlot.Ammo, price = 350, damage = 12, fireRate = -10, control = -8 },
        new AttachmentDef { id = "lammo", name = "Hafif Mermi", slot = AttachmentSlot.Ammo, price = 250, fireRate = 10, damage = -6, control = 5 },
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
            case AttachmentSlot.Barrel: return "slot_barrel";
            case AttachmentSlot.Laser: return "slot_laser";
            case AttachmentSlot.RearGrip: return "slot_reargrip";
            case AttachmentSlot.Ammo: return "slot_ammo";
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

    /// <summary>
    /// Equipped loadout for a weapon type: 9 attachment ids (by AttachmentSlot) + the camo id at CamoIndex.
    /// Saved as "zm_gs2_&lt;type&gt;"; the older 5-slot save ("zm_gs_&lt;type&gt;") is moved over the first time.
    /// </summary>
    public static string[] Loadout(WeaponType w)
    {
        var result = new string[SlotCount + 1];
        for (int i = 0; i < result.Length; i++)
            result[i] = "";
        string raw = PlayerPrefs.GetString("zm_gs2_" + w, null);
        if (raw == null)
        {
            // Old format: 5 attachments (each put in its slot now) + camo.
            var old = PlayerPrefs.GetString("zm_gs_" + w, ",,,,,").Split(',');
            for (int i = 0; i < 5 && i < old.Length; i++)
            {
                var a = FindAttachment(old[i]);
                if (a != null)
                    result[(int)a.slot] = a.id;
            }
            if (old.Length > 5)
                result[CamoIndex] = old[5];
            SetLoadout(w, result);
            return result;
        }
        var parts = raw.Split(',');
        for (int i = 0; i < result.Length && i < parts.Length; i++)
            result[i] = parts[i];
        return result;
    }

    public static void SetLoadout(WeaponType w, string[] loadout)
    {
        PlayerPrefs.SetString("zm_gs2_" + w, string.Join(",", loadout));
        PlayerPrefs.Save();
    }

    public static int EquippedCount(string[] loadout)
    {
        int n = 0;
        for (int i = 0; i < SlotCount; i++)
            if (FindAttachment(loadout[i]) != null)
                n++;
        return n;
    }

    /// <summary>Puts an attachment in its slot (null/"" empties it). False if that would exceed 5 attachments.</summary>
    public static bool Equip(WeaponType w, AttachmentSlot slot, string id)
    {
        var l = Loadout(w);
        bool adding = !string.IsNullOrEmpty(id) && FindAttachment(l[(int)slot]) == null;
        if (adding && EquippedCount(l) >= MaxEquipped)
            return false;
        l[(int)slot] = id ?? "";
        SetLoadout(w, l);
        return true;
    }

    public static void EquipCamo(WeaponType w, string camo)
    {
        var l = Loadout(w);
        l[CamoIndex] = camo ?? "";
        SetLoadout(w, l);
    }

    // ----- Applying a loadout -----

    /// <summary>Returns a copy of the weapon with the player's saved attachments and camo applied.</summary>
    public static WeaponData Apply(WeaponData baseData)
    {
        var l = Loadout(baseData.weaponType);
        var d = baseData.Clone();
        d.modelSkin = ModelLibrary.SelectedGunSkin(d.weaponType);
        int used = 0;
        for (int i = 0; i < SlotCount && used < MaxEquipped; i++)
        {
            var a = FindAttachment(l[i]);
            if (a == null || (int)a.slot != i || !Fits(a, d.weaponType) || !OwnsAttachment(a.id))
                continue;
            used++;
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
        if (OwnsCamo(l[CamoIndex]))
            d.camo = l[CamoIndex];
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
