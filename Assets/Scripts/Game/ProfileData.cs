using UnityEngine;

public class ProfileData : MonoBehaviour
{
    public string playerName = "Rogue";
    public int xp;
    public int level = 1;
    public int coins = 350;
    public int wins;
    public int losses;
    public int kills;

    public void AddExperience(int amount)
    {
        xp += amount;
        while (xp >= level * 150)
        {
            xp -= level * 150;
            level++;
            coins += 25;
        }
    }

    public void UnlockSkin(string skinId, int cost)
    {
        if (coins < cost)
            return;

        coins -= cost;
    }

    public void Save()
    {
        PlayerPrefs.SetString("player_name", playerName);
        PlayerPrefs.SetInt("player_xp", xp);
        PlayerPrefs.SetInt("player_level", level);
        PlayerPrefs.SetInt("player_coins", coins);
        PlayerPrefs.Save();
    }
}
