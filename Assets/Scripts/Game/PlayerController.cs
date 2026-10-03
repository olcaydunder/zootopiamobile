using UnityEngine;

public enum PlayerState
{
    Ground,
    Plane,
    Freefall,
    Parachute,
    Driving
}

public class PlayerController : MonoBehaviour, IDamageable
{
    public static PlayerController LocalPlayer;

    public const int IgnoreRaycastLayer = 2;

    public CharacterController controller;
    public Camera playerCamera;
    public Transform cameraPivot;
    public WeaponController currentWeapon;
    public CharacterRig rig;
    public Inventory inventory = new Inventory();
    public PlayerState state = PlayerState.Ground;
    public Vehicle vehicle;

    public float health = 100f;
    public float maxHealth = 100f;
    public float armor;
    public float maxArmor = 100f;

    public float moveSpeed = 5f;
    public float sprintSpeed = 7.5f;
    public float crouchSpeed = 2.6f;
    public float jumpHeight = 1.3f;
    public float gravity = -20f;
    public float touchLookSensitivity = 0.16f;
    public float mouseSensitivity = 2.5f;

    public bool isDead;
    public bool isCrouching;
    public bool isSprinting;
    public int kills;

    private readonly WeaponSlot[] slots = { new WeaponSlot(), new WeaponSlot() };
    private int activeSlot;

    private Vector3 velocity;
    private Vector3 airVelocity;
    private float pitch;
    private float lookYaw;          // free look while driving
    private Renderer weaponModel;
    private AirPlane plane;
    private AudioSource wind;
    private float camDistance = 3.6f;
    private float camTarget = 3.6f;
    private float shake;
    private float stepDistance;
    private float boostRemaining;
    private float lastFireTime = -10f;

    // Aim down sights, knock-down and damage direction
    public bool aimingDownSights;
    public bool isDowned;
    public float reviveProgress;
    public const float ReviveTime = 5f;
    private Vector3 lastHitFrom;
    private bool lastHitHasSource;
    private string currentSkin = ModelLibrary.PlayerSkin;

    public int Team { get { return 0; } }
    public bool IsDead { get { return isDead; } }
    public bool IsAirborne { get { return state == PlayerState.Plane || state == PlayerState.Freefall || state == PlayerState.Parachute; } }
    public string DisplayName { get { return GameManager.Instance != null ? GameManager.Instance.profile.playerName : "Oyuncu"; } }
    public Vector3 AimPoint { get { return transform.position + controller.center + Vector3.up * (controller.height * 0.5f - 0.5f); } }

    public string ActiveWeaponName { get { return currentWeapon.weaponData != null ? currentWeapon.weaponData.weaponName : ""; } }
    public string OtherWeaponName { get { var s = slots[1 - activeSlot]; return s.data != null ? s.data.weaponName : ""; } }
    public bool HasOtherWeapon { get { return slots[1 - activeSlot].data != null; } }
    public float HeightAboveGround { get; private set; }
    public float BoostRemaining { get { return boostRemaining; } }

    private void Awake()
    {
        LocalPlayer = this;

        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = Vector3.zero;
        controller.slopeLimit = 50f;

        rig = CharacterRig.Build(gameObject, new Color(0.2f, 0.45f, 0.85f), new Color(0.25f, 0.27f, 0.3f),
            new Color(0.93f, 0.78f, 0.63f), new Color(0.32f, 0.38f, 0.26f), new Color(0.42f, 0.34f, 0.22f), ModelLibrary.PlayerSkin);

        CreateCamera();
        CreateWeapon();
        rig.weaponHold = currentWeapon.transform;
        rig.aimReference = cameraPivot;

        wind = Sfx.CreateLoop(transform, SoundBank.WindLoop, 0.5f, false);

        SetLayerRecursively(gameObject, IgnoreRaycastLayer);
    }

