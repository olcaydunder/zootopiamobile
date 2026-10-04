using UnityEngine;

/// <summary>Gözcü: an eagle circles above the user and keeps every enemy within range marked.</summary>
public class ScoutEagle : MonoBehaviour
{
    public const float Duration = 8f;

    private Vector3 center;
    private float radius;
    private int team;
    private float until;
    private float nextScan;
    private Transform wingL, wingR;
    private float angle;

    public static ScoutEagle Spawn(IAbilityUser user, int level)
    {
        var go = new GameObject("ScoutEagle");
        var e = go.AddComponent<ScoutEagle>();
        e.center = user.transform.position;
        e.radius = level >= 2 ? 90f : 60f;
        e.team = user.Team;
        e.until = Time.time + Duration;
        e.angle = Random.Range(0f, 360f);
        e.Build();
        e.Scan();
        AbilityFx.Track(go);
        Sfx.PlayAt(SoundBank.Whoosh, user.transform.position, 0.6f, 1.4f);
        return e;
    }

    private void Build()
    {
        var brown = MaterialCache.Lit(new Color(0.35f, 0.22f, 0.12f));
        var white = MaterialCache.Lit(new Color(0.95f, 0.95f, 0.92f));
        var beak = MaterialCache.Lit(new Color(1f, 0.75f, 0.15f));
        AbilityFx.Primitive(transform, PrimitiveType.Capsule, Vector3.zero, new Vector3(0.35f, 0.5f, 0.35f), brown).localRotation = Quaternion.Euler(90f, 0f, 0f);
        AbilityFx.Primitive(transform, PrimitiveType.Sphere, new Vector3(0f, 0.08f, 0.5f), new Vector3(0.26f, 0.26f, 0.28f), white);
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0.68f), new Vector3(0.08f, 0.08f, 0.14f), beak);
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0f, 0f, -0.55f), new Vector3(0.4f, 0.04f, 0.3f), white);
        wingL = new GameObject("WingL").transform;
        wingL.SetParent(transform, false);
        AbilityFx.Primitive(wingL, PrimitiveType.Cube, new Vector3(-0.7f, 0f, 0f), new Vector3(1.4f, 0.04f, 0.45f), brown);
        wingR = new GameObject("WingR").transform;
        wingR.SetParent(transform, false);
        AbilityFx.Primitive(wingR, PrimitiveType.Cube, new Vector3(0.7f, 0f, 0f), new Vector3(1.4f, 0.04f, 0.45f), brown);
        transform.localScale = Vector3.one * 1.6f;
    }

    private void Scan()
    {
        nextScan = Time.time + 1f;
        var gm = GameManager.Instance;
        if (gm == null)
            return;
        foreach (var d in gm.Combatants)
        {
            if (d == null || d.IsDead || d.Team == team)
                continue;
            Vector3 p = d.transform.position - center;
            p.y = 0f;
            if (p.sqrMagnitude < radius * radius)
                Marks.Add(d, team, 2f);
        }
    }

    private void Update()
    {
        angle += 35f * Time.deltaTime;
        float rad = angle * Mathf.Deg2Rad;
        Vector3 pos = center + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * 14f;
        pos.y = World.GroundHeight(pos.x, pos.z) + 26f;
        Vector3 tangent = new Vector3(-Mathf.Sin(rad), -0.05f, Mathf.Cos(rad));
        transform.SetPositionAndRotation(pos, Quaternion.LookRotation(tangent) * Quaternion.Euler(0f, 0f, 20f));
        float flap = Mathf.Sin(Time.time * 6f) * 25f;
        wingL.localRotation = Quaternion.Euler(0f, 0f, flap);
        wingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
        if (Time.time >= nextScan)
            Scan();
        if (Time.time > until)
            Destroy(gameObject);
    }
}
