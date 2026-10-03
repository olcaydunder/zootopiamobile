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
        ownedSkins = PlayerPrefs.GetString("zm_skins", ModelLibrary.PlayerSkin);
        equippedSkin = PlayerPrefs.GetString("zm_skin", ModelLibrary.PlayerSkin);
        if (!OwnsSkin(equippedSkin))
            equippedSkin = ModelLibrary.PlayerSkin;
        tutorialDone = PlayerPrefs.GetInt("zm_tutorial", 0) == 1;
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

        xp += xpGained;
        while (xp >= XpForNextLevel)
        {
            xp -= XpForNextLevel;
            level++;
        }

        Save();
    }
}
