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
    public float fireRate;     // seconds between shots
    public float range;
    public float spread;       // degrees
    public int magazineSize;
    public int reserveAmmo;
    public float reloadTime;
    public Color color;

    // Set by the gunsmith (attachments / camo). Defaults leave the gun unchanged.
    public string[] attachments = new string[Gunsmith.SlotCount];
    public string camo = "";
    public string modelSkin = "";   // gun model variant (see ModelLibrary.GunSkins)
    public float recoilMul = 1f;
    public float mobilityMul = 1f;
    public float zoomMul = 1f;
    public bool suppressed;

    public WeaponData Clone()
    {
        var c = (WeaponData)MemberwiseClone();
        c.attachments = (string[])attachments.Clone();
        return c;
    }

    public static WeaponData CreateRifle()
    {
        return new WeaponData
        {
            weaponName = "Bozkurt AR",
            weaponType = WeaponType.Rifle,
            damage = 20f,
            fireRate = 0.11f,
            range = 80f,
            spread = 1.4f,
            magazineSize = 30,
            reserveAmmo = 120,
            reloadTime = 1.9f,
            color = new Color(0.2f, 0.25f, 0.2f)
        };
    }

    public static WeaponData CreateSMG()
    {
        return new WeaponData
        {
            weaponName = "Şimşek SMG",
            weaponType = WeaponType.SMG,
            damage = 14f,
            fireRate = 0.075f,
            range = 40f,
            spread = 2.4f,
            magazineSize = 32,
            reserveAmmo = 128,
            reloadTime = 1.5f,
            color = new Color(0.15f, 0.35f, 0.45f)
        };
    }

    public static WeaponData CreateShotgun()
    {
        return new WeaponData
        {
            weaponName = "Kaya-12",
            weaponType = WeaponType.Shotgun,
            damage = 11f,      // per pellet, 8 pellets
            fireRate = 0.8f,
            range = 20f,
            spread = 5.5f,
            magazineSize = 6,
            reserveAmmo = 30,
            reloadTime = 2.3f,
            color = new Color(0.45f, 0.2f, 0.12f)
        };
    }

    public static WeaponData CreateSniper()
    {
        return new WeaponData
        {
            weaponName = "Kartal SR",
            weaponType = WeaponType.Sniper,
            damage = 85f,
            fireRate = 1.3f,
            range = 150f,
            spread = 0.15f,
            magazineSize = 5,
            reserveAmmo = 20,
            reloadTime = 2.6f,
            color = new Color(0.3f, 0.3f, 0.12f)
        };
    }

    public static WeaponData CreatePistol()
    {
        return new WeaponData
        {
            weaponName = "Tabanca P9",
            weaponType = WeaponType.Pistol,
            damage = 16f,
            fireRate = 0.22f,
            range = 45f,
            spread = 1.8f,
            magazineSize = 12,
            reserveAmmo = 48,
            reloadTime = 1.2f,
            color = new Color(0.1f, 0.1f, 0.1f)
        };
    }

    /// <summary>Rough power ranking used when deciding whether to swap weapons.</summary>
    public static int Tier(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Pistol: return 0;
            case WeaponType.SMG:
            case WeaponType.Shotgun: return 1;
            default: return 2;
        }
    }

    /// <summary>Camera kick per shot in degrees.</summary>
    public float Recoil
    {
        get
        {
            switch (weaponType)
            {
                case WeaponType.Sniper: return 3f * recoilMul;
                case WeaponType.Shotgun: return 2.5f * recoilMul;
                case WeaponType.Pistol: return 1.2f * recoilMul;
                case WeaponType.SMG: return 0.5f * recoilMul;
                default: return 0.7f * recoilMul;
            }
        }
    }

    public static WeaponData CreateRandomLoot()
    {
        float r = Random.value;
        if (r < 0.35f) return CreateRifle();
        if (r < 0.65f) return CreateSMG();
        if (r < 0.85f) return CreateShotgun();
        return CreateSniper();
    }

    /// <summary>Weapon a bot spawns with, weighted toward weaker guns.</summary>
    public static WeaponData CreateRandomBotWeapon()
    {
        float r = Random.value;
        if (r < 0.35f) return CreatePistol();
        if (r < 0.65f) return CreateSMG();
        if (r < 0.85f) return CreateRifle();
        return CreateShotgun();
    }
}
