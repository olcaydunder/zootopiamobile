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

public enum MissionStat { Matches, Kills, Wins, Top5, Damage, Headshots, Loot, SurviveMinutes, TeamMatches, TeamWins }   // Team*: the arena modes (5v5, Hakimiyet, Herkes Tek, Soygun)

/// <summary>
/// Never-ending missions: 3 daily ones (new every day at midnight) and 3 weekly ones (new every Monday),
/// picked from pools by the date so everyone has the same; finishing all daily ones gives a bonus box.
/// Season missions (4 weeks) have five stages each. Every collected mission fills this week's mission box
/// track (boxes at 1, 5 and 8), and every win today fills the daily victories track (six boxes).
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
        new Def { id = "d_team", text = "{0} arena maçı oyna (5v5, Hakimiyet, Herkes Tek, Soygun)", stat = MissionStat.TeamMatches, goal = 2, reward = Box("bronze") },
    };

    private static readonly Def[] WeeklyPool =
    {
        new Def { id = "w_matches", text = "{0} maç oyna", stat = MissionStat.Matches, goal = 15, reward = Box("silver") },
        new Def { id = "w_kills", text = "{0} düşman indir", stat = MissionStat.Kills, goal = 60, reward = Box("silver") },
        new Def { id = "w_wins", text = "{0} maç kazan", stat = MissionStat.Wins, goal = 3, reward = Box("gold") },
        new Def { id = "w_damage", text = "Toplam {0} hasar ver", stat = MissionStat.Damage, goal = 12000, reward = Credits(1000) },
        new Def { id = "w_head", text = "{0} kafadan vuruş yap", stat = MissionStat.Headshots, goal = 30, reward = Box("silver") },
        new Def { id = "w_teamwins", text = "{0} arena maçı kazan", stat = MissionStat.TeamWins, goal = 4, reward = Box("silver") },
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
        foreach (var s in SeasonMissions()) if (s.Ready) n++;
        for (int i = 0; i < ChestAt.Length; i++) if (ChestReady(i)) n++;
        for (int i = 0; i < WinBoxes; i++) if (WinReady(i)) n++;
        return n;
    }

    private static void Add(MissionStat stat, int amount)
    {
        if (amount <= 0)
            return;
        foreach (var sd in SeasonPool)
            if (sd.stat == stat)
                PlayerPrefs.SetInt(SeasonKey(sd.id), SeasonProgress(sd) + amount);
        foreach (bool weekly in new[] { false, true })
            foreach (var s in Current(weekly))
                if (s.def.stat == stat && !s.claimed)
                    PlayerPrefs.SetInt(Key(weekly, s.def.id), Mathf.Min(s.def.goal, s.progress + amount));
    }

    /// <summary>Counts a finished match (called with the result, bots or online).</summary>
    public static void OnMatchEnd(bool won, int place, int kills, MatchMode mode)
    {
        bool team5v5 = Modes.Arena(mode);   // every arena mode counts as an "arena match"
        Add(MissionStat.Matches, 1);
        Add(MissionStat.Kills, kills);
        if (won)
        {
            Add(MissionStat.Wins, 1);
            PlayerPrefs.SetInt(WinsKey, WinsToday + 1);
        }
        if (Modes.TwoTeams(mode) ? won : mode == MatchMode.FreeForAll ? place <= 3 : place <= 5)
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
        AddChestPoint();
        PlayerPrefs.Save();
        return true;
    }

    // ----- Season missions: five stages each, for four weeks -----

    public const int SeasonDays = 28;
    public static int Season { get { return Day / SeasonDays + 1; } }
    public static int SeasonDaysLeft { get { return SeasonDays - Day % SeasonDays; } }

    public class SeasonDef
    {
        public string id, text;    // "{0}" = the stage's goal
        public MissionStat stat;
        public int[] goals;        // five stages
    }

    public class SeasonState
    {
        public SeasonDef def;
        public int progress, claimed;   // claimed: stages collected (0..5)
        public bool AllDone { get { return claimed >= def.goals.Length; } }
        public int Goal { get { return def.goals[Mathf.Min(claimed, def.goals.Length - 1)]; } }
        public bool Ready { get { return !AllDone && progress >= Goal; } }
    }

    public static readonly SeasonDef[] SeasonPool =
    {
        new SeasonDef { id = "s_kills", text = "{0} düşman indir", stat = MissionStat.Kills, goals = new[] { 25, 75, 150, 300, 500 } },
        new SeasonDef { id = "s_wins", text = "{0} maç kazan", stat = MissionStat.Wins, goals = new[] { 2, 5, 10, 20, 35 } },
        new SeasonDef { id = "s_head", text = "{0} kafadan vuruş yap", stat = MissionStat.Headshots, goals = new[] { 10, 30, 70, 150, 300 } },
        new SeasonDef { id = "s_damage", text = "Toplam {0} hasar ver", stat = MissionStat.Damage, goals = new[] { 5000, 15000, 40000, 80000, 150000 } },
        new SeasonDef { id = "s_matches", text = "{0} maç oyna", stat = MissionStat.Matches, goals = new[] { 5, 15, 30, 60, 100 } },
        new SeasonDef { id = "s_loot", text = "{0} ganimet sandığı topla", stat = MissionStat.Loot, goals = new[] { 30, 90, 200, 400, 700 } },
        new SeasonDef { id = "s_arena", text = "{0} arena maçı oyna", stat = MissionStat.TeamMatches, goals = new[] { 3, 8, 15, 30, 50 } },
    };

    /// <summary>What each of the five stages gives.</summary>
    public static readonly Reward[] StageRewards = { Credits(250), Box("wood"), Box("bronze"), Box("silver"), Box("gold") };

    private static string SeasonKey(string id) { return "zm_season_" + Season + "_" + id; }
    private static int SeasonProgress(SeasonDef d) { return PlayerPrefs.GetInt(SeasonKey(d.id), 0); }

    public static List<SeasonState> SeasonMissions()
    {
        var list = new List<SeasonState>();
        foreach (var d in SeasonPool)
            list.Add(new SeasonState { def = d, progress = SeasonProgress(d), claimed = PlayerPrefs.GetInt(SeasonKey(d.id) + "_c", 0) });
        return list;
    }

    public static string SeasonText(SeasonState s) { return string.Format(s.def.text, s.Goal.ToString("N0")); }

    public static bool ClaimStage(SeasonState s, ProfileData profile, out GrantedReward given)
    {
        given = default(GrantedReward);
        if (!s.Ready)
            return false;
        given = Shop.Give(profile, StageRewards[s.claimed]);
        PlayerPrefs.SetInt(SeasonKey(s.def.id) + "_c", s.claimed + 1);
        AddChestPoint();
        PlayerPrefs.Save();
        return true;
    }

    // ----- This week's mission box track: boxes at 1, 5 and 8 collected missions -----

    public static readonly int[] ChestAt = { 1, 5, 8 };
    public static readonly Reward[] ChestRewards = { Box("bronze"), Box("silver"), Box("gold") };

    private static string ChestKey { get { return "zm_mchest_w" + Week; } }
    public static int ChestPoints { get { return PlayerPrefs.GetInt(ChestKey, 0); } }
    private static void AddChestPoint() { PlayerPrefs.SetInt(ChestKey, ChestPoints + 1); }
    public static bool ChestClaimed(int i) { return PlayerPrefs.GetInt(ChestKey + "_c" + i, 0) == 1; }
    public static bool ChestReady(int i) { return !ChestClaimed(i) && ChestPoints >= ChestAt[i]; }

    public static bool ClaimChest(int i, ProfileData profile, out GrantedReward given)
    {
        given = default(GrantedReward);
        if (!ChestReady(i))
            return false;
        PlayerPrefs.SetInt(ChestKey + "_c" + i, 1);
        given = Shop.Give(profile, ChestRewards[i]);
        PlayerPrefs.Save();
        return true;
    }

    // ----- Today's victories: a box for each of the first six wins -----

    public const int WinBoxes = 6;
    public static readonly Reward[] WinRewards = { Box("wood"), Box("wood"), Box("bronze"), Box("bronze"), Box("silver"), Box("gold") };

    private static string WinsKey { get { return "zm_dwins_d" + Day; } }
    public static int WinsToday { get { return PlayerPrefs.GetInt(WinsKey, 0); } }
    public static bool WinClaimed(int i) { return PlayerPrefs.GetInt(WinsKey + "_c" + i, 0) == 1; }
    public static bool WinReady(int i) { return !WinClaimed(i) && WinsToday > i; }

    public static bool ClaimWin(int i, ProfileData profile, out GrantedReward given)
    {
        given = default(GrantedReward);
        if (!WinReady(i))
            return false;
        PlayerPrefs.SetInt(WinsKey + "_c" + i, 1);
        given = Shop.Give(profile, WinRewards[i]);
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
