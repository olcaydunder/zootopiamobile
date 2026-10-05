using System.Collections.Generic;
using UnityEngine;

public enum GearSlot
{
    Head,       // Kask
    Body,       // Yelek
    Legs,       // Bot
    Explosive,  // Patlayıcı
    Tactical,   // Taktik
    Medical,    // Sağlık
    Perk,       // Güçlendirici (3 can be equipped)
    Mask        // Maske (cosmetic)
}

/// <summary>One inventory item: armour, a throwable, a medical item, a perk card or a mask.</summary>
public class GearDef
{
    public string id, name, rarity, blurb;
    public GearSlot slot;
    public float a, aPerLevel;       // main effect at level 1 and its growth per level (meaning depends on the item)
    public float b;                  // a second, fixed effect (e.g. a speed penalty), 0 if none
    public int price;                // store price in Kredi (0: only from boxes / free)
    public Color color = Color.white;   // masks: main colour; others: icon tint
    public Color color2 = Color.white;  // masks: second colour

    public float At(int level) { return a + aPerLevel * (Mathf.Max(1, level) - 1); }
    public bool Cosmetic { get { return slot == GearSlot.Mask; } }
    public int MaxLevel { get { return slot == GearSlot.Mask ? 1 : slot == GearSlot.Perk ? 5 : 10; } }
    public string Icon { get { return "gear_" + id; } }
}

/// <summary>
/// ENVANTER: what the player owns and has equipped besides guns and characters — helmet, vest and boots
/// (armour), an explosive, a tactical and a medical item, up to three perk cards, and a mask. Items come
/// from boxes as cards; cards and Kredi level an item up (armour and equipment to 10, perks to 5 stars).
/// Effects are read in the match (Gear.Mul..., Gear.Armor...). GÜÇ (power) sums up the equipped items.
/// Saved on the device (PlayerPrefs "zm_gear": id:level:cards;...).
/// </summary>
public static class Gear
{
    public static readonly string[] SlotNames = { "KASK", "YELEK", "BOT", "PATLAYICI", "TAKTİK", "SAĞLIK", "GÜÇLENDİRİCİ", "MASKE" };
    public const int PerkSlots = 3;

