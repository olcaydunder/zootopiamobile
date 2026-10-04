using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every drivable vehicle: off-road car, ATV, motorbike, truck (on the ground), boat (on the sea),
/// helicopter (flies) and tank (turret + cannon). Arcade handling on a CharacterController; the
/// model comes from Resources/Models/Vehicles with named moving parts (Wheel_n, Rotor, Turret…).
/// Occupied vehicles can be shot and destroyed; the driver takes a share of the damage.
/// </summary>
public class Vehicle : MonoBehaviour, IDamageable
{
    public PlayerController driver;
    public float speed;
    public VehicleDef def;

    private CharacterController cc;
    private Transform body;          // tilts with the ground / leans / banks
    private readonly List<Transform> wheels = new List<Transform>();
    private Transform handlebar, rotor, tailRotor, turret, gun, muzzle;
    private float wheelSpin;
    private float yaw;
    private float verticalVelocity;
    private float steer;
    private float hp;
    private bool destroyed;
    private AudioSource engine;
    private float rotorSpeed;
    private float altitudeInput;
    private Vector3 flyVelocity;
    private float turretYaw, gunPitch;
    private float nextCannon;
    private readonly Dictionary<IDamageable, float> lastHit = new Dictionary<IDamageable, float>();

    // Paint slots that camouflage replaces.
    private readonly List<Renderer> paintRenderers = new List<Renderer>();
    private readonly List<int> paintSlots = new List<int>();
    private Material factoryPaint;
    private string camoShown = "";

    public const float CannonReload = 4f;
    public const float CannonDamage = 120f;
    public const float CannonRadius = 4f;

    public int Team { get { return driver != null ? driver.Team : -99; } }
    public bool IsDead { get { return destroyed || driver == null; } }   // empty vehicles are not targets
    public bool IsAirborne { get { return false; } }
    public string DisplayName { get { return def != null ? def.name : "Araç"; } }
    public Vector3 AimPoint { get { return transform.position + Vector3.up * (def != null ? def.ccHeight * 0.6f : 1.3f); } }
    public Vector3 SeatPosition { get { return transform.position + transform.rotation * def.seat; } }
    public float Yaw { get { return yaw; } }
    public float Health01 { get { return def != null ? Mathf.Clamp01(hp / def.health) : 1f; } }
    public bool Destroyed { get { return destroyed; } }
    public float HeightAboveGround { get; private set; }
    public float CannonReady01 { get { return Mathf.Clamp01(1f - (nextCannon - Time.time) / CannonReload); } }

    public static Vehicle Spawn(VehicleKind kind, Vector3 groundPos, float yawDeg)
    {
        var d = VehicleDefs.Get(kind);
        var go = new GameObject(d.model);
        go.transform.position = groundPos + Vector3.up * 0.05f;
        go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        var v = go.AddComponent<Vehicle>();
        v.def = d;
        v.yaw = yawDeg;
        v.Build();
        return v;
    }

