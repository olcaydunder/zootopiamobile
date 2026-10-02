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

    public int XpForNextLevel
    {
        get { return level * 150; }
    }

    public void Load()
    {
        playerName = PlayerPrefs.GetString("zm_name", "Oyuncu");
        xp = PlayerPrefs.GetInt("zm_xp", 0);
        level = Mathf.Max(1, PlayerPrefs.GetInt("zm_level", 1));
        coins = PlayerPrefs.GetInt("zm_coins", 0);
        matches = PlayerPrefs.GetInt("zm_matches", 0);
        wins = PlayerPrefs.GetInt("zm_wins", 0);
        totalKills = PlayerPrefs.GetInt("zm_kills", 0);
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
        PlayerPrefs.Save();
    }

    public void AddMatchResult(bool won, int kills, int xpGained, int coinsGained)
    {
        matches++;
        if (won)
            wins++;
        totalKills += kills;
        coins += coinsGained;

        xp += xpGained;
        while (xp >= XpForNextLevel)
        {
            xp -= XpForNextLevel;
            level++;
        }

        Save();
    }
}
