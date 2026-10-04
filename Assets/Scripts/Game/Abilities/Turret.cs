using UnityEngine;

/// <summary>Mühendis: an automatic turret that shoots the nearest visible enemy for a while.</summary>
public class Turret : MonoBehaviour, IDamageable, IStructure
{
    public const float Range = 30f;
    public const float Damage = 6f;
    public const float FireInterval = 0.25f;

    private int team;
    private float hp = 150f;
    private float until;
    private bool dead;
    private bool playerOwned;
    private Transform head;
    private Transform muzzle;
    private IDamageable target;
    private float nextShot, nextThink;
    private LineRenderer tracer;
    private float tracerUntil;

    public int Team { get { return team; } }
    public bool IsDead { get { return dead; } }
    public bool IsAirborne { get { return false; } }
    public string DisplayName { get { return "Taret"; } }
    public Vector3 AimPoint { get { return transform.position + Vector3.up * 0.9f; } }

    public static Turret Spawn(IAbilityUser user, int level)
    {
        Vector3 dir = user.AimDirection;
        dir.y = 0f;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : user.transform.forward;
        Vector3 p = user.transform.position + dir * PlaceDistance(user, dir, 1.4f);
        RaycastHit hit;
        float ground = Physics.Raycast(p + Vector3.up * 1f, Vector3.down, out hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.point.y : World.GroundHeight(p.x, p.z);
        var go = new GameObject("Turret");
        go.transform.SetPositionAndRotation(new Vector3(p.x, ground, p.z), Quaternion.LookRotation(dir));
        var t = go.AddComponent<Turret>();
        t.team = user.Team;
        t.playerOwned = user.IsPlayer;
        t.until = Time.time + (level >= 2 ? 25f : 15f);
        t.Build();
        AbilityFx.Track(go);
        Sfx.PlayAt(SoundBank.Reload, go.transform.position, 0.9f, 0.8f);
        return t;
    }

    private void Build()
    {
        var box = gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.6f, 0f);
        box.size = new Vector3(0.7f, 1.2f, 0.7f);
        var metal = MaterialCache.Lit(new Color(0.3f, 0.32f, 0.3f));
        var orange = MaterialCache.Lit(new Color(0.95f, 0.5f, 0.15f));
        var dark = MaterialCache.Lit(new Color(0.1f, 0.1f, 0.11f));
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f;
            var leg = AbilityFx.Primitive(transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.06f, 0.4f, 0.06f), metal);
            leg.localRotation = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(30f, 0f, 0f);
            leg.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0.35f, 0.2f);
        }
        head = new GameObject("Head").transform;
        head.SetParent(transform, false);
        head.localPosition = new Vector3(0f, 0.85f, 0f);
        AbilityFx.Primitive(head, PrimitiveType.Cube, Vector3.zero, new Vector3(0.45f, 0.3f, 0.55f), orange);
        AbilityFx.Primitive(head, PrimitiveType.Cube, new Vector3(0f, 0.06f, -0.05f), new Vector3(0.3f, 0.12f, 0.3f), dark);
        var barrel = AbilityFx.Primitive(head, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.5f), new Vector3(0.07f, 0.3f, 0.07f), dark);
        barrel.localRotation = Quaternion.Euler(90f, 0f, 0f);
        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(head, false);
        muzzle.localPosition = new Vector3(0f, 0f, 0.82f);

        var lineGo = new GameObject("Tracer");
        lineGo.transform.SetParent(transform, false);
        tracer = lineGo.AddComponent<LineRenderer>();
        tracer.positionCount = 2;
        tracer.startWidth = 0.035f;
        tracer.endWidth = 0.02f;
        tracer.sharedMaterial = AbilityFx.Glass(new Color(1f, 0.85f, 0.4f, 0.8f));
        tracer.enabled = false;
    }

    private void Update()
    {
        if (dead)
            return;
        if (Time.time > until)
        {
            Break();
            return;
        }
        if (tracer.enabled && Time.time > tracerUntil)
            tracer.enabled = false;

        if (Time.time >= nextThink)
        {
            nextThink = Time.time + 0.3f;
            target = FindTarget();
        }
        if (target == null || target.IsDead)
        {
            head.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
            return;
        }
        Vector3 to = target.AimPoint - head.position;
        head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(to), Time.deltaTime * 10f);
        if (Time.time >= nextShot && Vector3.Angle(head.forward, to) < 8f)
        {
            nextShot = Time.time + FireInterval;
            Shoot(to);
        }
    }

    /// <summary>How far ahead something can be placed before a wall (min 0.6 m).</summary>
    public static float PlaceDistance(IAbilityUser user, Vector3 dir, float wanted)
    {
        RaycastHit hit;
        if (Physics.SphereCast(user.AbilityOrigin, 0.3f, dir, out hit, wanted + 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return Mathf.Max(0.6f, hit.distance - 0.5f);
        return wanted;
    }

    private IDamageable FindTarget()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return null;
        IDamageable best = null;
        float bestDist = Range;
        foreach (var d in gm.Combatants)
        {
            if (d == null || d.IsDead || d.IsAirborne || d.Team == team || ClassAbility.IsStealthed(d))
                continue;
            float dist = Vector3.Distance(d.transform.position, transform.position);
            if (dist >= bestDist || !Visible(d))
                continue;
            best = d;
            bestDist = dist;
        }
        return best;
    }

    private bool Visible(IDamageable d)
    {
        RaycastHit hit;
        // From above the turret's own box, and with every layer (the player is on Ignore Raycast).
        Vector3 eye = transform.position + Vector3.up * 1.35f;
        if (Physics.Linecast(eye, d.AimPoint, out hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.collider.GetComponentInParent<IDamageable>() == d;
        return true;
    }

    private void Shoot(Vector3 to)
    {
        Vector3 dir = (to.normalized + Random.insideUnitSphere * 0.03f).normalized;
        Vector3 end = muzzle.position + dir * Range;
        RaycastHit hit;
        if (Physics.Raycast(muzzle.position, dir, out hit, Range, ~0, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            var d = hit.collider.GetComponentInParent<IDamageable>();
            bool body = d != null && !d.IsDead && !(d is IStructure);
            if (d != null && !d.IsDead && d.Team != team)
            {
                var hitPlayer = d as PlayerController;
                if (hitPlayer != null)
                    hitPlayer.MarkHitFrom(transform.position);
                bool killed = d.TakeDamage(Damage, team);
                var gm = GameManager.Instance;
                if (playerOwned && gm != null)
                {
                    if (killed)
                        gm.OnPlayerKill();
                    if (gm.uiManager != null)
                        gm.uiManager.ShowHit(hit.point, Damage, killed, false);
                }
            }
            Effects.Impact(hit.point, hit.normal, body);
        }
        tracer.SetPosition(0, muzzle.position);
        tracer.SetPosition(1, end);
        tracer.enabled = true;
        tracerUntil = Time.time + 0.05f;
        Sfx.PlayAt(SoundBank.Gunshot(WeaponType.SMG), muzzle.position, 0.5f, 1.25f);
    }

    public bool TakeDamage(float amount, int attackerTeam)
    {
        if (dead || attackerTeam == team)
            return false;
        hp -= amount;
        if (hp <= 0f)
            Break();
        return false;
    }

    private void Break()
    {
        dead = true;
        Effects.Explosion(transform.position + Vector3.up * 0.7f);
        Destroy(gameObject);
    }
}

/// <summary>Placed objects that can be shot (shields, turrets): no blood, never counted as a kill.</summary>
public interface IStructure
{
}
