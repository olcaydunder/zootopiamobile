using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Arcade jeep. The player drives it with the joystick; it rides over the terrain using a
/// CharacterController, runs over enemies and shields the driver (bullets do reduced damage).
/// </summary>
public class Vehicle : MonoBehaviour, IDamageable
{
    public const float MaxSpeed = 17f;
    public const float ReverseSpeed = 6f;

    public PlayerController driver;
    public float speed;

    private CharacterController cc;
    private Transform body;
    private Transform[] wheels;
    private float yaw;
    private float verticalVelocity;
    private AudioSource engine;
    private readonly Dictionary<IDamageable, float> lastHit = new Dictionary<IDamageable, float>();
    private readonly List<Renderer> paintParts = new List<Renderer>();
    private readonly List<Material> paintOriginal = new List<Material>();
    private string camoShown = "";

    /// <summary>Paints the body with a vehicle camo (Cosmetics.VehicleCamos); "" restores the factory paint.</summary>
    public void SetCamo(string id)
    {
        id = id ?? "";
        if (id == camoShown)
            return;
        if (paintOriginal.Count == 0)
            foreach (var r in paintParts)
                paintOriginal.Add(r.sharedMaterial);
        var mat = WeaponDressing.CamoMaterial(Cosmetics.FindVehicleCamo(id), 1.73f, 1.2f);
        for (int i = 0; i < paintParts.Count; i++)
            paintParts[i].sharedMaterial = mat != null ? mat : paintOriginal[i];
        camoShown = id;
    }

    public int Team { get { return driver != null ? driver.Team : -99; } }
    public bool IsDead { get { return driver == null; } }   // empty jeeps don't count as targets
    public bool IsAirborne { get { return false; } }
    public string DisplayName { get { return "Cip"; } }
    public Vector3 AimPoint { get { return transform.position + Vector3.up * 1.3f; } }
    public Vector3 SeatPosition { get { return transform.position + Vector3.up * 1.45f - transform.forward * 0.3f; } }
    public float Yaw { get { return yaw; } }

    public static Vehicle Spawn(Vector3 groundPos, float yawDeg, Color paint)
    {
        var go = new GameObject("Jeep");
        go.transform.position = groundPos + Vector3.up * 0.05f;
        var v = go.AddComponent<Vehicle>();
        v.yaw = yawDeg;
        go.transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
        v.Build(paint);
        return v;
    }

