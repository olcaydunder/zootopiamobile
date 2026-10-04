using UnityEngine;

/// <summary>PlayerController part: Third-person camera, look input (swipe, gyro, mouse), aim assist and scope.</summary>
public partial class PlayerController
{
    /// <summary>Set by the title screen while it flies the camera around the city.</summary>
    public bool cinematic;

    private void LateUpdate()
    {
        if (cinematic)
            return;
        if (lobbyView)
        {
            // Camera in front of the character, slowly drifting, like a menu showcase.
            float t = Time.time * 0.25f;
            Vector3 focus = transform.position + Vector3.up * 0.25f;
            // A little further back and higher than a close-up, so the clinic sign behind shows too.
            Vector3 offset = transform.forward * 3.9f + transform.right * Mathf.Sin(t) * 0.35f + Vector3.up * (0.45f + Mathf.Sin(t * 0.7f) * 0.05f);
            playerCamera.transform.position = focus + offset;
            playerCamera.transform.LookAt(focus + Vector3.up * 0.6f);
            playerCamera.fieldOfView = 44f;
            return;
        }

        if (state != PlayerState.Ground || isDead || isDowned)
            aimingDownSights = false;
        float wantedDistance = aimingDownSights ? 1.6f : camTarget;
        camDistance = Mathf.Lerp(camDistance, wantedDistance, Time.deltaTime * (aimingDownSights ? 8f : 3f));
        float wantedFov = aimingDownSights ? ZoomFov() : GameSettings.Fov;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, wantedFov, Time.deltaTime * 10f);

        // Keep the camera out of walls and hills.
        float dist = camDistance;
        Vector3 desired = cameraPivot.TransformPoint(new Vector3(0.55f, 0.35f, -camDistance));
        Vector3 from = cameraPivot.position;
        RaycastHit hit;
        if (state == PlayerState.Ground && Physics.Linecast(from, desired, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            float full = Mathf.Max(0.01f, Vector3.Distance(from, desired));
            float allowed = Vector3.Distance(from, hit.point) - 0.25f;
            dist = Mathf.Max(0.6f, camDistance * Mathf.Clamp01(allowed / full));
        }

        shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 2.5f);
        Vector3 jitter = Random.insideUnitSphere * shake * 0.25f;
        bool scopedNow = IsScoped;
        if (scopedNow != gunHiddenForScope || (scopedNow && scopedWeapon != currentWeapon))
        {
            SetGunVisible(scopedWeapon, true);
            scopedWeapon = scopedNow ? currentWeapon : null;
            SetGunVisible(scopedWeapon, false);
            gunHiddenForScope = scopedNow;
        }
        if (scopedNow)
        {
            // Through the scope: eye position in front of the head, so the body never blocks the lens.
            // Pulled back when a wall is that close, so the scope can't look through cover.
            Vector3 eyeLocal = new Vector3(0.12f, 0.3f, 0.35f);
            Vector3 eye = cameraPivot.TransformPoint(eyeLocal);
            RaycastHit eyeHit;
            if (Physics.Linecast(cameraPivot.position, eye, out eyeHit, Physics.DefaultRaycastLayers & ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                float full = Mathf.Max(0.01f, Vector3.Distance(cameraPivot.position, eye));
                float k = Mathf.Clamp01((Vector3.Distance(cameraPivot.position, eyeHit.point) - 0.12f) / full);
                eyeLocal *= k;
            }
            playerCamera.transform.localPosition = eyeLocal + jitter * 0.3f;
            return;
        }
        playerCamera.transform.localPosition = new Vector3(0.55f, 0.35f, -dist) + jitter;
    }

    public void Shake(float amount)
    {
        if (!GameSettings.CameraShake)
            return;
        shake = Mathf.Max(shake, amount);
    }

