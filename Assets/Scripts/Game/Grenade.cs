using System.Collections.Generic;
using UnityEngine;

/// <summary>What is thrown (the explosive and tactical items of the inventory). Sent online as a byte.</summary>
public enum ThrowKind
{
    Frag = 0,      // El Bombası: bounces, explodes after a short fuse
    Molotov = 1,   // breaks where it lands and sets the ground on fire
    Charge = 2,    // Patlayıcı Paket: sticks where it lands, bigger and stronger blast
    Smoke = 3,     // a thick cloud nobody sees through
    Flash = 4,     // blinds whoever looks at it
    Gas = 5        // a poison cloud that ignores armour
}

/// <summary>
/// Thrown items: a frag grenade, a molotov, an explosive charge, smoke, flash and gas grenades. Physics
/// flight, then the effect; burning ground and clouds are <see cref="AreaEffect"/>s. <c>power</c> scales the
/// damage (explosives, gas) or the duration (smoke, flash) — the inventory item's level and perks.
/// </summary>
public class Grenade : MonoBehaviour
{
    public const float Radius = 7f;
    public const float MaxDamage = 120f;

    private IDamageable thrower;
    private int team;
    private ThrowKind kind;
    private float power = 1f;
    private float fuse = 2.6f;
    private bool exploded;
    private bool visualOnly;
    private bool stuck;
    private Rigidbody rb;

    public static ThrowKind KindOf(string gearId)
    {
        switch (gearId)
        {
            case "x_molotov": return ThrowKind.Molotov;
            case "x_charge": return ThrowKind.Charge;
            case "t_smoke": return ThrowKind.Smoke;
            case "t_flash": return ThrowKind.Flash;
            case "t_gas": return ThrowKind.Gas;
            default: return ThrowKind.Frag;
        }
    }

    public static string Name(ThrowKind k)
    {
        switch (k)
        {
            case ThrowKind.Molotov: return "MOLOTOF";
            case ThrowKind.Charge: return "PATLAYICI";
            case ThrowKind.Smoke: return "SİS";
            case ThrowKind.Flash: return "FLAŞ";
            case ThrowKind.Gas: return "GAZ";
            default: return "BOMBA";
        }
    }

    /// <summary>Online: someone else's throw — it flies and does its effect here, the damage is handled by the thrower / server.</summary>
    public static void ThrowVisual(Vector3 position, Vector3 velocity, ThrowKind kind, float fuse)
    {
        var g = Spawn(position, velocity, null, kind, 1f, fuse);
        g.visualOnly = true;
    }

    /// <summary>Game server: a player's smoke or flash, so the server's bots are blinded / cannot see through it.</summary>
    public static void ServerEffect(Vector3 position, Vector3 velocity, ThrowKind kind, int team, float fuse)
    {
        var g = Spawn(position, velocity, null, kind, 1f, fuse);
        g.team = team;
    }

    public static void Throw(Vector3 position, Vector3 velocity, IDamageable owner)
    {
        Throw(position, velocity, owner, ThrowKind.Frag, 1f);
    }

    public static void Throw(Vector3 position, Vector3 velocity, IDamageable owner, ThrowKind kind, float power)
    {
        Throw(position, velocity, owner, kind, power, 0f);
    }

    /// <summary>A throw; <paramref name="fuse"/> &gt; 0 is what is left of a cooked fuse (0: the kind's own).</summary>
    public static void Throw(Vector3 position, Vector3 velocity, IDamageable owner, ThrowKind kind, float power, float fuse)
    {
        Spawn(position, velocity, owner, kind, power, fuse);
        NetGame.GrenadeThrown(position, velocity, owner, kind, fuse);
    }

    /// <summary>Fuse a held grenade starts with (counted from the moment the button is pressed).</summary>
    public const float CookFuse = 6f;

    /// <summary>Molotovs break on impact; everything else can be cooked.</summary>
    public static bool Cookable(ThrowKind k) { return k != ThrowKind.Molotov; }

