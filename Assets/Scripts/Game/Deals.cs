using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Daily things of the store and the lobby (Kredi only, saved on the device):
/// - the free daily gift (FIRSATLAR),
/// - six daily deals, the same for everyone that day (picked by the date), each bought once a day,
/// - weekly offers (bundles), each bought once a week,
/// - the lucky wheel in the lobby: one free spin a day, more for Kredi.
/// </summary>
public static class Deals
{
    public class Deal
    {
        public int index;
        public string title;
        public Reward reward;
        public int price, oldPrice;
        public bool bought;
        public int Discount { get { return oldPrice > 0 ? Mathf.RoundToInt(100f * (oldPrice - price) / oldPrice) : 0; } }
    }

    public class Offer
    {
        public string id, title, blurb;
        public int price, worth;
        public Color color;
        public Reward[] rewards;
    }

    private static int Day { get { return Missions.Day; } }

    // ----- Free daily gift -----

    public static bool FreeReady { get { return PlayerPrefs.GetInt("zm_free_day", -1) != Day; } }

    /// <summary>Today's free gift: some Kredi, a wooden box or a few item cards.</summary>
    public static List<GrantedReward> ClaimFree(ProfileData p)
    {
        var list = new List<GrantedReward>();
        if (!FreeReady)
            return list;
        PlayerPrefs.SetInt("zm_free_day", Day);
        var rng = new System.Random(System.Environment.TickCount);
        int roll = rng.Next(100);
        Reward r;
        if (roll < 45)
            r = new Reward { kind = RewardKind.Credits, amount = 100 + rng.Next(4) * 50 };
        else if (roll < 75)
            r = RandomCards(rng, new[] { "Sıradan", "Nadir" }, 2, 3);
        else
            r = new Reward { kind = RewardKind.Crate, id = roll < 95 ? "wood" : "bronze", amount = 1 };
        list.Add(Shop.Give(p, r));
        PlayerPrefs.Save();
        return list;
    }

    private static Reward RandomCards(System.Random rng, string[] rarities, int min, int max)
    {
        var pool = new List<GearDef>();
        foreach (var g in Gear.All)
            if (!g.Cosmetic && System.Array.IndexOf(rarities, g.rarity) >= 0)
                pool.Add(g);
        var pick = pool[rng.Next(pool.Count)];
        return new Reward { kind = RewardKind.Gear, id = pick.id, amount = rng.Next(min, max + 1) };
    }

    // ----- Daily deals -----

    private static string DealKey(int i) { return "zm_deal_" + Day + "_" + i; }

    /// <summary>Today's six deals (the same list all day; a new one at midnight).</summary>
    public static List<Deal> Today()
    {
        var rng = new System.Random(Day * 104729 + 31);
        var list = new List<Deal>();
        // 0-2: item cards of three different items
        var used = new HashSet<string>();
        for (int i = 0; i < 3; i++)
        {
            Reward r;
            int guard = 0;
            do
            {
                r = RandomCards(rng, i == 2 ? new[] { "Epik", "Efsanevi" } : new[] { "Sıradan", "Nadir", "Epik" }, 4, 8);
            } while (used.Contains(r.id) && guard++ < 20);
            used.Add(r.id);
            var g = Gear.Find(r.id);
            int worth = Mathf.RoundToInt(r.amount * 70f * Gear.RarityFactor(g.rarity) / 10f) * 10;
            list.Add(Make(i, g.name + " x" + r.amount, r, worth, 0.6f + 0.05f * rng.Next(5)));
        }
        // 3: a mask
        var masks = Gear.OfSlot(GearSlot.Mask);
        var m = masks[rng.Next(masks.Count)];
        list.Add(Make(3, m.name + " Maskesi", new Reward { kind = RewardKind.Gear, id = m.id, amount = 1 }, m.price, 0.7f));
        // 4: a box
        var crate = Shop.Crates[1 + rng.Next(3)];   // bronze, silver or gold
        list.Add(Make(4, crate.name, new Reward { kind = RewardKind.Crate, id = crate.id, amount = 1 }, crate.price, 0.75f));
        // 5: a weapon camo
        var camos = new List<CamoDef>();
        foreach (var c in Gunsmith.Camos)
            if (!string.IsNullOrEmpty(c.id) && c.price > 0)
                camos.Add(c);
        if (camos.Count > 0)
        {
            var c = camos[rng.Next(camos.Count)];
            list.Add(Make(5, c.name + " Kamuflajı", new Reward { kind = RewardKind.WeaponCamo, id = c.id }, c.price, 0.7f));
        }
        return list;
    }

