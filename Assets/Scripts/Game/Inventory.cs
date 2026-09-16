using UnityEngine;

public class Inventory
{
    public int medkits = 2;
    public int grenades = 1;
    public int boostPacks = 1;
    public bool hasArmorPlate;

    public void AddLoot(string tag)
    {
        if (tag == "medkit")
            medkits++;
        if (tag == "grenade")
            grenades++;
        if (tag == "armor")
            hasArmorPlate = true;
    }
}
