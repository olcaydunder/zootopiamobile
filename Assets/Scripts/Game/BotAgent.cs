using UnityEngine;

/// <summary>
/// Offline AI opponent / teammate. Wanders inside the safe zone, picks the nearest visible enemy,
/// closes distance and shoots with human-like reaction time and inaccuracy.
/// </summary>
public class BotAgent : MonoBehaviour, IDamageable
{
    public int team;
    public string botName = "Bot";
    public float health = 100f;
    public float armor;
    public float moveSpeed = 4.2f;
    public float detectRange = 45f;
    public bool isDead;

    public CharacterController controller;
    public WeaponController weapon;

    private Renderer bodyRenderer;
    private Renderer weaponModel;
    private IDamageable target;
    private bool targetVisible;
    private float thinkTimer;
    private float nextShotTime;
    private float reactionUntil;
    private float accuracy;
    private float strafeSign = 1f;
    private float strafeTimer;
    private Vector3 wanderTarget;
    private float wanderTimer;
    private float verticalVelocity;
    private const float Gravity = -20f;

    public int Team { get { return team; } }
    public bool IsDead { get { return isDead; } }
    public string DisplayName { get { return botName; } }
    public Vector3 AimPoint { get { return transform.position + Vector3.up * 0.4f; } }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
            controller = gameObject.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = Vector3.zero;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "BotBody";
        DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(transform, false);
        body.transform.localScale = new Vector3(0.75f, 0.9f, 0.75f);
        bodyRenderer = body.GetComponent<Renderer>();

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(head.GetComponent<Collider>());
        head.transform.SetParent(body.transform, false);
        head.transform.localPosition = new Vector3(0f, 0.85f, 0f);
        head.transform.localScale = new Vector3(0.75f, 0.6f, 0.75f);
        head.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.9f, 0.75f, 0.6f));

        var weaponObj = new GameObject("BotWeapon");
        weaponObj.transform.SetParent(transform, false);
        weaponObj.transform.localPosition = new Vector3(0.35f, 0.35f, 0.5f);

        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        DestroyImmediate(model.GetComponent<Collider>());
        model.transform.SetParent(weaponObj.transform, false);
        model.transform.localScale = new Vector3(0.12f, 0.14f, 0.8f);
        weaponModel = model.GetComponent<Renderer>();

        weapon = weaponObj.AddComponent<WeaponController>();
        accuracy = Random.Range(3f, 7.5f);
    }

    public void Setup(int teamId, string displayName, Color color, WeaponData weaponData)
    {
        team = teamId;
        botName = displayName;
        bodyRenderer.sharedMaterial = MaterialCache.Lit(color);
        weapon.Initialize(weaponData, weaponModel);
        armor = Random.value < 0.3f ? 40f : 0f;
        wanderTarget = transform.position;
        thinkTimer = Random.Range(0f, 0.4f);
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.currentState != GameState.InGame || isDead)
            return;

        thinkTimer -= Time.deltaTime;
        if (thinkTimer <= 0f)
        {
            thinkTimer = 0.3f;
            Think(gm);
        }

        Vector3 move = DecideMovement(gm);
        Vector3 lookDir = move;

        if (target != null && target.IsDead)
            target = null;

        if (target != null && targetVisible)
        {
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            lookDir = toTarget;
            TryShoot(gm);
        }

        if (lookDir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += Gravity * Time.deltaTime;

        controller.Move((move * moveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);

        if (transform.position.y < -15f)
            TakeDamage(9999f, -1);
    }

    private void Think(GameManager gm)
    {
        IDamageable best = null;
        float bestDist = detectRange;
        Vector3 eye = transform.position + Vector3.up * 0.6f;

        foreach (var c in gm.Combatants)
        {
            if (c == null || c.IsDead || c.Team == team)
                continue;

            float d = Vector3.Distance(transform.position, c.transform.position);
            if (d >= bestDist)
                continue;

            if (HasLineOfSight(eye, c))
            {
                best = c;
                bestDist = d;
            }
        }

        if (best != null && best != target)
            reactionUntil = Time.time + Random.Range(0.35f, 0.9f);

        target = best;
        targetVisible = best != null;
    }

    private static bool HasLineOfSight(Vector3 eye, IDamageable other)
    {
        RaycastHit hit;
        if (Physics.Linecast(eye, other.AimPoint, out hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponentInParent<IDamageable>() == other;
        return true;
    }

    private Vector3 DecideMovement(GameManager gm)
    {
        var zone = gm.safeZone;

        // Get back inside the zone first.
        if (zone != null && zone.DistanceFromCenter(transform.position) > zone.radius * 0.85f)
            return FlatDirection(zone.center - transform.position);

        if (target != null && targetVisible)
        {
            float dist = Vector3.Distance(transform.position, target.transform.position);
            float preferred = weapon.weaponData != null && weapon.weaponData.weaponType == WeaponType.Shotgun ? 6f : 16f;

            strafeTimer -= Time.deltaTime;
            if (strafeTimer <= 0f)
            {
                strafeTimer = Random.Range(0.8f, 2f);
                strafeSign = Random.value < 0.5f ? -1f : 1f;
            }

            Vector3 toTarget = FlatDirection(target.transform.position - transform.position);
            Vector3 side = Vector3.Cross(Vector3.up, toTarget) * strafeSign;

            if (dist > preferred * 1.3f)
                return (toTarget + side * 0.3f).normalized;
            if (dist < preferred * 0.5f)
                return (-toTarget + side * 0.5f).normalized * 0.7f;
            return side * 0.6f;
        }

        // Wander.
        wanderTimer -= Time.deltaTime;
        Vector3 toWander = wanderTarget - transform.position;
        toWander.y = 0f;
        if (wanderTimer <= 0f || toWander.magnitude < 2f)
        {
            wanderTimer = Random.Range(4f, 9f);
            wanderTarget = zone != null ? zone.RandomPointInside(0.7f) : transform.position + Random.insideUnitSphere * 10f;
            return Vector3.zero;
        }
        return toWander.normalized * 0.75f;
    }

    private void TryShoot(GameManager gm)
    {
        if (Time.time < reactionUntil || Time.time < nextShotTime || weapon.weaponData == null)
            return;

        Vector3 origin = transform.position + Vector3.up * 0.6f;
        Vector3 aim = target.AimPoint - origin;
        if (aim.magnitude > weapon.weaponData.range)
            return;

        if (!weapon.CanFire)
        {
            weapon.Reload();
            if (weapon.reserveAmmo <= 0 && weapon.currentAmmo <= 0)
                weapon.AddAmmo(weapon.weaponData.magazineSize * 2); // bots never stay empty
            return;
        }

        // Bots fire slower than the player and in short bursts.
        nextShotTime = Time.time + weapon.weaponData.fireRate * 2.2f + Random.Range(0f, 0.2f);

        bool killed;
        weapon.TryFire(origin, aim, team, ~0, accuracy, out killed);
    }

    private static Vector3 FlatDirection(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
    }

    public bool TakeDamage(float amount, int attackerTeam)
    {
        if (isDead)
            return false;

        if (attackerTeam >= 0 && armor > 0f)
        {
            float absorbed = Mathf.Min(armor, amount * 0.5f);
            armor -= absorbed;
            amount -= absorbed;
        }

        health -= amount;
        if (health <= 0f)
        {
            health = 0f;
            isDead = true;
            gameObject.SetActive(false);
            if (GameManager.Instance != null)
                GameManager.Instance.OnBotEliminated(this, attackerTeam);
            return true;
        }
        return false;
    }
}
