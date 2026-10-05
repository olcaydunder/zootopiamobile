using System.Collections.Generic;
using UnityEngine;

public enum TokenType
{
    Revive = 0,    // Dirilme Jetonu
    Airdrop = 1,   // Hava İkmali Jetonu
    Boost = 2      // Güçlendirme Jetonu
}

public enum RewardKind
{
    Credits,
    WeaponCamo,
    VehicleCamo,
    ParachuteCamo,
    Attachment,
    Token,
    Skin,      // a character (id = skin)
    Crate,     // an unopened gift box (id = crate id)
    Gear       // inventory item cards (id = Gear id, amount = cards): armour, equipment, perks, masks
}

/// <summary>One level's reward.</summary>
public struct Reward
{
    public RewardKind kind;
    public string id;      // camo / attachment id
    public int amount;     // credits, or the TokenType for tokens

    public string Name
    {
        get
        {
            switch (kind)
            {
                case RewardKind.Credits: return amount.ToString("N0") + " Kredi";
                case RewardKind.Token: return Progression.TokenNames[amount];
                case RewardKind.Skin: return Shop.SkinName(id) + " (Karakter)";
                case RewardKind.Crate: return Shop.Crate(id).name;
                case RewardKind.Gear:
                {
                    var g = global::Gear.Find(id);
                    string n = g != null ? g.name : id;
                    return g != null && g.Cosmetic ? n + " (Maske)" : n + (amount > 1 ? " x" + amount : "") + " kart";
                }
                case RewardKind.Attachment:
                {
                    var a = Gunsmith.FindAttachment(id);
                    return a != null ? a.name : id;
                }
                default:
                {
                    var c = Cosmetics.FindAny(id);
                    string what = kind == RewardKind.WeaponCamo ? "Silah" : kind == RewardKind.VehicleCamo ? "Araç" : "Paraşüt";
                    return c.name + " (" + what + ")";
                }
            }
        }
    }

    public string Rarity
    {
        get
        {
            switch (kind)
            {
                case RewardKind.WeaponCamo:
                case RewardKind.VehicleCamo:
                case RewardKind.ParachuteCamo:
                    return Cosmetics.FindAny(id).rarity;
                case RewardKind.Token: return "Epik";
                case RewardKind.Attachment: return "Nadir";
                case RewardKind.Skin: return Shop.SkinRarity(id);
                case RewardKind.Crate: return Shop.Crate(id).rarity;
                case RewardKind.Gear: { var g = global::Gear.Find(id); return g != null ? g.rarity : "Sıradan"; }
                default: return "Sıradan";
            }
        }
    }

    /// <summary>Icon name in Resources/UI/Icons (camos are drawn as a pattern swatch instead).</summary>
    public string IconName
    {
        get
        {
            switch (kind)
            {
                case RewardKind.Credits: return "currency";
                case RewardKind.Token: return Progression.TokenIcons[amount];
                case RewardKind.Skin: return "skin";
                case RewardKind.Crate: return "crate_" + id;
                case RewardKind.Gear: return "gear_" + id;
                case RewardKind.Attachment:
                {
                    var a = Gunsmith.FindAttachment(id);
                    return a != null ? Gunsmith.SlotIcon(a.slot) : "slot_muzzle";
                }
                default: return null;
            }
        }
    }

    public bool IsCamo { get { return kind == RewardKind.WeaponCamo || kind == RewardKind.VehicleCamo || kind == RewardKind.ParachuteCamo; } }
}

/// <summary>A reward that was given to the player (duplicates went to the spares).</summary>
public struct GrantedReward
{
    public int level;
    public Reward reward;
    public bool duplicate;
}

/// <summary>
/// Levels 1–500, military ranks from Piyade Er to Mareşal, the per-level reward track, tokens and
/// spares (duplicate rewards that can be sold for credits). Everything is deterministic so the reward
/// track can be shown ahead of time.
/// </summary>
public static class Progression
{
    public const int MaxLevel = 500;

    /// <summary>XP needed to go from <paramref name="level"/> to the next one.</summary>
    public static int XpToNext(int level)
    {
        return level >= MaxLevel ? 0 : 200 + 10 * level;
    }

    // ----- Ranks -----

