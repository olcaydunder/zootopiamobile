using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The in-game economy (Kredi only, no real money): character prices, five boxes (wood, bronze, silver,
/// gold, diamond) with what can come out of them (Kredi, item cards, camos, masks, characters), the boxes the player keeps unopened, giving any reward to the
/// profile, and turning a received gift into rewards.
/// </summary>
public static class Shop
{
    public class CrateDef
    {
        public string id, name, rarity, blurb;
        public int price, rolls;
        public Color color, accent;
        // chances (weights) of what each roll gives
        public int wCredits, wCamo, wAttachment, wToken, wSkin, wGear, wMask;
        public int creditsMin, creditsMax;
        public int cardsMin = 1, cardsMax = 2;
        public string[] camoRarities, gearRarities, maskRarities;
        public string guarantee;   // at least one reward of this rarity or better ("" none)
    }

    /// <summary>Five boxes from common to mythic (ids kept from the first three boxes so saved ones stay).</summary>
    public static readonly CrateDef[] Crates =
    {
        new CrateDef
        {
            id = "wood", name = "AHŞAP SANDIK", rarity = "Sıradan", price = 200, rolls = 2,
            blurb = "2 ödül: kredi, eşya kartları, kamuflaj",
            color = new Color(0.62f, 0.45f, 0.28f), accent = new Color(0.55f, 0.85f, 0.45f),
            wCredits = 40, wGear = 48, wCamo = 12, creditsMin = 50, creditsMax = 160, cardsMin = 1, cardsMax = 3,
            camoRarities = new[] { "Sıradan" }, gearRarities = new[] { "Sıradan", "Nadir" }, maskRarities = new[] { "Sıradan" }, guarantee = ""
        },
        new CrateDef
        {
            id = "bronze", name = "BRONZ KUTU", rarity = "Nadir", price = 400, rolls = 3,
            blurb = "3 ödül: eşya kartları, kamuflaj, eklenti, kredi",
            color = new Color(0.78f, 0.5f, 0.28f), accent = new Color(0.98f, 0.82f, 0.35f),
            wCredits = 28, wGear = 40, wCamo = 20, wAttachment = 8, wToken = 2, wMask = 2, creditsMin = 80, creditsMax = 260, cardsMin = 2, cardsMax = 4,
            camoRarities = new[] { "Sıradan", "Nadir" }, gearRarities = new[] { "Sıradan", "Nadir" }, maskRarities = new[] { "Sıradan", "Nadir" }, guarantee = ""
        },
        new CrateDef
        {
            id = "silver", name = "GÜMÜŞ KUTU", rarity = "Epik", price = 1000, rolls = 3,
            blurb = "3 ödül: nadir ve epik eşyalar, kamuflaj, şansla maske ya da karakter",
            color = new Color(0.75f, 0.8f, 0.88f), accent = new Color(0.3f, 0.62f, 1f),
            wCredits = 18, wGear = 40, wCamo = 25, wAttachment = 5, wToken = 4, wMask = 4, wSkin = 4, creditsMin = 150, creditsMax = 500, cardsMin = 2, cardsMax = 5,
            camoRarities = new[] { "Nadir", "Epik" }, gearRarities = new[] { "Nadir", "Epik" }, maskRarities = new[] { "Nadir", "Epik" }, guarantee = "Nadir"
        },
        new CrateDef
        {
            id = "gold", name = "ALTIN KUTU", rarity = "Efsanevi", price = 2500, rolls = 4,
            blurb = "4 ödül: epik ve efsanevi eşyalar, kamuflaj, maske, karakter",
            color = new Color(1f, 0.78f, 0.25f), accent = new Color(0.72f, 0.32f, 1f),
            wCredits = 12, wGear = 40, wCamo = 22, wAttachment = 3, wToken = 5, wMask = 8, wSkin = 10, creditsMin = 300, creditsMax = 900, cardsMin = 3, cardsMax = 6,
            camoRarities = new[] { "Epik", "Efsanevi", "Mitik" }, gearRarities = new[] { "Epik", "Efsanevi" }, maskRarities = new[] { "Epik", "Efsanevi" }, guarantee = "Epik"
        },
        new CrateDef
        {
            id = "diamond", name = "ELMAS KUTU", rarity = "Mitik", price = 6000, rolls = 5,
            blurb = "5 ödül: efsanevi eşyalar, mitik kamuflaj ve maske, karakterler",
            color = new Color(0.45f, 0.95f, 1f), accent = new Color(1f, 0.3f, 0.45f),
            wCredits = 8, wGear = 42, wCamo = 20, wToken = 5, wMask = 12, wSkin = 13, creditsMin = 600, creditsMax = 1600, cardsMin = 4, cardsMax = 8,
            camoRarities = new[] { "Efsanevi", "Mitik" }, gearRarities = new[] { "Epik", "Efsanevi" }, maskRarities = new[] { "Efsanevi", "Mitik" }, guarantee = "Efsanevi"
        },
    };

    public static CrateDef Crate(string id)
    {
        foreach (var c in Crates)
            if (c.id == id)
                return c;
        return Crates[0];
    }

    public static bool IsCrate(string id)
    {
        foreach (var c in Crates)
            if (c.id == id)
                return true;
        return false;
    }

    // ----- Characters -----

    public static int SkinIndex(string skin)
    {
        return System.Array.IndexOf(ModelLibrary.ShopSkins, skin);
    }

    public static string SkinName(string skin)
    {
        int i = SkinIndex(skin);
        return i >= 0 ? ModelLibrary.ShopNames[i] : skin;
    }

    public static int SkinPrice(string skin)
    {
        int i = SkinIndex(skin);
        return i >= 0 ? ModelLibrary.ShopPrices[i] : 0;
    }

    public static string SkinRarity(string skin)
    {
        int p = SkinPrice(skin);
        return p >= 3000 ? "Efsanevi" : p >= 2000 ? "Epik" : p >= 1200 ? "Nadir" : "Sıradan";
    }

    // ----- Boxes the player keeps -----

    public static int CrateCount(string id) { return PlayerPrefs.GetInt("zm_crate_" + id, 0); }

    public static void AddCrate(string id, int n)
    {
        PlayerPrefs.SetInt("zm_crate_" + id, Mathf.Max(0, CrateCount(id) + n));
        PlayerPrefs.Save();
    }

    public static int TotalCrates()
    {
        int n = 0;
        foreach (var c in Crates)
            n += CrateCount(c.id);
        return n;
    }

    /// <summary>Buys a box with Kredi (kept unopened). False when there is not enough Kredi.</summary>
    public static bool BuyCrate(ProfileData profile, string id)
    {
        var c = Crate(id);
        if (profile.coins < c.price)
            return false;
        profile.coins -= c.price;
        profile.Save();
        AddCrate(c.id, 1);
        return true;
    }

    /// <summary>Opens one kept box: rolls its rewards and gives them. Null when there is none.</summary>
    public static List<GrantedReward> OpenCrate(ProfileData profile, string id)
    {
        if (CrateCount(id) <= 0)
            return null;
        AddCrate(id, -1);
        var rng = new System.Random(System.Environment.TickCount ^ (id.GetHashCode() * 7919));
        var list = new List<GrantedReward>();
        foreach (var r in Roll(Crate(id), rng))
            list.Add(Give(profile, r));
        profile.Save();
        PlayerPrefs.Save();
        return list;
    }

    /// <summary>What comes out of a box. Better boxes guarantee at least one reward of their rarity.</summary>
    public static List<Reward> Roll(CrateDef c, System.Random rng)
    {
        var list = new List<Reward>();
        for (int i = 0; i < c.rolls; i++)
        {
            int total = c.wCredits + c.wCamo + c.wAttachment + c.wToken + c.wSkin + c.wGear + c.wMask;
            int pick = rng.Next(total);
            Reward r;
            if ((pick -= c.wCredits) < 0)
                r = new Reward { kind = RewardKind.Credits, amount = (rng.Next(c.creditsMin, c.creditsMax + 1) / 10) * 10 };
            else if ((pick -= c.wGear) < 0)
                r = RandomGear(c.gearRarities, c, rng);
            else if ((pick -= c.wCamo) < 0)
                r = RandomCamo(c.camoRarities, rng);
            else if ((pick -= c.wAttachment) < 0)
                r = new Reward { kind = RewardKind.Attachment, id = Gunsmith.Attachments[rng.Next(Gunsmith.Attachments.Count)].id };
            else if ((pick -= c.wToken) < 0)
                r = new Reward { kind = RewardKind.Token, amount = rng.Next(Progression.TokenTypes) };
            else if ((pick -= c.wMask) < 0)
                r = RandomMask(c.maskRarities, rng);
            else
                r = RandomSkin(rng);
            list.Add(r);
        }
        if (!string.IsNullOrEmpty(c.guarantee))
        {
            int need = RarityRank(c.guarantee);
            bool good = false;
            foreach (var r in list)
                good |= RarityRank(r.Rarity) >= need;
            if (!good)
                list[list.Count - 1] = RandomGear(new[] { c.guarantee }, c, rng);
        }
        return list;
    }

    public static int RarityRank(string rarity)
    {
        switch (rarity)
        {
            case "Nadir": return 1;
            case "Epik": return 2;
            case "Efsanevi": return 3;
            case "Mitik": return 4;
            default: return 0;
        }
    }

    private static Reward RandomGear(string[] rarities, CrateDef c, System.Random rng)
    {
        var pool = new List<GearDef>();
        foreach (var g in Gear.All)
            if (!g.Cosmetic && System.Array.IndexOf(rarities, g.rarity) >= 0)
                pool.Add(g);
        if (pool.Count == 0)
            return new Reward { kind = RewardKind.Credits, amount = 200 };
        var pickG = pool[rng.Next(pool.Count)];
        int cards = rng.Next(c.cardsMin, c.cardsMax + 1);
        if (pickG.slot == GearSlot.Perk)
            cards = Mathf.Max(1, cards / 2);
        return new Reward { kind = RewardKind.Gear, id = pickG.id, amount = cards };
    }

    private static Reward RandomMask(string[] rarities, System.Random rng)
    {
        var pool = new List<GearDef>();
        foreach (var g in Gear.All)
            if (g.Cosmetic && System.Array.IndexOf(rarities, g.rarity) >= 0)
                pool.Add(g);
        if (pool.Count == 0)
            return new Reward { kind = RewardKind.Credits, amount = 300 };
        return new Reward { kind = RewardKind.Gear, id = pool[rng.Next(pool.Count)].id, amount = 1 };
    }

    private static Reward RandomCamo(string[] rarities, System.Random rng)
    {
        var pool = new List<Reward>();
        foreach (var cd in Gunsmith.Camos)
            if (!string.IsNullOrEmpty(cd.id) && System.Array.IndexOf(rarities, cd.rarity) >= 0)
                pool.Add(new Reward { kind = RewardKind.WeaponCamo, id = cd.id });
        foreach (var cd in Cosmetics.VehicleCamos)
            if (!string.IsNullOrEmpty(cd.id) && System.Array.IndexOf(rarities, cd.rarity) >= 0)
                pool.Add(new Reward { kind = RewardKind.VehicleCamo, id = cd.id });
        foreach (var cd in Cosmetics.ParachuteCamos)
            if (!string.IsNullOrEmpty(cd.id) && System.Array.IndexOf(rarities, cd.rarity) >= 0)
                pool.Add(new Reward { kind = RewardKind.ParachuteCamo, id = cd.id });
        if (pool.Count == 0)
            return new Reward { kind = RewardKind.Credits, amount = 200 };
        return pool[rng.Next(pool.Count)];
    }

    private static Reward RandomSkin(System.Random rng)
    {
        var pool = new List<string>();
        for (int i = 0; i < ModelLibrary.ShopSkins.Length; i++)
            if (ModelLibrary.ShopPrices[i] > 0)
                pool.Add(ModelLibrary.ShopSkins[i]);
        if (pool.Count == 0)
            return new Reward { kind = RewardKind.Credits, amount = 500 };
        return new Reward { kind = RewardKind.Skin, id = pool[rng.Next(pool.Count)] };
    }

    /// <summary>
    /// Gives any reward. Something already owned becomes Kredi (characters: half their price) or goes to the
    /// spares (camos, attachments) like the level rewards; duplicate = true then.
    /// </summary>
    public static GrantedReward Give(ProfileData profile, Reward r)
    {
        bool dup = false;
        switch (r.kind)
        {
            case RewardKind.Credits:
                profile.coins += r.amount;
                break;
            case RewardKind.Token:
                Progression.AddTokens((TokenType)r.amount, 1);
                break;
            case RewardKind.Skin:
                if (profile.OwnsSkin(r.id) || SkinIndex(r.id) < 0)
                {
                    dup = true;
                    profile.coins += Mathf.Max(200, SkinPrice(r.id) / 2);
                }
                else
                    profile.ownedSkins += "," + r.id;
                break;
            case RewardKind.Crate:
                AddCrate(r.id, Mathf.Max(1, r.amount));
                break;
            case RewardKind.WeaponCamo:
                dup = Gunsmith.OwnsCamo(r.id);
                if (dup) profile.coins += Cosmetics.SpareValue(r.Rarity); else Cosmetics.AddWeaponCamo(r.id);
                break;
            case RewardKind.VehicleCamo:
                dup = Cosmetics.OwnsVehicleCamo(r.id);
                if (dup) profile.coins += Cosmetics.SpareValue(r.Rarity); else Cosmetics.AddVehicleCamo(r.id);
                break;
            case RewardKind.ParachuteCamo:
                dup = Cosmetics.OwnsParachuteCamo(r.id);
                if (dup) profile.coins += Cosmetics.SpareValue(r.Rarity); else Cosmetics.AddParachuteCamo(r.id);
                break;
            case RewardKind.Attachment:
                dup = Gunsmith.OwnsAttachment(r.id);
                if (dup) profile.coins += 60; else Cosmetics.AddAttachment(r.id);
                break;
            case RewardKind.Gear:
            {
                bool dupMask;
                Gear.AddCards(r.id, Mathf.Max(1, r.amount), out dupMask);
                if (dupMask)
                {
                    dup = true;
                    profile.coins += DuplicateValue(r);
                }
                break;
            }
        }
        profile.Save();
        return new GrantedReward { reward = r, duplicate = dup };
    }

    /// <summary>What a duplicate turned into, for the reward card ("+300 Kredi").</summary>
    public static int DuplicateValue(Reward r)
    {
        switch (r.kind)
        {
            case RewardKind.Skin: return Mathf.Max(200, SkinPrice(r.id) / 2);
            case RewardKind.Attachment: return 60;
            case RewardKind.Gear: { var g = Gear.Find(r.id); return g != null ? Mathf.Max(150, g.price / 3) : 150; }
            default: return Cosmetics.SpareValue(r.Rarity);
        }
    }

    // ----- Gifts -----

    /// <summary>Camo gift items: "w:id" (weapon), "v:id" (vehicle), "p:id" (parachute).</summary>
    public static Reward CamoFromItem(string item)
    {
        string id = item.Length > 2 ? item.Substring(2) : item;
        RewardKind k = item.StartsWith("v:") ? RewardKind.VehicleCamo : item.StartsWith("p:") ? RewardKind.ParachuteCamo : RewardKind.WeaponCamo;
        return new Reward { kind = k, id = id };
    }

    public static string ItemFromCamo(Reward r)
    {
        return (r.kind == RewardKind.VehicleCamo ? "v:" : r.kind == RewardKind.ParachuteCamo ? "p:" : "w:") + r.id;
    }

    public static int CamoPrice(Reward r)
    {
        CamoDef c = r.kind == RewardKind.VehicleCamo ? Cosmetics.FindVehicleCamo(r.id)
            : r.kind == RewardKind.ParachuteCamo ? Cosmetics.FindParachuteCamo(r.id) : Gunsmith.FindCamo(r.id);
        return c != null && c.price > 0 ? c.price : Cosmetics.SpareValue(r.Rarity) * 3;
    }

    /// <summary>The reward a gift stands for (a box stays a box: it is opened on screen).</summary>
    public static Reward FromGift(OnlineService.Gift g)
    {
        switch (g.kind)
        {
            case "credits": return new Reward { kind = RewardKind.Credits, amount = Mathf.Clamp(g.amount, 0, 1000) };
            case "box": return new Reward { kind = RewardKind.Crate, id = IsCrate(g.item) ? g.item : "bronze", amount = 1 };
            case "skin": return new Reward { kind = RewardKind.Skin, id = g.item };
            case "gear": return Gear.Find(g.item) != null ? new Reward { kind = RewardKind.Gear, id = g.item, amount = 1 } : new Reward { kind = RewardKind.Credits, amount = 300 };
            default: return CamoFromItem(g.item);
        }
    }

    /// <summary>What sending this gift costs the sender in Kredi.</summary>
    public static int GiftCost(string kind, string item, int amount)
    {
        switch (kind)
        {
            case "credits": return amount;
            case "box": return Crate(item).price;
            case "skin": return Mathf.Max(300, SkinPrice(item));
            case "gear": { var g = Gear.Find(item); return g != null && g.price > 0 ? g.price : 1000; }
            default: return CamoPrice(CamoFromItem(item));
        }
    }
}
