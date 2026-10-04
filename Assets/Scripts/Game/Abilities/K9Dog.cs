using UnityEngine;

/// <summary>
/// K9 Eğitmeni: a dog runs to the nearest enemy in front of the user, bites (20 damage) and marks it
/// for the user's team. With no enemy around it sniffs the area ahead and marks anyone close to it.
/// </summary>
public class K9Dog : MonoBehaviour
{
    public const float Range = 40f;
    public const float Speed = 11f;
    public const float BiteDamage = 20f;

    private IDamageable target;
    private Vector3 goal;
    private int team;
    private float markSeconds;
    private float until;
    private bool done;
    private float doneAt;
    private Transform[] legs;
    private Transform tail;
    private bool playerOwned;

    /// <summary>Releases one dog (two at level 2). False if there is nothing the dog could do.</summary>
    public static bool Release(IAbilityUser user, int level)
    {
        int count = level >= 2 ? 2 : 1;
        var first = FindTarget(user, null);
        IDamageable second = count > 1 ? FindTarget(user, first) : null;
        Spawn(user, first, level, 0f);
        if (count > 1)
            Spawn(user, second, level, 0.6f);
        Sfx.PlayAt(Bark(), user.transform.position, 0.9f, 1f);
        return true;
    }

    private static IDamageable FindTarget(IAbilityUser user, IDamageable skip)
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return null;
        Vector3 pos = user.transform.position;
        Vector3 aim = user.AimDirection;
        aim.y = 0f;
        aim.Normalize();
        IDamageable best = null;
        float bestScore = float.MaxValue;
        foreach (var d in gm.Combatants)
        {
            if (d == null || d == skip || d.IsDead || d.IsAirborne || d.Team == user.Team)
                continue;
            Vector3 to = d.transform.position - pos;
            float dist = to.magnitude;
            if (dist > Range)
                continue;
            to.y = 0f;
            float angle = Vector3.Angle(aim, to);
            float score = dist + (angle > 70f ? 60f : 0f);   // prefer what the user is looking at
            if (score < bestScore)
            {
                bestScore = score;
                best = d;
            }
        }
        return best;
    }

    private static void Spawn(IAbilityUser user, IDamageable target, int level, float side)
    {
        var go = new GameObject("K9Dog");
        Vector3 aim = user.AimDirection;
        aim.y = 0f;
        aim = aim.sqrMagnitude > 0.01f ? aim.normalized : user.transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, aim);
        go.transform.position = user.transform.position + aim * 0.8f + right * side - Vector3.up * 0.9f;
        go.transform.rotation = Quaternion.LookRotation(aim);
        var dog = go.AddComponent<K9Dog>();
        dog.team = user.Team;
        dog.target = target;
        dog.goal = target != null ? target.transform.position : user.transform.position + aim * 15f;
        dog.markSeconds = level >= 2 ? 10f : 6f;
        dog.until = Time.time + 8f;
        dog.playerOwned = user.IsPlayer;
        dog.Build();
        AbilityFx.Track(go);
    }

    private void Build()
    {
        var fur = MaterialCache.Lit(new Color(0.55f, 0.36f, 0.18f));
        var dark = MaterialCache.Lit(new Color(0.18f, 0.12f, 0.08f));
        var vest = MaterialCache.Lit(new Color(0.15f, 0.2f, 0.12f));
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0f, 0.55f, 0f), new Vector3(0.32f, 0.3f, 0.85f), fur);
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0f, 0.6f, 0.05f), new Vector3(0.34f, 0.22f, 0.45f), vest);   // K9 vest
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0f, 0.78f, 0.5f), new Vector3(0.26f, 0.26f, 0.3f), fur);     // head
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0f, 0.72f, 0.7f), new Vector3(0.15f, 0.13f, 0.18f), dark);   // snout
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(-0.09f, 0.97f, 0.45f), new Vector3(0.07f, 0.15f, 0.05f), dark);
        AbilityFx.Primitive(transform, PrimitiveType.Cube, new Vector3(0.09f, 0.97f, 0.45f), new Vector3(0.07f, 0.15f, 0.05f), dark);
        tail = new GameObject("Tail").transform;
        tail.SetParent(transform, false);
        tail.localPosition = new Vector3(0f, 0.65f, -0.42f);
        AbilityFx.Primitive(tail, PrimitiveType.Cube, new Vector3(0f, 0.1f, -0.12f), new Vector3(0.06f, 0.06f, 0.28f), fur);
        legs = new Transform[4];
        for (int i = 0; i < 4; i++)
        {
            var hip = new GameObject("Leg").transform;
            hip.SetParent(transform, false);
            hip.localPosition = new Vector3(i % 2 == 0 ? -0.11f : 0.11f, 0.45f, i < 2 ? 0.3f : -0.3f);
            AbilityFx.Primitive(hip, PrimitiveType.Cube, new Vector3(0f, -0.22f, 0f), new Vector3(0.08f, 0.45f, 0.08f), i < 2 ? fur : dark);
            legs[i] = hip;
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (done)
        {
            transform.localScale = Vector3.one * Mathf.Clamp01(1f - (Time.time - doneAt) * 2f);
            if (Time.time - doneAt > 0.5f)
                Destroy(gameObject);
            return;
        }
        if (target != null && !target.IsDead)
            goal = target.transform.position;

        Vector3 to = goal - transform.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist < 1.2f || Time.time > until)
        {
            Finish();
            return;
        }
        Vector3 dir = to / Mathf.Max(0.001f, dist);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), dt * 10f);
        Vector3 next = transform.position + dir * Speed * dt;

        // Follow floors and steps: probe down from a little above.
        RaycastHit hit;
        float ground = Physics.Raycast(next + Vector3.up * 1.5f, Vector3.down, out hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.point.y : World.GroundHeight(next.x, next.z);
        if (hit.collider != null && hit.collider.GetComponentInParent<IDamageable>() != null)
            ground = World.GroundHeight(next.x, next.z);
        next.y = Mathf.Lerp(transform.position.y, ground, dt * 12f);
        transform.position = next;

        float swing = Mathf.Sin(Time.time * 22f) * 40f;
        for (int i = 0; i < legs.Length; i++)
            legs[i].localRotation = Quaternion.Euler(((i == 0 || i == 3) ? swing : -swing), 0f, 0f);
        tail.localRotation = Quaternion.Euler(-30f, Mathf.Sin(Time.time * 18f) * 35f, 0f);
    }

    private void Finish()
    {
        done = true;
        doneAt = Time.time;
        var gm = GameManager.Instance;
        if (target != null && !target.IsDead && !target.IsAirborne && Vector3.Distance(target.transform.position, transform.position) < 2.5f)
        {
            Marks.Add(target, team, markSeconds);
            var hitPlayer = target as PlayerController;
            if (hitPlayer != null)
                hitPlayer.MarkHitFrom(transform.position);
            bool killed = target.TakeDamage(BiteDamage, team);
            if (playerOwned && gm != null)
            {
                if (killed)
                    gm.OnPlayerKill();
                if (gm.uiManager != null)
                    gm.uiManager.ShowHit(target.AimPoint, BiteDamage, killed, false);
            }
            Sfx.PlayAt(Bark(), transform.position, 1f, 0.9f);
        }
        else if (gm != null)
        {
            // Sniff: mark anyone close to where the dog stopped.
            foreach (var d in gm.Combatants)
                if (d != null && !d.IsDead && !d.IsAirborne && d.Team != team && Vector3.Distance(d.transform.position, transform.position) < 15f)
                    Marks.Add(d, team, markSeconds);
        }
    }

    private static AudioClip bark;

    /// <summary>Short synthetic bark.</summary>
    public static AudioClip Bark()
    {
        if (bark != null)
            return bark;
        const int rate = 22050;
        int n = (int)(rate * 0.32f);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            bool second = t > 0.17f;
            float lt = second ? t - 0.17f : t;
            float env = Mathf.Clamp01(lt * 60f) * Mathf.Exp(-lt * 22f);
            float f = (second ? 520f : 600f) - lt * 900f;
            float s = Mathf.Sin(2f * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * f * 2.02f * t) + 0.3f * (Random.value * 2f - 1f);
            data[i] = s * env * 0.45f;
        }
        bark = AudioClip.Create("Bark", n, 1, rate, false);
        bark.SetData(data, 0);
        return bark;
    }
}
