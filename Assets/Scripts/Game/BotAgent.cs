using UnityEngine;

public enum BotAir
{
    None,
    Plane,
    Freefall,
    Parachute
}

/// <summary>
/// Offline AI opponent / teammate. Drops from the plane, then wanders inside the safe zone,
/// picks the nearest visible enemy, closes distance and shoots with human-like reaction time,
/// inaccuracy and the odd grenade.
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
    public BotAir air = BotAir.None;

    public CharacterController controller;
    public WeaponController weapon;
    public CharacterRig rig;

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
    private float nextGrenadeTime;
    private int grenades;
    private float stepDistance;
    private const float Gravity = -20f;

    // Drop
    private AirPlane plane;
    private float jumpAt;
    private Vector3 landTarget;
    private bool followPlayer;
    private Vector3 followOffset;
    private Vector3 airVelocity;

    public int Team { get { return team; } }
    public bool IsDead { get { return isDead; } }
    public bool IsAirborne { get { return air != BotAir.None; } }
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
        controller.slopeLimit = 50f;

        var weaponObj = new GameObject("BotWeapon");
        weaponObj.transform.SetParent(transform, false);
        weaponObj.transform.localPosition = new Vector3(0.28f, 0.28f, 0.45f);

        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        DestroyImmediate(model.GetComponent<Collider>());
        model.transform.SetParent(weaponObj.transform, false);
        weaponModel = model.GetComponent<Renderer>();

        weapon = weaponObj.AddComponent<WeaponController>();
        accuracy = Random.Range(3f, 7.5f);
    }

    private static readonly Color[] Helmets =
    {
        new Color(0.3f, 0.35f, 0.25f), new Color(0.25f, 0.25f, 0.28f), new Color(0.5f, 0.42f, 0.3f), new Color(0.6f, 0.6f, 0.6f)
    };
    private static readonly Color[] Skins =
    {
        new Color(0.95f, 0.8f, 0.66f), new Color(0.82f, 0.64f, 0.48f), new Color(0.6f, 0.43f, 0.3f), new Color(0.42f, 0.3f, 0.22f)
    };

    public void Setup(int teamId, string displayName, Color color, WeaponData weaponData)
    {
        team = teamId;
        botName = displayName;
        string skin = teamId == 0 ? ModelLibrary.PlayerSkin : ModelLibrary.EnemySkins[Random.Range(0, ModelLibrary.EnemySkins.Length)];
        rig = CharacterRig.Build(gameObject, color, new Color(0.22f, 0.23f, 0.25f),
            Skins[Random.Range(0, Skins.Length)], Helmets[Random.Range(0, Helmets.Length)], new Color(0.38f, 0.32f, 0.22f), skin);
        rig.weaponHold = weapon.transform;
        if (teamId == 0)
        {
            // Green marker over teammates' heads.
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            marker.transform.localScale = new Vector3(0.18f, 0.24f, 0.18f);
            var mr = marker.GetComponent<Renderer>();
            mr.sharedMaterial = UIUtil.UnlitMaterial(new Color(0.3f, 1f, 0.4f, 0.9f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        weapon.Initialize(weaponData, weaponModel);

        // Opponents get a little sharper as the player levels up.
        int level = GameManager.Instance != null ? GameManager.Instance.profile.level : 1;
        if (teamId != 0)
            accuracy = Mathf.Max(1.8f, accuracy - Mathf.Min(level, 12) * 0.2f);
        armor = Random.value < 0.3f ? 40f : 0f;
        grenades = Random.value < 0.4f ? 1 : 0;
        wanderTarget = transform.position;
        thinkTimer = Random.Range(0f, 0.4f);
        nextGrenadeTime = Time.time + Random.Range(20f, 60f);
    }

    // ----- Drop from the plane -----

    public void BoardPlane(AirPlane dropPlane, float jumpProgress, Vector3 landing, bool followsPlayer)
    {
        plane = dropPlane;
        jumpAt = jumpProgress;
        landTarget = landing;
        followPlayer = followsPlayer;
        followOffset = new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
        air = BotAir.Plane;
        controller.enabled = false;
        rig.SetVisible(false);
        weapon.gameObject.SetActive(false);
        transform.position = dropPlane.transform.position;
    }

    /// <summary>Teammates leave the plane together with the player.</summary>
    public void JumpNow()
    {
        if (air == BotAir.Plane)
            Jump();
    }

    private void Jump()
    {
        Vector3 forward = plane != null ? plane.Direction : transform.forward;
        if (plane != null)
            transform.position = plane.transform.position - Vector3.up * 4f + Random.insideUnitSphere * 2f;
        air = BotAir.Freefall;
        airVelocity = forward * 10f;
        rig.SetVisible(true);
        rig.pose = RigPose.Freefall;
        // Bots show off a random parachute from the catalogue.
        var list = Cosmetics.ParachuteCamos;
        rig.parachuteCamo = list[Random.Range(0, list.Count)].id;
    }

    private void UpdateAir()
    {
        float dt = Time.deltaTime;
        if (air == BotAir.Plane)
        {
            if (plane == null || plane.Finished || plane.Progress >= jumpAt)
                Jump();
            else
                transform.position = plane.transform.position;
            return;
        }

        Vector3 goal = landTarget;
        var player = PlayerController.LocalPlayer;
        if (followPlayer && player != null && !player.isDead)
            goal = player.transform.position + followOffset;

        Vector3 toGoal = goal - transform.position;
        toGoal.y = 0f;
        float ground = World.GroundHeight(transform.position.x, transform.position.z);
        float height = transform.position.y - ground;

        // Glide far enough to reach landing spots away from the flight path.
        float horizontalMax = air == BotAir.Freefall ? 30f : 15f;
        float vertical = air == BotAir.Freefall ? -32f : -6f;
        Vector3 desired = Vector3.ClampMagnitude(toGoal * 0.6f, horizontalMax) + Vector3.up * vertical;
        airVelocity = Vector3.Lerp(airVelocity, desired, dt * 2f);

        if (toGoal.sqrMagnitude > 1f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toGoal), dt * 3f);

        if (air == BotAir.Freefall && height < 90f)
        {
            air = BotAir.Parachute;
            rig.pose = RigPose.Parachute;
        }

        Vector3 next = transform.position + airVelocity * dt;
        float landHeight = World.GroundHeight(next.x, next.z);
        if (next.y - 0.95f <= landHeight)
        {
            transform.position = new Vector3(next.x, landHeight + 0.95f, next.z);
            // Never end up stuck on a roof (bots can't jump down parapets): step down next to the building.
            if (World.IsBlocked(next.x, next.z))
                transform.position = World.RandomOpenPoint(new Vector3(next.x, 0f, next.z), 14f);
            air = BotAir.None;
            controller.enabled = true;
            rig.pose = RigPose.Normal;
            weapon.gameObject.SetActive(true);
            wanderTarget = transform.position;
            Effects.Dust(transform.position - Vector3.up * 0.9f, 6);
            return;
        }
        transform.position = next;
    }

    // ----- Ground AI -----

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.currentState != GameState.InGame || isDead)
            return;

        if (air != BotAir.None)
        {
            UpdateAir();
            return;
        }

        thinkTimer -= Time.deltaTime;
        if (thinkTimer <= 0f)
        {
            thinkTimer = 0.3f;
            Think(gm);
            Door.PushOpenNear(transform.position, 2f);   // walk through doorways
        }

        if (target != null && (target.IsDead || target.IsAirborne))
            target = null;

        Vector3 move = Steer(DecideMovement(gm));
        Vector3 lookDir = move;

        if (target != null && targetVisible)
        {
            Vector3 toTarget = target.transform.position - transform.position;
            toTarget.y = 0f;
            lookDir = toTarget;
            TryShoot(gm);
            TryGrenade();
        }

        if (lookDir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += Gravity * Time.deltaTime;

        // Stay out of deep water.
        Vector3 ahead = transform.position + move * 1.5f;
        if (move.sqrMagnitude > 0.01f && World.HeightAt(ahead.x, ahead.z) < -1.2f &&
            World.HeightAt(ahead.x, ahead.z) < World.HeightAt(transform.position.x, transform.position.z))
            move = Vector3.zero;

        controller.Move((move * moveSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);

        // Footsteps you can hear when they are close.
        if (controller.isGrounded && move.sqrMagnitude > 0.05f)
        {
            stepDistance += move.magnitude * moveSpeed * Time.deltaTime;
            if (stepDistance > 2.3f)
            {
                stepDistance = 0f;
                var p = PlayerController.LocalPlayer;
                if (p != null && team != 0 && Vector3.Distance(p.transform.position, transform.position) < 28f)
                    Sfx.PlayAt(SoundBank.Footstep, transform.position - Vector3.up * 0.8f, 0.8f, Random.Range(0.85f, 1.1f));
            }
        }

        rig.aiming = target != null && targetVisible;
        rig.aimPitch = 0f;

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
            if (c == null || c.IsDead || c.IsAirborne || c.Team == team)
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
        {
            IDamageable seen = hit.collider.GetComponentInParent<IDamageable>();
            if (seen == other)
                return true;
            // A player driving a jeep is visible through it.
            var car = seen as Vehicle;
            return car != null && ReferenceEquals(car.driver, other);
        }
        return true;
    }

    // ----- Getting around buildings -----

    private static readonly float[] AvoidAngles = { 35f, 70f, 110f, 150f };
    private float avoidSign = 1f;
    private float avoidSignUntil;
    private Vector3 stuckCheckPos;
    private float stuckCheckTime;
    private Vector3 unstickDir;
    private float unstickUntil;

    /// <summary>Turns the wanted direction away from walls, and breaks free when the bot hasn't moved for a while.</summary>
    private Vector3 Steer(Vector3 move)
    {
        float speed = move.magnitude;
        if (speed < 0.05f || IsAirborne)
        {
            stuckCheckPos = transform.position;
            stuckCheckTime = Time.time;
            return move;
        }

        if (Time.time < unstickUntil)
            return unstickDir * speed;

        // Stuck: wanted to walk for 1.5 s but barely moved (not while fighting: strafing goes back and forth).
        if (target != null && targetVisible)
        {
            stuckCheckPos = transform.position;
            stuckCheckTime = Time.time;
        }
        else if (Time.time - stuckCheckTime > 1.5f)
        {
            Vector3 moved = transform.position - stuckCheckPos;
            moved.y = 0f;
            if (moved.magnitude < 0.5f)
            {
                avoidSign = -avoidSign;
                Vector2 r = Random.insideUnitCircle.normalized;
                unstickDir = new Vector3(r.x, 0f, r.y);
                unstickUntil = Time.time + 0.9f;
                wanderTimer = 0f;   // pick a new wander target
            }
            stuckCheckPos = transform.position;
            stuckCheckTime = Time.time;
        }

        Vector3 dir = move / speed;
        if (!Blocked(dir))
            return move;
        if (Time.time > avoidSignUntil)
        {
            avoidSign = Random.value < 0.5f ? -1f : 1f;
            avoidSignUntil = Time.time + 1.2f;
        }
        foreach (float a in AvoidAngles)
        {
            for (int s = 0; s < 2; s++)
            {
                float sign = s == 0 ? avoidSign : -avoidSign;
                Vector3 d = Quaternion.Euler(0f, a * sign, 0f) * dir;
                if (!Blocked(d))
                {
                    avoidSign = sign;
                    return d * speed;
                }
            }
        }
        return move;
    }

    private bool Blocked(Vector3 dir)
    {
        // From knee height (0.6 m above the feet): steps and kerbs the controller can climb are ignored.
        Vector3 origin = transform.position - Vector3.up * 0.35f;
        RaycastHit hit;
        if (!Physics.SphereCast(origin, 0.3f, dir, out hit, 1.6f, Physics.DefaultRaycastLayers & ~(1 << 2), QueryTriggerInteraction.Ignore))
            return false;
        // Gentle slopes and kerbs are fine; walls and other characters are not.
        return hit.normal.y < 0.6f;
    }

    private Vector3 DecideMovement(GameManager gm)
    {
        var zone = gm.safeZone;

        // A knocked-down player comes first, even outside the zone or mid-fight.
        var downed = PlayerController.LocalPlayer;
        if (team == 0 && downed != null && downed.isDowned && !downed.isDead)
        {
            Vector3 toDowned = downed.transform.position - transform.position;
            toDowned.y = 0f;
            if (toDowned.magnitude > 1.8f)
                return toDowned.normalized * 1.4f;
            downed.ReviveTick(Time.deltaTime);
            return Vector3.zero;
        }
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

        var player = PlayerController.LocalPlayer;
        if (team == 0 && player != null && !player.isDead && !player.IsAirborne)
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.magnitude > 7f)
                return toPlayer.normalized * (toPlayer.magnitude > 15f ? 1.4f : 1f);
            return Vector3.zero;
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

    private void TryGrenade()
    {
        if (grenades <= 0 || Time.time < nextGrenadeTime)
            return;
        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist < 8f || dist > 22f)
            return;

        grenades--;
        nextGrenadeTime = Time.time + 30f;
        Vector3 to = target.transform.position - transform.position;
        Vector3 flat = new Vector3(to.x, 0f, to.z);
        float d = flat.magnitude;
        // Rough lob: 45 degrees, speed for distance d (ignoring drag).
        float speed = Mathf.Sqrt(d * 20f) * Random.Range(0.85f, 1.1f);
        Vector3 vel = (flat.normalized + Vector3.up).normalized * speed;
        Grenade.Throw(transform.position + Vector3.up * 0.9f + flat.normalized * 0.6f, vel, this);
    }

    private static Vector3 FlatDirection(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.zero;
    }

    public bool TakeDamage(float amount, int attackerTeam)
    {
        if (isDead || IsAirborne)
            return false;

        if (attackerTeam >= 0 && armor > 0f)
        {
            float absorbed = Mathf.Min(armor, amount * 0.5f);
            armor -= absorbed;
            amount -= absorbed;
        }

        health -= amount;
        if (attackerTeam >= 0)
            rig.PlayHit();
        if (health <= 0f)
        {
            health = 0f;
            Die(attackerTeam);
            return true;
        }
        return false;
    }

    private void Die(int attackerTeam)
    {
        isDead = true;
        controller.enabled = false;     // corpse no longer blocks shots or movement
        weapon.gameObject.SetActive(false);
        rig.pose = RigPose.Dead;

        var gm = GameManager.Instance;
        if (gm != null)
        {
            if (gm.lootSystem != null)
                gm.lootSystem.DropDeathCrate(transform.position);
            gm.OnBotEliminated(this, attackerTeam);
        }
        Invoke("HideCorpse", 6f);
    }

    private void HideCorpse()
    {
        gameObject.SetActive(false);
    }
}
