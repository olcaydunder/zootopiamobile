using System.Collections;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    public WeaponData weaponData;
    public int currentAmmo;
    public int reserveAmmo;
    public float nextShotTime;
    public bool isReloading;
    public Transform muzzle;

    public void Initialize(WeaponData data)
    {
        weaponData = data;
        currentAmmo = data.magazineSize;
        reserveAmmo = data.reserveAmmo;
        muzzle = transform;
    }

    public bool TryFire(Vector3 origin, Vector3 direction, out RaycastHit hit)
    {
        hit = default;

        if (weaponData == null)
            return false;

        if (Time.time < nextShotTime)
            return false;

        if (isReloading)
            return false;

        if (currentAmmo <= 0)
        {
            Reload();
            return false;
        }

        currentAmmo--;
        nextShotTime = Time.time + weaponData.fireRate;

        Ray ray = new Ray(origin, direction.normalized);
        if (Physics.Raycast(ray, out hit, weaponData.range))
        {
            var bot = hit.collider.GetComponentInParent<BotAgent>();
            if (bot != null)
                bot.TakeDamage(weaponData.damage, true);

            var player = hit.collider.GetComponentInParent<PlayerController>();
            if (player != null && this.GetComponentInParent<PlayerController>() != null)
                player.TakeDamage(weaponData.damage, true);
        }

        return true;
    }

    public void Reload()
    {
        if (weaponData == null || isReloading || currentAmmo >= weaponData.magazineSize || reserveAmmo <= 0)
            return;

        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        yield return new WaitForSeconds(weaponData.reloadTime);

        int needed = weaponData.magazineSize - currentAmmo;
        int toLoad = Mathf.Min(needed, reserveAmmo);
        currentAmmo += toLoad;
        reserveAmmo -= toLoad;
        isReloading = false;
    }
}
