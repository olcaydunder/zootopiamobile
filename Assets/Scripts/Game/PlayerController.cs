using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public static PlayerController LocalPlayer;

    public CharacterController controller;
    public Camera playerCamera;
    public Transform cameraPivot;
    public WeaponController currentWeapon;
    public Inventory inventory = new Inventory();

    public float health = 100f;
    public float armor = 50f;
    public float moveSpeed = 5.0f;
    public float sprintSpeed = 8.0f;
    public float crouchSpeed = 3.0f;
    public float jumpHeight = 1.8f;
    public float gravity = -18f;
    public float aimSensitivity = 2f;

    public bool isDead;
    public bool isCrouching;
    public bool isAiming;

    private Vector3 velocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();

        controller.height = 1.8f;
        controller.radius = 0.35f;

        CreatePlayerVisuals();
        SpawnWeapon();

        LocalPlayer = this;
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterPlayer(this);
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.InGame || isDead)
            return;

        HandleInput();
        UpdateMovement();
        UpdateAim();

        if (transform.position.y < -20f)
            TakeDamage(100f, true);
    }

    private void CreatePlayerVisuals()
    {
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "PlayerBody";
        body.transform.SetParent(transform, false);
        body.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        body.GetComponent<Renderer>().material.color = new Color(0.17f, 0.62f, 1f);

        cameraPivot = new GameObject("CameraPivot").transform;
        cameraPivot.SetParent(transform, false);
        cameraPivot.localPosition = new Vector3(0f, 1.5f, 0f);

        if (Camera.main == null)
        {
            var camObj = new GameObject("PlayerCamera");
            camObj.tag = "MainCamera";
            playerCamera = camObj.AddComponent<Camera>();
        }
        else
        {
            playerCamera = Camera.main;
        }

        playerCamera.transform.SetParent(cameraPivot, false);
        playerCamera.transform.localPosition = new Vector3(0.4f, 0.1f, -4.5f);
        playerCamera.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
    }

    private void SpawnWeapon()
    {
        var weaponObj = new GameObject("PlayerWeapon");
        weaponObj.transform.SetParent(cameraPivot, false);
        weaponObj.transform.localPosition = new Vector3(0.7f, -0.2f, 1.0f);

        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        model.transform.SetParent(weaponObj.transform, false);
        model.transform.localScale = new Vector3(0.18f, 0.14f, 1.0f);
        model.GetComponent<Renderer>().material.color = Color.black;

        currentWeapon = weaponObj.AddComponent<WeaponController>();
        currentWeapon.Initialize(WeaponData.CreateRifle());
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) || GameManager.Instance.touchJump)
        {
            if (controller.isGrounded)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        if (Input.GetKeyDown(KeyCode.LeftControl) || GameManager.Instance.touchCrouch)
            isCrouching = !isCrouching;

        if (Input.GetMouseButton(0) || GameManager.Instance.touchFire)
        {
            var origin = playerCamera.transform.position;
            var direction = playerCamera.transform.forward;
            currentWeapon.TryFire(origin, direction, out var hit);
        }

        if (Input.GetKeyDown(KeyCode.R) || GameManager.Instance.touchReload)
            currentWeapon.Reload();

        if (Input.GetKeyDown(KeyCode.X))
            UseMedkit();
    }

    private void UpdateMovement()
    {
        Vector2 input = GameManager.Instance.touchMove;
        if (input == Vector2.zero)
            input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        Vector3 move = transform.right * input.x + transform.forward * input.y;
        move = Vector3.ClampMagnitude(move, 1f);

        bool sprinting = Input.GetKey(KeyCode.LeftShift) || GameManager.Instance.touchSprint;
        float speed = isCrouching ? crouchSpeed : (sprinting ? sprintSpeed : moveSpeed);

        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -1f;

        velocity.y += gravity * Time.deltaTime;
        controller.Move(move * speed * Time.deltaTime);
        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateAim()
    {
        float mouseX = Input.GetAxis("Mouse X") * aimSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * aimSensitivity;

        if (mouseX != 0f || mouseY != 0f)
        {
            transform.Rotate(0f, mouseX, 0f);
            Vector3 rot = playerCamera.transform.localEulerAngles;
            rot.x -= mouseY;
            rot.x = Mathf.Clamp(rot.x, -70f, 70f);
            playerCamera.transform.localEulerAngles = rot;
        }
    }

    public void TakeDamage(float amount, bool armorFirst)
    {
        if (isDead)
            return;

        if (armorFirst && armor > 0f)
        {
            float absorbed = Mathf.Min(armor, amount * 0.6f);
            armor -= absorbed;
            amount -= absorbed;
        }

        health -= amount;
        if (health <= 0f)
        {
            health = 0f;
            Die();
        }
    }

    public void UseMedkit()
    {
        if (inventory.medkits <= 0)
            return;

        inventory.medkits--;
        health = Mathf.Min(100f, health + 35f);
    }

    public void Die()
    {
        isDead = true;
        GameManager.Instance.OnPlayerEliminated(this);
    }

    public void Respawn(Vector3 spawnPosition)
    {
        transform.position = spawnPosition;
        health = 100f;
        armor = 50f;
        velocity = Vector3.zero;
        isDead = false;
    }
}
