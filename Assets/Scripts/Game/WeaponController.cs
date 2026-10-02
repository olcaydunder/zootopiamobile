using System.Collections;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    public WeaponData weaponData;
    public int currentAmmo;
    public int reserveAmmo;
    public bool isReloading;

    private float nextShotTime;
    private Renderer modelRenderer;
    private LineRenderer tracer;
    private float tracerOffTime;

    public void Initialize(WeaponData data, Renderer model)
    {
        StopAllCoroutines();
        isReloading = false;
        nextShotTime = 0f;

        weaponData = data;
        currentAmmo = data.magazineSize;
        reserveAmmo = data.reserveAmmo;

        if (model != null)
            modelRenderer = model;
        if (modelRenderer != null)
            modelRenderer.sharedMaterial = MaterialCache.Lit(data.color);

        EnsureTracer();
    }

    public bool CanFire
    {
        get { return weaponData != null && !isReloading && currentAmmo > 0; }
    }

    /// <summary>
    /// Fires one shot (8 pellets for shotguns). Returns true if a shot was fired.
    /// killed is true if this shot eliminated someone.
    /// </summary>
    public bool TryFire(Vector3 origin, Vector3 direction, int shooterTeam, int layerMask, float extraSpread, out bool killed)
    {
        killed = false;

        if (weaponData == null || isReloading || Time.time < nextShotTime)
            return false;

        if (currentAmmo <= 0)
        {
            Reload();
            return false;
        }

        currentAmmo--;
        nextShotTime = Time.time + weaponData.fireRate;

        int pellets = weaponData.weaponType == WeaponType.Shotgun ? 8 : 1;
        Vector3 tracerEnd = origin + direction.normalized * weaponData.range;

        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = ApplySpread(direction, weaponData.spread + extraSpread);
            Vector3 end = origin + dir * weaponData.range;

            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, weaponData.range, layerMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                if (target != null && !target.IsDead && target.Team != shooterTeam)
                {
                    if (target.TakeDamage(weaponData.damage, shooterTeam))
                        killed = true;
                }
            }

            if (i == 0)
                tracerEnd = end;
        }

        ShowTracer(transform.position, tracerEnd);

        if (currentAmmo <= 0)
            Reload();

        return true;
    }

    public void Reload()
    {
        if (weaponData == null || isReloading || currentAmmo >= weaponData.magazineSize || reserveAmmo <= 0)
            return;

        if (!gameObject.activeInHierarchy)
            return;

        StartCoroutine(ReloadRoutine());
    }

    public void AddAmmo(int amount)
    {
        reserveAmmo += amount;
        if (currentAmmo <= 0)
            Reload();
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

    private void Update()
    {
        if (tracer != null && tracer.enabled && Time.time >= tracerOffTime)
            tracer.enabled = false;
    }

    private void OnDisable()
    {
        isReloading = false;
        if (tracer != null)
            tracer.enabled = false;
    }

    private static Vector3 ApplySpread(Vector3 direction, float degrees)
    {
        Vector3 dir = direction.normalized;
        if (degrees <= 0f)
            return dir;

        Vector3 right = Vector3.Cross(Vector3.up, dir);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        right.Normalize();

        Vector2 r = Random.insideUnitCircle * degrees;
        return Quaternion.AngleAxis(r.x, Vector3.up) * Quaternion.AngleAxis(r.y, right) * dir;
    }

    private void EnsureTracer()
    {
        if (tracer != null)
            return;

        tracer = gameObject.AddComponent<LineRenderer>();
        tracer.positionCount = 2;
        tracer.useWorldSpace = true;
        tracer.startWidth = 0.05f;
        tracer.endWidth = 0.02f;
        tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tracer.receiveShadows = false;
        tracer.material = UIUtil.UnlitMaterial(new Color(1f, 0.85f, 0.45f, 1f));
        tracer.startColor = new Color(1f, 0.9f, 0.5f, 1f);
        tracer.endColor = new Color(1f, 0.9f, 0.5f, 0.2f);
        tracer.enabled = false;
    }

    private void ShowTracer(Vector3 from, Vector3 to)
    {
        if (tracer == null)
            return;

        tracer.SetPosition(0, from);
        tracer.SetPosition(1, to);
        tracer.enabled = true;
        tracerOffTime = Time.time + 0.04f;
    }
}
