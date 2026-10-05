using UnityEngine;

/// <summary>PlayerController part: Weapons, items and the action buttons (fire, aim, reload, heal, grenade, swap).</summary>
public partial class PlayerController
{
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

    /// <summary>The equipped explosive (Envanter): frag grenade, molotov or charge.</summary>
    public ThrowKind ExplosiveKind { get { return Grenade.KindOf(Gear.ExplosiveId); } }
    /// <summary>The equipped tactical item: smoke, flash or gas (frag when none is equipped).</summary>
    public ThrowKind TacticalKind { get { return Grenade.KindOf(Gear.TacticalId); } }
    public bool HasTactical { get { return Gear.TacticalId.Length > 0; } }

    // ----- Throwing: hold BOMBA / TAKTİK to aim (an arc shows where it lands), release to throw -----
    // The fuse starts when the button goes down: 6 s (Grenade.CookFuse), so holding longer leaves less time once it
    // lands; held the whole 6 s it goes off at your feet. Molotovs still break where they land.

    private bool cooking, cookTactical, grenadeWasHeld, tacticalWasHeld;
    private ThrowKind cookKind;
    private float cookPower, cookStart;
    private LineRenderer arcLine, arcRing;
    private static readonly Vector3[] arcPoints = new Vector3[64];
    private static readonly Vector3[] ringPoints = new Vector3[25];

    public bool IsCooking { get { return cooking; } }

    private void ThrowStart(out Vector3 origin, out Vector3 velocity)
    {
        Transform cam = playerCamera.transform;
        origin = cameraPivot.position + cam.forward * 0.9f + Vector3.up * 0.2f;
        velocity = cam.forward * 17f + Vector3.up * 5f;
    }

    private void UpdateThrowInput(IPlayerInput tc)
    {
        bool g = tc != null && tc.GrenadeHeld, t = tc != null && tc.TacticalHeld;
        bool gDown = g && !grenadeWasHeld, tDown = t && !tacticalWasHeld;
        grenadeWasHeld = g;
        tacticalWasHeld = t;
        if (!cooking)
        {
            if (gDown)
                StartCook(false);
            else if (tDown)
                StartCook(true);
            if (!cooking)
                return;
        }
        bool held = cookTactical ? t : g;
        float left = Grenade.Cookable(cookKind) ? Grenade.CookFuse - (Time.time - cookStart) : 99f;
        if (left <= 0f)
        {
            FinishCook(false, 0.02f);   // held too long
            return;
        }
        if (held)
        {
            ShowArc(left);
            return;
        }
        FinishCook(true, left);
    }

    private void StartCook(bool tactical)
    {
        var ui = GameManager.Instance.uiManager;
        if (tactical)
        {
            if (!HasTactical)
            {
                ui.Toast("Taktik eşya kuşanılmamış (ENVANTER)");
                return;
            }
            if (inventory.tacticals <= 0)
            {
                ui.Toast("Taktik eşyan kalmadı");
                return;
            }
            cookKind = TacticalKind;
            cookPower = Gear.TacticalMul;
        }
        else
        {
            if (inventory.grenades <= 0)
            {
                ui.Toast("Patlayıcın yok");
                return;
            }
            cookKind = ExplosiveKind;
            cookPower = Gear.ExplosiveMul;
        }
        cooking = true;
        cookTactical = tactical;
        cookStart = Time.time;
        aimingDownSights = false;
        Sfx.Play(SoundBank.Reload, 0.35f, 1.7f);   // the pin comes out
    }

    private void FinishCook(bool thrown, float fuseLeft)
    {
        cooking = false;
        HideArc();
        if (cookTactical)
            inventory.tacticals = Mathf.Max(0, inventory.tacticals - 1);
        else
            inventory.grenades = Mathf.Max(0, inventory.grenades - 1);
        float fuse = Grenade.Cookable(cookKind) ? Mathf.Max(0.02f, fuseLeft) : 0f;
        if (thrown)
        {
            Vector3 origin, velocity;
            ThrowStart(out origin, out velocity);
            Grenade.Throw(origin, velocity, this, cookKind, cookPower, fuse);
            Sfx.Play(SoundBank.Whoosh, 0.4f, 1.3f);
        }
        else
        {
            // kept in the hand: it drops at your feet
            Grenade.Throw(transform.position + transform.forward * 0.3f, Vector3.down * 0.5f, this, cookKind, cookPower, fuse);
            GameManager.Instance.uiManager.Toast("Elinde tuttun!");
        }
    }

    /// <summary>Dying, going down or climbing into a vehicle with a cooked grenade: it drops where you are.</summary>
    public void CancelCook()
    {
        if (!cooking)
            return;
        float left = Grenade.CookFuse - (Time.time - cookStart);
        FinishCook(false, left);
    }

