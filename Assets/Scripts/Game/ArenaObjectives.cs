using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the arena modes play for besides kills.
/// HAKİMİYET: three capture points A, B, C (a coloured ring on the ground and a flag). Standing in a point
/// alone takes it over in ~6 s (faster with more players, stopped while both teams are in it); every 2 s each
/// held point gives its team a point.
/// SOYGUN: the money bag waits in the vault in the middle; whoever touches it carries it (on their back), and
/// a team scores by bringing it into its base (its spawn side). A carrier who dies drops it; a dropped bag
/// goes back to the vault after 20 s. After a score a new bag appears in 3 s.
/// Offline and on the game server this runs the rules (<see cref="simulate"/>); an online phone only shows
/// the state the server sends (<see cref="ApplyNet"/>).
/// </summary>
public class ArenaObjectives : MonoBehaviour
{
    public static ArenaObjectives Instance;

    public const float PointRadius = 7f;
    public const float CaptureSeconds = 6f;
    public const float ScoreEvery = 2f;
    public const float BaseRadius = 6.5f;
    public const float PickupDistance = 2.2f;

    public static readonly Color[] TeamColors = { new Color(0.25f, 0.55f, 1f), new Color(0.95f, 0.3f, 0.25f) };
    private static readonly Color Neutral = new Color(0.9f, 0.9f, 0.9f);

    public class Point
    {
        public string name;
        public Vector3 pos;
        public int owner = -1;       // team, -1 nobody
        public float progress;       // -1 (team 1 holds) .. 0 .. +1 (team 0 holds)
        public int inside0, inside1; // who stands in it (this frame)
        public Material ringMat, discMat, flagMat;
        public Transform flag;
        public TextMesh label;
    }

    public MatchMode mode;
    public bool simulate;
    public readonly List<Point> points = new List<Point>();
    private float nextScore;

    // Soygun
    public Vector3 vault;
    public readonly Vector3[] bases = new Vector3[2];
    public IDamageable carrier;
    public Vector3 bagPos;
    public bool bagHome = true, bagGone;
    private float bagBackAt, droppedAt;
    private Transform bag;
    private Material[] baseMats;

    public bool Domination { get { return mode == MatchMode.Domination; } }
    public bool Heist { get { return mode == MatchMode.Heist; } }

    /// <summary>Builds the objectives of the mode in the current arena (TeamMatch.Setup first). Null for modes without any.</summary>
    public static ArenaObjectives Begin(MatchMode mode, bool simulate)
    {
        End();
        if (mode != MatchMode.Domination && mode != MatchMode.Heist)
            return null;
        var go = new GameObject("ArenaObjectives");
        var o = go.AddComponent<ArenaObjectives>();
        o.mode = mode;
        o.simulate = simulate;
        Instance = o;
        if (mode == MatchMode.Domination)
            o.BuildPoints();
        else
            o.BuildHeist();
        o.nextScore = Time.time + ScoreEvery;
        return o;
    }

    public static void End()
    {
        if (Instance != null)
            Destroy(Instance.gameObject);
        Instance = null;
    }

    /// <summary>The nearest open spot on the ground (no randomness: the server and every phone get the same place).</summary>
    private static Vector3 Ground(Vector3 p)
    {
        Vector3 q = p;
        bool found = World.IsLand(p.x, p.z) && !World.IsBlocked(p.x, p.z);
        for (int ring = 1; ring <= 12 && !found; ring++)
        {
            float r = ring * 2f;
            for (int k = 0; k < 12 && !found; k++)
            {
                float a = k * Mathf.PI / 6f + ring * 0.37f;
                float x = p.x + Mathf.Cos(a) * r, z = p.z + Mathf.Sin(a) * r;
                if (World.IsLand(x, z) && !World.IsBlocked(x, z))
                {
                    q = new Vector3(x, 0f, z);
                    found = true;
                }
            }
        }
        return new Vector3(q.x, World.GroundHeight(q.x, q.z), q.z);
    }

    // ----- Building -----

