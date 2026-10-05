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
        {
            ClassAbility existing;
            Ability = TryGetComponent(out existing) ? existing : gameObject.AddComponent<ClassAbility>();
        }
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
        if (NetGame.Online)
        {
            if (ui != null)
                ui.Toast("Sınıf yetenekleri çevrimiçi maçta yakında");
            return;
        }
        if (Ability.Charges <= 0)
        {
            if (ui != null) ui.Toast(Ability.Def.ability + " hazır değil (" + Mathf.CeilToInt(Ability.SecondsLeft) + " sn)");
            return;
        }
        if (!CanUseAbility)
            return;
        if (!Ability.Use())
        {
            if (ui != null) ui.Toast(Ability.cls == PlayerClass.Teleport ? "Önün kapalı" : Ability.cls == PlayerClass.Airborne ? "Üstün kapalı, açık alana çık" : "Burada kullanılamaz");
            return;
        }
        Haptics.Tap(30);
    }

    // ----- Tokens -----

    private void UseAirdropToken()
    {
        if (!MatchTokens.Use(TokenType.Airdrop))
            return;
        AirdropCall.Call(transform.position);
        var gm = GameManager.Instance;
        if (gm != null && gm.uiManager != null)
            gm.uiManager.Toast("Hava ikmali yolda! " + Mathf.RoundToInt(AirdropCall.Delay) + " sn");
        Haptics.Tap(30);
    }

    private void UseBoostToken()
    {
        if (Ability == null || Ability.IsLevel2 || !MatchTokens.Use(TokenType.Boost))
            return;
        Ability.Upgrade();
        AbilityFx.Flash(transform.position, new Color(0.65f, 0.4f, 1f, 0.6f), 3f, 0.5f);
        Sfx.Play(SoundBank.Pickup, 0.8f, 1.3f);
        var gm = GameManager.Instance;
        if (gm != null && gm.uiManager != null)
            gm.uiManager.Toast(Ability.Def.ability.ToUpper() + " SEVİYE 2!");
    }

    /// <summary>Dirilme Jetonu: back in the match, parachuting into the safe zone with a pistol.</summary>
    public void RespawnFromToken(Vector3 groundPoint)
    {
        Killcam.Stop();
        ResetFall();
        bool wasLevel2 = Ability != null && Ability.IsLevel2;
        EndVault();
        StopSwim();
        ClearScope();
        isDead = false;
        isDowned = false;
        reviveProgress = 0f;
        health = maxHealth;
        armor = 50f;
        boostRemaining = 0f;
        aimingDownSights = false;
        isSprinting = false;
        inventory.Reset();
        if (isCrouching)
            SetCrouch(false);
        slots[0].data = Gunsmith.Apply(WeaponData.CreatePistol());
        slots[0].ammo = slots[0].data.magazineSize;
        slots[0].reserve = slots[0].data.reserveAmmo;
        slots[1].data = null;
        LoadSlot(0);
        rig.ResetPose();
        InitAbility();
        if (wasLevel2)
            Ability.Upgrade();   // keep what an upgrade station or Güçlendirme Jetonu gave

        controller.enabled = false;
        transform.position = groundPoint + Vector3.up * 90f;
        state = PlayerState.Parachute;
        airVelocity = Vector3.zero;
        rig.SetVisible(true);
        rig.parachuteCamo = Cosmetics.EquippedParachuteCamo;
        rig.pose = RigPose.Parachute;
        currentWeapon.gameObject.SetActive(false);
        camTarget = 7f;
        wind.volume = 0.2f;
        wind.Play();
        Sfx.Play(SoundBank.Whoosh, 0.6f);
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
