public class Inventory
{
    public const int MaxMedkits = 5;
    public const int MaxGrenades = 3;
    public const int MaxDrinks = 4;
    public const int MaxTacticals = 2;

    public int medkits = 1;
    public int grenades;      // the equipped explosive (frag, molotov or charge)
    public int tacticals;     // the equipped tactical item (smoke, flash or gas)
    public int drinks;

    public void Reset()
    {
        medkits = 1;
        grenades = 0;
        tacticals = 0;
        drinks = 0;
    }

    public bool AddTactical()
    {
        if (tacticals >= MaxTacticals)
            return false;
        tacticals++;
        return true;
    }

    public bool AddMedkit()
    {
        if (medkits >= MaxMedkits)
            return false;
        medkits++;
        return true;
    }

    public bool AddGrenade()
    {
        if (grenades >= MaxGrenades)
            return false;
        grenades++;
        return true;
    }

    public bool AddDrink()
    {
        if (drinks >= MaxDrinks)
            return false;
        drinks++;
        return true;
    }
}

/// <summary>A carried weapon that is not currently in hand.</summary>
public class WeaponSlot
{
    public WeaponData data;
    public int ammo;
    public int reserve;
}