    public static readonly List<GearDef> All = new List<GearDef>
    {
        // ----- Helmets: less damage from headshots (%) -----
        new GearDef { id = "h_train", name = "Eğitim Kaskı", slot = GearSlot.Head, rarity = "Sıradan", a = 10, aPerLevel = 2, blurb = "Kafa vuruşu hasarı -%{0}" },
        new GearDef { id = "h_tactical", name = "Taktik Kask", slot = GearSlot.Head, rarity = "Nadir", a = 15, aPerLevel = 2.5f, price = 1500, blurb = "Kafa vuruşu hasarı -%{0}" },
        new GearDef { id = "h_night", name = "Gece Görüşlü Kask", slot = GearSlot.Head, rarity = "Epik", a = 18, aPerLevel = 2.5f, price = 3200, blurb = "Kafa vuruşu hasarı -%{0}, yakındaki düşmanlar haritada" },
        new GearDef { id = "h_heavy", name = "Ağır Muharebe Kaskı", slot = GearSlot.Head, rarity = "Efsanevi", a = 28, aPerLevel = 3, b = 3, blurb = "Kafa vuruşu hasarı -%{0}, hız -%3" },

        // ----- Vests: armour at the start (points) -----
        new GearDef { id = "b_light", name = "Hafif Yelek", slot = GearSlot.Body, rarity = "Sıradan", a = 15, aPerLevel = 5, blurb = "Başlangıçta +{0} zırh" },
        new GearDef { id = "b_plate", name = "Plaka Taşıyıcı", slot = GearSlot.Body, rarity = "Nadir", a = 25, aPerLevel = 6, price = 1500, blurb = "Başlangıçta +{0} zırh" },
        new GearDef { id = "b_commando", name = "Komando Yeleği", slot = GearSlot.Body, rarity = "Epik", a = 30, aPerLevel = 6, price = 3200, blurb = "Başlangıçta +{0} zırh, +1 patlayıcı" },
        new GearDef { id = "b_heavy", name = "Ağır Zırh", slot = GearSlot.Body, rarity = "Efsanevi", a = 45, aPerLevel = 7, b = 5, blurb = "Başlangıçta +{0} zırh, hız -%5" },

        // ----- Boots: faster (%) -----
        new GearDef { id = "l_field", name = "Saha Botu", slot = GearSlot.Legs, rarity = "Sıradan", a = 3, aPerLevel = 0.8f, blurb = "Hız +%{0}" },
        new GearDef { id = "l_runner", name = "Koşu Botu", slot = GearSlot.Legs, rarity = "Nadir", a = 5, aPerLevel = 1f, price = 1500, blurb = "Hız +%{0}" },
        new GearDef { id = "l_mountain", name = "Dağ Botu", slot = GearSlot.Legs, rarity = "Epik", a = 4, aPerLevel = 1f, price = 3200, blurb = "Hız +%{0}, adımların daha sessiz" },
        new GearDef { id = "l_knee", name = "Dizlikli Harekât Botu", slot = GearSlot.Legs, rarity = "Efsanevi", a = 6, aPerLevel = 1.2f, b = 25, blurb = "Hız +%{0}, eğilerek yürüme +%25" },

        // ----- Explosives: damage bonus (%) -----
        new GearDef { id = "x_frag", name = "El Bombası", slot = GearSlot.Explosive, rarity = "Sıradan", a = 0, aPerLevel = 4, blurb = "Klasik parçalı bomba, hasar +%{0}" },
        new GearDef { id = "x_molotov", name = "Molotof", slot = GearSlot.Explosive, rarity = "Nadir", a = 0, aPerLevel = 5, price = 1800, blurb = "Değdiği yeri 6 sn yakar, yanma +%{0}" },
        new GearDef { id = "x_charge", name = "Patlayıcı Paket", slot = GearSlot.Explosive, rarity = "Epik", a = 0, aPerLevel = 4, price = 3500, blurb = "Daha geniş ve güçlü patlama, hasar +%{0}" },

        // ----- Tactical: duration bonus (%) -----
        new GearDef { id = "t_smoke", name = "Sis Bombası", slot = GearSlot.Tactical, rarity = "Sıradan", a = 0, aPerLevel = 6, blurb = "12 sn sis: görüşü keser, süre +%{0}" },
        new GearDef { id = "t_flash", name = "Flaş Bombası", slot = GearSlot.Tactical, rarity = "Nadir", a = 0, aPerLevel = 6, price = 1800, blurb = "Bakanları 3 sn kör eder, süre +%{0}" },
        new GearDef { id = "t_gas", name = "Gaz Bombası", slot = GearSlot.Tactical, rarity = "Epik", a = 0, aPerLevel = 5, price = 3500, blurb = "8 sn zehirli bulut, zırhı deler, hasar +%{0}" },

        // ----- Medical: healing bonus (%) -----
        new GearDef { id = "m_kit", name = "İlk Yardım Çantası", slot = GearSlot.Medical, rarity = "Sıradan", a = 0, aPerLevel = 4, blurb = "+40 can, iyileşme +%{0}" },
        new GearDef { id = "m_adrenaline", name = "Adrenalin İğnesi", slot = GearSlot.Medical, rarity = "Nadir", a = 0, aPerLevel = 4, price = 1800, blurb = "Anında +25 can, 6 sn +%20 hız, iyileşme +%{0}" },
        new GearDef { id = "m_pack", name = "Sağlık Paketi", slot = GearSlot.Medical, rarity = "Epik", a = 0, aPerLevel = 4, price = 3500, blurb = "4 sn'de +70 can, iyileşme +%{0}" },

        // ----- Perks -----
        new GearDef { id = "p_reload", name = "Hızlı Eller", slot = GearSlot.Perk, rarity = "Sıradan", a = 12, aPerLevel = 3, blurb = "Şarjör değiştirme +%{0} hızlı" },
        new GearDef { id = "p_agile", name = "Çevik", slot = GearSlot.Perk, rarity = "Sıradan", a = 4, aPerLevel = 1, blurb = "Hız +%{0}" },
        new GearDef { id = "p_sharp", name = "Keskin Göz", slot = GearSlot.Perk, rarity = "Nadir", a = 12, aPerLevel = 3, price = 2000, blurb = "Kafa vuruşları +%{0} hasar" },
        new GearDef { id = "p_armor", name = "Zırh Takviyesi", slot = GearSlot.Perk, rarity = "Nadir", a = 12, aPerLevel = 4, price = 2000, blurb = "Başlangıçta +{0} zırh" },
        new GearDef { id = "p_regen", name = "Toparlanma", slot = GearSlot.Perk, rarity = "Nadir", a = 1.5f, aPerLevel = 0.4f, price = 2000, blurb = "5 sn hasar almazsan saniyede +{0} can" },
        new GearDef { id = "p_bomber", name = "Bombacı", slot = GearSlot.Perk, rarity = "Epik", a = 15, aPerLevel = 4, price = 3500, blurb = "Bombalar +%{0} güçlü, +1 patlayıcı" },
        new GearDef { id = "p_medic", name = "Saha Doktoru", slot = GearSlot.Perk, rarity = "Epik", a = 20, aPerLevel = 5, price = 3500, blurb = "İyileşme +%{0}" },
        new GearDef { id = "p_back", name = "Sırt Plakası", slot = GearSlot.Perk, rarity = "Epik", a = 25, aPerLevel = 5, price = 3500, blurb = "Arkadan gelen hasar -%{0}" },
        new GearDef { id = "p_lastbreath", name = "Son Nefes", slot = GearSlot.Perk, rarity = "Efsanevi", a = 15, aPerLevel = 3, blurb = "Canın azaldıkça hasarın artar (en çok +%{0})" },
        new GearDef { id = "p_hunter", name = "Avcı", slot = GearSlot.Perk, rarity = "Efsanevi", a = 15, aPerLevel = 5, blurb = "Her öldürmede +{0} can" },

        // ----- Masks (cosmetic): animal heads -----
        new GearDef { id = "k_cat", name = "Kedi", slot = GearSlot.Mask, rarity = "Sıradan", price = 600, color = new Color(0.95f, 0.6f, 0.25f), color2 = new Color(0.98f, 0.92f, 0.85f), blurb = "Maske" },
        new GearDef { id = "k_dog", name = "Köpek", slot = GearSlot.Mask, rarity = "Sıradan", price = 600, color = new Color(0.55f, 0.38f, 0.22f), color2 = new Color(0.92f, 0.85f, 0.72f), blurb = "Maske" },
        new GearDef { id = "k_rabbit", name = "Tavşan", slot = GearSlot.Mask, rarity = "Nadir", price = 1200, color = new Color(0.92f, 0.92f, 0.95f), color2 = new Color(1f, 0.7f, 0.78f), blurb = "Maske" },
        new GearDef { id = "k_bear", name = "Ayı", slot = GearSlot.Mask, rarity = "Nadir", price = 1200, color = new Color(0.36f, 0.24f, 0.16f), color2 = new Color(0.7f, 0.55f, 0.4f), blurb = "Maske" },
        new GearDef { id = "k_raccoon", name = "Rakun", slot = GearSlot.Mask, rarity = "Nadir", price = 1200, color = new Color(0.55f, 0.56f, 0.58f), color2 = new Color(0.12f, 0.12f, 0.14f), blurb = "Maske" },
        new GearDef { id = "k_owl", name = "Baykuş", slot = GearSlot.Mask, rarity = "Epik", price = 2400, color = new Color(0.55f, 0.42f, 0.28f), color2 = new Color(1f, 0.8f, 0.2f), blurb = "Maske" },
        new GearDef { id = "k_penguin", name = "Penguen", slot = GearSlot.Mask, rarity = "Epik", price = 2400, color = new Color(0.1f, 0.12f, 0.16f), color2 = new Color(0.97f, 0.97f, 0.97f), blurb = "Maske" },
        new GearDef { id = "k_wolf", name = "Kurt", slot = GearSlot.Mask, rarity = "Efsanevi", price = 4500, color = new Color(0.45f, 0.48f, 0.55f), color2 = new Color(0.85f, 0.87f, 0.9f), blurb = "Maske" },
        new GearDef { id = "k_lion", name = "Aslan", slot = GearSlot.Mask, rarity = "Efsanevi", price = 4500, color = new Color(0.9f, 0.68f, 0.3f), color2 = new Color(0.62f, 0.32f, 0.12f), blurb = "Maske" },
        new GearDef { id = "k_dragon", name = "Ejderha", slot = GearSlot.Mask, rarity = "Mitik", price = 9000, color = new Color(0.75f, 0.12f, 0.12f), color2 = new Color(1f, 0.78f, 0.2f), blurb = "Maske" },
    };

