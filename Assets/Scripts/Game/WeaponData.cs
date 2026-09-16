using UnityEngine;

public enum WeaponType
{
    Rifle,
    SMG,
    Shotgun,
    Sniper,
    Pistol
}

[System.Serializable]
public class WeaponData
{
    public string weaponName;
    public WeaponType weaponType;
    public float damage;
    public float fireRate;
    public float range;
    public int magazineSize;
    public int reserveAmmo;
    public float reloadTime;
    public int cost;
    public Color color;

    public static WeaponData CreateRifle()
    {
        return new WeaponData
        {
            weaponName = "Ranger AR",
            weaponType = WeaponType.Rifle,
            damage = 22f,
            fireRate = 0.12f,
            range = 70f,
            magazineSize = 30,
            reserveAmmo = 120,
            reloadTime = 1.8f,
            cost = 0,
            color = Color.green
        };
    }

    public static WeaponData CreateSMG()
    {
        return new WeaponData
        {
            weaponName = "Nova SMG",
            weaponType = WeaponType.SMG,
            damage = 15f,
            fireRate = 0.08f,
            range = 36f,
            magazineSize = 32,
            reserveAmmo = 96,
            reloadTime = 1.5f,
            cost = 280,
            color = Color.cyan
        };
    }

    public static WeaponData CreateShotgun()
    {
        return new WeaponData
        {
            weaponName = "Breaker 12",
            weaponType = WeaponType.Shotgun,
            damage = 12f,
            fireRate = 0.72f,
            range = 18f,
            magazineSize = 8,
            reserveAmmo = 32,
            reloadTime = 2.2f,
            cost = 500,
            color = Color.red
        };
    }

    public static WeaponData CreateSniper()
    {
        return new WeaponData
        {
            weaponName = "Longview",
            weaponType = WeaponType.Sniper,
            damage = 72f,
            fireRate = 1.2f,
            range = 120f,
            magazineSize = 5,
            reserveAmmo = 20,
            reloadTime = 2.5f,
            cost = 800,
            color = Color.magenta
        };
    }

    public static WeaponData CreatePistol()
    {
        return new WeaponData
        {
            weaponName = "Cinder Pistol",
            weaponType = WeaponType.Pistol,
            damage = 18f,
            fireRate = 0.18f,
            range = 40f,
            magazineSize = 12,
            reserveAmmo = 48,
            reloadTime = 1.2f,
            cost = 0,
            color = Color.yellow
        };
    }
}
