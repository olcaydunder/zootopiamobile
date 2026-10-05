using UnityEngine;

/// <summary>
/// Phone side: another player or a server bot, drawn from the server's snapshots. Moves smoothly
/// about 0.1 s behind the server (interpolating between snapshots). When the local player hits it,
/// the hit goes to the server, which decides what happens.
/// </summary>
public class NetPuppet : MonoBehaviour, IDamageable
{
    public int id;
    public int team;           // already remapped: 0 = the local player's team
    public bool isBot;
    public string displayName = "";
    public int flags = NetProtocol.F_Plane;
    public float health = 100f;
    public CharacterRig rig;
    public WeaponController weapon;

    private CapsuleCollider body;
    private Renderer weaponBox;
    private int shownWeapon = -2;
    private int shownFlags = -1;
    private float deadSince = -1f;
    private bool hasSample;

    private struct Sample
    {
        public double time;
        public Vector3 position;
        public float yaw;
        public float pitch;
    }

    private readonly Sample[] samples = new Sample[16];
    private int sampleCount;

    private static readonly Color AllyColor = new Color(0.2f, 0.7f, 0.35f);
    private static readonly Color[] EnemyColors =
    {
        new Color(0.85f, 0.35f, 0.2f), new Color(0.8f, 0.2f, 0.4f), new Color(0.9f, 0.6f, 0.15f),
        new Color(0.55f, 0.3f, 0.8f), new Color(0.7f, 0.15f, 0.15f), new Color(0.55f, 0.45f, 0.3f)
    };

    public int Team { get { return team; } }
    public bool IsDead { get { return (flags & NetProtocol.F_Dead) != 0; } }
    public bool IsAirborne { get { return (flags & NetProtocol.F_Air) != 0; } }
    public string DisplayName { get { return displayName; } }
    public Vector3 AimPoint { get { return transform.position + Vector3.up * ((flags & NetProtocol.F_Crouch) != 0 ? 0.05f : 0.4f); } }

