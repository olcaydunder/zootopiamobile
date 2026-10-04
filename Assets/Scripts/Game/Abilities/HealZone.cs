using UnityEngine;

/// <summary>Sahra Hekimi: a glowing circle that heals the user's team while they stand in it.</summary>
public class HealZone : MonoBehaviour
{
    public const float Radius = 6f;
    public const float HealPerSecond = 8f;
    public const float ArmorPerSecond = 5f;

    private int team;
    private bool repairArmor;
    private float until;
    private float tick;
    private Transform ring;
    private Material ringMat;
    private Transform cross;

    public static HealZone Spawn(IAbilityUser user, int level)
    {
        Vector3 pos = user.transform.position;
        RaycastHit hit;
        float ground = Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.point.y : World.GroundHeight(pos.x, pos.z);
        var go = new GameObject("HealZone");
        go.transform.position = new Vector3(pos.x, ground + 0.05f, pos.z);
        var z = go.AddComponent<HealZone>();
        z.team = user.Team;
        z.repairArmor = level >= 2;
        z.until = Time.time + (level >= 2 ? 12f : 8f);
        z.Build();
        AbilityFx.Track(go);
        Sfx.PlayAt(SoundBank.Pickup, go.transform.position, 0.8f, 0.6f);
        return z;
    }

    private void Build()
    {
        var green = new Color(0.25f, 1f, 0.5f, 0.22f);
        AbilityFx.Primitive(transform, PrimitiveType.Cylinder, Vector3.zero, new Vector3(Radius * 2f, 0.01f, Radius * 2f), AbilityFx.Glass(green));
        ringMat = UIUtil.UnlitMaterial(new Color(0.4f, 1f, 0.6f, 0.6f));
        ring = AbilityFx.Primitive(transform, PrimitiveType.Cylinder, Vector3.up * 0.02f, new Vector3(1f, 0.01f, 1f), ringMat);
        // Medical cross standing in the middle.
        var white = MaterialCache.Lit(new Color(0.95f, 0.97f, 0.95f));
        cross = new GameObject("Cross").transform;
        cross.SetParent(transform, false);
        cross.localPosition = Vector3.up * 1.4f;
        AbilityFx.Primitive(cross, PrimitiveType.Cube, Vector3.zero, new Vector3(0.18f, 0.6f, 0.18f), MaterialCache.Lit(new Color(0.2f, 0.85f, 0.4f)));
        AbilityFx.Primitive(cross, PrimitiveType.Cube, Vector3.zero, new Vector3(0.6f, 0.18f, 0.18f), MaterialCache.Lit(new Color(0.2f, 0.85f, 0.4f)));
        AbilityFx.Primitive(transform, PrimitiveType.Cylinder, Vector3.up * 0.5f, new Vector3(0.08f, 0.5f, 0.08f), white);
    }

    private void Update()
    {
        float pulse = Mathf.Repeat(Time.time * 0.8f, 1f);
        ring.localScale = new Vector3(Radius * 2f * pulse, 0.01f, Radius * 2f * pulse);
        ringMat.color = new Color(0.4f, 1f, 0.6f, 0.6f * (1f - pulse));
        cross.Rotate(0f, 90f * Time.deltaTime, 0f);

        tick += Time.deltaTime;
        if (tick >= 0.25f)
        {
            HealAround(tick);
            tick = 0f;
        }
        if (Time.time > until)
            Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (ringMat != null)
            Destroy(ringMat);
    }

    private void HealAround(float dt)
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;
        Vector3 c = transform.position;
        foreach (var d in gm.Combatants)
        {
            if (d == null || d.IsDead || d.Team != team)
                continue;
            Vector3 p = d.transform.position;
            if (Mathf.Abs(p.y - c.y) > 3f)
                continue;
            p.y = c.y;
            if ((p - c).sqrMagnitude > Radius * Radius)
                continue;
            var u = d as IAbilityUser;
            if (u != null)
                u.HealBy(HealPerSecond * dt, repairArmor ? ArmorPerSecond * dt : 0f);
        }
    }
}