    private static Grenade Spawn(Vector3 position, Vector3 velocity, IDamageable owner, ThrowKind kind, float power, float fuse = 0f)
    {
        var go = GameObject.CreatePrimitive(kind == ThrowKind.Charge ? PrimitiveType.Cube : PrimitiveType.Sphere);
        go.name = "Grenade_" + kind;
        go.layer = PlayerController.IgnoreRaycastLayer; // never blocks shots
        go.transform.position = position;
        go.transform.localScale = kind == ThrowKind.Charge ? new Vector3(0.24f, 0.12f, 0.16f) : Vector3.one * 0.18f;
        var rend = go.GetComponent<Renderer>();
        rend.sharedMaterial = MaterialCache.Lit(Tint(kind));

        string modelName = ModelName(kind);
        if (modelName != null)
        {
            var model = ModelLibrary.Spawn(ModelLibrary.PropPath(modelName), go.transform);
            if (model == null && kind == ThrowKind.Frag)
                model = ModelLibrary.Spawn(ModelLibrary.PropPath("Grenade"), go.transform);
            if (model != null)
            {
                rend.enabled = false;
                ModelLibrary.ShareMaterials(model, true);
                // The sphere (the collider) is scaled 0.18: fit the model to ~0.16 m and centre it on the sphere.
                Bounds b = ModelLibrary.RenderBounds(model);
                float size = Mathf.Max(0.001f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
                model.transform.localScale *= 0.16f / size;
                model.transform.localPosition = Vector3.zero;
                b = ModelLibrary.RenderBounds(model);
                model.transform.position -= b.center - go.transform.position;
                ModelLibrary.SetLayer(model, go.layer);
            }
        }
        else if (kind == ThrowKind.Molotov)
        {
            // a bottle: long body and a burning rag on top
            go.transform.localScale = new Vector3(0.11f, 0.2f, 0.11f);
            var rag = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(rag.GetComponent<Collider>());
            rag.transform.SetParent(go.transform, false);
            rag.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            rag.transform.localScale = new Vector3(0.6f, 0.35f, 0.6f);
            rag.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(1f, 0.6f, 0.15f));
            rag.layer = go.layer;
        }

        var body = go.AddComponent<Rigidbody>();
        body.mass = 0.4f;
        body.drag = 0.15f;
        body.angularDrag = 1f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.velocity = velocity;

        var g = go.AddComponent<Grenade>();
        g.rb = body;
        g.thrower = owner;
        g.team = owner != null ? owner.Team : -1;
        g.kind = kind;
        g.power = Mathf.Clamp(power, 0.5f, 3f);
        g.fuse = kind == ThrowKind.Molotov ? 4f : kind == ThrowKind.Charge ? 3f : kind == ThrowKind.Frag ? 2.6f : 1.6f;
        if (fuse > 0f && kind != ThrowKind.Molotov)
            g.fuse = fuse;
        return g;
    }

    /// <summary>Prop model of a thrown kind (Models/Props), null for the ones drawn from primitives.</summary>
    private static string ModelName(ThrowKind k)
    {
        switch (k)
        {
            case ThrowKind.Frag: return "Grenade_M67";
            case ThrowKind.Smoke: return "Grenade_Smoke";
            case ThrowKind.Flash: return "Grenade_Flash";
            case ThrowKind.Gas: return "Grenade_Gas";
            default: return null;
        }
    }

    private static Color Tint(ThrowKind k)
    {
        switch (k)
        {
            case ThrowKind.Molotov: return new Color(0.35f, 0.7f, 0.35f);
            case ThrowKind.Charge: return new Color(0.75f, 0.6f, 0.35f);
            case ThrowKind.Smoke: return new Color(0.6f, 0.62f, 0.66f);
            case ThrowKind.Flash: return new Color(0.92f, 0.93f, 0.95f);
            case ThrowKind.Gas: return new Color(0.45f, 0.65f, 0.2f);
            default: return new Color(0.2f, 0.28f, 0.16f);
        }
    }