    private void HandleLook(IPlayerInput tc)
    {
        Vector2 look = Vector2.zero;
        float sens = IsScoped ? GameSettings.ScopeSensitivity : (aimingDownSights ? GameSettings.AdsSensitivity : GameSettings.Sensitivity);
        if (tc != null)
        {
            Vector2 d = tc.TouchLookDelta;
            // "Hız ivmesi": fast swipes turn further than slow, precise ones.
            if (GameSettings.RotationMode == 1 && Time.deltaTime > 0f)
            {
                float speed = d.magnitude / Time.deltaTime;          // canvas units per second
                d *= 1f + GameSettings.Acceleration / 100f * Mathf.Clamp01((speed - 300f) / 2500f);
            }
            look += d * touchLookSensitivity * sens * (aimingDownSights ? ZoomFov() / 70f : 1f);
        }
        look += GyroLook(sens);

        // Desktop testing: hold right mouse button to look around.
        if (tc != null)
            look += tc.MouseLook * mouseSensitivity;

        pitch = Mathf.Clamp(pitch - look.y, -60f, state == PlayerState.Ground ? 60f : 80f);

        if (state == PlayerState.Driving)
        {
            lookYaw += look.x;
            bool turret = vehicle != null && vehicle.def.cannon;   // the tank aims where you look
            if (Mathf.Abs(look.x) < 0.01f && !turret)
                lookYaw = Mathf.MoveTowardsAngle(lookYaw, 0f, 40f * Time.deltaTime);
            cameraPivot.localRotation = Quaternion.Euler(pitch, lookYaw, 0f);
        }
        else
        {
            transform.Rotate(0f, look.x, 0f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    /// <summary>Gyroscope aiming (off / only while aiming / always).</summary>
    private Vector2 GyroLook(float sens)
    {
        bool want = GameSettings.Gyro == 2 || (GameSettings.Gyro == 1 && aimingDownSights);
        want &= state == PlayerState.Ground && SystemInfo.supportsGyroscope;
        if (Input.gyro.enabled != want && SystemInfo.supportsGyroscope)
            Input.gyro.enabled = want;
        if (!want)
            return Vector2.zero;
        Vector3 r = Input.gyro.rotationRateUnbiased;   // rad/s in device axes (portrait)
        // Landscape: device +x points up the screen, device -y points right.
        float flip = Screen.orientation == ScreenOrientation.LandscapeRight ? -1f : 1f;
        float k = Mathf.Rad2Deg * Time.deltaTime * GameSettings.GyroSensitivity * Mathf.Lerp(0.6f, 1f, sens) * (aimingDownSights ? ZoomFov() / 70f : 1f);
        return new Vector2(-r.x * flip, -r.y * flip) * k;
    }

    // ----- Aim assist -----

    /// <summary>Gently pulls the crosshair onto a visible enemy close to it while shooting or aiming.</summary>
    private void AimAssistTick(bool firing)
    {
        if (!GameSettings.AimAssist || !(firing || aimingDownSights) || currentWeapon.weaponData == null)
            return;
        var gm = GameManager.Instance;
        Transform cam = playerCamera.transform;
        IDamageable best = null;
        float bestAngle = aimingDownSights ? 6f : 4f;
        float range = currentWeapon.weaponData.range;
        foreach (var c in gm.Combatants)
        {
            if (c == null || c.IsDead || c.IsAirborne || c.Team == Team)
                continue;
            if (ClassAbility.IsStealthed(c) && Vector3.Distance(c.transform.position, transform.position) > 7f)
                continue;
            Vector3 to = c.AimPoint - cam.position;
            if (to.magnitude > range)
                continue;
            float a = Vector3.Angle(cam.forward, to);
            if (a < bestAngle)
            {
                RaycastHit hit;
                if (Physics.Linecast(cam.position + cam.forward * 0.5f, c.AimPoint, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<IDamageable>() != c)
                    continue;
                bestAngle = a;
                best = c;
            }
        }
        if (best == null)
            return;
        Vector3 dir = best.AimPoint - cam.position;
        Vector3 flat = new Vector3(dir.x, 0f, dir.z);
        Vector3 fwdFlat = new Vector3(cam.forward.x, 0f, cam.forward.z);
        float yawErr = Vector3.SignedAngle(fwdFlat, flat, Vector3.up);
        float pitchErr = -Mathf.Atan2(dir.y, flat.magnitude) * Mathf.Rad2Deg - pitch;
        float strength = Time.deltaTime * (aimingDownSights ? 5f : 3.5f);
        transform.Rotate(0f, yawErr * strength, 0f);
        pitch = Mathf.Clamp(pitch + pitchErr * strength, -60f, 60f);
    }

    /// <summary>For the HUD crosshair: an enemy is right under it.</summary>
    public bool EnemyInSights()
    {
        return state == PlayerState.Ground && currentWeapon != null && currentWeapon.weaponData != null && !isDead && EnemyUnderCrosshair();
    }

    private bool EnemyUnderCrosshair()
    {
        Transform cam = playerCamera.transform;
        float camToPivot = Vector3.Distance(cam.position, cameraPivot.position);
        RaycastHit hit;
        if (!Physics.Raycast(cam.position + cam.forward * camToPivot, cam.forward, out hit, currentWeapon.weaponData.range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;
        var d = hit.collider.GetComponentInParent<IDamageable>();
        return d != null && !d.IsDead && d.Team != Team && !(ClassAbility.IsStealthed(d) && hit.distance > 7f);
    }

    // ----- Aiming, knock-down, skins -----

    private bool gunHiddenForScope;
    private WeaponController scopedWeapon;

    /// <summary>Leaves the scope view and makes the hidden gun visible again.</summary>
    private void ClearScope()
    {
        aimingDownSights = false;
        SetGunVisible(scopedWeapon, true);
        scopedWeapon = null;
        gunHiddenForScope = false;
    }

    private static void SetGunVisible(WeaponController w, bool visible)
    {
        if (w == null)
            return;
        foreach (var r in w.GetComponentsInChildren<Renderer>(true))
        {
            if (r is LineRenderer || r.name == "MuzzleFlash")
                continue;
            r.forceRenderingOff = !visible;
        }
    }

    /// <summary>True while looking through a 3x/6x optic or a sniper scope (full-screen scope view).</summary>
    public bool IsScoped
    {
        get
        {
            if (!aimingDownSights || state != PlayerState.Ground || currentWeapon == null || currentWeapon.weaponData == null)
                return false;
            return currentWeapon.weaponData.weaponType == WeaponType.Sniper || currentWeapon.weaponData.zoomMul < 0.75f;
        }
    }

    private float ZoomFov()
    {
        if (currentWeapon == null || currentWeapon.weaponData == null)
            return 55f;
        return Mathf.Clamp(BaseZoomFov() * currentWeapon.weaponData.zoomMul, 6f, 65f);
    }

    private float BaseZoomFov()
    {
        switch (currentWeapon.weaponData.weaponType)
        {
            case WeaponType.Sniper: return 22f;
            case WeaponType.Rifle: return 45f;
            case WeaponType.SMG: return 55f;
            case WeaponType.Shotgun: return 60f;
            default: return 58f;
        }
    }
}