    private void CreateCamera()
    {
        cameraPivot = new GameObject("CameraPivot").transform;
        cameraPivot.SetParent(transform, false);
        cameraPivot.localPosition = new Vector3(0f, 0.75f, 0f);

        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            var camObj = new GameObject("PlayerCamera");
            camObj.tag = "MainCamera";
            playerCamera = camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }

        if (RenderSettings.skybox != null)
        {
            playerCamera.clearFlags = CameraClearFlags.Skybox;
        }
        else
        {
            playerCamera.clearFlags = CameraClearFlags.SolidColor;
            playerCamera.backgroundColor = World.SkyHorizon;
        }
        playerCamera.nearClipPlane = 0.1f;
        playerCamera.farClipPlane = 450f;
        playerCamera.fieldOfView = 70f;

        playerCamera.transform.SetParent(cameraPivot, false);
        playerCamera.transform.localPosition = new Vector3(0.55f, 0.35f, -camDistance);
        playerCamera.transform.localRotation = Quaternion.identity;
    }

    private void CreateWeapon()
    {
        var weaponObj = new GameObject("PlayerWeapon");
        weaponObj.transform.SetParent(cameraPivot, false);
        weaponObj.transform.localPosition = new Vector3(0.32f, -0.22f, 0.55f);

        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        DestroyImmediate(model.GetComponent<Collider>());
        model.transform.SetParent(weaponObj.transform, false);
        weaponModel = model.GetComponent<Renderer>();

        currentWeapon = weaponObj.AddComponent<WeaponController>();
        currentWeapon.playerOwned = true;
        currentWeapon.Initialize(WeaponData.CreatePistol(), weaponModel);
    }

    // ----- Weapons & items -----

    private void StoreActive()
    {
        slots[activeSlot].data = currentWeapon.weaponData;
        slots[activeSlot].ammo = currentWeapon.currentAmmo;
        slots[activeSlot].reserve = currentWeapon.reserveAmmo;
    }

    private void LoadSlot(int index)
    {
        activeSlot = index;
        var s = slots[index];
        currentWeapon.Initialize(s.data, weaponModel, s.ammo, s.reserve);
    }

    public void SwapWeapon()
    {
        if (!HasOtherWeapon)
            return;
        StoreActive();
        LoadSlot(1 - activeSlot);
        Sfx.Play(SoundBank.Reload, 0.35f, 1.4f);
    }

    /// <summary>Picks up a weapon from loot. Returns the message to show.</summary>
    public string GiveWeapon(WeaponData found)
    {
        found = Gunsmith.Apply(found);
        StoreActive();
        int other = 1 - activeSlot;
        int ammo = found.magazineSize * 2;

        for (int i = 0; i < 2; i++)
        {
            if (slots[i].data != null && slots[i].data.weaponType == found.weaponType)
            {
                AddAmmoToSlot(i, ammo);
                return "+" + ammo + " mermi (" + slots[i].data.weaponName + ")";
            }
        }

        if (slots[other].data == null)
        {
            slots[other].data = found;
            slots[other].ammo = found.magazineSize;
            slots[other].reserve = found.reserveAmmo;
            LoadSlot(other);
            return found.weaponName + " alındı";
        }

        int weaker = WeaponData.Tier(slots[0].data.weaponType) <= WeaponData.Tier(slots[1].data.weaponType) ? 0 : 1;
        if (WeaponData.Tier(found.weaponType) > WeaponData.Tier(slots[weaker].data.weaponType))
        {
            slots[weaker].data = found;
            slots[weaker].ammo = found.magazineSize;
            slots[weaker].reserve = found.reserveAmmo;
            LoadSlot(weaker);
            return found.weaponName + " alındı";
        }

        AddAmmoToSlot(activeSlot, ammo);
        return "+" + ammo + " mermi";
    }

    public string GiveAmmo()
    {
        int amount = currentWeapon.weaponData != null ? currentWeapon.weaponData.magazineSize * 2 : 30;
        currentWeapon.AddAmmo(amount);
        return "+" + amount + " mermi";
    }

    private void AddAmmoToSlot(int index, int amount)
    {
        if (index == activeSlot)
            currentWeapon.AddAmmo(amount);
        else
            slots[index].reserve += amount;
    }

    private void ThrowGrenade()
    {
        var ui = GameManager.Instance.uiManager;
        if (inventory.grenades <= 0)
        {
            ui.Toast("El bomban yok");
            return;
        }
        inventory.grenades--;
        Transform cam = playerCamera.transform;
        Vector3 origin = cameraPivot.position + cam.forward * 0.9f + Vector3.up * 0.2f;
        Grenade.Throw(origin, cam.forward * 17f + Vector3.up * 5f, this);
        Sfx.Play(SoundBank.Whoosh, 0.4f, 1.3f);
    }

    private void UseDrink()
    {
        var ui = GameManager.Instance.uiManager;
        if (inventory.drinks <= 0)
        {
            ui.Toast("Enerji içeceğin yok");
            return;
        }
        if (health >= maxHealth)
        {
            ui.Toast("Canın zaten dolu");
            return;
        }
        inventory.drinks--;
        boostRemaining += 30f;
        ui.Toast("Enerji: +30 can (yavaşça)");
        Sfx.Play(SoundBank.Pickup, 0.4f, 0.8f);
    }

    public bool UseMedkit()
    {
        var ui = GameManager.Instance != null ? GameManager.Instance.uiManager : null;

        if (inventory.medkits <= 0)
        {
            if (ui != null) ui.Toast("İlk yardım çantan yok");
            return false;
        }
        if (health >= maxHealth)
        {
            if (ui != null) ui.Toast("Canın zaten dolu");
            return false;
        }

        inventory.medkits--;
        health = Mathf.Min(maxHealth, health + 40f);
        if (ui != null) ui.Toast("+40 can");
        Sfx.Play(SoundBank.Pickup, 0.4f, 0.7f);
        return true;
    }

    // ----- Frame update -----

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.currentState != GameState.InGame || isDead)
            return;
        if (Time.timeScale == 0f)
            return;   // paused

        var tc = TouchControls.Instance;
        HandleLook(tc);

        if (isDowned)
        {
            UpdateDowned(gm, tc);
            return;
        }

        switch (state)
        {
            case PlayerState.Plane:
                UpdatePlane(tc);
                break;
            case PlayerState.Freefall:
            case PlayerState.Parachute:
                UpdateAir(tc);
                break;
            case PlayerState.Driving:
                UpdateDriving(gm, tc);
                break;
            default:
                HandleActions(gm, tc);
                HandleMovement(tc);
                if (gm.lootSystem != null)
                    gm.lootSystem.TryCollectLoot(this);
                if (transform.position.y < -15f)
                    TakeDamage(9999f, -1);
                break;
        }

        if (boostRemaining > 0f && health < maxHealth)
        {
            float heal = Mathf.Min(boostRemaining, 3f * Time.deltaTime);
            boostRemaining -= heal;
            health = Mathf.Min(maxHealth, health + heal);
        }

        rig.aimPitch = pitch;
        rig.aiming = Time.time - lastFireTime < 1.2f;
    }

    // ----- Lobby showcase -----

    public bool lobbyView;

    public void SetLobbyView(bool on)
    {
        lobbyView = on;
        ClearScope();
        if (on)
            cameraPivot.localRotation = Quaternion.Euler(28f, -50f, 0f);   // gun held low across the body
        if (!on)
        {
            playerCamera.transform.localRotation = Quaternion.identity;
            playerCamera.fieldOfView = 70f;
        }
    }

    /// <summary>Shows a weapon in the character's hands in the lobby (does not change the round loadout).</summary>
    public void ShowcaseWeapon(WeaponData data)
    {
        currentWeapon.gameObject.SetActive(true);
        currentWeapon.Initialize(data, weaponModel);
    }

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
        float wantedFov = aimingDownSights ? ZoomFov() : 70f;
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
        shake = Mathf.Max(shake, amount);
    }

    private void HandleLook(TouchControls tc)
    {
        Vector2 look = Vector2.zero;
        float sens = IsScoped ? GameSettings.ScopeSensitivity : (aimingDownSights ? GameSettings.AdsSensitivity : GameSettings.Sensitivity);
        if (tc != null)
        {
            Vector2 d = tc.LookDelta;
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
        if (!Application.isMobilePlatform && Input.GetMouseButton(1))
            look += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseSensitivity;

        pitch = Mathf.Clamp(pitch - look.y, -60f, state == PlayerState.Ground ? 60f : 80f);

        if (state == PlayerState.Driving)
        {
            lookYaw += look.x;
            if (Mathf.Abs(look.x) < 0.01f)
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
        return new Vector2(-r.x * flip, r.y * flip) * k;
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

    private bool EnemyUnderCrosshair()
    {
        Transform cam = playerCamera.transform;
        float camToPivot = Vector3.Distance(cam.position, cameraPivot.position);
        RaycastHit hit;
        if (!Physics.Raycast(cam.position + cam.forward * camToPivot, cam.forward, out hit, currentWeapon.weaponData.range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;
        var d = hit.collider.GetComponentInParent<IDamageable>();
        return d != null && !d.IsDead && d.Team != Team;
    }

    private bool adsFromFire;

    private Vector2 MoveInput(TouchControls tc)
    {
        Vector2 input = tc != null ? tc.Move : Vector2.zero;
        if (input.sqrMagnitude < 0.01f)
            input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        return Vector2.ClampMagnitude(input, 1f);
    }

    private void HandleActions(GameManager gm, TouchControls tc)
    {
        bool jump = Input.GetKeyDown(KeyCode.Space) || (tc != null && tc.ConsumeJump());
        bool crouch = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C) || (tc != null && tc.ConsumeCrouch());
        bool reload = Input.GetKeyDown(KeyCode.R) || (tc != null && tc.ConsumeReload());
        bool medkit = Input.GetKeyDown(KeyCode.X) || (tc != null && tc.ConsumeMedkit());
        bool drink = Input.GetKeyDown(KeyCode.V) || (tc != null && tc.ConsumeDrink());
        bool grenade = Input.GetKeyDown(KeyCode.G) || (tc != null && tc.ConsumeGrenade());
        bool swap = Input.GetKeyDown(KeyCode.Q) || (tc != null && tc.ConsumeSwap());
        bool useVehicle = Input.GetKeyDown(KeyCode.F) || (tc != null && tc.ConsumeVehicle());
        bool fire = (!Application.isMobilePlatform && Input.GetMouseButton(0)) || (tc != null && tc.FireHeld);
        bool aim = Input.GetKeyDown(KeyCode.E) || (tc != null && tc.ConsumeAim());

        if (aim)
        {
            aimingDownSights = !aimingDownSights;
            adsFromFire = false;
        }

        // Fire mode from the settings: tap to aim (ADS while the button is held), hip fire, or automatic.
        int fireMode = currentWeapon.weaponData != null ? GameSettings.FireModeFor(currentWeapon.weaponData.weaponType) : 1;
        if (fireMode == 2 && currentWeapon.weaponData != null && !isSprinting && EnemyUnderCrosshair())
            fire = true;
        if (fireMode == 0 && fire && !aimingDownSights && !isSprinting)
        {
            aimingDownSights = true;
            adsFromFire = true;
        }
        else if (adsFromFire && !fire)
        {
            aimingDownSights = false;
            adsFromFire = false;
        }
        if (isSprinting)
            aimingDownSights = false;
        AimAssistTick(fire);

        if (jump && controller.isGrounded)
        {
            if (isCrouching)
                SetCrouch(false);
            else
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        if (crouch)
            SetCrouch(!isCrouching);
        if (reload)
            currentWeapon.Reload();
        if (medkit)
            UseMedkit();
        if (drink)
            UseDrink();
        if (grenade)
            ThrowGrenade();
        if (swap)
        {
            SwapWeapon();
            aimingDownSights = false;
        }
        if (useVehicle)
        {
            Vehicle near = gm.NearestVehicle(transform.position, 4.5f);
            if (near != null)
            {
                EnterVehicle(near);
                return;
            }
        }

        if (fire)
        {
            Transform cam = playerCamera.transform;
            float camToPivot = Vector3.Distance(cam.position, cameraPivot.position);
            Vector3 origin = cam.position + cam.forward * camToPivot;
            float extraSpread = isSprinting ? 2.5f : (isCrouching ? 0f : 0.3f);
            if (aimingDownSights)
                extraSpread = -currentWeapon.weaponData.spread * 0.65f;   // much tighter when aiming
            bool killed;
            if (currentWeapon.TryFire(origin, cam.forward, Team, Physics.DefaultRaycastLayers, extraSpread, out killed))
            {
                lastFireTime = Time.time;
                pitch -= currentWeapon.weaponData.Recoil;
                transform.Rotate(0f, Random.Range(-0.3f, 0.3f) * currentWeapon.weaponData.Recoil, 0f);
                Shake(0.08f + currentWeapon.weaponData.Recoil * 0.04f);
                if (killed)
                    gm.OnPlayerKill();
            }
        }
    }

    private void HandleMovement(TouchControls tc)
    {
        Vector2 input = MoveInput(tc);

        bool sprintInput = Input.GetKey(KeyCode.LeftShift) || (tc != null && tc.SprintOn);
        isSprinting = sprintInput && input.y > 0.4f && !isCrouching;

        Vector3 move = transform.right * input.x + transform.forward * input.y;
        move = Vector3.ClampMagnitude(move, 1f);

        float speed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : moveSpeed);
        if (currentWeapon != null && currentWeapon.weaponData != null)
            speed *= Mathf.Clamp(0.85f + 0.15f * currentWeapon.weaponData.mobilityMul, 0.75f, 1.15f);
        if (aimingDownSights)
            speed *= 0.6f;

        // Shallow water only: stop before wading into deep sea.
        Vector3 ahead = transform.position + move * 1.5f;
        if (move.sqrMagnitude > 0.01f && World.HeightAt(ahead.x, ahead.z) < -1.2f &&
            World.HeightAt(ahead.x, ahead.z) < World.HeightAt(transform.position.x, transform.position.z))
            move = Vector3.zero;

        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;

        controller.Move((move * speed + Vector3.up * velocity.y) * Time.deltaTime);

        if (controller.isGrounded && move.sqrMagnitude > 0.05f)
        {
            stepDistance += move.magnitude * speed * Time.deltaTime;
            float stride = isSprinting ? 2.6f : 2.1f;
            if (stepDistance > stride)
            {
                stepDistance = 0f;
                Sfx.Play(SoundBank.Footstep, isCrouching ? 0.08f : 0.22f, Random.Range(0.85f, 1.1f));
            }
        }
        rig.crouched = isCrouching;
    }

    private void SetCrouch(bool crouched)
    {
        isCrouching = crouched;
        controller.height = crouched ? 1.2f : 1.8f;
        controller.center = crouched ? new Vector3(0f, -0.3f, 0f) : Vector3.zero;
        cameraPivot.localPosition = crouched ? new Vector3(0f, 0.25f, 0f) : new Vector3(0f, 0.75f, 0f);
        rig.crouched = crouched;
    }

    // ----- Plane / skydive / parachute -----

    public void BoardPlane(AirPlane dropPlane)
    {
        plane = dropPlane;
        state = PlayerState.Plane;
        controller.enabled = false;
        rig.SetVisible(false);
        currentWeapon.gameObject.SetActive(false);
        camTarget = 22f;
        camDistance = 22f;
        pitch = 15f;
        transform.position = plane.transform.position;
        transform.rotation = Quaternion.LookRotation(plane.Direction);
    }

    private void UpdatePlane(TouchControls tc)
    {
        if (plane == null)
        {
            Jump();
            return;
        }
        transform.position = plane.transform.position - Vector3.up * 1.5f;
        HeightAboveGround = transform.position.y - World.HeightAt(transform.position.x, transform.position.z);

        bool jump = Input.GetKeyDown(KeyCode.Space) || (tc != null && tc.ConsumeAirAction());
        if (jump || plane.Finished)
            Jump();
    }

    private void Jump()
    {
        Vector3 forward = plane != null ? plane.Direction : transform.forward;
        if (plane != null)
            transform.position = plane.transform.position - Vector3.up * 4f;
        state = PlayerState.Freefall;
        airVelocity = forward * 12f;
        rig.SetVisible(true);
        rig.pose = RigPose.Freefall;
        camTarget = 6f;
        pitch = 35f;
        wind.Play();
        Sfx.Play(SoundBank.Whoosh, 0.6f);

        var gm = GameManager.Instance;
        if (gm != null)
            gm.OnPlayerJumped();
    }

    private void OpenParachute()
    {
        state = PlayerState.Parachute;
        rig.pose = RigPose.Parachute;
        camTarget = 7f;
        wind.volume = 0.2f;
        Sfx.Play(SoundBank.Whoosh, 0.5f, 0.6f);
    }

    private void UpdateAir(TouchControls tc)
    {
        float dt = Time.deltaTime;
        Vector2 input = MoveInput(tc);
        Vector3 fwd = transform.forward;
        Vector3 right = transform.right;
        bool action = Input.GetKeyDown(KeyCode.Space) || (tc != null && tc.ConsumeAirAction());

        float ground = World.GroundHeight(transform.position.x, transform.position.z);
        HeightAboveGround = transform.position.y - ground;

        Vector3 target;
        if (state == PlayerState.Freefall)
        {
            float dive = input.y > 0.5f ? -40f : -30f;
            target = fwd * (input.y * 22f) + right * (input.x * 12f) + Vector3.up * dive;
            if (action || HeightAboveGround < 45f)
                OpenParachute();
        }
        else
        {
            target = fwd * (5f + input.y * 5f) + right * (input.x * 5f) + Vector3.up * (input.y < -0.5f ? -4f : -6.5f);
        }

        // Over the sea and getting low: drift back toward land so nobody lands in the water.
        if (HeightAboveGround < 60f && World.HeightAt(transform.position.x, transform.position.z) < 0.5f)
        {
            Vector3 toCentre = -new Vector3(transform.position.x, 0f, transform.position.z).normalized;
            target += toCentre * 9f;
        }

        airVelocity = Vector3.Lerp(airVelocity, target, dt * 2f);
        Vector3 next = transform.position + airVelocity * dt;

        // Stay above the sea area around the island.
        Vector3 flat = new Vector3(next.x, 0f, next.z);
        if (flat.magnitude > World.MapSize * 0.5f - 5f)
        {
            flat = flat.normalized * (World.MapSize * 0.5f - 5f);
            next = new Vector3(flat.x, next.y, flat.z);
        }

        float landHeight = World.GroundHeight(next.x, next.z);
        if (next.y - 0.95f <= landHeight)
        {
            Land(new Vector3(next.x, landHeight + 0.95f, next.z));
            return;
        }
        transform.position = next;
        wind.pitch = state == PlayerState.Freefall ? 1.2f : 0.8f;
    }

    private void Land(Vector3 position)
    {
        if (World.HeightAt(position.x, position.z) < 0f)
            position = World.RandomOpenPoint(new Vector3(position.x, 0f, position.z).normalized * (World.IslandRadius - 15f), 8f);
        transform.position = position;
        state = PlayerState.Ground;
        controller.enabled = true;
        velocity = Vector3.zero;
        rig.pose = RigPose.Normal;
        currentWeapon.gameObject.SetActive(true);
        camTarget = 3.6f;
        pitch = 5f;
        wind.Stop();
        wind.volume = 0.5f;
        Effects.Dust(position - Vector3.up * 0.9f, 10);
        Sfx.Play(SoundBank.Land, 0.6f);
        HeightAboveGround = 0f;
    }

    // ----- Vehicles -----

    private void EnterVehicle(Vehicle v)
    {
        vehicle = v;
        v.SetDriver(this);
        state = PlayerState.Driving;
        controller.enabled = false;
        if (isCrouching)
            SetCrouch(false);
        rig.pose = RigPose.Driving;
        currentWeapon.gameObject.SetActive(false);
        camTarget = 7f;
        lookYaw = 0f;
    }

    private void ExitVehicle()
    {
        if (vehicle == null)
            return;
        transform.position = vehicle.ExitPosition();
        transform.rotation = Quaternion.Euler(0f, vehicle.Yaw, 0f);
        vehicle.SetDriver(null);
        vehicle = null;
        state = PlayerState.Ground;
        controller.enabled = true;
        rig.pose = RigPose.Normal;
        currentWeapon.gameObject.SetActive(true);
        camTarget = 3.6f;
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void UpdateDriving(GameManager gm, TouchControls tc)
    {
        if (vehicle == null)
        {
            state = PlayerState.Ground;
            controller.enabled = true;
            return;
        }

        bool exit = Input.GetKeyDown(KeyCode.F) || (tc != null && tc.ConsumeVehicle());
        if (exit)
        {
            ExitVehicle();
            return;
        }

        vehicle.Drive(MoveInput(tc), Time.deltaTime);
        transform.position = vehicle.SeatPosition;
        transform.rotation = Quaternion.Euler(0f, vehicle.Yaw, 0f);

        if (vehicle.transform.position.y < -5f)
            TakeDamage(9999f, -1);
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
        return Mathf.Clamp(BaseZoomFov() * currentWeapon.weaponData.zoomMul, 10f, 65f);
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

    /// <summary>Called by weapons and grenades just before damaging the player, for the hit indicator.</summary>
    public void MarkHitFrom(Vector3 source)
    {
        lastHitFrom = source;
        lastHitHasSource = true;
    }

    private void GoDown()
    {
        isDowned = true;
        health = maxHealth;            // now bleed-out health
        boostRemaining = 0f;
        reviveProgress = 0f;
        aimingDownSights = false;
        if (state == PlayerState.Driving)
            ExitVehicle();
        if (!isCrouching)
            SetCrouch(true);
        currentWeapon.gameObject.SetActive(false);
        var gm = GameManager.Instance;
        if (gm != null && gm.uiManager != null)
            gm.uiManager.Toast("Yere düştün! Takım arkadaşın seni kaldıracak");
    }

    private void UpdateDowned(GameManager gm, TouchControls tc)
    {
        // Bleed out slowly; crawl at walking-pace / 4.
        health -= 4f * Time.deltaTime;
        if (health <= 0f || gm.AliveAllies() == 0 || transform.position.y < -15f)
        {
            health = 0f;
            isDowned = false;
            isDead = true;
            rig.pose = RigPose.Dead;
            gm.OnPlayerEliminated();
            return;
        }

        Vector2 input = MoveInput(tc);
        Vector3 move = Vector3.ClampMagnitude(transform.right * input.x + transform.forward * input.y, 1f);
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        controller.Move((move * 1.2f + Vector3.up * velocity.y) * Time.deltaTime);
        rig.crouched = true;
        rig.aiming = false;
    }

    /// <summary>Teammate bots call this every frame while standing next to the downed player.</summary>
    public void ReviveTick(float dt)
    {
        if (!isDowned || isDead)
            return;
        reviveProgress += dt;
        if (reviveProgress >= ReviveTime)
        {
            isDowned = false;
            reviveProgress = 0f;
            health = 30f;
            SetCrouch(false);
            currentWeapon.gameObject.SetActive(true);
            var gm = GameManager.Instance;
            if (gm != null && gm.uiManager != null)
                gm.uiManager.Toast("Kaldırıldın! +30 can");
        }
    }

    public string Skin { get { return currentSkin; } }

    /// <summary>Swaps the character model (lobby shop).</summary>
    public void ApplySkin(string skin)
    {
        if (skin == currentSkin && rig != null && rig.HasModel)
            return;
        var old = rig;
        rig = CharacterRig.Build(gameObject, new Color(0.2f, 0.45f, 0.85f), new Color(0.25f, 0.27f, 0.3f),
            new Color(0.93f, 0.78f, 0.63f), new Color(0.32f, 0.38f, 0.26f), new Color(0.42f, 0.34f, 0.22f), skin);
        rig.weaponHold = currentWeapon.transform;
        rig.aimReference = cameraPivot;
        rig.crouched = isCrouching;
        if (old != null)
            old.Teardown();
        currentSkin = skin;
        SetLayerRecursively(gameObject, IgnoreRaycastLayer);
    }

    // ----- Damage & round reset -----

    public bool TakeDamage(float amount, int attackerTeam)
    {
        if (isDead || IsAirborne)
        {
            lastHitHasSource = false;
            return false;
        }

        // Zone damage (attackerTeam -1) ignores armor.
        if (attackerTeam >= 0 && armor > 0f)
        {
            float absorbed = Mathf.Min(armor, amount * 0.5f);
            armor -= absorbed;
            amount -= absorbed;
        }

        health -= amount;
        boostRemaining = Mathf.Max(0f, boostRemaining - amount * 0.5f);

        var gm = GameManager.Instance;
        if (gm != null && gm.uiManager != null && attackerTeam >= 0)
        {
            gm.uiManager.FlashDamage();
            Shake(0.15f);
            rig.PlayHit();
            if (lastHitHasSource)
                gm.uiManager.ShowDamageDirection(lastHitFrom);
        }
        lastHitHasSource = false;

        // Duo / Squad: knocked down first while a teammate can still pick you up.
        if (health <= 0f && !isDowned && amount < 9000f && gm != null && gm.TeamSize() > 1 && gm.AliveAllies() > 0)
        {
            GoDown();
            return false;
        }

        if (health <= 0f)
        {
            health = 0f;
            isDead = true;
            currentWeapon.gameObject.SetActive(false);
            if (state == PlayerState.Driving)
                ExitVehicle();
            rig.pose = RigPose.Dead;
            if (gm != null)
                gm.OnPlayerEliminated();
            return true;
        }
        return false;
    }

    public void ResetForRound(Vector3 spawnPosition)
    {
        ClearScope();
        if (vehicle != null)
        {
            vehicle.SetDriver(null);
            vehicle = null;
        }
        plane = null;
        state = PlayerState.Ground;
        controller.enabled = false;
        transform.position = spawnPosition;
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        controller.enabled = true;

        health = maxHealth;
        armor = 0f;
        kills = 0;
        velocity = Vector3.zero;
        airVelocity = Vector3.zero;
        boostRemaining = 0f;
        pitch = 0f;
        isDead = false;
        isDowned = false;
        reviveProgress = 0f;
        aimingDownSights = false;
        isSprinting = false;
        SetCrouch(false);
        inventory.Reset();
        rig.ResetPose();
        wind.Stop();
        camTarget = 3.6f;
        camDistance = 3.6f;
        HeightAboveGround = 0f;

        slots[0].data = Gunsmith.Apply(WeaponData.CreatePistol());
        slots[0].ammo = slots[0].data.magazineSize;
        slots[0].reserve = slots[0].data.reserveAmmo;
        slots[1].data = null;
        currentWeapon.gameObject.SetActive(true);
        LoadSlot(0);
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
