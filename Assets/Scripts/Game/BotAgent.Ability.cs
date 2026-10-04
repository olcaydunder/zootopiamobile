using UnityEngine;

/// <summary>BotAgent part: the bot's class ability, when it decides to use it, and the IAbilityUser side.</summary>
public partial class BotAgent : IAbilityUser
{
    public ClassAbility Ability { get; private set; }
    public bool IsPlayer { get { return false; } }
    public Vector3 AbilityOrigin { get { return transform.position + Vector3.up * 0.5f; } }
    public bool CanUseAbility { get { return !isDead && air == BotAir.None; } }

    private Vector3 aimOverride;
    private float speedBoostMul = 1f, speedBoostUntil;
    private float launchTargetY;
    private bool launchGlide;

    private float SpeedBoostFactor { get { return Time.time < speedBoostUntil ? speedBoostMul : 1f; } }

    public Vector3 AimDirection
    {
        get
        {
            if (aimOverride.sqrMagnitude > 0.01f)
                return aimOverride;
            if (target != null && !target.IsDead)
                return (target.AimPoint - AbilityOrigin).normalized;
            return transform.forward;
        }
    }

    private void InitAbility()
    {
        ClassAbility existing;
        Ability = TryGetComponent(out existing) ? existing : gameObject.AddComponent<ClassAbility>();
        Ability.Setup(this, (PlayerClass)Random.Range(0, ClassDefs.All.Length));
        weapon.owner = Ability;
    }

    /// <summary>Simple rules for when a bot uses its ability (checked a few times a second).</summary>
    private void AbilityThink(GameManager gm)
    {
        if (Ability == null || !Ability.Ready || Random.value < 0.4f)
            return;
        bool fighting = target != null && targetVisible;
        float dist = target != null ? Vector3.Distance(transform.position, target.transform.position) : 999f;
        bool use = false;
        switch (Ability.cls)
        {
            case PlayerClass.Medic:
                use = health < 65f;
                break;
            case PlayerClass.K9:
                use = fighting || NearestEnemyDistance(gm) < 35f;
                break;
            case PlayerClass.Scout:
                use = fighting || NearestEnemyDistance(gm) < 55f;
                break;
            case PlayerClass.Teleport:
                if (fighting && health < 50f)
                {
                    // Dodge sideways.
                    Vector3 to = target.transform.position - transform.position;
                    to.y = 0f;
                    aimOverride = Vector3.Cross(Vector3.up, to.normalized) * (Random.value < 0.5f ? 1f : -1f);
                    use = true;
                }
                break;
            case PlayerClass.Shield:
                use = fighting && dist > 8f && dist < 60f;
                break;
            case PlayerClass.Engineer:
                use = fighting && dist < Turret.Range;
                break;
            case PlayerClass.Airborne:
                use = gm.safeZone != null && gm.safeZone.active && gm.safeZone.IsOutside(transform.position) && !fighting;
                break;
            case PlayerClass.Shadow:
                use = fighting && health < 60f;
                break;
        }
        if (use)
            Ability.Use();
        aimOverride = Vector3.zero;
    }

    private float NearestEnemyDistance(GameManager gm)
    {
        float best = 999f;
        foreach (var c in gm.Combatants)
        {
            if (c == null || c.IsDead || c.IsAirborne || c.Team == team)
                continue;
            best = Mathf.Min(best, Vector3.Distance(c.transform.position, transform.position));
        }
        return best;
    }

    public void HealBy(float hp, float armorAmount)
    {
        if (isDead)
            return;
        health = Mathf.Min(100f, health + hp);
        if (armorAmount > 0f && armor > 0f)
            armor = Mathf.Min(100f, armor + armorAmount);
    }

    public void TeleportTo(Vector3 feet)
    {
        controller.enabled = false;
        transform.position = feet + Vector3.up * 0.9f;
        controller.enabled = true;
        verticalVelocity = 0f;
        wanderTarget = transform.position;
    }

    /// <summary>Paraşütçü: up into the air, then glide toward the safe zone (or away from the fight).</summary>
    public void LaunchUp(float height)
    {
        controller.enabled = false;
        air = BotAir.Parachute;
        launchGlide = true;
        launchTargetY = transform.position.y + height;
        rig.pose = RigPose.Parachute;
        weapon.gameObject.SetActive(false);
        var gm = GameManager.Instance;
        Vector3 goal = transform.position + transform.forward * 40f;
        if (gm != null && gm.safeZone != null && gm.safeZone.active)
        {
            Vector3 toZone = gm.safeZone.center - transform.position;
            toZone.y = 0f;
            goal = transform.position + Vector3.ClampMagnitude(toZone, 45f);
        }
        landTarget = goal;
        followPlayer = false;
        airVelocity = Vector3.zero;
    }

    private bool UpdateLaunch()
    {
        if (launchTargetY <= 0f)
            return false;
        if (transform.position.y >= launchTargetY)
        {
            launchTargetY = 0f;
            return false;
        }
        transform.position += Vector3.up * 28f * Time.deltaTime;
        return true;
    }

    public void SetStealth(bool on)
    {
        GhostState.Set(gameObject, on, 0.07f);
    }

    public void SpeedBoost(float multiplier, float seconds)
    {
        speedBoostMul = multiplier;
        speedBoostUntil = Time.time + seconds;
    }
}