    /// <summary>The flight path from the hand (same physics as the thrown grenade) up to where it first lands.</summary>
    private void ShowArc(float secondsLeft)
    {
        if (arcLine == null)
        {
            arcLine = NewLine("ThrowArc", 0.045f);
            arcRing = NewLine("ThrowLanding", 0.05f);
            arcRing.loop = true;
        }
        Color col = cookKind == ThrowKind.Smoke || cookKind == ThrowKind.Flash || cookKind == ThrowKind.Gas ? new Color(0.85f, 0.95f, 1f, 0.85f) : new Color(1f, 0.55f, 0.2f, 0.85f);
        arcLine.startColor = col;
        arcLine.endColor = new Color(col.r, col.g, col.b, 0.35f);
        arcRing.startColor = arcRing.endColor = col;

        Vector3 p, v;
        ThrowStart(out p, out v);
        const float dt = 0.03f;
        int count = 0;
        arcPoints[count++] = p;
        Vector3 land = p;
        bool landed = false;
        int mask = Physics.DefaultRaycastLayers & ~(1 << IgnoreRaycastLayer);
        for (int i = 0; i < 160 && count < arcPoints.Length; i++)
        {
            v += Physics.gravity * dt;
            v *= 1f / (1f + 0.15f * dt);   // the Rigidbody's drag
            Vector3 next = p + v * dt;
            RaycastHit hit;
            if (Physics.Linecast(p, next, out hit, mask, QueryTriggerInteraction.Ignore))
            {
                land = hit.point;
                arcPoints[count++] = land;
                landed = true;
                break;
            }
            p = next;
            if (i % 2 == 1)
                arcPoints[count++] = p;
        }
        if (!landed)
            land = p;
        arcLine.positionCount = count;
        for (int i = 0; i < count; i++)
            arcLine.SetPosition(i, arcPoints[i]);
        float r = cookKind == ThrowKind.Smoke ? 1.2f : cookKind == ThrowKind.Molotov ? 1.4f : 0.9f;
        for (int i = 0; i < ringPoints.Length; i++)
        {
            float a = i * Mathf.PI * 2f / ringPoints.Length;
            arcRing.SetPosition(i, land + new Vector3(Mathf.Cos(a) * r, 0.06f, Mathf.Sin(a) * r));
        }
        arcLine.enabled = arcRing.enabled = true;
        var ui = GameManager.Instance.uiManager;
        if (ui != null)
            ui.ShowCook(Grenade.Name(cookKind), Grenade.Cookable(cookKind) ? secondsLeft : -1f);
    }

    private void HideArc()
    {
        if (arcLine != null)
            arcLine.enabled = arcRing.enabled = false;
        var gm = GameManager.Instance;
        if (gm != null && gm.uiManager != null)
            gm.uiManager.ShowCook(null, -1f);
    }

    private LineRenderer NewLine(string name, float width)
    {
        var go = new GameObject(name);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.startWidth = lr.endWidth = width;
        lr.material = UIUtil.UnlitMaterial(Color.white);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.positionCount = ringPoints.Length;
        lr.enabled = false;
        return lr;
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
        float mul = (Ability != null && Ability.cls == PlayerClass.Medic ? 1.3f : 1f) * Gear.HealMul;   // Sahra Hekimi: +30%
        switch (Gear.MedicalId)
        {
            case "m_adrenaline":
            {
                // Adrenalin: a little health at once and a burst of speed.
                float amount = 25f * mul;
                health = Mathf.Min(maxHealth, health + amount);
                adrenalineUntil = Time.time + 6f;
                if (ui != null) ui.Toast("ADRENALİN  +" + Mathf.RoundToInt(amount) + " can, 6 sn hız");
                break;
            }
            case "m_pack":
            {
                // Sağlık Paketi: a lot of health over four seconds.
                healOverTime += 70f * mul;
                healRate = 70f * mul / 4f;
                if (ui != null) ui.Toast("SAĞLIK PAKETİ  +" + Mathf.RoundToInt(70f * mul) + " can (4 sn)");
                break;
            }
            default:
            {
                float amount = 40f * mul;
                health = Mathf.Min(maxHealth, health + amount);
                if (ui != null) ui.Toast("+" + Mathf.RoundToInt(amount) + " can");
                break;
            }
        }
        Sfx.Play(SoundBank.Pickup, 0.4f, 0.7f);
        return true;
    }

    private bool adsFromFire;

    /// <summary>How close the player must be to a door to open or close it.</summary>
    public const float DoorReach = 2.4f;

