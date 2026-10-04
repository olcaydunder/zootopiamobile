using UnityEngine;

/// <summary>PlayerController part: Walking, crouching, the plane / skydive / parachute, vehicles and the knocked-down crawl.</summary>
public partial class PlayerController
{
    private Vector2 MoveInput(IPlayerInput tc)
    {
        return tc != null ? tc.Move : Vector2.zero;
    }

    private void HandleMovement(IPlayerInput tc)
    {
        if (isVaulting)
        {
            UpdateVault();
            return;
        }
        Vector2 input = MoveInput(tc);

        bool sprintInput = tc != null && tc.SprintHeld;
        isSprinting = sprintInput && input.y > 0.4f && !isCrouching;

        Vector3 move = transform.right * input.x + transform.forward * input.y;
        move = Vector3.ClampMagnitude(move, 1f);

        float speed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : moveSpeed);
        if (currentWeapon != null && currentWeapon.weaponData != null)
            speed *= Mathf.Clamp(0.85f + 0.15f * currentWeapon.weaponData.mobilityMul, 0.75f, 1.15f);
        if (aimingDownSights)
            speed *= 0.6f;

        // Shallow water only: stop before wading into deep sea.
        Vector3 ahead = transform.position + move * 1.5f;
        if (move.sqrMagnitude > 0.01f && World.HeightAt(ahead.x, ahead.z) < -1.2f &&
            World.HeightAt(ahead.x, ahead.z) < World.HeightAt(transform.position.x, transform.position.z))
            move = Vector3.zero;

        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;

        controller.Move((move * speed + Vector3.up * velocity.y) * Time.deltaTime);

