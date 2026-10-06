using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ŞANS ÇEKİLİŞİ data: a weekly pool of ten prizes (a draw-only Mitik camo, an Efsanevi camo, other camos,
/// attachments and Kredi). Every draw gives one of the prizes still on the board, so ten draws win them all;
/// each draw costs more than the one before and the first one of the week is half price. Saved on the device
/// per pool and week ("zm_draw_&lt;pool&gt;_&lt;week&gt;" = bit mask of the prizes taken).
/// </summary>
public static class LuckyDraw
{
    public const int Count = 10;
    /// <summary>Tile indices: 0-3 top row, 4 big left (the Mitik prize), 5 big right, 6-9 bottom row.</summary>
    public const int GrandIndex = 4, SecondIndex = 5;
    public static readonly int[] Costs = { 160, 220, 300, 400, 520, 660, 820, 1000, 1200, 1450 };

    public class Pool
    {
        public string id, name;
        public WeaponType gun;      // the showcase weapon on the right (wearing the Mitik camo)
        public string gunSkin;
        public Reward[] prizes;     // Count entries, by tile index
    }

    private static Reward Camo(string id) { return new Reward { kind = RewardKind.WeaponCamo, id = id }; }
    private static Reward Att(string id) { return new Reward { kind = RewardKind.Attachment, id = id }; }
    private static Reward Kredi(int n) { return new Reward { kind = RewardKind.Credits, amount = n }; }

    public static readonly Pool[] Pools =
    {
        new Pool
        {
            id = "prism", name = "PRİZMA", gun = WeaponType.Rifle, gunSkin = "AK47",
            prizes = new[] { Att("hsup"), Camo("emerald"), Att("x6"), Kredi(300), Camo("prism"), Camo("aurora"), Att("fastext"), Camo("arctic"), Att("tgrip"), Kredi(600) }
        },
        new Pool
        {
            id = "neon", name = "NEON", gun = WeaponType.SMG, gunSkin = "U45",
            prizes = new[] { Att("drum"), Camo("toxic"), Att("glaser"), Kredi(300), Camo("neon"), Camo("lava"), Att("brake"), Camo("crimson"), Att("hstock"), Kredi(600) }
        },
        new Pool
        {
            id = "goldflame", name = "ALTIN ALEV", gun = WeaponType.Pistol, gunSkin = "Magnum",
            prizes = new[] { Att("sniperb"), Camo("copper"), Att("hammo"), Kredi(300), Camo("goldflame"), Camo("obsidian"), Att("ap"), Camo("zebra"), Att("agrip"), Kredi(600) }
        },
    };

    // ----- The week -----

    private static readonly System.DateTime Epoch = new System.DateTime(2026, 1, 5, 0, 0, 0, System.DateTimeKind.Utc);   // a Monday

    public static int Week { get { return Mathf.Max(0, (int)((System.DateTime.UtcNow - Epoch).TotalDays / 7.0)); } }

    public static Pool Current { get { return Pools[Week % Pools.Length]; } }

    /// <summary>Time left until the next pool, e.g. "3g 14sa".</summary>
    public static string TimeLeft
    {
        get
        {
            var end = Epoch.AddDays((Week + 1) * 7);
            var left = end - System.DateTime.UtcNow;
            if (left.TotalHours >= 24)
                return (int)left.TotalDays + "g " + left.Hours + "sa";
            return left.Hours + "sa " + left.Minutes + "dk";
        }
    }

    private static string Key { get { return "zm_draw_" + Current.id + "_" + Week; } }

    private static int Mask { get { return PlayerPrefs.GetInt(Key, 0); } }

    public static bool Taken(int index) { return (Mask & (1 << index)) != 0; }

    public static int DrawsDone
    {
        get
        {
            int m = Mask, n = 0;
            for (int i = 0; i < Count; i++)
                if ((m & (1 << i)) != 0)
                    n++;
            return n;
        }
    }

    public static bool Finished { get { return DrawsDone >= Count; } }

    /// <summary>Full price of the next draw (before the first-draw discount).</summary>
    public static int ListPrice { get { return Costs[Mathf.Clamp(DrawsDone, 0, Count - 1)]; } }

    /// <summary>What the next draw costs (the week's first draw is half price).</summary>
    public static int NextCost { get { return DrawsDone == 0 ? ListPrice / 2 : ListPrice; } }

    /// <summary>How likely a prize is picked, by its rarity (the Mitik one is rare until few prizes are left).</summary>
    private static float Weight(Reward r)
    {
        if (r.kind == RewardKind.Credits)
            return 30f;
        if (r.kind == RewardKind.Attachment)
            return 22f;
        switch (r.Rarity)
        {
            case "Nadir": return 18f;
            case "Epik": return 11f;
            case "Efsanevi": return 5f;
            case "Mitik": return 1.5f;
            default: return 25f;
        }
    }

    /// <summary>The chance of each prize still on the board in the next draw (the rest add up to 100%).</summary>
    public static List<string> OddsLines()
    {
        var lines = new List<string>();
        var pool = Current;
        float total = 0f;
        for (int i = 0; i < Count; i++)
            if (!Taken(i))
                total += Weight(pool.prizes[i]);
        lines.Add("Bir sonraki çekilişte tahtada kalan ödüllerin olasılıkları (çıkan ödül tahtadan kalkar, kalanların olasılığı artar):");
        for (int i = 0; i < Count; i++)
        {
            var r = pool.prizes[i];
            lines.Add("•  " + r.Name + (string.IsNullOrEmpty(r.Rarity) ? "" : " (" + r.Rarity + ")") + ": " +
                      (Taken(i) ? "alındı" : OddsPanel.Percent(Weight(r) / Mathf.Max(0.0001f, total))));
        }
        lines.Add("Her çekilişte bir ödül kesin çıkar; " + Count + " çekilişte tahtadaki her şey alınmış olur.");
        return lines;
    }

    /// <summary>
    /// Pays for and makes one draw. Returns the tile index won (and what was given), or -1 when the player
    /// can't afford it or everything is taken.
    /// </summary>
    public static int Draw(ProfileData profile, out GrantedReward given)
    {
        given = default(GrantedReward);
        if (Finished)
            return -1;
        int cost = NextCost;
        if (profile.coins < cost)
            return -1;
        var pool = Current;
        float total = 0f;
        for (int i = 0; i < Count; i++)
            if (!Taken(i))
                total += Weight(pool.prizes[i]);
        float pick = Random.value * total;
        int won = -1;
        for (int i = 0; i < Count; i++)
        {
            if (Taken(i))
                continue;
            won = i;
            pick -= Weight(pool.prizes[i]);
            if (pick <= 0f)
                break;
        }
        if (won < 0)
            return -1;
        profile.coins -= cost;
        PlayerPrefs.SetInt(Key, Mask | (1 << won));
        given = Shop.Give(profile, pool.prizes[won]);   // saves the profile
        PlayerPrefs.Save();
        return won;
    }
}
