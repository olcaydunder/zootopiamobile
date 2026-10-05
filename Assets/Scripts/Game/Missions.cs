using System.Collections.Generic;
using UnityEngine;

/// <summary>What the player did in the current match (counted for the missions at the end).</summary>
public static class MatchStats
{
    public static float Damage;
    public static int Headshots;
    public static int Loot;
    public static float StartTime;

    public static void Reset()
    {
        Damage = 0f;
        Headshots = 0;
        Loot = 0;
        StartTime = Time.time;
    }
}

public enum MissionStat { Matches, Kills, Wins, Top5, Damage, Headshots, Loot, SurviveMinutes, TeamMatches, TeamWins }

/// <summary>
/// Never-ending missions: 3 daily ones (new every day at midnight) and 3 weekly ones (new every Monday),
/// picked from pools by the date so everyone has the same; finishing all daily ones gives a bonus box.
/// Progress is counted at the end of every match (bots or online). Rewards: Kredi and gift boxes,
/// collected on the GÖREVLER screen (a box opens there with its animation).
/// </summary>
public static class Missions
{
    public class Def
    {
        public string id;
        public string text;      // "{0}" = goal
        public MissionStat stat;
        public int goal;
        public Reward reward;
    }

    public class State
    {
        public Def def;
        public bool weekly;
        public int progress;
        public bool claimed;
        public bool Done { get { return progress >= def.goal; } }
    }

    private static Reward Credits(int n) { return new Reward { kind = RewardKind.Credits, amount = n }; }
    private static Reward Box(string id) { return new Reward { kind = RewardKind.Crate, id = id, amount = 1 }; }

    private static readonly Def[] DailyPool =
    {
        new Def { id = "d_matches", text = "{0} maç oyna", stat = MissionStat.Matches, goal = 3, reward = Credits(150) },
        new Def { id = "d_kills", text = "{0} düşman indir", stat = MissionStat.Kills, goal = 10, reward = Box("bronze") },
        new Def { id = "d_damage", text = "Toplam {0} hasar ver", stat = MissionStat.Damage, goal = 1500, reward = Credits(200) },
        new Def { id = "d_top5", text = "Bir maçta ilk 5'e gir", stat = MissionStat.Top5, goal = 1, reward = Credits(200) },
        new Def { id = "d_head", text = "{0} kafadan vuruş yap", stat = MissionStat.Headshots, goal = 5, reward = Box("bronze") },
        new Def { id = "d_loot", text = "{0} ganimet sandığı topla", stat = MissionStat.Loot, goal = 12, reward = Credits(120) },
        new Def { id = "d_survive", text = "Toplam {0} dakika hayatta kal", stat = MissionStat.SurviveMinutes, goal = 15, reward = Credits(150) },
        new Def { id = "d_team", text = "{0} tane 5v5 maçı oyna", stat = MissionStat.TeamMatches, goal = 2, reward = Box("bronze") },
    };

    private static readonly Def[] WeeklyPool =
    {
        new Def { id = "w_matches", text = "{0} maç oyna", stat = MissionStat.Matches, goal = 15, reward = Box("silver") },
        new Def { id = "w_kills", text = "{0} düşman indir", stat = MissionStat.Kills, goal = 60, reward = Box("silver") },
        new Def { id = "w_wins", text = "{0} maç kazan", stat = MissionStat.Wins, goal = 3, reward = Box("gold") },
        new Def { id = "w_damage", text = "Toplam {0} hasar ver", stat = MissionStat.Damage, goal = 12000, reward = Credits(1000) },
        new Def { id = "w_head", text = "{0} kafadan vuruş yap", stat = MissionStat.Headshots, goal = 30, reward = Box("silver") },
        new Def { id = "w_teamwins", text = "{0} tane 5v5 maçı kazan", stat = MissionStat.TeamWins, goal = 4, reward = Box("silver") },
    };

    public static readonly Def DailyBonus = new Def { id = "d_all", text = "Bugünün bütün görevlerini bitir", stat = MissionStat.Matches, goal = 1, reward = Box("silver") };

    public static int Day { get { return (int)(System.DateTime.Now.Date - new System.DateTime(2026, 1, 1)).TotalDays; } }
    public static int Week { get { return (Day + 3) / 7; } }   // 2026-01-01 was a Thursday: weeks start on Monday

