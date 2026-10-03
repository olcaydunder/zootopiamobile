using UnityEngine;

/// <summary>Thrown frag grenade: bounces, then explodes after a short fuse.</summary>
public class Grenade : MonoBehaviour
{
    public const float Radius = 7f;
    public const float MaxDamage = 120f;

    private IDamageable thrower;
    private int team;
    private float fuse = 2.6f;
    private bool exploded;

    public static void Throw(Vector3 position, Vector3 velocity, IDamageable owner)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Grenade";
        go.layer = PlayerController.IgnoreRaycastLayer; // never blocks shots
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.18f;
        go.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.2f, 0.28f, 0.16f));

        var model = ModelLibrary.Spawn(ModelLibrary.PropPath("Grenade"), go.transform);
        if (model != null)
        {
            go.GetComponent<Renderer>().enabled = false;
            // The sphere is scaled 0.18; the pack's grenade is ~0.6 m, so scale it to ~0.16 m.
            Bounds b = ModelLibrary.RenderBounds(model);
            float size = Mathf.Max(0.001f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
            model.transform.localScale *= 0.16f / size;
            model.transform.localPosition = Vector3.zero;
            ModelLibrary.SetLayer(model, go.layer);
        }

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.4f;
        rb.drag = 0.15f;
        rb.angularDrag = 1f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.velocity = velocity;

        var g = go.AddComponent<Grenade>();
        g.thrower = owner;
        g.team = owner != null ? owner.Team : -1;
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
        Effects.Explosion(pos);
        Sfx.PlayAt(SoundBank.Explosion, pos, 1f, Random.Range(0.9f, 1.05f));

        var gm = GameManager.Instance;
        if (gm != null)
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
                if (d > Radius)
                    continue;

                float falloff = 1f - d / Radius;
                float damage = MaxDamage * falloff * falloff;

                // Walls soak most of the blast.
                RaycastHit hit;
                if (Physics.Linecast(pos + Vector3.up * 0.3f, c.AimPoint, out hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.GetComponentInParent<IDamageable>() != c)
                        damage *= 0.3f;
                }

                var hitPlayer = c as PlayerController;
                if (hitPlayer != null)
                    hitPlayer.MarkHitFrom(pos);
                bool killed = c.TakeDamage(damage, team);
                if (killed && !self && thrower is PlayerController)
                    gm.OnPlayerKill();
                if (thrower is PlayerController && !self && gm.uiManager != null)
                    gm.uiManager.ShowHit(c.AimPoint, damage, killed, false);
            }

            if (gm.player != null)
            {
                float pd = Vector3.Distance(gm.player.transform.position, pos);
                gm.player.Shake(Mathf.Clamp01(1f - pd / 30f) * 0.6f);
            }
        }

        Destroy(gameObject);
    }
}
