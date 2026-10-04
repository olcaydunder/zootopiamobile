using UnityEngine;

/// <summary>PlayerController part: the class ability (IAbilityUser) and its effects on the player.</summary>
public partial class PlayerController : IAbilityUser
{
    public ClassAbility Ability { get; private set; }
    public bool IsPlayer { get { return true; } }
    public Vector3 AbilityOrigin { get { return transform.position + Vector3.up * 0.5f; } }
    public Vector3 AimDirection { get { return playerCamera != null ? playerCamera.transform.forward : transform.forward; } }
    public bool CanUseAbility { get { return !isDead && !isDowned && state == PlayerState.Ground && !isSwimming && !isVaulting; } }
    public bool IsStealthed { get { return Ability != null && Ability.StealthActive; } }

    private float speedBoostMul = 1f;
    private float speedBoostUntil;
    private float launchTargetY;
    private bool launchGlide;

    private float SpeedBoostFactor { get { return Time.time < speedBoostUntil ? speedBoostMul : 1f; } }

    /// <summary>Sets up the chosen class for a new round.</summary>
    private void InitAbility()
    {
        if (Ability == null)
            Ability = GetComponent<ClassAbility>() ?? gameObject.AddComponent<ClassAbility>();
        Ability.EndStealth();
        Ability.Setup(this, ClassDefs.Selected);
        if (currentWeapon != null)
            currentWeapon.owner = Ability;
        speedBoostUntil = 0f;
        launchTargetY = 0f;
        launchGlide = false;
        GhostState.Set(gameObject, false, 0f);
    }

    /// <summary>The SINIF button.</summary>
    private void TryUseAbility()
    {
        if (Ability == null)
            return;
        var ui = GameManager.Instance != null ? GameManager.Instance.uiManager : null;
        if (Ability.Charges <= 0)
        {
            if (ui != null) ui.Toast(Ability.Def.ability + " hazır değil (" + Mathf.CeilToInt(Ability.SecondsLeft) + " sn)");
            return;
        }
        if (!CanUseAbility)
            return;
        if (!Ability.Use())
        {
            if (ui != null) ui.Toast(Ability.cls == PlayerClass.Teleport ? "Önün kapalı" : "Burada kullanılamaz");
            return;
        }
        Haptics.Tap(30);
    }

    public void HealBy(float hp, float armorAmount)
    {
        if (isDead || isDowned)
            return;
        health = Mathf.Min(maxHealth, health + hp);
        if (armorAmount > 0f && armor > 0f)
            armor = Mathf.Min(maxArmor, armor + armorAmount);
    }

    public void TeleportTo(Vector3 feet)
    {
        EndVault();
        controller.enabled = false;
        transform.position = feet + Vector3.up * FeetOffset;
        controller.enabled = true;
        velocity = Vector3.zero;
    }

    /// <summary>Paraşütçü: shoots up, then glides down on the parachute.</summary>
    public void LaunchUp(float height)
    {
        EndVault();
        StopSwim();
        ClearScope();
        if (isCrouching)
            SetCrouch(false);
        aimingDownSights = false;
        state = PlayerState.Parachute;
        controller.enabled = false;
        launchTargetY = transform.position.y + height;
        launchGlide = true;
        airVelocity = transform.forward * 6f;
        rig.parachuteCamo = Cosmetics.EquippedParachuteCamo;
        rig.pose = RigPose.Parachute;
        currentWeapon.gameObject.SetActive(false);
        camTarget = 7f;
        wind.volume = 0.3f;
        wind.Play();
        Sfx.Play(SoundBank.Whoosh, 0.7f, 0.8f);
    }

    /// <summary>The rising part of the launch. Returns true while still going up.</summary>
    private bool UpdateLaunch()
    {
        if (launchTargetY <= 0f)
            return false;
        if (transform.position.y >= launchTargetY)
        {
            launchTargetY = 0f;
            return false;
        }
        transform.position += (Vector3.up * 28f + transform.forward * 4f) * Time.deltaTime;
        HeightAboveGround = transform.position.y - World.GroundHeight(transform.position.x, transform.position.z);
        return true;
    }

    public void SetStealth(bool on)
    {
        GhostState.Set(gameObject, on, 0.3f);   // you still see yourself faintly
    }

    public void SpeedBoost(float multiplier, float seconds)
    {
        speedBoostMul = multiplier;
        speedBoostUntil = Time.time + seconds;
    }
}