    private Transform Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(color);
        return go.transform;
    }

    private void Build(Color paint)
    {
        cc = gameObject.AddComponent<CharacterController>();
        cc.radius = 1.1f;
        cc.height = 2.2f;
        cc.center = new Vector3(0f, 1.1f, 0f);
        cc.slopeLimit = 50f;
        cc.stepOffset = 0.5f;

        body = new GameObject("Body").transform;
        body.SetParent(transform, false);

        Color dark = new Color(0.15f, 0.15f, 0.15f);
        paintParts.Add(Part(body, PrimitiveType.Cube, new Vector3(0f, 0.75f, 0f), new Vector3(1.9f, 0.6f, 3.9f), paint).GetComponent<Renderer>());
        paintParts.Add(Part(body, PrimitiveType.Cube, new Vector3(0f, 1.1f, 1.35f), new Vector3(1.85f, 0.2f, 1.1f), paint * 0.9f).GetComponent<Renderer>());
        Part(body, PrimitiveType.Cube, new Vector3(0f, 1.45f, 0.65f), new Vector3(1.7f, 0.55f, 0.06f), new Color(0.4f, 0.55f, 0.65f));
        Part(body, PrimitiveType.Cube, new Vector3(0f, 1.15f, -0.5f), new Vector3(0.6f, 0.6f, 0.15f), dark);
        paintParts.Add(Part(body, PrimitiveType.Cube, new Vector3(0f, 1.0f, -1.6f), new Vector3(1.8f, 0.35f, 0.6f), paint * 0.85f).GetComponent<Renderer>());
        Part(body, PrimitiveType.Cube, new Vector3(0f, 0.7f, 1.98f), new Vector3(1.7f, 0.3f, 0.08f), dark);
        Part(body, PrimitiveType.Sphere, new Vector3(-0.6f, 0.8f, 1.98f), new Vector3(0.22f, 0.22f, 0.1f), new Color(1f, 0.95f, 0.7f));
        Part(body, PrimitiveType.Sphere, new Vector3(0.6f, 0.8f, 1.98f), new Vector3(0.22f, 0.22f, 0.1f), new Color(1f, 0.95f, 0.7f));

        wheels = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            float x = i % 2 == 0 ? -1f : 1f;
            float z = i < 2 ? 1.25f : -1.25f;
            var pivot = new GameObject("Wheel").transform;
            pivot.SetParent(body, false);
            pivot.localPosition = new Vector3(x * 0.98f, 0.42f, z);
            var w = Part(pivot, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.84f, 0.16f, 0.84f), dark);
            w.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(w, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.5f, 1.05f, 0.5f), new Color(0.6f, 0.6f, 0.6f));
            wheels[i] = pivot;
        }

        engine = Sfx.CreateLoop(transform, SoundBank.EngineLoop, 0.35f, true);
    }

    /// <summary>Called every frame by the driving player.</summary>
    public void Drive(Vector2 input, float dt)
    {
        float target = input.y > 0f ? input.y * MaxSpeed : input.y * ReverseSpeed;
        float accel = Mathf.Abs(target) > Mathf.Abs(speed) && Mathf.Sign(target) == Mathf.Sign(speed) ? 7f : 14f;
        speed = Mathf.MoveTowards(speed, target, accel * dt);

        float steerFactor = Mathf.Clamp(speed / 6f, -1f, 1f);
        yaw += input.x * 75f * steerFactor * dt;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (driver == null)
            speed = Mathf.MoveTowards(speed, 0f, 10f * dt);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

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
                speed *= 0.4f;
        }

        AlignBody(dt);

        for (int i = 0; i < wheels.Length; i++)
        {
            wheels[i].Rotate(speed * dt * 70f, 0f, 0f, Space.Self);
            if (i < 2 && driver != null)
            {
                Vector3 e = wheels[i].localEulerAngles;
                wheels[i].localRotation = Quaternion.Euler(e.x, TouchSteer() * 25f, 0f);
            }
        }

        if (driver != null)
        {
            if (!engine.isPlaying)
                engine.Play();
            engine.pitch = 0.7f + Mathf.Abs(speed) / MaxSpeed * 1.1f;
        }
        else if (engine.isPlaying)
        {
            engine.Stop();
        }

        if (transform.position.y < -8f)
        {
            // Drove into the sea: put it back on the beach.
            Vector3 p = World.RandomOpenPoint(transform.position * 0.8f, 6f);
            cc.enabled = false;
            transform.position = p;
            cc.enabled = true;
            speed = 0f;
        }
    }

    private float TouchSteer()
    {
        var tc = TouchControls.Instance;
        float x = tc != null ? tc.Move.x : 0f;
        if (Mathf.Abs(x) < 0.01f)
            x = Input.GetAxisRaw("Horizontal");
        return Mathf.Clamp(x, -1f, 1f);
    }

    private void AlignBody(float dt)
    {
        Vector3 f = transform.position + transform.forward * 1.4f;
        Vector3 b = transform.position - transform.forward * 1.4f;
        float hf = World.HeightAt(f.x, f.z);
        float hb = World.HeightAt(b.x, b.z);
        float pitch = -Mathf.Atan2(hf - hb, 2.8f) * Mathf.Rad2Deg;
        Quaternion target = Quaternion.Euler(Mathf.Clamp(pitch, -25f, 25f), 0f, 0f);
        body.localRotation = Quaternion.Slerp(body.localRotation, target, dt * 8f);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (driver == null || Mathf.Abs(speed) < 5f)
            return;
        IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
        if (target == null || ReferenceEquals(target, this) || target.IsDead || target.Team == Team)
            return;

        float last;
        if (lastHit.TryGetValue(target, out last) && Time.time - last < 0.6f)
            return;
        lastHit[target] = Time.time;

        float damage = Mathf.Abs(speed) * 6f;
        bool killed = target.TakeDamage(damage, Team);
        speed *= 0.6f;
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
        // The jeep itself is indestructible; the driver takes part of the hit.
        if (driver != null && attackerTeam != Team)
            return driver.TakeDamage(amount * 0.6f * ClassAbility.VehicleTaken(driver), attackerTeam);
        return false;
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
        Vector3 side = transform.position - transform.right * 2.2f;
        return new Vector3(side.x, World.HeightAt(side.x, side.z) + 1f, side.z);
    }
}