    public static readonly string[] RankNames =
    {
        "Piyade Er", "Onbaşı", "Çavuş", "Uzman Çavuş", "Astsubay Çavuş", "Kıdemli Çavuş", "Üstçavuş",
        "Kıdemli Üstçavuş", "Başçavuş", "Kıdemli Başçavuş", "Asteğmen", "Teğmen", "Üsteğmen", "Yüzbaşı",
        "Binbaşı", "Yarbay", "Albay", "Tuğgeneral", "Tümgeneral", "Korgeneral", "Orgeneral", "Mareşal"
    };

    /// <summary>First level of each rank.</summary>
    public static readonly int[] RankStart =
    {
        1, 10, 25, 45, 70, 95, 120, 150, 180, 210, 240, 265, 290, 315, 345, 375, 405, 435, 460, 480, 495, 500
    };

    public static int RankIndex(int level)
    {
        int r = 0;
        for (int i = 0; i < RankStart.Length; i++)
            if (level >= RankStart[i])
                r = i;
        return r;
    }

    public static string RankName(int level) { return RankNames[RankIndex(level)]; }

    public static bool IsRankUp(int level)
    {
        for (int i = 1; i < RankStart.Length; i++)
            if (RankStart[i] == level)
                return true;
        return false;
    }

    // ----- Tokens -----

    public static readonly string[] TokenNames = { "Dirilme Jetonu", "Hava İkmali Jetonu", "Güçlendirme Jetonu" };
    public static readonly string[] TokenIcons = { "token_revive", "token_airdrop", "token_boost" };
    public static readonly string[] TokenInfo =
    {
        "Öldükten sonra güvenli bölgede paraşütle yeniden doğarsın (maçta bir kez)",
        "Bulunduğun yere üst seviye ganimetli ikmal kasası düşer",
        "Sınıf yeteneğini anında Seviye 2 yapar ve doldurur"
    };
    public static readonly int[] TokenSellValue = { 150, 120, 80 };
    public const int TokenTypes = 3;

    public static int TokenCount(TokenType t) { return PlayerPrefs.GetInt("zm_tok_" + (int)t, 0); }

    public static void AddTokens(TokenType t, int n)
    {
        PlayerPrefs.SetInt("zm_tok_" + (int)t, Mathf.Clamp(TokenCount(t) + n, 0, 999));
    }

