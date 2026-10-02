using System.Collections;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    public WeaponData weaponData;
    public int currentAmmo;
    public int reserveAmmo;
    public bool isReloading;
    public bool playerOwned;

    private float nextShotTime;
    private Renderer modelRenderer;
    private LineRenderer tracer;
    private float tracerOffTime;
    private Transform flash;
    private float flashOffTime;

    public void Initialize(WeaponData data, Renderer model)
    {
        Initialize(data, model, data.magazineSize, data.reserveAmmo);
    }

    public void Initialize(WeaponData data, Renderer model, int ammo, int reserve)
    {
        StopAllCoroutines();
        isReloading = false;
        nextShotTime = Time.time + 0.2f;

        weaponData = data;
        currentAmmo = ammo;
        reserveAmmo = reserve;

        if (model != null)
            modelRenderer = model;
        if (modelRenderer != null)
        {
            modelRenderer.sharedMaterial = MaterialCache.Lit(data.color);
            float length = data.weaponType == WeaponType.Pistol ? 0.35f : data.weaponType == WeaponType.Sniper ? 1.1f : data.weaponType == WeaponType.SMG ? 0.6f : 0.85f;
            modelRenderer.transform.localScale = new Vector3(0.1f, 0.13f, length);
        }

        EnsureTracer();
        EnsureFlash();
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
        float totalDamage = 0f;
        bool anyHead = false;
        Vector3 hitPoint = Vector3.zero;

        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = ApplySpread(direction, weaponData.spread + extraSpread);
            Vector3 end = origin + dir * weaponData.range;

            RaycastHit hit;
            if (Physics.Raycast(origin, dir, out hit, weaponData.range, layerMask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
                bool body = target != null && !target.IsDead;
                if (body && target.Team != shooterTeam)
                {
                    bool head = hit.point.y - target.transform.position.y > 0.55f;
                    float damage = weaponData.damage * (head ? 2f : 1f);
                    if (target.TakeDamage(damage, shooterTeam))
                        killed = true;
                    totalDamage += damage;
                    anyHead |= head;
                    hitPoint = hit.point;
                }
                Effects.Impact(hit.point, hit.normal, body);
            }

            if (i == 0)
                tracerEnd = end;
        }

        ShowTracer(transform.position, tracerEnd);
        ShowFlash();

        float volume = playerOwned ? 0.55f : 0.9f;
        float pitch = Random.Range(0.94f, 1.06f);
        if (playerOwned)
            Sfx.Play(SoundBank.Gunshot(weaponData.weaponType), volume, pitch);
        else
            Sfx.PlayAt(SoundBank.Gunshot(weaponData.weaponType), transform.position, volume, pitch);

        if (playerOwned && totalDamage > 0f)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.uiManager != null)
                gm.uiManager.ShowHit(hitPoint, totalDamage, killed, anyHead);
            Sfx.Play(killed ? SoundBank.Kill : SoundBank.Hit, 0.6f);
        }

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
        if (playerOwned)
            Sfx.Play(SoundBank.Reload, 0.6f);
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

        if (flash != null && flash.gameObject.activeSelf)
        {
            if (Time.time >= flashOffTime)
                flash.gameObject.SetActive(false);
            else if (Camera.main != null)
                flash.rotation = Camera.main.transform.rotation;
        }
    }

    private void OnDisable()
    {
        isReloading = false;
        if (tracer != null)
            tracer.enabled = false;
        if (flash != null)
            flash.gameObject.SetActive(false);
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
        tracer.sharedMaterial = UIUtil.UnlitMaterial(new Color(1f, 0.85f, 0.45f, 1f));
        tracer.startColor = new Color(1f, 0.9f, 0.5f, 1f);
        tracer.endColor = new Color(1f, 0.9f, 0.5f, 0.2f);
        tracer.enabled = false;
    }

    private void EnsureFlash()
    {
        if (flash != null)
            return;

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "MuzzleFlash";
        Destroy(quad.GetComponent<Collider>());
        quad.layer = gameObject.layer;
        var mat = UIUtil.UnlitMaterial(new Color(1f, 0.8f, 0.35f, 0.95f));
        mat.mainTexture = UIUtil.Circle.texture;
        var r = quad.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flash = quad.transform;
        flash.SetParent(transform, false);
        flash.gameObject.SetActive(false);
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

    private void ShowFlash()
    {
        if (flash == null)
            return;
        float length = modelRenderer != null ? modelRenderer.transform.localScale.z : 0.8f;
        flash.localPosition = new Vector3(0f, 0f, length * 0.5f + 0.12f);
        flash.localScale = Vector3.one * Random.Range(0.28f, 0.45f);
        flash.gameObject.SetActive(true);
        flashOffTime = Time.time + 0.045f;
    }
}