    public static NetPuppet Create(int id, int team, bool bot, string name, string skin, string parachute)
    {
        var go = new GameObject((bot ? "NetBot_" : "NetPlayer_") + id);
        go.transform.position = new Vector3(0f, 170f, 0f);
        var p = go.AddComponent<NetPuppet>();
        p.id = id;
        p.team = team;
        p.isBot = bot;
        p.displayName = name;

        p.body = go.AddComponent<CapsuleCollider>();
        p.body.height = 1.8f;
        p.body.radius = 0.35f;
        p.body.center = Vector3.zero;
        p.body.enabled = false;

        var weaponObj = new GameObject("Weapon");
        weaponObj.transform.SetParent(go.transform, false);
        weaponObj.transform.localPosition = new Vector3(0.28f, 0.28f, 0.45f);
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(box.GetComponent<Collider>());
        box.transform.SetParent(weaponObj.transform, false);
        p.weaponBox = box.GetComponent<Renderer>();
        p.weapon = weaponObj.AddComponent<WeaponController>();

        Color shirt = team == 0 ? AllyColor : EnemyColors[team % EnemyColors.Length];
        if (string.IsNullOrEmpty(skin))
            skin = ModelLibrary.EnemySkins[id % ModelLibrary.EnemySkins.Length];
        p.rig = CharacterRig.Build(go, shirt, new Color(0.22f, 0.23f, 0.25f), new Color(0.82f, 0.64f, 0.48f),
            new Color(0.3f, 0.35f, 0.25f), new Color(0.38f, 0.32f, 0.22f), skin);
        p.rig.weaponHold = p.weapon.transform;
        p.rig.footstep = p.OnStep;
        if (!string.IsNullOrEmpty(parachute))
            p.rig.parachuteCamo = parachute;
        else
        {
            var list = Cosmetics.ParachuteCamos;
            p.rig.parachuteCamo = list[id % list.Count].id;
        }
        p.rig.SetVisible(false);
        weaponObj.SetActive(false);

        if (team == 0)
        {
            // Green marker over teammates' heads (same as the offline teammates).
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(marker.GetComponent<Collider>());
            marker.transform.SetParent(go.transform, false);
            marker.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            marker.transform.localScale = new Vector3(0.18f, 0.24f, 0.18f);
            var mr = marker.GetComponent<Renderer>();
            mr.sharedMaterial = UIUtil.UnlitMaterial(new Color(0.3f, 1f, 0.4f, 0.9f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        return p;
    }

    /// <summary>Other players' (and server bots') footsteps when they are close: enemies only.</summary>
    private void OnStep(bool left)
    {
        var p = PlayerController.LocalPlayer;
        if (p == null || team == 0 || IsDead)
            return;
        if (Vector3.Distance(p.transform.position, transform.position) < 30f)
            Footsteps.Play(transform.position - Vector3.up * 0.9f, 0.95f, false);
    }

    /// <summary>New state from a snapshot (server time in seconds since the match started).</summary>
    public void Push(double serverTime, Vector3 position, float yaw, float pitch, int stateFlags, int weaponType, float hp)
    {
        if (sampleCount > 0 && serverTime <= samples[sampleCount - 1].time)
            return;   // out of order
        if (reviveTime >= 0 && serverTime < reviveTime)
            return;   // from before a respawn (still dead there)
        if (sampleCount == samples.Length)
        {
            System.Array.Copy(samples, 1, samples, 0, samples.Length - 1);
            sampleCount--;
        }
        samples[sampleCount++] = new Sample { time = serverTime, position = position, yaw = yaw, pitch = pitch };
        health = hp;
        if (!hasSample)
        {
            hasSample = true;
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        ApplyFlags(stateFlags, weaponType);
    }

    private void ApplyFlags(int stateFlags, int weaponType)
    {
        if (IsDead && (stateFlags & NetProtocol.F_Dead) == 0)
            stateFlags |= NetProtocol.F_Dead;   // the dead stay dead (a late snapshot must not revive them)
        if (stateFlags != shownFlags)
        {
            shownFlags = stateFlags;
            flags = stateFlags;
            bool dead = (flags & NetProtocol.F_Dead) != 0;
            bool plane = (flags & NetProtocol.F_Plane) != 0;
            rig.SetVisible(!plane && (!dead || deadSince < 0f || Time.time - deadSince < 6f));
            if (dead)
            {
                if (deadSince < 0f)
                    deadSince = Time.time;
                rig.pose = RigPose.Dead;
            }
            else if ((flags & NetProtocol.F_Freefall) != 0)
                rig.pose = RigPose.Freefall;
            else if ((flags & NetProtocol.F_Parachute) != 0)
                rig.pose = RigPose.Parachute;
            else if ((flags & NetProtocol.F_Swim) != 0)
                rig.pose = RigPose.Swim;
            else
                rig.pose = RigPose.Normal;
            rig.crouched = (flags & NetProtocol.F_Crouch) != 0 && !dead;
            rig.aiming = (flags & NetProtocol.F_Aim) != 0;
            bool crouch = rig.crouched;
            body.height = crouch ? 1.2f : 1.8f;
            body.center = crouch ? new Vector3(0f, -0.3f, 0f) : Vector3.zero;
            body.enabled = !dead && !IsAirborne;
        }
        if (IsDead || IsAirborne || (flags & NetProtocol.F_Swim) != 0)
            weaponType = -1;
        if (weaponType != shownWeapon)
        {
            shownWeapon = weaponType;
            bool show = weaponType >= 0 && weaponType <= (int)WeaponType.Pistol;
            weapon.gameObject.SetActive(show);
            if (show)
                weapon.Initialize(DefaultWeapon((WeaponType)weaponType), weaponBox);
        }
    }

    private static WeaponData DefaultWeapon(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.SMG: return WeaponData.CreateSMG();
            case WeaponType.Shotgun: return WeaponData.CreateShotgun();
            case WeaponType.Sniper: return WeaponData.CreateSniper();
            case WeaponType.Pistol: return WeaponData.CreatePistol();
            default: return WeaponData.CreateRifle();
        }
    }

    private double reviveTime = -1;

    /// <summary>5v5: back in the fight at <paramref name="position"/> (snapshots older than the respawn are ignored).</summary>
    public void Revive(double serverTime, Vector3 position)
    {
        reviveTime = serverTime;
        sampleCount = 0;
        hasSample = false;
        deadSince = -1f;
        flags = 0;
        shownFlags = -1;
        health = 100f;
        transform.position = position;
        if (rig != null)
        {
            rig.ResetPose();
            rig.SetVisible(true);
        }
        ApplyFlags(0, (int)WeaponType.Rifle);
    }

    /// <summary>The server says this one is out (kill message), before the next snapshot shows it.</summary>
    public void MarkDead()
    {
        ApplyFlags(flags | NetProtocol.F_Dead, -1);
    }

    /// <summary>Places the puppet at <paramref name="renderTime"/> (server time), between the two snapshots around it.</summary>
    public void Tick(double renderTime)
    {
        if (sampleCount == 0)
            return;
        if (deadSince >= 0f && Time.time - deadSince > 6f && rig != null)
            rig.SetVisible(false);   // corpses disappear like the offline bots

        Sample a = samples[0], b = samples[0];
        if (renderTime <= samples[0].time)
        {
            a = b = samples[0];
        }
        else if (renderTime >= samples[sampleCount - 1].time)
        {
            a = b = samples[sampleCount - 1];
            // Short extrapolation over a lost snapshot, then wait.
            if (sampleCount >= 2)
            {
                var prev = samples[sampleCount - 2];
                double span = b.time - prev.time;
                double over = System.Math.Min(renderTime - b.time, 0.15);
                if (span > 0.0001 && over > 0)
                {
                    Vector3 vel = (b.position - prev.position) / (float)span;
                    if (vel.sqrMagnitude < 40f * 40f)
                        b.position += vel * (float)over;
                }
                a = b;
            }
        }
        else
        {
            for (int i = 0; i < sampleCount - 1; i++)
            {
                if (samples[i + 1].time >= renderTime)
                {
                    a = samples[i];
                    b = samples[i + 1];
                    break;
                }
            }
        }
        float t = b.time > a.time ? (float)((renderTime - a.time) / (b.time - a.time)) : 1f;
        t = Mathf.Clamp01(t);
        transform.position = Vector3.Lerp(a.position, b.position, t);
        transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(a.yaw, b.yaw, t), 0f);
        rig.aimPitch = Mathf.Lerp(a.pitch, b.pitch, t);
    }

    /// <summary>Muzzle flash, tracer and shot sound for a shot the server reported.</summary>
    public void PlayShot(Vector3 end, int weaponType)
    {
        if (IsDead)
            return;
        if (weaponType >= 0 && weaponType != shownWeapon && !IsAirborne)
            ApplyFlags(flags, weaponType);
        if (weapon.gameObject.activeInHierarchy)
            weapon.PlayRemoteShot(end);
        else
            Sfx.PlayAt(SoundBank.Gunshot(weaponType >= 0 ? (WeaponType)weaponType : WeaponType.Rifle), transform.position, 0.9f, Random.Range(0.94f, 1.06f));
    }

    /// <summary>The local player hit this one: tell the server (which applies it and decides kills).</summary>
    public bool TakeDamage(float amount, int attackerTeam)
    {
        if (IsDead || IsAirborne || attackerTeam < 0)
            return false;
        if (NetClient.Instance != null)
            NetClient.Instance.SendHit(this, amount, HitContext.Head, HitContext.Weapon);
        rig.PlayHit();
        return false;
    }
}
