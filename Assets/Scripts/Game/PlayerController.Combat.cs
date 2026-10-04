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

    private void ThrowGrenade()
    {
        var ui = GameManager.Instance.uiManager;
        if (inventory.grenades <= 0)
        {
            ui.Toast("El bomban yok");
            return;
        }
        inventory.grenades--;
        Transform cam = playerCamera.transform;
        Vector3 origin = cameraPivot.position + cam.forward * 0.9f + Vector3.up * 0.2f;
        Grenade.Throw(origin, cam.forward * 17f + Vector3.up * 5f, this);
        Sfx.Play(SoundBank.Whoosh, 0.4f, 1.3f);
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
        float amount = Ability != null && Ability.cls == PlayerClass.Medic ? 52f : 40f;   // Sahra Hekimi: +30%
        health = Mathf.Min(maxHealth, health + amount);
        if (ui != null) ui.Toast("+" + Mathf.RoundToInt(amount) + " can");
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
        bool reload = tc != null && tc.ConsumeReload();
        bool medkit = tc != null && tc.ConsumeMedkit();
        bool drink = tc != null && tc.ConsumeDrink();
        bool grenade = tc != null && tc.ConsumeGrenade();
        bool swap = tc != null && tc.ConsumeSwap();
        bool useVehicle = tc != null && tc.ConsumeVehicle();
        bool fire = tc != null && tc.FireHeld;
        bool aim = tc != null && tc.ConsumeAim();
        bool door = tc != null && tc.ConsumeDoor();
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
                near.Toggle(transform.position);
        }

        if (aim)
        {
            aimingDownSights = !aimingDownSights;
            adsFromFire = false;
        }

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
        if (jump && controller.isGrounded)
        {
            if (isCrouching)
                SetCrouch(false);
            else if (!TryVault())
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        if (crouch)
            SetCrouch(!isCrouching);
        if (reload)
            currentWeapon.Reload();
        if (medkit)
            UseMedkit();
        if (drink)
            UseDrink();
        if (grenade)
            ThrowGrenade();
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
            float extraSpread = isSprinting ? 2.5f : (isCrouching ? 0f : 0.3f);
            if (aimingDownSights)
                extraSpread = -currentWeapon.weaponData.spread * 0.65f;   // much tighter when aiming
            bool killed;
            if (currentWeapon.TryFire(origin, cam.forward, Team, Physics.DefaultRaycastLayers, extraSpread, out killed))
            {
                lastFireTime = Time.time;
                if (Ability != null)
                    Ability.EndStealth();   // shooting gives you away
                pitch -= currentWeapon.weaponData.Recoil;
                transform.Rotate(0f, Random.Range(-0.3f, 0.3f) * currentWeapon.weaponData.Recoil, 0f);
                Shake(0.08f + currentWeapon.weaponData.Recoil * 0.04f);
                if (killed)
                    gm.OnPlayerKill();
            }
        }
    }
}