    private void BuildPoints()
    {
        Vector3 c = TeamMatch.Center, a = TeamMatch.Spawns[0], b = TeamMatch.Spawns[1];
        Vector3[] at = { Vector3.Lerp(c, a, 0.55f), c, Vector3.Lerp(c, b, 0.55f) };
        // A sideways nudge for the outer points so the three are not on one line.
        Vector3 side = Vector3.Cross(Vector3.up, (a - b).normalized) * TeamMatch.Radius * 0.18f;
        at[0] += side;
        at[2] -= side;
        string[] names = { "A", "B", "C" };
        for (int i = 0; i < 3; i++)
        {
            var p = new Point { name = names[i], pos = Ground(at[i]) };
            if (!NetGame.IsServer)
                Visual(p);
            points.Add(p);
        }
    }

    private void Visual(Point p)
    {
        var root = new GameObject("Point_" + p.name).transform;
        root.SetParent(transform, false);
        root.position = p.pos;
        p.discMat = UIUtil.UnlitMaterial(new Color(1f, 1f, 1f, 0.12f));
        p.ringMat = UIUtil.UnlitMaterial(new Color(1f, 1f, 1f, 0.85f));
        AddMesh(root, "Disc", Annulus(p.pos, 0f, PointRadius - 0.3f, 40, 0.06f), p.discMat);
        AddMesh(root, "Ring", Annulus(p.pos, PointRadius - 0.45f, PointRadius, 56, 0.09f), p.ringMat);
        // flag pole with the flag and the letter
        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(pole.GetComponent<Collider>());
        pole.transform.SetParent(root, false);
        pole.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        pole.transform.localScale = new Vector3(0.12f, 2.2f, 0.12f);
        pole.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.8f, 0.82f, 0.85f));
        var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(flag.GetComponent<Collider>());
        flag.transform.SetParent(root, false);
        flag.transform.localPosition = new Vector3(0.75f, 3.9f, 0f);
        flag.transform.localScale = new Vector3(1.4f, 0.9f, 0.05f);
        p.flagMat = new Material(MaterialCache.Lit(Neutral));
        flag.GetComponent<Renderer>().sharedMaterial = p.flagMat;
        p.flag = flag.transform;
        var label = new GameObject("Letter");
        label.transform.SetParent(root, false);
        label.transform.localPosition = new Vector3(0f, 5.4f, 0f);
        var tm = label.AddComponent<TextMesh>();
        tm.text = p.name;
        tm.fontSize = 64;
        tm.characterSize = 0.09f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.font = UIUtil.DefaultFont;
        label.GetComponent<MeshRenderer>().sharedMaterial = UIUtil.DefaultFont.material;
        p.label = tm;
    }

    private void BuildHeist()
    {
        vault = Ground(TeamMatch.Center);
        bases[0] = Ground(TeamMatch.Spawns[0]);
        bases[1] = Ground(TeamMatch.Spawns[1]);
        bagPos = vault + Vector3.up * 0.9f;
        if (NetGame.IsServer)
            return;
        // the vault: a steel safe with gold bars on it
        var safe = GameObject.CreatePrimitive(PrimitiveType.Cube);
        safe.name = "Vault";
        safe.transform.SetParent(transform, false);
        safe.transform.position = vault + new Vector3(0f, 0.8f, 0f);
        safe.transform.localScale = new Vector3(2.4f, 1.6f, 1.6f);
        safe.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.32f, 0.35f, 0.4f));
        var door = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(door.GetComponent<Collider>());
        door.transform.SetParent(safe.transform, false);
        door.transform.localPosition = new Vector3(0f, 0f, -0.52f);
        door.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        door.transform.localScale = new Vector3(0.35f, 0.03f, 0.5f);
        door.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.75f, 0.7f, 0.5f));
        for (int i = 0; i < 4; i++)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(bar.GetComponent<Collider>());
            bar.transform.SetParent(transform, false);
            bar.transform.position = vault + new Vector3(-0.6f + (i % 2) * 0.9f + (i / 2) * 0.3f, 1.68f + (i / 2) * 0.16f, -0.2f + (i % 2) * 0.35f);
            bar.transform.localScale = new Vector3(0.55f, 0.16f, 0.26f);
            bar.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(1f, 0.8f, 0.25f));
        }
        baseMats = new Material[2];
        for (int t = 0; t < 2; t++)
        {
            var root = new GameObject("Base" + t).transform;
            root.SetParent(transform, false);
            Color c = TeamColors[t];
            baseMats[t] = UIUtil.UnlitMaterial(new Color(c.r, c.g, c.b, 0.85f));
            AddMesh(root, "Ring", Annulus(bases[t], BaseRadius - 0.5f, BaseRadius, 56, 0.09f), baseMats[t]);
            AddMesh(root, "Disc", Annulus(bases[t], 0f, BaseRadius - 0.4f, 40, 0.06f), UIUtil.UnlitMaterial(new Color(c.r, c.g, c.b, 0.15f)));
        }
        // the bag: a fat sack with a tied neck
        var sack = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sack.GetComponent<Collider>());
        sack.name = "MoneyBag";
        sack.transform.SetParent(transform, false);
        sack.transform.localScale = new Vector3(0.62f, 0.66f, 0.55f);
        sack.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.7f, 0.55f, 0.3f));
        var neck = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(neck.GetComponent<Collider>());
        neck.transform.SetParent(sack.transform, false);
        neck.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        neck.transform.localScale = new Vector3(0.35f, 0.12f, 0.35f);
        neck.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.4f, 0.28f, 0.12f));
        var band = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(band.GetComponent<Collider>());
        band.transform.SetParent(sack.transform, false);
        band.transform.localPosition = new Vector3(0f, 0f, -0.47f);
        band.transform.localScale = new Vector3(0.35f, 0.35f, 0.06f);
        band.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.25f, 0.65f, 0.3f));
        bag = sack.transform;
    }

    private static void AddMesh(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = Vector3.zero;   // the mesh is in world space
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    /// <summary>A flat ring (or disc, inner = 0) lying on the ground (each vertex at the ground height + lift), world space.</summary>
    private static Mesh Annulus(Vector3 c, float inner, float outer, int seg, float lift)
    {
        var v = new List<Vector3>();
        var tri = new List<int>();
        for (int i = 0; i <= seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 o = c + d * outer, n = c + d * inner;
            o.y = World.GroundHeight(o.x, o.z) + lift;
            n.y = World.GroundHeight(n.x, n.z) + lift;
            if (inner <= 0f)
                n.y = Mathf.Max(n.y, c.y + lift);
            v.Add(n);
            v.Add(o);
            if (i < seg)
            {
                int k = i * 2;
                tri.Add(k); tri.Add(k + 2); tri.Add(k + 1);
                tri.Add(k + 1); tri.Add(k + 2); tri.Add(k + 3);
            }
        }
        var m = new Mesh();
        m.SetVertices(v);
        m.SetTriangles(tri, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // ----- Rules -----

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null)
            return;
        bool playing = gm.currentState == GameState.InGame;
        if (simulate && playing)
        {
            if (Domination)
                CapturePoints(gm);
            else
                MoveBag(gm);
        }
        if (!NetGame.IsServer)
            Show();
    }

    private void CapturePoints(GameManager gm)
    {
        float dt = Time.deltaTime;
        foreach (var p in points)
        {
            p.inside0 = p.inside1 = 0;
            foreach (var c in gm.Combatants)
            {
                if (c == null || c.IsDead || c.IsAirborne)
                    continue;
                Vector3 d = c.transform.position - p.pos;
                if (Mathf.Abs(d.y) > 4f || d.x * d.x + d.z * d.z > PointRadius * PointRadius)
                    continue;
                if (c.Team == 0) p.inside0++;
                else if (c.Team == 1) p.inside1++;
            }
            float rate = dt / CaptureSeconds;
            if (p.inside0 > 0 && p.inside1 == 0)
                Push(p, +1, rate * Mathf.Min(2f, 1f + 0.35f * (p.inside0 - 1)));
            else if (p.inside1 > 0 && p.inside0 == 0)
                Push(p, -1, rate * Mathf.Min(2f, 1f + 0.35f * (p.inside1 - 1)));
            else if (p.inside0 == 0 && p.inside1 == 0)
            {
                // Nobody there: an unfinished capture slowly falls back.
                float rest = p.owner == 0 ? 1f : p.owner == 1 ? -1f : 0f;
                p.progress = Mathf.MoveTowards(p.progress, rest, rate * 0.3f);
            }
        }
        if (Time.time >= nextScore)
        {
            nextScore = Time.time + ScoreEvery;
            if (!NetGame.InOnlineMatch)
                foreach (var p in points)
                    if (p.owner >= 0)
                        TeamMatch.Score[p.owner]++;
        }
    }

    private void Push(Point p, int sign, float amount)
    {
        float before = p.progress;
        p.progress = Mathf.Clamp(p.progress + sign * amount, -1f, 1f);
        // Crossing the middle: the old owner loses the point.
        if (p.owner >= 0 && ((p.owner == 0 && p.progress <= 0f) || (p.owner == 1 && p.progress >= 0f)))
        {
            p.owner = -1;
            Announce(p.name + " bölgesi tarafsız kaldı", false);
        }
        if (p.owner < 0 && Mathf.Abs(p.progress) >= 1f && Mathf.Abs(before) < 1f)
        {
            p.owner = sign > 0 ? 0 : 1;
            Announce(p.name + " bölgesi " + (p.owner == 0 ? "TAKIMININ!" : "düşmana geçti"), p.owner == 0);
        }
    }

    private static void Announce(string text, bool good)
    {
        if (NetGame.IsServer)
            return;
        var gm = GameManager.Instance;
        if (gm != null && gm.uiManager != null)
            gm.uiManager.AddKillFeed(text);
        Sfx.Play(SoundBank.Pickup, good ? 0.7f : 0.5f, good ? 1.2f : 0.7f);
    }

    private void MoveBag(GameManager gm)
    {
        float now = Time.time;
        if (bagGone)
        {
            if (now >= bagBackAt)
            {
                bagGone = false;
                bagHome = true;
                carrier = null;
                bagPos = vault + Vector3.up * 0.9f;
                Announce("Kasada yeni para çantası var!", true);
            }
            return;
        }
        if (carrier != null)
        {
            if (carrier.IsDead || carrier.transform == null)
            {
                // Dropped where the carrier fell.
                Vector3 at = carrier.transform != null ? carrier.transform.position : bagPos;
                bagPos = new Vector3(at.x, World.GroundHeight(at.x, at.z) + 0.4f, at.z);
                Announce("Para çantası yere düştü!", false);
                carrier = null;
                droppedAt = now;
                return;
            }
            bagPos = carrier.transform.position + Vector3.up * 0.5f;
            int t = carrier.Team;
            if (t >= 0 && t < 2)
            {
                Vector3 d = carrier.transform.position - bases[t];
                if (d.x * d.x + d.z * d.z < BaseRadius * BaseRadius)
                {
                    if (!NetGame.InOnlineMatch)
                        TeamMatch.Score[t]++;
                    Announce(t == 0 ? "ÇANTA ÜSSE ULAŞTI! +1" : "Düşman çantayı kaçırdı", t == 0);
                    carrier = null;
                    bagGone = true;
                    bagBackAt = now + 3f;
                }
            }
            return;
        }
        // On the ground or in the vault: the closest one touching it takes it.
        if (!bagHome && now - droppedAt > 20f)
        {
            bagHome = true;
            bagPos = vault + Vector3.up * 0.9f;
            Announce("Çanta kasaya geri döndü", false);
            return;
        }
        IDamageable best = null;
        float bestD = PickupDistance * PickupDistance;
        foreach (var c in gm.Combatants)
        {
            if (c == null || c.IsDead || c.IsAirborne || c.Team < 0 || c.Team > 1)
                continue;
            Vector3 d = c.transform.position - bagPos;
            d.y *= 0.5f;
            float dd = d.sqrMagnitude;
            if (dd < bestD)
            {
                best = c;
                bestD = dd;
            }
        }
        if (best != null)
        {
            carrier = best;
            bagHome = false;
            if (ReferenceEquals(best, gm.player))
                Announce("ÇANTA SENDE!  Mavi üsse götür", true);
            else
                Announce(best.DisplayName + " çantayı aldı", best.Team == 0);
        }
    }

    // ----- Looks -----

    private void Show()
    {
        if (Domination)
        {
            var cam = Camera.main;
            foreach (var p in points)
            {
                if (p.ringMat == null)
                    continue;
                Color own = p.owner >= 0 ? TeamColors[p.owner] : Neutral;
                // the colour the capture is heading to shows in the disc while it is being taken
                Color going = p.progress > 0.01f ? TeamColors[0] : p.progress < -0.01f ? TeamColors[1] : Neutral;
                p.ringMat.color = new Color(own.r, own.g, own.b, 0.9f);
                p.discMat.color = new Color(going.r, going.g, going.b, 0.12f + 0.2f * Mathf.Abs(p.progress));
                p.flagMat.color = own;
                if (p.flag != null)
                    p.flag.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 2f + p.pos.x) * 12f, 0f);
                if (p.label != null)
                {
                    p.label.color = own;
                    if (cam != null)
                        p.label.transform.rotation = Quaternion.LookRotation(p.label.transform.position - cam.transform.position);
                }
            }
        }
        else if (bag != null)
        {
            bag.gameObject.SetActive(!bagGone);
            if (carrier != null && carrier.transform != null)
            {
                Transform ct = carrier.transform;
                bag.position = ct.position + Vector3.up * 0.45f - ct.forward * 0.42f;
                bag.rotation = ct.rotation;
            }
            else
            {
                bag.position = bagPos + Vector3.up * (0.12f * Mathf.Sin(Time.time * 3f));
                bag.rotation = Quaternion.Euler(0f, Time.time * 60f, 0f);
            }
        }
    }

    // ----- For the bots, the HUD and the network -----

    /// <summary>Where a bot should be going for the objective; urgent: even while fighting.</summary>
    public bool BotGoal(BotAgent bot, int pref, out Vector3 goal, out bool urgent)
    {
        goal = bot.transform.position;
        urgent = false;
        if (Domination)
        {
            if (points.Count == 0)
                return false;
            // Its favourite point unless that one is safely ours; then the nearest point that is not.
            Point pick = points[Mathf.Abs(pref) % points.Count];
            if (pick.owner == bot.team && Mathf.Abs(pick.progress) >= 1f)
            {
                float best = float.MaxValue;
                foreach (var p in points)
                {
                    if (p.owner == bot.team && Mathf.Abs(p.progress) >= 1f)
                        continue;
                    float d = (p.pos - bot.transform.position).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        pick = p;
                    }
                }
            }
            float r = (bot.transform.position - pick.pos).magnitude;
            Vector2 off = new Vector2(Mathf.Sin(pref * 2.1f), Mathf.Cos(pref * 2.1f)) * PointRadius * 0.45f;
            goal = pick.pos + new Vector3(off.x, 0f, off.y);
            urgent = r > PointRadius * 0.8f;
            return true;
        }
        if (Heist)
        {
            if (bagGone)
            {
                goal = vault;
                return true;
            }
            if (ReferenceEquals(carrier, bot))
            {
                goal = bases[Mathf.Clamp(bot.team, 0, 1)];
                urgent = true;
                return true;
            }
            if (carrier == null)
            {
                goal = bagPos;
                urgent = (bagPos - bot.transform.position).sqrMagnitude < 45f * 45f;
                return true;
            }
            if (carrier.Team == bot.team)
            {
                // escort: walk a few metres behind / beside the carrier
                Vector2 o = new Vector2(Mathf.Sin(pref * 1.7f), Mathf.Cos(pref * 1.7f)) * 4f;
                goal = carrier.transform.position + new Vector3(o.x, 0f, o.y);
                return true;
            }
            goal = carrier.transform.position;
            urgent = true;
            return true;
        }
        return false;
    }

    /// <summary>Online phone: the state the server sent (owners and progress are in server teams; flip = our server team is 1).</summary>
    public void ApplyNet(int index, int owner, float progress, bool flip)
    {
        if (index < 0 || index >= points.Count)
            return;
        var p = points[index];
        int before = p.owner;
        p.owner = owner < 0 ? -1 : flip ? 1 - owner : owner;
        p.progress = flip ? -progress : progress;
        if (before != p.owner && p.owner >= 0)
            Announce(p.name + " bölgesi " + (p.owner == 0 ? "TAKIMININ!" : "düşmana geçti"), p.owner == 0);
    }

    /// <summary>The point the local player stands in (for the capture bar), or null.</summary>
    public Point PointAt(Vector3 pos)
    {
        foreach (var p in points)
        {
            Vector3 d = pos - p.pos;
            if (Mathf.Abs(d.y) < 4f && d.x * d.x + d.z * d.z < PointRadius * PointRadius)
                return p;
        }
        return null;
    }
}
