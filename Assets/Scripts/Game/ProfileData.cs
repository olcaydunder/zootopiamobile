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
        PlayerPrefs.Save();
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