    private void Build()
    {
        hp = def.health;
        cc = gameObject.AddComponent<CharacterController>();
        cc.radius = def.ccRadius;
        cc.height = Mathf.Max(def.ccHeight, def.ccRadius * 2f);
        cc.center = new Vector3(0f, cc.height * 0.5f + (def.water ? -0.4f : 0f), 0f);
        cc.slopeLimit = 50f;
        cc.stepOffset = Mathf.Min(0.6f, cc.height * 0.3f);

        body = new GameObject("Body").transform;
        body.SetParent(transform, false);

        Color paint = def.paints[Random.Range(0, def.paints.Length)];
        factoryPaint = MaterialCache.Lit(paint);
        var model = ModelLibrary.Spawn("Models/Vehicles/" + def.model, body);
        if (model != null)
            SetupModel(model);
        else
            BuildFallback(paint);

        // A box matching the model for bullets (the capsule alone is too round for long vehicles).
        if (model != null)
        {
            Bounds b = ModelLibrary.RenderBounds(model);
            var hit = new GameObject("HitBox");
            hit.transform.SetParent(transform, false);
            var box = hit.AddComponent<BoxCollider>();
            Vector3 local = transform.InverseTransformPoint(b.center);
            box.center = local;
            box.size = new Vector3(b.size.x * 0.9f, b.size.y * 0.85f, b.size.z * 0.92f);
            if (def.flying)
                box.size = new Vector3(Mathf.Min(box.size.x, 2.6f), box.size.y, box.size.z);   // not the rotor disc
            Physics.IgnoreCollision(cc, box);
        }

        engine = Sfx.CreateLoop(transform, SoundBank.EngineLoop, def.flying ? 0.5f : 0.35f, true);
        if (def.water)
            SnapToWater();
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name)
            return t;
        foreach (Transform c in t)
        {
            var r = FindDeep(c, name);
            if (r != null)
                return r;
        }
        return null;
    }

    private void SetupModel(GameObject model)
    {
        for (int i = 0; i < 8; i++)
        {
            var w = FindDeep(model.transform, "Wheel_" + i);
            if (w == null)
                break;
            wheels.Add(w);
        }
        handlebar = FindDeep(model.transform, "Handlebar");
        rotor = FindDeep(model.transform, "Rotor");
        tailRotor = FindDeep(model.transform, "TailRotor");
        turret = FindDeep(model.transform, "Turret");
        gun = FindDeep(model.transform, "Gun");
        muzzle = FindDeep(model.transform, "Muzzle");

        // Remember which material slots are paint, then share materials by colour (batching).
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name.StartsWith("Paint"))
                {
                    paintRenderers.Add(r);
                    paintSlots.Add(i);
                }
        }
        ModelLibrary.ShareMaterials(model, true);
        ApplyPaint(factoryPaint);
    }

    private void BuildFallback(Color paint)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(box.GetComponent<Collider>());
        box.transform.SetParent(body, false);
        box.transform.localPosition = new Vector3(0f, cc.height * 0.4f, 0f);
        box.transform.localScale = new Vector3(def.ccRadius * 1.8f, cc.height * 0.6f, def.ccRadius * 3.6f);
        var r = box.GetComponent<Renderer>();
        r.sharedMaterial = factoryPaint;
        paintRenderers.Add(r);
        paintSlots.Add(0);
    }

    private void ApplyPaint(Material m)
    {
        for (int i = 0; i < paintRenderers.Count; i++)
        {
            var mats = paintRenderers[i].sharedMaterials;
            mats[paintSlots[i]] = m;
            paintRenderers[i].sharedMaterials = mats;
        }
    }

    /// <summary>Paints the body with a vehicle camo (Cosmetics.VehicleCamos); "" restores the factory paint.</summary>
    public void SetCamo(string id)
    {
        id = id ?? "";
        if (id == camoShown || destroyed)
            return;
        var mat = WeaponDressing.CamoMaterial(Cosmetics.FindVehicleCamo(id), 2.5f, 1.4f);
        ApplyPaint(mat != null ? mat : factoryPaint);
        camoShown = id;
    }

    // ----- Driving -----

    /// <summary>Called every frame by the driver. vertical: helicopter climb (+1) / descend (-1).</summary>
    public void Drive(Vector2 input, float vertical, float dt)
    {
        if (destroyed)
            return;
        steer = Mathf.Clamp(input.x, -1f, 1f);
        altitudeInput = Mathf.Clamp(vertical, -1f, 1f);
        float target = input.y > 0f ? input.y * def.maxSpeed : input.y * def.reverse;
        float accel = Mathf.Abs(target) > Mathf.Abs(speed) && Mathf.Sign(target) == Mathf.Sign(speed) ? def.accel : def.accel * 2f;
        speed = Mathf.MoveTowards(speed, target, accel * dt);

        float steerFactor = def.pivotTurn || def.flying || def.water ? 1f : Mathf.Clamp(speed / 6f, -1f, 1f);
        if (def.water)
            steerFactor = Mathf.Clamp(Mathf.Abs(speed) / 4f, 0.25f, 1f) * Mathf.Sign(speed == 0f ? 1f : speed);
        yaw += steer * def.turnRate * steerFactor * dt;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (destroyed)
            return;
        if (driver == null)
        {
            speed = Mathf.MoveTowards(speed, 0f, 10f * dt);
            steer = 0f;
            altitudeInput = 0f;
        }
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (def.flying)
            UpdateFlying(dt);
        else if (def.water)
            UpdateBoat(dt);
        else
            UpdateGround(dt);

        AnimateParts(dt);

        bool running = driver != null || (def.flying && rotorSpeed > 0.1f);
        if (running)
        {
            if (!engine.isPlaying)
                engine.Play();
            engine.pitch = def.flying ? 0.6f + rotorSpeed * 0.6f : 0.7f + Mathf.Abs(speed) / def.maxSpeed * 1.1f;
        }
        else if (engine.isPlaying)
            engine.Stop();
    }

    private void UpdateGround(float dt)
    {
        if (cc.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += -20f * dt;

        Vector3 ahead = transform.position + transform.forward * Mathf.Sign(speed) * 3f;
        if (Mathf.Abs(speed) > 0.05f && World.HeightAt(ahead.x, ahead.z) < -0.8f)
            speed = 0f;     // don't drive into the sea

        if (driver != null || Mathf.Abs(speed) > 0.05f || !cc.isGrounded)
        {
            CollisionFlags flags = cc.Move((transform.forward * speed + Vector3.up * verticalVelocity) * dt);
            if ((flags & CollisionFlags.Sides) != 0 && Mathf.Abs(speed) > 3f)
                speed *= def.kind == VehicleKind.Truck || def.kind == VehicleKind.Tank ? 0.75f : 0.4f;
        }

        // Tilt with the ground; bikes lean into turns.
        float len = Mathf.Max(1.2f, def.ccRadius * 1.6f);
        Vector3 f = transform.position + transform.forward * len;
        Vector3 b = transform.position - transform.forward * len;
        float pitch = -Mathf.Atan2(World.HeightAt(f.x, f.z) - World.HeightAt(b.x, b.z), len * 2f) * Mathf.Rad2Deg;
        float lean = def.kind == VehicleKind.Moto ? -steer * Mathf.Clamp01(Mathf.Abs(speed) / 12f) * 22f : 0f;
        body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(Mathf.Clamp(pitch, -25f, 25f), 0f, lean), dt * 8f);

        if (transform.position.y < -8f)
        {
            // Drove into the sea: put it back on land.
            Vector3 p = World.RandomOpenPoint(transform.position * 0.8f, 6f);
            cc.enabled = false;
            transform.position = p;
            cc.enabled = true;
            speed = 0f;
        }
    }

    /// <summary>Ground (or roof) under the vehicle, ignoring its own colliders (the ray starts inside them).</summary>
    private float GroundBelow()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out hit, 600f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return World.HeightAt(transform.position.x, transform.position.z);
    }

    private void SnapToWater()
    {
        cc.enabled = false;
        var p = transform.position;
        transform.position = new Vector3(p.x, PlayerController.WaterY - 0.05f, p.z);
        cc.enabled = true;
    }

    private void UpdateBoat(float dt)
    {
        // Only where it is deep enough: stop at the beach.
        Vector3 ahead = transform.position + transform.forward * Mathf.Sign(speed) * 3.2f;
        if (Mathf.Abs(speed) > 0.05f && PlayerController.WaterDepthAt(ahead) < 0.7f)
            speed = Mathf.MoveTowards(speed, 0f, 30f * dt);
        Vector3 move = transform.forward * speed * dt;
        float bob = Mathf.Sin(Time.time * 1.6f + transform.position.x * 0.1f) * 0.06f;
        float targetY = PlayerController.WaterY - 0.05f + bob;
        move.y = targetY - transform.position.y;
        CollisionFlags flags = cc.Move(move);
        if ((flags & CollisionFlags.Sides) != 0 && Mathf.Abs(speed) > 3f)
            speed *= 0.5f;
        // Bow lifts with speed, rolls in turns.
        float rise = Mathf.Clamp01(Mathf.Abs(speed) / def.maxSpeed) * -7f;
        body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(rise + bob * 20f, 0f, -steer * 8f * Mathf.Clamp01(Mathf.Abs(speed) / 8f)), dt * 4f);
    }

    private void UpdateFlying(float dt)
    {
        rotorSpeed = Mathf.MoveTowards(rotorSpeed, driver != null ? 1f : 0f, dt * 0.5f);
        float ground = GroundBelow();
        HeightAboveGround = transform.position.y - ground;
        bool lifted = rotorSpeed > 0.75f;

        Vector3 want = transform.forward * (lifted ? speed : 0f);
        float climb;
        if (driver == null)
            climb = HeightAboveGround > 0.2f ? -4f : 0f;              // settles down when abandoned
        else if (!lifted)
            climb = 0f;                                                // spinning up
        else
            climb = altitudeInput * 9f + (altitudeInput == 0f ? 0f : 0f);
        if (HeightAboveGround > 160f && climb > 0f)
            climb = 0f;                                                // ceiling
        want.y = climb;
        flyVelocity = Vector3.Lerp(flyVelocity, want, dt * 1.8f);

        // Keep inside the map.
        Vector3 next = transform.position + flyVelocity * dt;
        float lim = World.MapSize * 0.5f - 10f;
        if (Mathf.Abs(next.x) > lim || Mathf.Abs(next.z) > lim)
            flyVelocity = new Vector3(0f, flyVelocity.y, 0f);

        CollisionFlags flags = cc.Move(flyVelocity * dt);
        if ((flags & CollisionFlags.Sides) != 0)
            speed *= 0.5f;
        if ((flags & CollisionFlags.Below) != 0 && flyVelocity.y < 0f)
            flyVelocity.y = 0f;

        // Nose down when flying forward, bank into turns.
        float pitch = Mathf.Clamp(speed / def.maxSpeed, -0.5f, 1f) * 14f * (lifted ? 1f : 0f);
        float bank = -steer * 18f * Mathf.Clamp01(Mathf.Abs(speed) / 10f);
        body.localRotation = Quaternion.Slerp(body.localRotation, Quaternion.Euler(pitch, 0f, bank), dt * 3f);
    }

    private void AnimateParts(float dt)
    {
        // Model parts: +X right, +Z up, -Y forward in their local space (Blender export).
        if (wheels.Count > 0 && def.wheelRadius > 0f)
        {
            wheelSpin += speed * dt / def.wheelRadius * Mathf.Rad2Deg;
            float steerAngle = steer * (def.kind == VehicleKind.Moto ? 18f : 28f);
            for (int i = 0; i < wheels.Count; i++)
            {
                bool front = def.kind == VehicleKind.Moto ? i == 0 : i < 2;
                Quaternion spin = Quaternion.AngleAxis(wheelSpin, Vector3.right);
                wheels[i].localRotation = front && def.kind != VehicleKind.Moto && !def.pivotTurn
                    ? Quaternion.AngleAxis(steerAngle, Vector3.forward) * spin
                    : spin;
            }
            if (handlebar != null)
                handlebar.localRotation = Quaternion.AngleAxis(steerAngle, new Vector3(0f, 0.43f, 0.9f).normalized);
        }
        if (rotor != null)
            rotor.Rotate(0f, 0f, rotorSpeed * 1100f * dt, Space.Self);
        if (tailRotor != null)
            tailRotor.Rotate(rotorSpeed * 1600f * dt, 0f, 0f, Space.Self);
        if (turret != null)
            turret.localRotation = Quaternion.AngleAxis(turretYaw, Vector3.forward);
        if (gun != null)
            gun.localRotation = Quaternion.AngleAxis(-gunPitch, Vector3.right);
    }

    // ----- Tank -----

    /// <summary>Turns the turret and gun toward a world direction (the driver's camera).</summary>
    public void AimTurret(Vector3 worldDir, float dt)
    {
        if (!def.cannon || turret == null)
            return;
        Vector3 flat = new Vector3(worldDir.x, 0f, worldDir.z);
        if (flat.sqrMagnitude > 0.001f)
        {
            float wantYaw = Mathf.DeltaAngle(yaw, Quaternion.LookRotation(flat).eulerAngles.y);
            turretYaw = Mathf.MoveTowardsAngle(turretYaw, wantYaw, 70f * dt);
        }
        float wantPitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(worldDir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg + 2f, -6f, 18f);
        gunPitch = Mathf.MoveTowards(gunPitch, wantPitch, 30f * dt);
    }

    /// <summary>Fires the tank cannon: an explosive shell along the barrel. False while reloading.</summary>
    public bool FireCannon(PlayerController shooter)
    {
        if (!def.cannon || destroyed || Time.time < nextCannon)
            return false;
        nextCannon = Time.time + CannonReload;
        Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 2.4f + transform.forward * 4f;
        Vector3 dir = muzzle != null ? muzzle.forward : transform.forward;
        Vector3 end = origin + dir * 220f;
        RaycastHit hit;
        if (Physics.Raycast(origin, dir, out hit, 220f, ~0, QueryTriggerInteraction.Ignore))
            end = hit.point;
        AbilityFx.Flash(origin, new Color(1f, 0.8f, 0.4f, 0.9f), 2.2f, 0.18f);
        Sfx.PlayAt(SoundBank.Explosion, origin, 1f, 1.5f);
        Explode(end, shooter);
        if (shooter != null)
            shooter.Shake(0.5f);
        return true;
    }

    private void Explode(Vector3 point, PlayerController shooter)
    {
        Effects.Explosion(point);
        Sfx.PlayAt(SoundBank.Explosion, point, 1f, 0.9f);
        var gm = GameManager.Instance;
        if (gm == null)
            return;
        int team = Team;
        foreach (var c in gm.Combatants)
        {
            if (c == null || c.IsDead || c.IsAirborne || c.Team == team)
                continue;
            float d = Vector3.Distance(c.AimPoint, point);
            if (d > CannonRadius * 1.5f)
                continue;
            float dmg = CannonDamage * Mathf.Clamp01(1f - d / (CannonRadius * 1.5f)) * ClassAbility.ExplosionTaken(c);
            var p = c as PlayerController;
            if (p != null)
                p.MarkHitFrom(point);
            bool killed = c.TakeDamage(dmg, team);
            if (shooter != null)
            {
                if (killed)
                    gm.OnPlayerKill();
                if (gm.uiManager != null)
                    gm.uiManager.ShowHit(c.AimPoint, dmg, killed, false);
            }
        }
        foreach (var v in gm.vehicles)
            if (v != null && v != this && !v.IsDead && v.Team != team && Vector3.Distance(v.transform.position, point) < CannonRadius + 2f)
                v.TakeDamage(CannonDamage * 2f, team);
    }

    // ----- Damage -----

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (driver == null || Mathf.Abs(speed) < 5f || def.flying)
            return;
        IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
        if (target == null || ReferenceEquals(target, this) || target.IsDead || target.Team == Team)
            return;

        float last;
        if (lastHit.TryGetValue(target, out last) && Time.time - last < 0.6f)
            return;
        lastHit[target] = Time.time;

        float damage = Mathf.Abs(speed) * (def.kind == VehicleKind.Truck || def.kind == VehicleKind.Tank ? 9f : 6f);
        bool killed = target.TakeDamage(damage, Team);
        speed *= def.kind == VehicleKind.Tank ? 0.95f : 0.6f;
        var gm = GameManager.Instance;
        if (gm != null)
        {
            if (killed)
                gm.OnPlayerKill();
            if (gm.uiManager != null)
                gm.uiManager.ShowHit(target.AimPoint, damage, killed, false);
        }
    }

    public bool TakeDamage(float amount, int attackerTeam)
    {
        if (destroyed || driver == null || attackerTeam == Team)
            return false;
        hp -= amount;
        bool killed = driver.TakeDamage(amount * (def.kind == VehicleKind.Tank ? 0.05f : 0.3f) * ClassAbility.VehicleTaken(driver), attackerTeam);
        if (hp <= 0f)
            Wreck(attackerTeam);
        return killed;
    }

    /// <summary>Destroyed: explosion, the driver is thrown out and hurt, the wreck stays dark.</summary>
    private void Wreck(int attackerTeam)
    {
        destroyed = true;
        hp = 0f;
        Effects.Explosion(transform.position + Vector3.up * 1.2f);
        Sfx.PlayAt(SoundBank.Explosion, transform.position, 1f, 0.8f);
        var d = driver;
        if (d != null)
        {
            d.ForceExitVehicle();
            d.TakeDamage(60f, attackerTeam);
        }
        var burnt = MaterialCache.Lit(new Color(0.08f, 0.08f, 0.08f));
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = burnt;
            r.sharedMaterials = mats;
        }
        if (engine != null)
            engine.Stop();
        if (def.flying)
            flyVelocity = Vector3.down * 10f;
    }

    private void LateUpdate()
    {
        // A destroyed helicopter falls to the ground.
        if (destroyed && def.flying)
        {
            float ground = GroundBelow();
            if (transform.position.y > ground + 0.1f)
                cc.Move(Vector3.down * 12f * Time.deltaTime);
        }
    }

    public void SetDriver(PlayerController p)
    {
        driver = p;
        if (p != null)
        {
            yaw = transform.eulerAngles.y;
            SetCamo(Cosmetics.EquippedVehicleCamo);   // your vehicle skin shows on whatever you drive
        }
    }

    public Vector3 ExitPosition()
    {
        Vector3 side = transform.position - transform.right * (def.ccRadius + 1.2f);
        if (def.water)
            return new Vector3(side.x, PlayerController.WaterY + 0.3f, side.z);
        if (def.flying && HeightAboveGround > 4f)
            return side + Vector3.up * 1f;
        return new Vector3(side.x, World.GroundHeight(side.x, side.z) + 1f, side.z);
    }
}
