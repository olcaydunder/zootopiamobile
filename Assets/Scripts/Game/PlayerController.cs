using UnityEngine;

public enum PlayerState
{
    Ground,
    Plane,
    Freefall,
    Parachute,
    Driving
}

public partial class PlayerController : MonoBehaviour, IDamageable
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
    /// <summary>Where this player's controls come from (touch/keyboard now; bots or network later).</summary>
    public IPlayerInput InputSource = new LocalPlayerInput();

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
    /// <summary>In the air: plane, skydive, parachute (also the Paraşütçü launch glide). Not targeted, not hit.</summary>
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
        playerCamera.fieldOfView = GameSettings.Fov;

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

    // ----- Frame update -----

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.currentState != GameState.InGame || isDead)
            return;
        if (Time.timeScale == 0f)
            return;   // paused

        var tc = InputSource;
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
                if (!isSwimming)
                    HandleActions(gm, tc);   // no shooting or items while swimming
                if (state != PlayerState.Ground)
                    break;                   // an ability changed state (launch)
                HandleMovement(tc);
                if (gm.lootSystem != null)
                    gm.lootSystem.TryCollectLoot(this);
                if (transform.position.y < -15f)
                    TakeDamage(9999f, -1);
                break;
        }

        if (boostRemaining > 0f && health < maxHealth)
        {
            float rate = Ability != null && Ability.cls == PlayerClass.Medic ? 6f : 3f;   // Sahra Hekimi: drinks work twice as fast
            float heal = Mathf.Min(boostRemaining, rate * Time.deltaTime);
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
            playerCamera.fieldOfView = GameSettings.Fov;
        }
    }

    /// <summary>Shows a weapon in the character's hands in the lobby (does not change the round loadout).</summary>
    public void ShowcaseWeapon(WeaponData data)
    {
        currentWeapon.gameObject.SetActive(true);
        currentWeapon.Initialize(data, weaponModel);
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
            if (state == PlayerState.Driving)
                ExitVehicle();
            if (state == PlayerState.Freefall || state == PlayerState.Parachute)
                Land(new Vector3(transform.position.x, World.GroundHeight(transform.position.x, transform.position.z) + 0.95f, transform.position.z));
            wind.Stop();
            currentWeapon.gameObject.SetActive(false);
            rig.pose = RigPose.Dead;
            Haptics.Long();
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
        isVaulting = false;
        isSwimming = false;
        rig.SetVisible(true);   // hidden while driving a truck, helicopter or tank
        InitAbility();
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