    private void HandleActions(GameManager gm, IPlayerInput tc)
    {
        bool jump = tc != null && tc.ConsumeJump();
        bool crouch = tc != null && tc.ConsumeCrouch();
        bool prone = tc != null && tc.ConsumeProne();
        bool reload = tc != null && tc.ConsumeReload();
        bool medkit = tc != null && tc.ConsumeMedkit();
        bool drink = tc != null && tc.ConsumeDrink();
        bool grenade = tc != null && tc.ConsumeGrenade();
        bool tactical = tc != null && tc.ConsumeTactical();
        bool swap = tc != null && tc.ConsumeSwap();
        bool useVehicle = tc != null && tc.ConsumeVehicle();
        bool fire = tc != null && tc.FireHeld;
        bool aim = tc != null && tc.ConsumeAim();
        bool door = tc != null && tc.ConsumeDoor();
        if (tc != null && tc.ConsumeAirdropToken())
            UseAirdropToken();
        if (tc != null && tc.ConsumeBoostToken())
            UseBoostToken();
        bool ability = tc != null && tc.ConsumeAbility();
        if (ability)
        {
            TryUseAbility();
            if (state != PlayerState.Ground)
                return;   // launched into the air
        }

        if (door)
        {
            Door near = Door.Nearest(transform.position, DoorReach);
            if (near != null)
            {
                near.Toggle(transform.position);
                NetGame.LocalDoor(near, transform.position);
            }
        }

        if (aim)
        {
            aimingDownSights = !aimingDownSights;
            adsFromFire = false;
        }

        // Holding BOMBA / TAKTİK: aiming a throw (both hands on the grenade, no shooting or aiming down sights).
        UpdateThrowInput(tc);
        if (cooking)
            fire = false;

        // Fire mode from the settings: tap to aim (ADS while the button is held), hip fire, or automatic.
        int fireMode = currentWeapon.weaponData != null ? GameSettings.FireModeFor(currentWeapon.weaponData.weaponType) : 1;
        if (fireMode == 2 && currentWeapon.weaponData != null && !isSprinting && EnemyUnderCrosshair())
            fire = true;
        if (fireMode == 0 && fire && !aim && !aimingDownSights && !isSprinting)
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

        if (jump && isVaulting)
            jump = false;
        if (jump && isProne)
        {
            SetProne(false);   // jump from lying down: get up first
            jump = false;
        }
        if (jump && controller.isGrounded)
        {
            if (isCrouching)
                SetCrouch(false);
            else if (!TryVault())
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        if (crouch)
            SetCrouch(!isCrouching);
        if (prone)
        {
            if (isProne)
                SetProne(false);
            else if (controller.isGrounded && !isSwimming)
                SetProne(true);
        }
        if (reload)
            currentWeapon.Reload();
        if (medkit)
            UseMedkit();
        if (drink)
            UseDrink();
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
            float spread = currentWeapon.weaponData.spread;
            float extraSpread = isSprinting ? 2.5f : (isProne ? -spread * 0.3f : isCrouching ? 0f : 0.3f);
            var wtype = currentWeapon.weaponData.weaponType;
            bool automatic = wtype == WeaponType.Rifle || wtype == WeaponType.SMG;
            if (aimingDownSights)
            {
                // much tighter when aiming; assault rifles and SMGs land where the sight is
                float tight = automatic ? 0.8f : 0.65f;
                if (isProne) tight = Mathf.Max(tight, 0.85f);
                else if (isCrouching) tight = Mathf.Max(tight, automatic ? 0.84f : 0.72f);
                extraSpread = -spread * tight;
            }
            bool killed;
            if (currentWeapon.TryFire(origin, cam.forward, Team, Physics.DefaultRaycastLayers, extraSpread, out killed))
            {
                lastFireTime = Time.time;
                if (gm.uiManager != null)
                    gm.uiManager.CrosshairKick(0.18f + currentWeapon.weaponData.Recoil * 0.06f);
                if (Ability != null)
                    Ability.EndStealth();   // shooting gives you away
                float recoil = currentWeapon.weaponData.Recoil * (isProne ? 0.6f : isCrouching ? 0.85f : 1f);
                bool ads = aimingDownSights;
                if (ads)
                    recoil *= automatic ? 0.55f : 0.8f;   // a shouldered gun climbs less
                if (!currentWeapon.weaponData.suppressed)
                    BotAgent.Noise(transform.position, 45f, Team);   // gunshots are heard (a suppressor keeps you hidden)
                pendingRecoil += recoil;   // the aim climbs over a few frames (UpdatePunch) ...
                recoilDebt = Mathf.Min(recoilDebt + recoil, 25f);   // ... and settles back when you stop (recovery)
                transform.Rotate(0f, Random.Range(-1f, 1f) * recoil * (ads ? 0.08f : 0.25f), 0f);   // mostly vertical
                Punch(new Vector3(-recoil * (ads ? 0.35f : 0.7f), Random.Range(-0.25f, 0.25f) * recoil, Random.Range(-0.4f, 0.4f) * recoil * (ads ? 0.3f : 1f)));
                Shake((0.04f + recoil * 0.025f) * (ads ? 0.4f : 1f));
                if (killed)
                    gm.OnPlayerKill();
            }
        }
    }
}