    /// <summary>What a new player starts with (one of each basic item, equipped).</summary>
    private static readonly string[] Starter = { "h_train", "b_light", "l_field", "x_frag", "t_smoke", "m_kit", "p_reload" };

    public static GearDef Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (var g in All)
            if (g.id == id)
                return g;
        return null;
    }

    public static List<GearDef> OfSlot(GearSlot slot)
    {
        var list = new List<GearDef>();
        foreach (var g in All)
            if (g.slot == slot)
                list.Add(g);
        return list;
    }

    /// <summary>Text of an item's effect at a level ("Kafa vuruşu hasarı -%14").</summary>
    public static string Describe(GearDef g, int level)
    {
        float v = g.At(level);
        string n = Mathf.Abs(v - Mathf.Round(v)) < 0.05f ? Mathf.RoundToInt(v).ToString() : v.ToString("0.0");
        return g.blurb.Replace("{0}", n);
    }

    // ----- What the player owns -----

    private class Owned { public int level, cards; }
    private static Dictionary<string, Owned> owned;
    private static readonly string[] equipped = new string[8];
    private static readonly string[] perks = new string[PerkSlots];

    private static void Ensure()
    {
        if (owned != null)
            return;
        owned = new Dictionary<string, Owned>();
        string data = PlayerPrefs.GetString("zm_gear", "");
        foreach (var part in data.Split(';'))
        {
            var f = part.Split(':');
            int lv, cd;
            if (f.Length == 3 && Find(f[0]) != null && int.TryParse(f[1], out lv) && int.TryParse(f[2], out cd))
                owned[f[0]] = new Owned { level = Mathf.Clamp(lv, 1, Find(f[0]).MaxLevel), cards = Mathf.Max(0, cd) };
        }
        for (int s = 0; s < equipped.Length; s++)
            equipped[s] = PlayerPrefs.GetString("zm_gear_eq_" + s, "");
        var p = PlayerPrefs.GetString("zm_gear_perks", "").Split(',');
        for (int i = 0; i < PerkSlots; i++)
            perks[i] = i < p.Length && owned.ContainsKey(p[i]) ? p[i] : "";
        // First start: the starter kit, equipped.
        if (owned.Count == 0)
        {
            foreach (var id in Starter)
            {
                owned[id] = new Owned { level = 1, cards = 0 };
                var g = Find(id);
                if (g.slot == GearSlot.Perk)
                    perks[0] = id;
                else
                    equipped[(int)g.slot] = id;
            }
            Save();
        }
        // Anything equipped must be owned.
        for (int s = 0; s < equipped.Length; s++)
            if (equipped[s].Length > 0 && !owned.ContainsKey(equipped[s]))
                equipped[s] = "";
    }

    private static void Save()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var kv in owned)
            sb.Append(kv.Key).Append(':').Append(kv.Value.level).Append(':').Append(kv.Value.cards).Append(';');
        PlayerPrefs.SetString("zm_gear", sb.ToString());
        for (int s = 0; s < equipped.Length; s++)
            PlayerPrefs.SetString("zm_gear_eq_" + s, equipped[s] ?? "");
        PlayerPrefs.SetString("zm_gear_perks", string.Join(",", perks));
        PlayerPrefs.Save();
    }

    public static bool Owns(string id) { Ensure(); return owned.ContainsKey(id); }
    public static int Level(string id) { Ensure(); Owned o; return owned.TryGetValue(id, out o) ? o.level : 0; }
    public static int Cards(string id) { Ensure(); Owned o; return owned.TryGetValue(id, out o) ? o.cards : 0; }
    public static int OwnedCount(GearSlot slot)
    {
        Ensure();
        int n = 0;
        foreach (var g in All)
            if (g.slot == slot && owned.ContainsKey(g.id))
                n++;
        return n;
    }

    /// <summary>Adds an item's cards; a new item is unlocked (the first card unlocks it). Masks: a second copy is a duplicate.</summary>
    public static bool AddCards(string id, int n, out bool duplicateMask)
    {
        Ensure();
        duplicateMask = false;
        var g = Find(id);
        if (g == null || n <= 0)
            return false;
        Owned o;
        if (!owned.TryGetValue(id, out o))
        {
            owned[id] = new Owned { level = 1, cards = Mathf.Max(0, n - 1) };
            if (g.slot != GearSlot.Perk && string.IsNullOrEmpty(equipped[(int)g.slot]))
                equipped[(int)g.slot] = id;   // fills an empty slot right away
        }
        else if (g.Cosmetic)
        {
            duplicateMask = true;
        }
        else
        {
            o.cards += n;
        }
        Save();
        return true;
    }

    /// <summary>Cards needed to go from this level to the next.</summary>
    public static int CardsToNext(GearDef g, int level)
    {
        if (level >= g.MaxLevel)
            return 0;
        return g.slot == GearSlot.Perk ? 2 + level * 2 : 1 + level;
    }

    /// <summary>Kredi needed to go from this level to the next (rarer items cost more).</summary>
    public static int CreditsToNext(GearDef g, int level)
    {
        if (level >= g.MaxLevel)
            return 0;
        float r = RarityFactor(g.rarity);
        return Mathf.RoundToInt(Mathf.Pow(level, 1.4f) * 120f * r / 10f) * 10;
    }

    public static bool CanUpgrade(GearDef g, ProfileData p)
    {
        int lv = Level(g.id);
        return lv > 0 && lv < g.MaxLevel && Cards(g.id) >= CardsToNext(g, lv) && p.coins >= CreditsToNext(g, lv);
    }

    public static bool Upgrade(GearDef g, ProfileData p)
    {
        Ensure();
        if (!CanUpgrade(g, p))
            return false;
        var o = owned[g.id];
        o.cards -= CardsToNext(g, o.level);
        p.coins -= CreditsToNext(g, o.level);
        o.level++;
        p.Save();
        Save();
        return true;
    }

    /// <summary>Buys an item from the store with Kredi (unlocks it; a mask the player has is not sold twice).</summary>
    public static bool Buy(GearDef g, ProfileData p)
    {
        if (g == null || g.price <= 0 || p.coins < g.price || (g.Cosmetic && Owns(g.id)))
            return false;
        p.coins -= g.price;
        p.Save();
        bool dup;
        AddCards(g.id, Owns(g.id) ? 3 : 1, out dup);   // owned already: worth three cards
        return true;
    }

    // ----- Equipping -----

    public static string Equipped(GearSlot slot) { Ensure(); return slot == GearSlot.Perk ? "" : equipped[(int)slot]; }
    public static GearDef EquippedDef(GearSlot slot) { return Find(Equipped(slot)); }
    public static string Perk(int i) { Ensure(); return perks[i]; }

    public static bool IsEquipped(string id)
    {
        Ensure();
        var g = Find(id);
        if (g == null)
            return false;
        if (g.slot == GearSlot.Perk)
            return System.Array.IndexOf(perks, id) >= 0;
        return equipped[(int)g.slot] == id;
    }

    /// <summary>Equips (or, for a mask or a perk already on, takes off) an owned item. Perks go into the first free slot.</summary>
    public static string Toggle(string id)
    {
        Ensure();
        var g = Find(id);
        if (g == null || !owned.ContainsKey(id))
            return "Bu eşya sende yok";
        if (g.slot == GearSlot.Perk)
        {
            int at = System.Array.IndexOf(perks, id);
            if (at >= 0)
            {
                perks[at] = "";
                Save();
                return g.name + " çıkarıldı";
            }
            for (int i = 0; i < PerkSlots; i++)
            {
                if (string.IsNullOrEmpty(perks[i]))
                {
                    perks[i] = id;
                    Save();
                    return g.name + " takıldı";
                }
            }
            return "3 güçlendirici takılı: önce birini çıkar";
        }
        if (g.Cosmetic && equipped[(int)g.slot] == id)
        {
            equipped[(int)g.slot] = "";
            Save();
            return "Maske çıkarıldı";
        }
        equipped[(int)g.slot] = id;
        Save();
        return g.name + " kuşanıldı";
    }

    // ----- Power -----

    public static float RarityFactor(string rarity)
    {
        switch (rarity)
        {
            case "Nadir": return 1.5f;
            case "Epik": return 2.2f;
            case "Efsanevi": return 3.2f;
            case "Mitik": return 4.5f;
            default: return 1f;
        }
    }

    public static int ItemPower(GearDef g, int level)
    {
        if (g == null || level <= 0 || g.Cosmetic)
            return 0;
        return Mathf.RoundToInt(40f * RarityFactor(g.rarity) * (1f + 0.35f * (level - 1)));
    }

    /// <summary>GÜÇ: the equipped items' power, plus the player's level and characters.</summary>
    public static int Power(ProfileData p)
    {
        Ensure();
        int total = 0;
        for (int s = 0; s < equipped.Length; s++)
            total += ItemPower(Find(equipped[s]), Level(equipped[s]));
        foreach (var id in perks)
            total += ItemPower(Find(id), Level(id));
        if (p != null)
            total += p.level * 6 + (p.ownedSkins.Split(',').Length - 1) * 25;
        return total;
    }

    // ----- Effects in a match (the local player) -----

    private static float Val(GearSlot slot)
    {
        var g = EquippedDef(slot);
        return g != null ? g.At(Level(g.id)) : 0f;
    }

    private static float PerkVal(string id)
    {
        Ensure();
        foreach (var p in perks)
            if (p == id)
                return Find(id).At(Level(id));
        return 0f;
    }

    public static bool HasPerk(string id) { return PerkVal(id) > 0f || (Find(id) != null && IsEquipped(id)); }

    /// <summary>Damage multiplier on a headshot taken (helmet).</summary>
    public static float HeadshotTakenMul { get { return 1f - Mathf.Clamp(Val(GearSlot.Head), 0f, 60f) / 100f; } }
    /// <summary>Damage multiplier on a hit from behind (Sırt Plakası).</summary>
    public static float BackTakenMul { get { return 1f - Mathf.Clamp(PerkVal("p_back"), 0f, 60f) / 100f; } }
    /// <summary>Armour at the start of a life.</summary>
    public static float StartArmor { get { return Val(GearSlot.Body) + PerkVal("p_armor"); } }

    /// <summary>Movement speed multiplier (boots, perk, heavy gear).</summary>
    public static float SpeedMul
    {
        get
        {
            float pct = Val(GearSlot.Legs) + PerkVal("p_agile");
            var h = EquippedDef(GearSlot.Head);
            var b = EquippedDef(GearSlot.Body);
            if (h != null) pct -= h.b;
            if (b != null) pct -= b.b;
            return 1f + Mathf.Clamp(pct, -15f, 30f) / 100f;
        }
    }

    public static float CrouchSpeedMul
    {
        get
        {
            var l = EquippedDef(GearSlot.Legs);
            return 1f + (l != null && l.id == "l_knee" ? l.b / 100f : 0f);
        }
    }

    /// <summary>Own footsteps volume (Dağ Botu: quieter).</summary>
    public static float StepVolumeMul { get { var l = EquippedDef(GearSlot.Legs); return l != null && l.id == "l_mountain" ? 0.55f : 1f; } }
    public static float ReloadTimeMul { get { return 1f / (1f + PerkVal("p_reload") / 100f); } }
    public static float HeadshotDealtMul { get { return 1f + PerkVal("p_sharp") / 100f; } }
    public static float RegenPerSecond { get { return PerkVal("p_regen"); } }
    public static float KillHeal { get { return PerkVal("p_hunter"); } }

    /// <summary>Damage dealt multiplier from Son Nefes at this health fraction (0..1).</summary>
    public static float LowHealthDamageMul(float health01)
    {
        float max = PerkVal("p_lastbreath");
        return max <= 0f ? 1f : 1f + max / 100f * Mathf.Clamp01(1f - health01) ;
    }

    public static float ExplosiveMul
    {
        get { return (1f + Val(GearSlot.Explosive) / 100f) * (1f + PerkVal("p_bomber") / 100f); }
    }

    public static float TacticalMul { get { return 1f + Val(GearSlot.Tactical) / 100f; } }
    public static float HealMul { get { return (1f + Val(GearSlot.Medical) / 100f) * (1f + PerkVal("p_medic") / 100f); } }
    public static bool NightVision { get { var h = EquippedDef(GearSlot.Head); return h != null && h.id == "h_night"; } }

    /// <summary>Explosives at the start of a life (1, more with the commando vest and the bomber perk).</summary>
    public static int StartExplosives
    {
        get
        {
            int n = 1;
            var b = EquippedDef(GearSlot.Body);
            if (b != null && b.id == "b_commando") n++;
            if (PerkVal("p_bomber") > 0f) n++;
            return n;
        }
    }

    public static string ExplosiveId { get { var g = EquippedDef(GearSlot.Explosive); return g != null ? g.id : "x_frag"; } }
    public static string TacticalId { get { var g = EquippedDef(GearSlot.Tactical); return g != null ? g.id : ""; } }
    public static string MedicalId { get { var g = EquippedDef(GearSlot.Medical); return g != null ? g.id : "m_kit"; } }
    public static string MaskId { get { return Equipped(GearSlot.Mask); } }

    /// <summary>Any mask (bots wear them now and then).</summary>
    public static string RandomMaskId()
    {
        var list = OfSlot(GearSlot.Mask);
        return list[Random.Range(0, list.Count)].id;
    }
}