    private static Deal Make(int index, string title, Reward r, int worth, float factor)
    {
        int price = Mathf.Max(50, Mathf.RoundToInt(worth * factor / 10f) * 10);
        return new Deal { index = index, title = title, reward = r, oldPrice = worth, price = price, bought = PlayerPrefs.GetInt(DealKey(index), 0) == 1 };
    }

    public static bool BuyDeal(ProfileData p, Deal d, out GrantedReward given)
    {
        given = default(GrantedReward);
        if (d.bought || p.coins < d.price || (d.reward.kind == RewardKind.Gear && Gear.Find(d.reward.id).Cosmetic && Gear.Owns(d.reward.id)))
            return false;
        p.coins -= d.price;
        PlayerPrefs.SetInt(DealKey(d.index), 1);
        given = Shop.Give(p, d.reward);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>"5 sa 12 dk" until the deals change.</summary>
    public static string TimeLeft { get { return Missions.TimeLeft(false); } }

    // ----- Weekly offers -----

    private static Reward GearR(string id, int n) { return new Reward { kind = RewardKind.Gear, id = id, amount = n }; }
    private static Reward BoxR(string id, int n) { return new Reward { kind = RewardKind.Crate, id = id, amount = n }; }

    public static readonly Offer[] Offers =
    {
        new Offer { id = "o_soldier", title = "ASKER PAKETİ", blurb = "Taktik Kask, Plaka Taşıyıcı ve Koşu Botu", price = 2400, worth = 4500,
            color = new Color(0.45f, 0.6f, 0.3f), rewards = new[] { GearR("h_tactical", 1), GearR("b_plate", 1), GearR("l_runner", 1) } },
        new Offer { id = "o_bomber", title = "BOMBACI PAKETİ", blurb = "Molotof, Patlayıcı Paket ve 5 Bombacı kartı", price = 3600, worth = 6000,
            color = new Color(0.85f, 0.4f, 0.15f), rewards = new[] { GearR("x_molotov", 1), GearR("x_charge", 1), GearR("p_bomber", 5) } },
        new Offer { id = "o_medic", title = "DOKTOR PAKETİ", blurb = "Adrenalin, Sağlık Paketi ve 5 Saha Doktoru kartı", price = 3600, worth = 6000,
            color = new Color(0.25f, 0.6f, 0.9f), rewards = new[] { GearR("m_adrenaline", 1), GearR("m_pack", 1), GearR("p_medic", 5) } },
        new Offer { id = "o_gold", title = "ALTIN FIRSAT", blurb = "2 Altın Sandık ve 1 Gümüş Sandık", price = 4200, worth = 6000,
            color = new Color(1f, 0.75f, 0.2f), rewards = new[] { BoxR("gold", 2), BoxR("silver", 1) } },
        new Offer { id = "o_mythic", title = "MİTİK TEKLİF", blurb = "Elmas Kutu ve 10 Efsanevi eşya kartı", price = 7500, worth = 11000,
            color = new Color(0.75f, 0.35f, 1f), rewards = new[] { BoxR("diamond", 1), GearR("p_lastbreath", 5), GearR("p_hunter", 5) } },
    };

    private static string OfferKey(Offer o) { return "zm_offer_" + Missions.Week + "_" + o.id; }

    public static bool OfferBought(Offer o) { return PlayerPrefs.GetInt(OfferKey(o), 0) == 1; }

    public static bool BuyOffer(ProfileData p, Offer o, List<GrantedReward> given)
    {
        if (OfferBought(o) || p.coins < o.price)
            return false;
        p.coins -= o.price;
        PlayerPrefs.SetInt(OfferKey(o), 1);
        foreach (var r in o.rewards)
            given.Add(Shop.Give(p, r));
        PlayerPrefs.Save();
        return true;
    }

    // ----- Lucky wheel -----

    public class Slice
    {
        public Reward reward;
        public int weight;
        public Color color;
    }

    public static readonly Slice[] Wheel =
    {
        new Slice { reward = new Reward { kind = RewardKind.Credits, amount = 100 }, weight = 22, color = new Color(0.25f, 0.55f, 0.95f) },
        new Slice { reward = new Reward { kind = RewardKind.Crate, id = "wood", amount = 1 }, weight = 18, color = new Color(0.65f, 0.45f, 0.25f) },
        new Slice { reward = new Reward { kind = RewardKind.Credits, amount = 250 }, weight = 16, color = new Color(0.3f, 0.75f, 0.45f) },
        new Slice { reward = new Reward { kind = RewardKind.Gear, id = "", amount = 3 }, weight = 16, color = new Color(0.55f, 0.6f, 0.68f) },   // random cards
        new Slice { reward = new Reward { kind = RewardKind.Crate, id = "bronze", amount = 1 }, weight = 12, color = new Color(0.8f, 0.5f, 0.25f) },
        new Slice { reward = new Reward { kind = RewardKind.Credits, amount = 600 }, weight = 8, color = new Color(0.95f, 0.75f, 0.2f) },
        new Slice { reward = new Reward { kind = RewardKind.Crate, id = "silver", amount = 1 }, weight = 5, color = new Color(0.75f, 0.8f, 0.9f) },
        new Slice { reward = new Reward { kind = RewardKind.Crate, id = "gold", amount = 1 }, weight = 1, color = new Color(1f, 0.6f, 0.15f) },
    };

    public const int SpinPrice = 200;

    /// <summary>Each slice of the wheel and its chance (the same for the free and the paid spins).</summary>
    public static List<string> WheelOdds()
    {
        var lines = new List<string>();
        float total = 0f;
        foreach (var s in Wheel)
            total += s.weight;
        lines.Add("Her çevirişte olasılıklar (ücretsiz ve Kredi ile çevirişler aynıdır):");
        foreach (var s in Wheel)
        {
            string what = s.reward.kind == RewardKind.Gear && string.IsNullOrEmpty(s.reward.id) ? s.reward.amount + " rastgele eşya kartı" : s.reward.Name;
            lines.Add("•  " + what + ": " + OddsPanel.Percent(s.weight / total));
        }
        return lines;
    }
    public const int MaxPaidSpins = 5;

    public static bool FreeSpinReady { get { return PlayerPrefs.GetInt("zm_wheel_day", -1) != Day; } }
    public static int PaidSpinsToday { get { return PlayerPrefs.GetInt("zm_wheel_paid_" + Day, 0); } }

    /// <summary>Spins (free once a day, then for Kredi). Returns the slice index, or -1 when not allowed.</summary>
    public static int Spin(ProfileData p, out GrantedReward given)
    {
        given = default(GrantedReward);
        if (FreeSpinReady)
            PlayerPrefs.SetInt("zm_wheel_day", Day);
        else
        {
            if (PaidSpinsToday >= MaxPaidSpins || p.coins < SpinPrice)
                return -1;
            p.coins -= SpinPrice;
            PlayerPrefs.SetInt("zm_wheel_paid_" + Day, PaidSpinsToday + 1);
        }
        var rng = new System.Random(System.Environment.TickCount);
        int total = 0;
        foreach (var s in Wheel)
            total += s.weight;
        int pick = rng.Next(total), index = 0;
        for (; index < Wheel.Length - 1; index++)
        {
            pick -= Wheel[index].weight;
            if (pick < 0)
                break;
        }
        var r = Wheel[index].reward;
        if (r.kind == RewardKind.Gear && string.IsNullOrEmpty(r.id))
            r = RandomCards(rng, new[] { "Sıradan", "Nadir", "Epik" }, 2, 4);
        given = Shop.Give(p, r);
        PlayerPrefs.Save();
        return index;
    }
}