        if (controller.isGrounded && move.sqrMagnitude > 0.05f)
        {
            stepDistance += move.magnitude * speed * Time.deltaTime;
            float stride = isSprinting ? 2.6f : 2.1f;
            if (stepDistance > stride)
            {
                stepDistance = 0f;
                Sfx.Play(SoundBank.Footstep, isCrouching ? 0.08f : 0.22f, Random.Range(0.85f, 1.1f));
            }
        }
        rig.crouched = isCrouching;
    }

    private void SetCrouch(bool crouched)
    {
        isCrouching = crouched;
        controller.height = crouched ? 1.2f : 1.8f;
        controller.center = crouched ? new Vector3(0f, -0.3f, 0f) : Vector3.zero;
        cameraPivot.localPosition = crouched ? new Vector3(0f, 0.25f, 0f) : new Vector3(0f, 0.75f, 0f);
        rig.crouched = crouched;
    }

    // ----- Plane / skydive / parachute -----

    public void BoardPlane(AirPlane dropPlane)
    {
        plane = dropPlane;
        state = PlayerState.Plane;
        controller.enabled = false;
        rig.SetVisible(false);
        currentWeapon.gameObject.SetActive(false);
        camTarget = 22f;
        camDistance = 22f;
        pitch = 15f;
        transform.position = plane.transform.position;
        transform.rotation = Quaternion.LookRotation(plane.Direction);
    }

    private void UpdatePlane(IPlayerInput tc)
    {
        if (plane == null)
        {
            Jump();
            return;
        }
        transform.position = plane.transform.position - Vector3.up * 1.5f;
        HeightAboveGround = transform.position.y - World.HeightAt(transform.position.x, transform.position.z);

        bool jump = tc != null && tc.ConsumeAirAction();
        if (jump || plane.Finished)
            Jump();
    }

    private void Jump()
    {
        Vector3 forward = plane != null ? plane.Direction : transform.forward;
        if (plane != null)
            transform.position = plane.transform.position - Vector3.up * 4f;
        state = PlayerState.Freefall;
        airVelocity = forward * 12f;
        rig.SetVisible(true);
        rig.pose = RigPose.Freefall;
        camTarget = 6f;
        pitch = 35f;
        wind.Play();
        Sfx.Play(SoundBank.Whoosh, 0.6f);

        var gm = GameManager.Instance;
        if (gm != null)
            gm.OnPlayerJumped();
    }

    private void OpenParachute()
    {
        state = PlayerState.Parachute;
        rig.pose = RigPose.Parachute;
        camTarget = 7f;
        wind.volume = 0.2f;
        Sfx.Play(SoundBank.Whoosh, 0.5f, 0.6f);
    }

    private void UpdateAir(IPlayerInput tc)
    {
        float dt = Time.deltaTime;
        Vector2 input = MoveInput(tc);
        Vector3 fwd = transform.forward;
        Vector3 right = transform.right;
        bool action = tc != null && tc.ConsumeAirAction();

        float ground = World.GroundHeight(transform.position.x, transform.position.z);
        HeightAboveGround = transform.position.y - ground;

        Vector3 target;
        if (state == PlayerState.Freefall)
        {
            float dive = input.y > 0.5f ? -40f : -30f;
            target = fwd * (input.y * 22f) + right * (input.x * 12f) + Vector3.up * dive;
            if (action || HeightAboveGround < 45f)
                OpenParachute();
        }
        else
        {
            target = fwd * (5f + input.y * 5f) + right * (input.x * 5f) + Vector3.up * (input.y < -0.5f ? -4f : -6.5f);
        }

        // Over the sea and getting low: drift back toward land so nobody lands in the water.
        if (HeightAboveGround < 60f && World.HeightAt(transform.position.x, transform.position.z) < 0.5f)
        {
            Vector3 toCentre = -new Vector3(transform.position.x, 0f, transform.position.z).normalized;
            target += toCentre * 9f;
        }

        airVelocity = Vector3.Lerp(airVelocity, target, dt * 2f);
        Vector3 next = transform.position + airVelocity * dt;

        // Stay above the sea area around the island.
        Vector3 flat = new Vector3(next.x, 0f, next.z);
        if (flat.magnitude > World.MapSize * 0.5f - 5f)
        {
            flat = flat.normalized * (World.MapSize * 0.5f - 5f);
            next = new Vector3(flat.x, next.y, flat.z);
        }

        float landHeight = World.GroundHeight(next.x, next.z);
        if (next.y - 0.95f <= landHeight)
        {
            Land(new Vector3(next.x, landHeight + 0.95f, next.z));
            return;
        }
        transform.position = next;
        wind.pitch = state == PlayerState.Freefall ? 1.2f : 0.8f;
    }

    private void Land(Vector3 position)
    {
        if (World.HeightAt(position.x, position.z) < 0f)
            position = World.RandomOpenPoint(new Vector3(position.x, 0f, position.z).normalized * (World.IslandRadius - 15f), 8f);
        transform.position = position;
        state = PlayerState.Ground;
        controller.enabled = true;
        velocity = Vector3.zero;
        rig.pose = RigPose.Normal;
        currentWeapon.gameObject.SetActive(true);
        camTarget = 3.6f;
        pitch = 5f;
        wind.Stop();
        wind.volume = 0.5f;
        Effects.Dust(position - Vector3.up * 0.9f, 10);
        Sfx.Play(SoundBank.Land, 0.6f);
        HeightAboveGround = 0f;
    }

    // ----- Vehicles -----

    private void EnterVehicle(Vehicle v)
    {
        EndVault();
        vehicle = v;
        v.SetDriver(this);
        state = PlayerState.Driving;
        controller.enabled = false;
        if (isCrouching)
            SetCrouch(false);
        rig.pose = RigPose.Driving;
        currentWeapon.gameObject.SetActive(false);
        camTarget = 7f;
        lookYaw = 0f;
    }

    private void ExitVehicle()
    {
        if (vehicle == null)
            return;
        transform.position = vehicle.ExitPosition();
        transform.rotation = Quaternion.Euler(0f, vehicle.Yaw, 0f);
        vehicle.SetDriver(null);
        vehicle = null;
        state = PlayerState.Ground;
        controller.enabled = true;
        rig.pose = RigPose.Normal;
        currentWeapon.gameObject.SetActive(true);
        camTarget = 3.6f;
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void UpdateDriving(GameManager gm, IPlayerInput tc)
    {
        if (vehicle == null)
        {
            state = PlayerState.Ground;
            controller.enabled = true;
            return;
        }

        bool exit = tc != null && tc.ConsumeVehicle();
        if (exit)
        {
            ExitVehicle();
            return;
        }

        vehicle.Drive(MoveInput(tc), Time.deltaTime);
        transform.position = vehicle.SeatPosition;
        transform.rotation = Quaternion.Euler(0f, vehicle.Yaw, 0f);

        if (vehicle.transform.position.y < -5f)
            TakeDamage(9999f, -1);
    }

    /// <summary>Called by weapons and grenades just before damaging the player, for the hit indicator.</summary>
    public void MarkHitFrom(Vector3 source)
    {
        lastHitFrom = source;
        lastHitHasSource = true;
    }

    private void GoDown()
    {
        EndVault();
        isDowned = true;
        health = maxHealth;            // now bleed-out health
        boostRemaining = 0f;
        reviveProgress = 0f;
        aimingDownSights = false;
        if (state == PlayerState.Driving)
            ExitVehicle();
        if (!isCrouching)
            SetCrouch(true);
        currentWeapon.gameObject.SetActive(false);
        var gm = GameManager.Instance;
        if (gm != null && gm.uiManager != null)
            gm.uiManager.Toast("Yere düştün! Takım arkadaşın seni kaldıracak");
    }

    private void UpdateDowned(GameManager gm, IPlayerInput tc)
    {
        // Bleed out slowly; crawl at walking-pace / 4.
        health -= 4f * Time.deltaTime;
        if (health <= 0f || gm.AliveAllies() == 0 || transform.position.y < -15f)
        {
            health = 0f;
            isDowned = false;
            isDead = true;
            rig.pose = RigPose.Dead;
            gm.OnPlayerEliminated();
            return;
        }

        Vector2 input = MoveInput(tc);
        Vector3 move = Vector3.ClampMagnitude(transform.right * input.x + transform.forward * input.y, 1f);
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -2f;
        velocity.y += gravity * Time.deltaTime;
        controller.Move((move * 1.2f + Vector3.up * velocity.y) * Time.deltaTime);
        rig.crouched = true;
        rig.aiming = false;
    }

    /// <summary>Teammate bots call this every frame while standing next to the downed player.</summary>
    public void ReviveTick(float dt)
    {
        if (!isDowned || isDead)
            return;
        reviveProgress += dt;
        if (reviveProgress >= ReviveTime)
        {
            isDowned = false;
            reviveProgress = 0f;
            health = 30f;
            SetCrouch(false);
            currentWeapon.gameObject.SetActive(true);
            var gm = GameManager.Instance;
            if (gm != null && gm.uiManager != null)
                gm.uiManager.Toast("Kaldırıldın! +30 can");
        }
    }
}
