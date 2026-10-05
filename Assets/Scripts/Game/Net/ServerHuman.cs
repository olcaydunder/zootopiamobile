using UnityEngine;

/// <summary>
/// Game server: stand-in for a player on a phone. Follows the positions the phone sends and has a
/// body bots can see and shoot; damage it takes is forwarded to the phone, which owns its health
/// and reports its own death.
/// </summary>
public class ServerHuman : MonoBehaviour, IDamageable
{
    public int id;
    public int team;
    public string displayName = "Oyuncu";
    public bool dead;
    public int flags = NetProtocol.F_Plane;
    public int weapon = -1;          // WeaponType, -1 = none in hand
    public float pitch;
    public float health = 100f;
    public float armor;
    public double lastState;

    private CapsuleCollider body;

    public int Team { get { return team; } }
    public bool IsDead { get { return dead; } }
    public bool IsAirborne { get { return (flags & NetProtocol.F_Air) != 0; } }
    public string DisplayName { get { return displayName; } }
    public Vector3 AimPoint { get { return transform.position + Vector3.up * ((flags & NetProtocol.F_Crouch) != 0 ? 0.05f : 0.4f); } }

    public static ServerHuman Create(int id, int team, string name, Vector3 position)
    {
        var go = new GameObject("Human_" + id);
        go.transform.position = position;
        var h = go.AddComponent<ServerHuman>();
        h.id = id;
        h.team = team;
        h.displayName = name;
        h.body = go.AddComponent<CapsuleCollider>();
        h.body.height = 1.8f;
        h.body.radius = 0.35f;
        h.body.center = Vector3.zero;
        h.body.enabled = false;   // in the plane
        return h;
    }

    public void ApplyState(Vector3 position, float yaw, float aimPitch, int stateFlags, int weaponType, float hp, float ap)
    {
        if (dead)
            return;
        // Keep it on the map (a broken or hostile client can't park its body under the world).
        float half = World.MapSize * 0.5f;
        position.x = Mathf.Clamp(position.x, -half, half);
        position.z = Mathf.Clamp(position.z, -half, half);
        position.y = Mathf.Clamp(position.y, -30f, 400f);
        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        pitch = aimPitch;
        flags = stateFlags & ~NetProtocol.F_Dead;
        weapon = weaponType;
        health = hp;
        armor = ap;
        bool crouch = (flags & NetProtocol.F_Crouch) != 0;
        body.height = crouch ? 1.2f : 1.8f;
        body.center = crouch ? new Vector3(0f, -0.3f, 0f) : Vector3.zero;
        body.enabled = !IsAirborne;
    }

    /// <summary>5v5: back on the ground (the phone sends its states again from there).</summary>
    public void Revive(Vector3 position)
    {
        dead = false;
        flags = 0;
        weapon = -1;
        health = 100f;
        armor = 50f;
        transform.position = position;
        if (body != null)
            body.enabled = true;
    }

    public void MarkDead()
    {
        dead = true;
        flags |= NetProtocol.F_Dead;
        flags &= ~NetProtocol.F_Air;
        weapon = -1;
        health = 0f;
        if (body != null)
            body.enabled = false;
    }

    /// <summary>The phone applies the damage (armour, death); the server only passes it on.</summary>
    public bool TakeDamage(float amount, int attackerTeam)
    {
        if (dead || IsAirborne || amount <= 0f)
            return false;
        if (NetServer.Instance != null)
            NetServer.Instance.OnHumanDamaged(this, amount, attackerTeam < 0);
        return false;
    }
}