    /// <summary>Uses one token (in a match). Returns false if none left.</summary>
    public static bool UseToken(TokenType t)
    {
        int n = TokenCount(t);
        if (n <= 0)
            return false;
        PlayerPrefs.SetInt("zm_tok_" + (int)t, n - 1);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>Sells one token for credits.</summary>
    public static bool SellToken(ProfileData profile, TokenType t)
    {
        if (!UseToken(t))
            return false;
        profile.coins += TokenSellValue[(int)t];
        profile.Save();
        return true;
    }

    /// <summary>Whether the player takes this token type into matches (Teçhizat).</summary>
    public static bool CarryToken(TokenType t) { return PlayerPrefs.GetInt("zm_tokcarry_" + (int)t, 1) == 1; }
    public static void SetCarryToken(TokenType t, bool on) { PlayerPrefs.SetInt("zm_tokcarry_" + (int)t, on ? 1 : 0); PlayerPrefs.Save(); }

    // ----- Reward track -----

    private static List<CamoDef> Filter(List<CamoDef> list, string[] rarities)
    {
        var r = new List<CamoDef>();
        foreach (var c in list)
        {
            if (string.IsNullOrEmpty(c.id) || c.drawOnly)
                continue;
            foreach (var want in rarities)
                if (c.rarity == want)
                {
                    r.Add(c);
                    break;
                }
        }
        return r;
    }

    private static Reward CamoReward(int category, string[] rarities, int pick)
    {
        List<CamoDef> pool;
        RewardKind kind;
        switch (((category % 3) + 3) % 3)
        {
            case 1: pool = Filter(Cosmetics.ParachuteCamos, rarities); kind = RewardKind.ParachuteCamo; break;
            case 2: pool = Filter(Cosmetics.VehicleCamos, rarities); kind = RewardKind.VehicleCamo; break;
            default: pool = Filter(Gunsmith.Camos, rarities); kind = RewardKind.WeaponCamo; break;
        }
        if (pool.Count == 0)
            return new Reward { kind = RewardKind.Credits, amount = 300 };
        return new Reward { kind = kind, id = pool[pick % pool.Count].id };
    }

    /// <summary>The reward for reaching <paramref name="level"/> (2–500).</summary>
    public static Reward RewardFor(int level)
    {
        if (level >= MaxLevel)
            return new Reward { kind = RewardKind.WeaponCamo, id = "pati" };   // Mareşal: Altın Pati
        if (level % 50 == 0)
        {
            int k = level / 50;
            return CamoReward(k, new[] { "Efsanevi", "Mitik" }, k / 3);   // categories rotate, so k / 3 never repeats within one
        }
        if (level % 10 == 0)
        {
            int k = level / 10;
            return CamoReward(k, new[] { "Epik" }, k / 3);
        }
        if (level % 5 == 0)
            return new Reward { kind = RewardKind.Token, amount = (level / 5) % TokenTypes };
        if (IsRankUp(level))
            return new Reward { kind = RewardKind.Credits, amount = 500 + level * 2 };
        switch (level % 3)
        {
            case 0:
                return new Reward { kind = RewardKind.Credits, amount = 100 + (level / 10) * 10 };
            case 1:
            {
                int k = level / 3;
                return CamoReward(k, new[] { "Sıradan", "Nadir" }, k / 3 + level);
            }
            default:
            {
                int k = level / 3;
                var list = Gunsmith.Attachments;
                return new Reward { kind = RewardKind.Attachment, id = list[(k * 7) % list.Count].id };
            }
        }
    }

    /// <summary>Gives the reward for a level; an item the player already owns goes to the spares.</summary>
    public static GrantedReward Grant(ProfileData profile, int level)
    {
        var r = RewardFor(level);
        bool dup = false;
        switch (r.kind)
        {
            case RewardKind.Credits:
                profile.coins += r.amount;
                break;
            case RewardKind.Token:
                AddTokens((TokenType)r.amount, 1);
                break;
            case RewardKind.WeaponCamo:
                dup = Gunsmith.OwnsCamo(r.id);
                if (!dup) Cosmetics.AddWeaponCamo(r.id);
                break;
            case RewardKind.VehicleCamo:
                dup = Cosmetics.OwnsVehicleCamo(r.id);
                if (!dup) Cosmetics.AddVehicleCamo(r.id);
                break;
            case RewardKind.ParachuteCamo:
                dup = Cosmetics.OwnsParachuteCamo(r.id);
                if (!dup) Cosmetics.AddParachuteCamo(r.id);
                break;
            case RewardKind.Attachment:
                dup = Gunsmith.OwnsAttachment(r.id);
                if (!dup) Cosmetics.AddAttachment(r.id);
                break;
        }
        if (dup)
            AddSpare(r);
        return new GrantedReward { level = level, reward = r, duplicate = dup };
    }

    // ----- Spares (duplicates) -----

    private const string SparesKey = "zm_spares";

    private static string Code(RewardKind k)
    {
        switch (k)
        {
            case RewardKind.VehicleCamo: return "v";
            case RewardKind.ParachuteCamo: return "p";
            case RewardKind.Attachment: return "a";
            default: return "w";
        }
    }

    private static void AddSpare(Reward r)
    {
        string entry = Code(r.kind) + ":" + r.id;
        string cur = PlayerPrefs.GetString(SparesKey, "");
        PlayerPrefs.SetString(SparesKey, cur.Length == 0 ? entry : cur + "," + entry);
    }

    public static List<Reward> Spares()
    {
        var list = new List<Reward>();
        foreach (string e in PlayerPrefs.GetString(SparesKey, "").Split(','))
        {
            int c = e.IndexOf(':');
            if (c <= 0)
                continue;
            string code = e.Substring(0, c), id = e.Substring(c + 1);
            RewardKind k = code == "v" ? RewardKind.VehicleCamo : code == "p" ? RewardKind.ParachuteCamo : code == "a" ? RewardKind.Attachment : RewardKind.WeaponCamo;
            list.Add(new Reward { kind = k, id = id });
        }
        return list;
    }

    public static int SpareValue(Reward r)
    {
        if (r.kind == RewardKind.Attachment)
        {
            var a = Gunsmith.FindAttachment(r.id);
            return a != null ? Mathf.Max(50, a.price / 2) : 50;
        }
        return Cosmetics.SpareValue(r.Rarity);
    }

    public static int SparesTotalValue()
    {
        int sum = 0;
        foreach (var r in Spares())
            sum += SpareValue(r);
        return sum;
    }

    /// <summary>Sells every spare for credits. Returns the credits gained.</summary>
    public static int ConvertSpares(ProfileData profile)
    {
        int value = SparesTotalValue();
        PlayerPrefs.SetString(SparesKey, "");
        profile.coins += value;
        profile.Save();
        return value;
    }
}
