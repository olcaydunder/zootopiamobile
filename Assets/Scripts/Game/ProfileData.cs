using System.Collections.Generic;
using UnityEngine;

/// <summary>Local player profile, saved on the device with PlayerPrefs.</summary>
public class ProfileData
{
    public string playerName = "Oyuncu";
    public int xp;
    public int level = 1;
    public int coins;
    public int matches;
    public int wins;
    public int totalKills;
    public string equippedSkin = ModelLibrary.PlayerSkin;
    public string ownedSkins = ModelLibrary.PlayerSkin;
    public bool tutorialDone;
    // Profile statistics (PROFİL)
    public int trophies, deaths, bestKills, headshots;
    public long damage;
    public float playSeconds;

    /// <summary>Rewards given by the last AddMatchResult (one per level gained).</summary>
    public readonly List<GrantedReward> lastRewards = new List<GrantedReward>();
    public int lastLevelBefore = 1;

    public int XpForNextLevel
    {
        get { return Progression.XpToNext(level); }
    }

    public bool IsMaxLevel { get { return level >= Progression.MaxLevel; } }
    public int RankIndex { get { return Progression.RankIndex(level); } }
    public string RankName { get { return Progression.RankName(level); } }

    public void Load()
    {
        playerName = PlayerPrefs.GetString("zm_name", "Oyuncu");
        xp = PlayerPrefs.GetInt("zm_xp", 0);
        level = Mathf.Clamp(PlayerPrefs.GetInt("zm_level", 1), 1, Progression.MaxLevel);
        coins = PlayerPrefs.GetInt("zm_coins", 0);
        matches = PlayerPrefs.GetInt("zm_matches", 0);
        wins = PlayerPrefs.GetInt("zm_wins", 0);
        totalKills = PlayerPrefs.GetInt("zm_kills", 0);
        ownedSkins = PlayerPrefs.GetString("zm_skins", ModelLibrary.PlayerSkin);
        equippedSkin = PlayerPrefs.GetString("zm_skin", ModelLibrary.PlayerSkin);
        if (!OwnsSkin(equippedSkin))
            equippedSkin = ModelLibrary.PlayerSkin;
        tutorialDone = PlayerPrefs.GetInt("zm_tutorial", 0) == 1;
        trophies = PlayerPrefs.GetInt("zm_trophies", 0);
        deaths = PlayerPrefs.GetInt("zm_deaths", 0);
        bestKills = PlayerPrefs.GetInt("zm_bestkills", 0);
        headshots = PlayerPrefs.GetInt("zm_headshots", 0);
        if (!long.TryParse(PlayerPrefs.GetString("zm_damage", "0"), out damage))
            damage = 0;
        playSeconds = PlayerPrefs.GetFloat("zm_playtime", 0f);

        // Levels reached before the reward track existed still get their rewards.
        int rewarded = PlayerPrefs.GetInt("zm_reward_level", 1);
        if (rewarded < level)
        {
            for (int l = rewarded + 1; l <= level; l++)
                Progression.Grant(this, l);
            PlayerPrefs.SetInt("zm_reward_level", level);
            Save();
        }
    }

    public bool OwnsSkin(string skin)
    {
        return ("," + ownedSkins + ",").Contains("," + skin + ",");
    }

    /// <summary>Buys a skin with coins. Returns false if not enough coins.</summary>
    public bool BuySkin(string skin, int price)
    {
        if (OwnsSkin(skin))
            return true;
        if (coins < price)
            return false;
        coins -= price;
        ownedSkins += "," + skin;
        Save();
        return true;
    }

    public void EquipSkin(string skin)
    {
        if (!OwnsSkin(skin))
            return;
        equippedSkin = skin;
        Save();
    }

    public void Save()
    {
        PlayerPrefs.SetString("zm_name", playerName);
        PlayerPrefs.SetInt("zm_xp", xp);
        PlayerPrefs.SetInt("zm_level", level);
        PlayerPrefs.SetInt("zm_coins", coins);
        PlayerPrefs.SetInt("zm_matches", matches);
        PlayerPrefs.SetInt("zm_wins", wins);
        PlayerPrefs.SetInt("zm_kills", totalKills);
        PlayerPrefs.SetString("zm_skins", ownedSkins);
        PlayerPrefs.SetString("zm_skin", equippedSkin);
        PlayerPrefs.SetInt("zm_tutorial", tutorialDone ? 1 : 0);
        PlayerPrefs.SetInt("zm_trophies", trophies);
        PlayerPrefs.SetInt("zm_deaths", deaths);
        PlayerPrefs.SetInt("zm_bestkills", bestKills);
        PlayerPrefs.SetInt("zm_headshots", headshots);
        PlayerPrefs.SetString("zm_damage", damage.ToString());
        PlayerPrefs.SetFloat("zm_playtime", playSeconds);
        PlayerPrefs.Save();
    }

    public float WinRate { get { return matches > 0 ? 100f * wins / matches : 0f; } }
    public float KillsPerDeath { get { return totalKills / (float)Mathf.Max(1, deaths); } }

    public static readonly string[] Leagues = { "BRONZ", "GÜMÜŞ", "ALTIN", "PLATİN", "ELMAS", "USTA" };
    public static readonly int[] LeagueAt = { 0, 400, 1000, 2000, 3500, 5500 };

    public int LeagueIndex
    {
        get
        {
            int i = 0;
            while (i + 1 < LeagueAt.Length && trophies >= LeagueAt[i + 1])
                i++;
            return i;
        }
    }

    public string League { get { return Leagues[LeagueIndex]; } }

    /// <summary>
    /// KUPA: won or lost for a match (more for a good place and kills, a little lost for an early exit; never below 0).
    /// Also adds the match to the profile statistics. Returns the change.
    /// </summary>
    public int AddStats(MatchMode mode, bool won, int place, int teams, int kills, int matchDeaths, float seconds, float matchDamage, int matchHeadshots)
    {
        float share = teams > 1 ? (float)(teams - place) / (teams - 1) : 1f;
        int delta;
        if (Modes.Arena(mode) && mode != MatchMode.FreeForAll)
            delta = (won ? 24 : -8) + Mathf.Min(10, kills);
        else
            delta = Mathf.RoundToInt(-10f + 32f * share) + Mathf.Min(12, kills * 2) + (won ? 12 : 0);
        // Higher leagues are harder to climb.
        if (delta > 0)
            delta = Mathf.Max(1, Mathf.RoundToInt(delta * (1f - 0.08f * LeagueIndex)));
        int before = trophies;
        trophies = Mathf.Max(0, trophies + delta);
        deaths += Mathf.Max(0, matchDeaths);
        bestKills = Mathf.Max(bestKills, kills);
        headshots += Mathf.Max(0, matchHeadshots);
        damage += Mathf.Max(0, Mathf.RoundToInt(matchDamage));
        playSeconds += Mathf.Max(0f, seconds);
        Save();
        return trophies - before;
    }

    public void AddMatchResult(bool won, int kills, int xpGained, int coinsGained)
    {
        matches++;
        if (won)
            wins++;
        totalKills += kills;
        coins += coinsGained;

        lastRewards.Clear();
        lastLevelBefore = level;
        xp += xpGained;
        while (!IsMaxLevel && xp >= XpForNextLevel)
        {
            xp -= XpForNextLevel;
            level++;
            lastRewards.Add(Progression.Grant(this, level));
        }
        if (IsMaxLevel)
            xp = 0;
        PlayerPrefs.SetInt("zm_reward_level", level);

        Save();
    }
}
