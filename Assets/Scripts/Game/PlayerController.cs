using UnityEngine;

public class PlayerController : MonoBehaviour, IDamageable
{
    public static PlayerController LocalPlayer;

    public const int IgnoreRaycastLayer = 2;
    private const float CameraDistance = 3.6f;

    public CharacterController controller;
    public Camera playerCamera;
    public Transform cameraPivot;
    public WeaponController currentWeapon;
    public Inventory inventory = new Inventory();

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

    private Vector3 velocity;
    private float pitch;
    private Transform body;
    private Renderer weaponModel;

    public int Team { get { return 0; } }
    public bool IsDead { get { return isDead; } }
    public string DisplayName { get { return GameManager.Instance != null ? GameManager.Instance.profile.playerName : "Oyuncu"; } }
    public Vector3 AimPoint { get { return transform.position + Vector3.up * 0.4f; } }

    private void Awake()
    {
        LocalPlayer = this;

        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = Vector3.zero;

        CreateVisuals();
        CreateCamera();
        CreateWeapon();

        SetLayerRecursively(gameObject, IgnoreRaycastLayer);
    }

    private void CreateVisuals()
    {
        var bodyObj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        bodyObj.name = "PlayerBody";
        DestroyImmediate(bodyObj.GetComponent<Collider>());
        bodyObj.transform.SetParent(transform, false);
        bodyObj.transform.localScale = new Vector3(0.75f, 0.9f, 0.75f);
        bodyObj.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.17f, 0.55f, 1f));
        body = bodyObj.transform;

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        DestroyImmediate(head.GetComponent<Collider>());
        head.transform.SetParent(body, false);
        head.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        head.transform.localScale = new Vector3(0.75f, 0.6f, 0.75f);
        head.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.95f, 0.8f, 0.65f));

        var backpack = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backpack.name = "Backpack";
        DestroyImmediate(backpack.GetComponent<Collider>());
        backpack.transform.SetParent(body, false);
        backpack.transform.localPosition = new Vector3(0f, 0.2f, -0.45f);
        backpack.transform.localScale = new Vector3(0.7f, 0.6f, 0.3f);
        backpack.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.35f, 0.3f, 0.2f));
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

        playerCamera.clearFlags = CameraClearFlags.SolidColor;
        playerCamera.backgroundColor = new Color(0.55f, 0.76f, 0.93f);
        playerCamera.nearClipPlane = 0.1f;
        playerCamera.farClipPlane = 250f;
        playerCamera.fieldOfView = 70f;

        playerCamera.transform.SetParent(cameraPivot, false);
        playerCamera.transform.localPosition = new Vector3(0.55f, 0.35f, -CameraDistance);
        playerCamera.transform.localRotation = Quaternion.identity;
    }

    private void CreateWeapon()
    {
        var weaponObj = new GameObject("PlayerWeapon");
        weaponObj.transform.SetParent(cameraPivot, false);
        weaponObj.transform.localPosition = new Vector3(0.35f, -0.25f, 0.55f);

        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        DestroyImmediate(model.GetComponent<Collider>());
        model.transform.SetParent(weaponObj.transform, false);
        model.transform.localScale = new Vector3(0.12f, 0.14f, 0.8f);
        weaponModel = model.GetComponent<Renderer>();

        currentWeapon = weaponObj.AddComponent<WeaponController>();
        currentWeapon.Initialize(WeaponData.CreatePistol(), weaponModel);
    }

    public void EquipWeapon(WeaponData data)
    {
        currentWeapon.Initialize(data, weaponModel);
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.currentState != GameState.InGame || isDead)
            return;

        var tc = TouchControls.Instance;
        HandleLook(tc);
        HandleActions(gm, tc);
        HandleMovement(tc);

        if (gm.lootSystem != null)
            gm.lootSystem.TryCollectLoot(this);

        if (transform.position.y < -15f)
            TakeDamage(9999f, -1);
    }

    private void HandleLook(TouchControls tc)
    {
        Vector2 look = Vector2.zero;
        if (tc != null)
            look += tc.LookDelta * touchLookSensitivity;

        // Desktop testing: hold right mouse button to look around.
        if (!Application.isMobilePlatform && Input.GetMouseButton(1))
            look += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseSensitivity;

        transform.Rotate(0f, look.x, 0f);
        pitch = Mathf.Clamp(pitch - look.y, -60f, 60f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleActions(GameManager gm, TouchControls tc)
    {
        bool jump = Input.GetKeyDown(KeyCode.Space) || (tc != null && tc.ConsumeJump());
        bool crouch = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C) || (tc != null && tc.ConsumeCrouch());
        bool reload = Input.GetKeyDown(KeyCode.R) || (tc != null && tc.ConsumeReload());
        bool medkit = Input.GetKeyDown(KeyCode.X) || (tc != null && tc.ConsumeMedkit());
        bool fire = (!Application.isMobilePlatform && Input.GetMouseButton(0)) || (tc != null && tc.FireHeld);

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

        if (fire)
        {
            Transform cam = playerCamera.transform;
            // Start the ray level with the character so walls behind the player don't block it.
            Vector3 origin = cam.position + cam.forward * CameraDistance;
            float extraSpread = isSprinting ? 2.5f : 0f;
            bool killed;
            if (currentWeapon.TryFire(origin, cam.forward, Team, Physics.DefaultRaycastLayers, extraSpread, out killed) && killed)
                gm.OnPlayerKill();
        }
    }

    private void HandleMovement(TouchControls tc)
    {
        Vector2 input = tc != null ? tc.Move : Vector2.zero;
        if (input.sqrMagnitude < 0.01f)
            input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        bool sprintInput = Input.GetKey(KeyCode.LeftShift) || (tc != null && tc.SprintOn);
        isSprinting = sprintInput && input.y > 0.4f && !isCrouching;

        Vector3 move = transform.right * input.x + transform.forward * input.y;
        move = Vector3.ClampMagnitude(move, 1f);

        float speed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : moveSpeed);

        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;

        controller.Move((move * speed + Vector3.up * velocity.y) * Time.deltaTime);
    }

    private void SetCrouch(bool crouched)
    {
        isCrouching = crouched;
        controller.height = crouched ? 1.2f : 1.8f;
        controller.center = crouched ? new Vector3(0f, -0.3f, 0f) : Vector3.zero;
        body.localScale = crouched ? new Vector3(0.75f, 0.6f, 0.75f) : new Vector3(0.75f, 0.9f, 0.75f);
        body.localPosition = crouched ? new Vector3(0f, -0.3f, 0f) : Vector3.zero;
        cameraPivot.localPosition = crouched ? new Vector3(0f, 0.25f, 0f) : new Vector3(0f, 0.75f, 0f);
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
        return true;
    }

    public bool TakeDamage(float amount, int attackerTeam)
    {
        if (isDead)
            return false;

        // Zone damage (attackerTeam -1) ignores armor.
        if (attackerTeam >= 0 && armor > 0f)
        {
            float absorbed = Mathf.Min(armor, amount * 0.5f);
            armor -= absorbed;
            amount -= absorbed;
        }

        health -= amount;

        var gm = GameManager.Instance;
        if (gm != null && gm.uiManager != null && attackerTeam >= 0)
            gm.uiManager.FlashDamage();

        if (health <= 0f)
        {
            health = 0f;
            isDead = true;
            if (gm != null)
                gm.OnPlayerEliminated();
            return true;
        }
        return false;
    }

    public void ResetForRound(Vector3 spawnPosition)
    {
        controller.enabled = false;
        transform.position = spawnPosition;
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        controller.enabled = true;

        health = maxHealth;
        armor = 0f;
        kills = 0;
        velocity = Vector3.zero;
        pitch = 0f;
        isDead = false;
        isSprinting = false;
        SetCrouch(false);
        inventory.Reset();
        EquipWeapon(WeaponData.CreatePistol());
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