    private void OnCollisionEnter(Collision c)
    {
        if (exploded)
            return;
        if (kind == ThrowKind.Molotov)
        {
            // The bottle breaks on the first thing it hits.
            fuse = 0f;
        }
        else if (kind == ThrowKind.Charge && !stuck)
        {
            stuck = true;
            rb.isKinematic = true;
            Sfx.PlayAt(SoundBank.Hit, transform.position, 0.5f, 0.6f);
        }
    }

    private void Update()
    {
        fuse -= Time.deltaTime;
        if (fuse <= 0f && !exploded)
            Explode();
    }

    private void Explode()
    {
        exploded = true;
        Vector3 pos = transform.position;
        switch (kind)
        {
            case ThrowKind.Molotov:
            {
                Vector3 ground = new Vector3(pos.x, World.GroundHeight(pos.x, pos.z), pos.z);
                if (pos.y - ground.y > 2.5f)
                    ground = pos;   // broke on a roof or a wall: burns there
                Sfx.PlayAt(SoundBank.Hit, pos, 0.9f, 1.5f);
                AreaEffect.Start(AreaKind.Fire, ground, 3.4f, 6f, 16f * power, thrower, team, visualOnly);
                break;
            }
            case ThrowKind.Smoke:
                Sfx.PlayAt(SoundBank.Whoosh, pos, 0.8f, 0.6f);
                AreaEffect.Start(AreaKind.Smoke, pos, 5.5f, 12f * power, 0f, thrower, team, visualOnly);
                break;
            case ThrowKind.Gas:
                Sfx.PlayAt(SoundBank.Whoosh, pos, 0.8f, 0.5f);
                AreaEffect.Start(AreaKind.Gas, pos, 4.6f, 8f, 7f * power, thrower, team, visualOnly);
                break;
            case ThrowKind.Flash:
                Flash(pos);
                break;
            default:
                Blast(pos, kind == ThrowKind.Charge ? 9f : Radius, (kind == ThrowKind.Charge ? 170f : MaxDamage) * power);
                break;
        }
        Destroy(gameObject);
    }

    // ----- Frag and charge -----

    private void Blast(Vector3 pos, float radius, float maxDamage)
    {
        Effects.Explosion(pos);
        if (kind == ThrowKind.Charge)
        {
            Effects.Explosion(pos + Vector3.up * 0.8f);
            Effects.Explosion(pos + Random.insideUnitSphere * 1.5f);
        }
        Sfx.PlayAt(SoundBank.Explosion, pos, 1f, kind == ThrowKind.Charge ? 0.8f : Random.Range(0.9f, 1.05f));

        var gm = GameManager.Instance;
        if (gm != null && !visualOnly)
        {
            // Copy: damage can remove entries indirectly.
            var targets = gm.Combatants.ToArray();
            foreach (var c in targets)
            {
                if (c == null || c.IsDead || c.IsAirborne)
                    continue;
                bool self = ReferenceEquals(c, thrower);
                if (c.Team == team && !self)
                    continue;

                float d = Vector3.Distance(pos, c.AimPoint);
                if (d > radius)
                    continue;

                float falloff = 1f - d / radius;
                float damage = maxDamage * falloff * falloff;

                // Walls soak most of the blast.
                RaycastHit hit;
                if (Physics.Linecast(pos + Vector3.up * 0.3f, c.AimPoint, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<IDamageable>() != c)
                        damage *= 0.3f;
                }
                Hurt(c, damage, pos, false);
            }
        }
        if (gm != null && gm.player != null)
        {
            float pd = Vector3.Distance(gm.player.transform.position, pos);
            gm.player.Shake(Mathf.Clamp01(1f - pd / (kind == ThrowKind.Charge ? 40f : 30f)) * 0.6f);
        }
    }