    private static List<Def> Pick(Def[] pool, int seed, int count)
    {
        var order = new List<Def>(pool);
        var rng = new System.Random(seed * 7919 + 17);
        for (int i = order.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            var tmp = order[i]; order[i] = order[j]; order[j] = tmp;
        }
        return order.GetRange(0, Mathf.Min(count, order.Count));
    }

    private static string Key(bool weekly, string id) { return "zm_mis_" + (weekly ? "w" + Week : "d" + Day) + "_" + id; }

    public static List<State> Current(bool weekly)
    {
        var list = new List<State>();
        foreach (var d in weekly ? Pick(WeeklyPool, Week + 1000, 3) : Pick(DailyPool, Day, 3))
        {
            string k = Key(weekly, d.id);
            list.Add(new State { def = d, weekly = weekly, progress = PlayerPrefs.GetInt(k, 0), claimed = PlayerPrefs.GetInt(k + "_c", 0) == 1 });
        }
        return list;
    }

    /// <summary>The bonus for finishing all of today's: done when the three are done.</summary>
    public static State Bonus()
    {
        bool all = true;
        foreach (var s in Current(false))
            all &= s.Done;
        string k = Key(false, DailyBonus.id);
        return new State { def = DailyBonus, weekly = false, progress = all ? 1 : 0, claimed = PlayerPrefs.GetInt(k + "_c", 0) == 1 };
    }

    /// <summary>Missions finished but not collected (the lobby shows this on the GÖREVLER button).</summary>
    public static int ReadyCount()
    {
        int n = 0;
        foreach (var s in Current(false)) if (s.Done && !s.claimed) n++;
        foreach (var s in Current(true)) if (s.Done && !s.claimed) n++;
        var b = Bonus();
        if (b.Done && !b.claimed) n++;
        return n;
    }

    private static void Add(MissionStat stat, int amount)
    {
        if (amount <= 0)
            return;
        foreach (bool weekly in new[] { false, true })
            foreach (var s in Current(weekly))
                if (s.def.stat == stat && !s.claimed)
                    PlayerPrefs.SetInt(Key(weekly, s.def.id), Mathf.Min(s.def.goal, s.progress + amount));
    }

    /// <summary>Counts a finished match (called with the result, bots or online).</summary>
    public static void OnMatchEnd(bool won, int place, int kills, bool team5v5)
    {
        Add(MissionStat.Matches, 1);
        Add(MissionStat.Kills, kills);
        if (won)
            Add(MissionStat.Wins, 1);
        if (place <= 5 || team5v5 && won)
            Add(MissionStat.Top5, 1);
        Add(MissionStat.Damage, Mathf.RoundToInt(MatchStats.Damage));
        Add(MissionStat.Headshots, MatchStats.Headshots);
        Add(MissionStat.Loot, MatchStats.Loot);
        Add(MissionStat.SurviveMinutes, Mathf.FloorToInt((Time.time - MatchStats.StartTime) / 60f));
        if (team5v5)
        {
            Add(MissionStat.TeamMatches, 1);
            if (won)
                Add(MissionStat.TeamWins, 1);
        }
        PlayerPrefs.Save();
    }

    /// <summary>Collects a finished mission's reward. Returns what was given (a box is given unopened).</summary>
    public static bool Claim(State s, ProfileData profile, out GrantedReward given)
    {
        given = default(GrantedReward);
        if (!s.Done || s.claimed)
            return false;
        string k = Key(s.weekly, s.def.id);
        PlayerPrefs.SetInt(k + "_c", 1);
        given = Shop.Give(profile, s.def.reward);
        PlayerPrefs.Save();
        return true;
    }

    public static string Text(Def d) { return string.Format(d.text, d.goal.ToString("N0")); }

    /// <summary>Time left: "5 sa 12 dk" until midnight / Monday.</summary>
    public static string TimeLeft(bool weekly)
    {
        var now = System.DateTime.Now;
        var end = now.Date.AddDays(1);
        if (weekly)
        {
            int toMonday = ((int)System.DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7;
            end = now.Date.AddDays(toMonday == 0 ? 7 : toMonday);
        }
        var left = end - now;
        return left.TotalDays >= 1 ? (int)left.TotalDays + " gün " + left.Hours + " sa" : left.Hours + " sa " + left.Minutes + " dk";
    }
}
