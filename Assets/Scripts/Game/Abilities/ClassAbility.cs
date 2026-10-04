using UnityEngine;

/// <summary>
/// A player's or bot's class: the active ability (cooldown, charges, level 1/2) and the passives
/// other systems ask about. Level 2 comes from an upgrade station or a Güçlendirme Jetonu.
/// </summary>
public class ClassAbility : MonoBehaviour
{
    public PlayerClass cls;
    public int level = 1;

    private IAbilityUser user;
    private ClassDef def;
    private int charges;
    private float nextChargeAt;
    private float stealthUntil;
    private float lastUseTime = -100f;

    public ClassDef Def { get { return def; } }
    public int Charges { get { return charges; } }
    public int MaxCharges { get { return cls == PlayerClass.Teleport && level >= 2 ? 2 : 1; } }
    public float Cooldown { get { return def != null ? def.cooldown : 60f; } }
    public bool IsLevel2 { get { return level >= 2; } }
    public bool StealthActive { get { return Time.time < stealthUntil; } }
    public float LastUseTime { get { return lastUseTime; } }

    /// <summary>0 = just used, 1 = ready (for the HUD's cooldown ring).</summary>
    public float Readiness
    {
        get
        {
            if (charges >= MaxCharges)
                return 1f;
            if (charges > 0)
                return 1f;
            return 1f - Mathf.Clamp01((nextChargeAt - Time.time) / Mathf.Max(0.1f, Cooldown));
        }
    }

    public float SecondsLeft { get { return charges > 0 ? 0f : Mathf.Max(0f, nextChargeAt - Time.time); } }

    public bool Ready { get { return charges > 0 && user != null && user.CanUseAbility; } }

    /// <summary>(Re)starts the class for a new round. The first charge is ready halfway through the cooldown.</summary>
    public void Setup(IAbilityUser owner, PlayerClass c)
    {
        user = owner;
        cls = c;
        def = ClassDefs.Get(c);
        level = 1;
        charges = 0;
        nextChargeAt = Time.time + def.cooldown * 0.5f;
        stealthUntil = 0f;
        lastUseTime = -100f;
    }

    private void Update()
    {
        if (def == null)
            return;
        if (charges < MaxCharges && Time.time >= nextChargeAt)
        {
            charges++;
            if (charges < MaxCharges)
                nextChargeAt = Time.time + Cooldown;
        }
        if (stealthUntil > 0f && Time.time >= stealthUntil)
            EndStealth();
    }

    /// <summary>Level 2 and a full charge (upgrade station, Güçlendirme Jetonu).</summary>
    public void Upgrade()
    {
        level = 2;
        charges = MaxCharges;
    }

    /// <summary>Uses the ability. Returns false (nothing spent) if it is not ready or cannot work here.</summary>
    public bool Use()
    {
        if (!Ready)
            return false;
        bool ok;
        switch (cls)
        {
            case PlayerClass.Medic: ok = HealZone.Spawn(user, level) != null; break;
            case PlayerClass.K9: ok = K9Dog.Release(user, level); break;
            case PlayerClass.Teleport: ok = Teleport(); break;
            case PlayerClass.Scout: ok = ScoutEagle.Spawn(user, level) != null; break;
            case PlayerClass.Shield: ok = ShieldWall.Spawn(user, level) != null; break;
            case PlayerClass.Engineer: ok = Turret.Spawn(user, level) != null; break;
            case PlayerClass.Airborne: user.LaunchUp(level >= 2 ? 50f : 35f); ok = true; break;
            case PlayerClass.Shadow: StartStealth(level >= 2 ? 9f : 6f); ok = true; break;
            default: ok = false; break;
        }
        if (!ok)
            return false;
        lastUseTime = Time.time;
        bool wasFull = charges >= MaxCharges;
        charges--;
        if (wasFull || charges == MaxCharges - 1 && nextChargeAt < Time.time)
            nextChargeAt = Time.time + Cooldown;
        return true;
    }

    // ----- Işınlayıcı -----

    public const float TeleportDistance = 12f;

    private bool Teleport()
    {
        Vector3 dir = user.AimDirection;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f)
            dir = user.transform.forward;
        dir.Normalize();
        Vector3 start = user.AbilityOrigin;
        float dist = TeleportDistance;
        RaycastHit hit;
        if (Physics.SphereCast(start, 0.35f, dir, out hit, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            dist = hit.distance - 0.5f;
        if (dist < 2f)
            return false;   // a wall right in front
        Vector3 p = start + dir * dist;
        float ground;
        if (Physics.Raycast(p + Vector3.up * 1.2f, Vector3.down, out hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            ground = hit.point.y;
        else
            ground = World.GroundHeight(p.x, p.z);
        Vector3 feet = new Vector3(p.x, ground, p.z);
        AbilityFx.Flash(user.AbilityOrigin, new Color(0.3f, 0.9f, 1f, 0.6f), 2.2f, 0.35f);
        user.TeleportTo(feet);
        AbilityFx.Flash(feet + Vector3.up * 1f, new Color(0.3f, 0.9f, 1f, 0.6f), 2.6f, 0.45f);
        Sfx.PlayAt(SoundBank.Whoosh, feet, 0.7f, 1.6f);
        user.SpeedBoost(1.2f, 3f);   // passive
        return true;
    }

    // ----- Gölge -----

    private void StartStealth(float seconds)
    {
        stealthUntil = Time.time + seconds;
        user.SetStealth(true);
        Sfx.PlayAt(SoundBank.Whoosh, user.transform.position, 0.4f, 0.7f);
    }

    /// <summary>Ends stealth early (the user fired).</summary>
    public void EndStealth()
    {
        if (stealthUntil <= 0f)
            return;
        stealthUntil = 0f;
        if (user != null)
            user.SetStealth(false);
    }

    // ----- Passives -----

    /// <summary>Weapon damage multiplier against a target (Gözcü: +10% on marked enemies).</summary>
    public float DamageMultiplier(IDamageable target)
    {
        if (cls == PlayerClass.Scout && user != null && Marks.IsMarked(target, user.Team))
            return 1.1f;
        return 1f;
    }

    public static PlayerClass? ClassOf(IDamageable d)
    {
        var u = d as IAbilityUser;
        if (u == null || u.Ability == null)
            return null;
        return u.Ability.cls;
    }

    /// <summary>Explosion damage multiplier for a target (Kalkan Ustası: -25%).</summary>
    public static float ExplosionTaken(IDamageable d)
    {
        return ClassOf(d) == PlayerClass.Shield ? 0.75f : 1f;
    }

    /// <summary>Damage taken while driving (Mühendis: -30%).</summary>
    public static float VehicleTaken(IDamageable d)
    {
        return ClassOf(d) == PlayerClass.Engineer ? 0.7f : 1f;
    }

    public static bool IsSilent(IDamageable d)
    {
        return ClassOf(d) == PlayerClass.Shadow;
    }

    public static bool IsStealthed(IDamageable d)
    {
        var u = d as IAbilityUser;
        return u != null && u.Ability != null && u.Ability.StealthActive;
    }
}