    /// <summary>Damages one combatant on behalf of the thrower (kill credit, hit marker, hit direction).</summary>
    public static void Hurt(IDamageable c, float damage, Vector3 from, IDamageable thrower, int team, bool pierce)
    {
        var gm = GameManager.Instance;
        bool self = ReferenceEquals(c, thrower);
        var hitPlayer = c as PlayerController;
        if (hitPlayer != null)
            hitPlayer.MarkHitFrom(from);
        damage *= ClassAbility.ExplosionTaken(c);   // Kalkan Ustası takes less
        bool killed;
        HitContext.Set(thrower, from, false, NetProtocol.HitGrenade);
        HitContext.Pierce = pierce;
        try
        {
            killed = c.TakeDamage(damage, team);
        }
        finally
        {
            HitContext.Clear();
        }
        if (gm == null)
            return;
        if (killed && !self && thrower is PlayerController)
            gm.OnPlayerKill();
        if (thrower is PlayerController && !self && gm.uiManager != null && damage >= 1f)
            gm.uiManager.ShowHit(c.AimPoint, damage, killed, false);
    }

    private void Hurt(IDamageable c, float damage, Vector3 from, bool pierce)
    {
        Hurt(c, damage, from, thrower, team, pierce);
    }

    // ----- Flash -----

    private void Flash(Vector3 pos)
    {
        Sfx.PlayAt(SoundBank.Explosion, pos, 0.7f, 1.8f);
        Effects.FlashBurst(pos);
        float seconds = 3f * power;
        var gm = GameManager.Instance;
        if (gm == null)
            return;
        // The local player: blinded when looking towards it with nothing in between (thrower included).
        var p = gm.player;
        if (p != null && !p.isDead && !NetGame.IsServer && p.playerCamera != null)
        {
            Vector3 eye = p.playerCamera.transform.position;
            Vector3 to = pos - eye;
            float d = to.magnitude;
            if (d < 20f && !Physics.Linecast(eye, pos + to.normalized * -0.3f, Physics.DefaultRaycastLayers & ~(1 << PlayerController.IgnoreRaycastLayer), QueryTriggerInteraction.Ignore))
            {
                float facing = Vector3.Dot(p.playerCamera.transform.forward, to / Mathf.Max(0.01f, d));
                float k = Mathf.Clamp01((facing + 0.2f) / 0.8f) * Mathf.Clamp01(1.2f - d / 20f);
                if (d < 4f)
                    k = Mathf.Max(k, 0.6f);   // too close: bright from any side
                if (k > 0.05f && gm.uiManager != null)
                    gm.uiManager.Flashbang(k, seconds * k);
            }
        }
        if (visualOnly)
            return;
        foreach (var b in gm.bots)
        {
            if (b == null || b.isDead || b.IsAirborne || (b.team == team && !ReferenceEquals(b, thrower)))
                continue;
            float d = Vector3.Distance(b.transform.position, pos);
            if (d >= 16f)
                continue;
            // In sight: nothing but the bot itself between the flash and its eyes.
            RaycastHit hit;
            bool clear = !Physics.Linecast(pos, b.AimPoint, out hit, Physics.DefaultRaycastLayers & ~(1 << PlayerController.IgnoreRaycastLayer), QueryTriggerInteraction.Ignore)
                         || ReferenceEquals(hit.collider.GetComponentInParent<IDamageable>(), b);
            if (clear)
                b.Blind(seconds * Mathf.Clamp01(1.3f - d / 16f));
        }
    }
}

public enum AreaKind { Fire, Smoke, Gas }

/// <summary>
/// A burning patch (molotov), a smoke cloud or a poison cloud: shows its particles while it lasts and, unless
/// it is only a picture of someone else's (online), hurts the thrower's enemies in it twice a second. Smoke
/// blocks the bots' sight (<see cref="SmokeBlocks"/>).
/// </summary>
public class AreaEffect : MonoBehaviour
{
    public static readonly List<AreaEffect> Active = new List<AreaEffect>();

    public AreaKind kind;
    public float radius;
    private float until, dps, nextTick, nextPuff;
    private IDamageable thrower;
    private int team;
    private bool visualOnly;

    public static AreaEffect Start(AreaKind kind, Vector3 pos, float radius, float seconds, float dps, IDamageable thrower, int team, bool visualOnly)
    {
        var go = new GameObject("Area_" + kind);
        go.transform.position = pos;
        var a = go.AddComponent<AreaEffect>();
        a.kind = kind;
        a.radius = radius;
        a.until = Time.time + seconds;
        a.dps = dps;
        a.thrower = thrower;
        a.team = team;
        a.visualOnly = visualOnly;
        a.nextTick = Time.time + 0.25f;
        Active.Add(a);
        if (kind == AreaKind.Fire)
            Sfx.PlayAt(SoundBank.Explosion, pos, 0.45f, 1.6f);
        return a;
    }

