using UnityEngine;

/// <summary>Kalkan Ustası: a bullet-proof wall placed in front of the user. Takes damage, then breaks.</summary>
public class ShieldWall : MonoBehaviour, IDamageable, IStructure
{
    public const float Width = 2.5f, Height = 1.8f, Lifetime = 15f;

    private int team;
    private float hp, maxHp;
    private float until;
    private bool dead;
    private Material glassMat;

    public int Team { get { return team; } }
    public bool IsDead { get { return dead; } }
    public bool IsAirborne { get { return false; } }
    public string DisplayName { get { return "Kalkan"; } }
    public Vector3 AimPoint { get { return transform.position + Vector3.up * 1f; } }

    public static ShieldWall Spawn(IAbilityUser user, int level)
    {
        Vector3 dir = user.AimDirection;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : user.transform.forward;
        Vector3 p = user.transform.position + dir * Turret.PlaceDistance(user, dir, 1.6f);
        RaycastHit hit;
        float ground = Physics.Raycast(p + Vector3.up * 1f, Vector3.down, out hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.point.y : World.GroundHeight(p.x, p.z);
        var go = new GameObject("ShieldWall");
        go.transform.SetPositionAndRotation(new Vector3(p.x, ground, p.z), Quaternion.LookRotation(dir));
        var s = go.AddComponent<ShieldWall>();
        s.team = user.Team;
        s.maxHp = s.hp = level >= 2 ? 700f : 400f;
        s.until = Time.time + Lifetime;
        s.Build();
        AbilityFx.Track(go);
        Sfx.PlayAt(SoundBank.Reload, go.transform.position, 0.9f, 0.6f);
        return s;
    }

    private void Build()
    {
        var box = gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, Height * 0.5f, 0f);
        box.size = new Vector3(Width, Height, 0.15f);
        glassMat = UIUtil.UnlitMaterial(new Color(0.35f, 0.65f, 1f, 0.35f));
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0f, Height * 0.5f, 0f), new Vector3(Width, Height, 0.06f), glassMat);
        var frame = MaterialCache.Lit(new Color(0.18f, 0.22f, 0.3f));
        var stripe = MaterialCache.Lit(new Color(0.35f, 0.6f, 1f));
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0f, Height, 0f), new Vector3(Width + 0.1f, 0.1f, 0.14f), frame);
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0f, 0.05f, 0f), new Vector3(Width + 0.1f, 0.1f, 0.2f), frame);
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(-Width * 0.5f, Height * 0.5f, 0f), new Vector3(0.1f, Height, 0.14f), frame);
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(Width * 0.5f, Height * 0.5f, 0f), new Vector3(0.1f, Height, 0.14f), frame);
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0f, Height * 0.5f, -0.05f), new Vector3(0.06f, Height * 0.9f, 0.03f), stripe);
        // Feet
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(-0.9f, 0.05f, -0.3f), new Vector3(0.12f, 0.1f, 0.6f), frame);
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0.9f, 0.05f, -0.3f), new Vector3(0.12f, 0.1f, 0.6f), frame);
    }

    public bool TakeDamage(float amount, int attackerTeam)
    {
        if (dead || attackerTeam == team)
            return false;
        hp -= amount;
        float k = Mathf.Clamp01(hp / maxHp);
        glassMat.color = Color.Lerp(new Color(1f, 0.3f, 0.25f, 0.4f), new Color(0.35f, 0.65f, 1f, 0.35f), k);
        if (hp <= 0f)
            Break();
        return false;   // a shield is never a "kill"
    }

    private void Break()
    {
        dead = true;
        AbilityFx.Flash(transform.position + Vector3.up, new Color(0.5f, 0.75f, 1f, 0.6f), 3f, 0.3f);
        Sfx.PlayAt(SoundBank.Hit, transform.position, 1f, 0.5f);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (glassMat != null)
            Destroy(glassMat);
    }

    private void Update()
    {
        if (!dead && Time.time > until)
            Break();
    }
}