    public static void ClearAll()
    {
        foreach (var a in Active.ToArray())
            if (a != null)
                Destroy(a.gameObject);
        Active.Clear();
    }

    private void OnDestroy()
    {
        Active.Remove(this);
    }

    /// <summary>True when a smoke cloud is between a and b (the line passes through its sphere).</summary>
    public static bool SmokeBlocks(Vector3 a, Vector3 b)
    {
        foreach (var e in Active)
        {
            if (e == null || e.kind != AreaKind.Smoke || Time.time > e.until - 1.5f)
                continue;
            Vector3 c = e.transform.position + Vector3.up * 1.2f;
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(c - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            if ((a + ab * t - c).sqrMagnitude < e.radius * e.radius * 0.8f)
                return true;
        }
        return false;
    }

    /// <summary>Whether a point is inside a smoke cloud (the local camera: the screen goes grey).</summary>
    public static float SmokeAt(Vector3 p)
    {
        float k = 0f;
        foreach (var e in Active)
        {
            if (e == null || e.kind != AreaKind.Smoke)
                continue;
            float d = Vector3.Distance(p, e.transform.position + Vector3.up * 1.2f);
            float fade = Mathf.Clamp01((e.until - Time.time) / 1.5f);
            k = Mathf.Max(k, Mathf.Clamp01(1.15f - d / e.radius) * fade);
        }
        return k;
    }

    private void Update()
    {
        float now = Time.time;
        if (now > until)
        {
            Destroy(gameObject);
            return;
        }
        Vector3 c = transform.position;
        if (now >= nextPuff && !NetGame.IsServer)
        {
            nextPuff = now + (kind == AreaKind.Fire ? 0.05f : 0.12f);
            float left = until - now;
            switch (kind)
            {
                case AreaKind.Fire:
                    Effects.Flame(c + Flat(radius * 0.85f));
                    if (Random.value < 0.25f)
                        Effects.Smoke(c + Flat(radius * 0.6f) + Vector3.up * 0.8f, new Color(0.2f, 0.18f, 0.16f, 0.55f));
                    break;
                case AreaKind.Smoke:
                    if (left > 1.5f)
                        Effects.Cloud(c + Flat(radius * 0.7f) + Vector3.up * Random.Range(0.2f, 2.2f), radius * 0.75f, new Color(0.78f, 0.8f, 0.83f, 0.9f));
                    break;
                default:
                    if (left > 1f)
                        Effects.Cloud(c + Flat(radius * 0.7f) + Vector3.up * Random.Range(0.1f, 1.6f), radius * 0.6f, new Color(0.55f, 0.8f, 0.25f, 0.55f));
                    break;
            }
        }
        if (visualOnly || dps <= 0f || now < nextTick)
            return;
        nextTick = now + 0.5f;
        var gm = GameManager.Instance;
        if (gm == null)
            return;
        foreach (var t in gm.Combatants.ToArray())
        {
            if (t == null || t.IsDead || t.IsAirborne)
                continue;
            bool self = ReferenceEquals(t, thrower);
            if (t.Team == team && !self)
                continue;
            if (kind == AreaKind.Gas && self)
                continue;   // the thrower knows to hold their breath
            Vector3 p = t.transform.position;
            float flat = new Vector2(p.x - c.x, p.z - c.z).magnitude;
            float high = p.y - c.y;
            if (flat > radius || high < -2f || high > (kind == AreaKind.Fire ? 2.2f : 4f))
                continue;
            Grenade.Hurt(t, dps * 0.5f, c, thrower, team, kind == AreaKind.Gas);
        }
    }

    private static Vector3 Flat(float r)
    {
        Vector2 v = Random.insideUnitCircle * r;
        return new Vector3(v.x, 0.1f, v.y);
    }
}
