using UnityEngine;

/// <summary>
/// Shrinking play zone in phases: wait, then shrink toward a new random circle.
/// Anyone outside takes damage that grows each phase.
/// </summary>
public class SafeZoneController : MonoBehaviour
{
    public Vector3 center;
    public float radius = 60f;
    public bool active;

    // Tuned for the 700 m city map (runs ~6 m/s, jeeps on the streets).
    // The zone starts closing every 30 seconds (wait 30 s, then shrink).
    private static readonly float[] WaitTimes = { 30f, 30f, 30f, 30f, 30f, 30f };
    private static readonly float[] ShrinkTimes = { 50f, 40f, 32f, 26f, 20f, 15f };
    private static readonly float[] RadiusFractions = { 0.62f, 0.4f, 0.25f, 0.14f, 0.06f, 0f };
    private static readonly float[] DamagePerSecond = { 2f, 4f, 6f, 9f, 13f, 18f };

    private float initialRadius;
    private int phase;
    /// <summary>Current zone phase (0 = first wait).</summary>
    public int Phase { get { return phase; } }
    private bool shrinking;
    private bool finished;
    private float phaseTimer;

    private Vector3 startCenter;
    private float startRadius;
    private Vector3 nextCenter;
    private float nextRadius;

    private LineRenderer ring;
    private LineRenderer nextRing;
    private GameObject wall;
    private const int RingSegments = 72;

    public Vector3 NextCenter { get { return nextCenter; } }
    public float NextRadius { get { return nextRadius; } }
    public bool HasNext { get { return active && !finished; } }
    public bool Shrinking { get { return shrinking; } }
    public bool Finished { get { return finished; } }
    /// <summary>Seconds left of the current wait or shrink.</summary>
    public float Timer { get { return phaseTimer; } }

    /// <summary>Online phone: the server runs the zone, this one only shows it (and still hurts the local player).</summary>
    public bool remote;

    private void Awake()
    {
        ring = CreateRing("ZoneRing", new Color(0.3f, 0.6f, 1f, 0.9f), 0.6f);
        nextRing = CreateRing("NextZoneRing", new Color(1f, 1f, 1f, 0.8f), 0.3f);

        // Tall see-through blue wall marking the edge of the safe zone.
        wall = new GameObject("ZoneWall");
        wall.transform.SetParent(transform, false);
        wall.AddComponent<MeshFilter>().sharedMesh = MeshUtil.OpenCylinder;
        var mr = wall.AddComponent<MeshRenderer>();
        mr.sharedMaterial = UIUtil.UnlitMaterial(new Color(0.25f, 0.55f, 1f, 0.22f));
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        wall.SetActive(false);
    }

    private LineRenderer CreateRing(string name, Color color, float width)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(transform, false);
        var lr = obj.AddComponent<LineRenderer>();
        lr.loop = true;
        lr.useWorldSpace = true;
        lr.positionCount = RingSegments;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.material = UIUtil.UnlitMaterial(color);
        lr.startColor = color;
        lr.endColor = color;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
        return lr;
    }

    public void Init(Vector3 startCenterPos, float startRadiusValue)
    {
        remote = false;
        center = startCenterPos;
        radius = startRadiusValue;
        initialRadius = startRadiusValue;
        phase = 0;
        shrinking = false;
        finished = false;
        phaseTimer = WaitTimes[0];
        active = true;
        PrepareNextCircle();
        ring.enabled = true;
        nextRing.enabled = true;
        wall.SetActive(true);
        UpdateWall();
        DrawRing(ring, center, radius);
        DrawRing(nextRing, nextCenter, nextRadius);
    }

    /// <summary>Online phone: same start as the server; afterwards <see cref="ApplyNet"/> keeps it in step.</summary>
    public void InitRemote(Vector3 startCenterPos, float startRadiusValue)
    {
        Init(startCenterPos, startRadiusValue);
        remote = true;
        nextCenter = center;
        nextRadius = radius;
        nextRing.enabled = false;
    }

    /// <summary>Zone state from a server snapshot.</summary>
    public void ApplyNet(Vector3 c, float r, Vector3 nc, float nr, int netPhase, bool isActive, bool isShrinking, bool isFinished, float timer)
    {
        if (!remote)
            return;
        if (isShrinking && !shrinking && active)
        {
            var gm = GameManager.Instance;
            if (gm != null && gm.uiManager != null)
                gm.uiManager.Toast("Güvenli bölge daralıyor!");
        }
        center = c;
        radius = r;
        nextCenter = nc;
        nextRadius = nr;
        phase = Mathf.Clamp(netPhase, 0, WaitTimes.Length - 1);
        shrinking = isShrinking;
        finished = isFinished;
        phaseTimer = timer;
        if (!isActive && active)
            Stop();
        nextRing.enabled = active && !finished;
    }

    public void Stop()
    {
        active = false;
        if (ring != null) ring.enabled = false;
        if (nextRing != null) nextRing.enabled = false;
        if (wall != null) wall.SetActive(false);
    }

    private void PrepareNextCircle()
    {
        nextRadius = initialRadius * RadiusFractions[phase];
        Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, radius - nextRadius) * 0.8f;
        nextCenter = center + new Vector3(offset.x, 0f, offset.y);

        // Keep the final circles on land.
        Vector3 flat = new Vector3(nextCenter.x, 0f, nextCenter.z);
        float maxDist = Mathf.Max(0f, World.IslandRadius - nextRadius - 8f);
        if (flat.magnitude > maxDist)
            nextCenter = flat.normalized * maxDist;
    }

    private void Update()
    {
        if (!active)
            return;

        var gm = GameManager.Instance;
        if (gm == null || gm.currentState != GameState.InGame)
            return;

        if (remote)
        {
            phaseTimer = Mathf.Max(0f, phaseTimer - Time.deltaTime);   // smooth countdown between snapshots
        }
        else if (!finished)
        {
            phaseTimer -= Time.deltaTime;
            if (!shrinking)
            {
                if (phaseTimer <= 0f)
                {
                    shrinking = true;
                    phaseTimer = ShrinkTimes[phase];
                    startCenter = center;
                    startRadius = radius;
                    gm.uiManager.Toast("Güvenli bölge daralıyor!");
                }
            }
            else
            {
                float t = 1f - Mathf.Clamp01(phaseTimer / ShrinkTimes[phase]);
                radius = Mathf.Lerp(startRadius, nextRadius, t);
                center = Vector3.Lerp(startCenter, nextCenter, t);

                if (phaseTimer <= 0f)
                {
                    radius = nextRadius;
                    center = nextCenter;
                    shrinking = false;
                    phase++;
                    if (phase >= WaitTimes.Length)
                    {
                        phase = WaitTimes.Length - 1;
                        finished = true;
                        nextRing.enabled = false;
                    }
                    else
                    {
                        phaseTimer = WaitTimes[phase];
                        PrepareNextCircle();
                    }
                }
            }
        }

        DrawRing(ring, center, radius);
        if (nextRing.enabled)
            DrawRing(nextRing, nextCenter, nextRadius);
        UpdateWall();

        ApplyDamage(gm);
    }

    private void UpdateWall()
    {
        wall.transform.position = new Vector3(center.x, -10f, center.z);
        wall.transform.localScale = new Vector3(Mathf.Max(0.1f, radius), 160f + Mathf.Max(0f, MapData.MaxHeight - 40f), Mathf.Max(0.1f, radius));
    }

    private void ApplyDamage(GameManager gm)
    {
        float dmg = DamagePerSecond[phase] * Time.deltaTime;

        var player = gm.player;
        if (player != null && !player.isDead && !player.IsAirborne && IsOutside(player.transform.position))
            player.TakeDamage(dmg, -1);

        for (int i = gm.bots.Count - 1; i >= 0; i--)
        {
            var bot = gm.bots[i];
            if (bot != null && !bot.isDead && !bot.IsAirborne && IsOutside(bot.transform.position))
                bot.TakeDamage(dmg, -1);
        }
    }

    public float DistanceFromCenter(Vector3 position)
    {
        Vector3 d = position - center;
        d.y = 0f;
        return d.magnitude;
    }

    public bool IsOutside(Vector3 position)
    {
        return DistanceFromCenter(position) > radius;
    }

    /// <summary>Random point inside the current zone (and on the island).</summary>
    public Vector3 RandomPointInside(float fraction)
    {
        float r = Mathf.Min(radius, World.IslandRadius - 8f) * fraction;
        for (int i = 0; i < 8; i++)
        {
            Vector2 p = Random.insideUnitCircle * r;
            float x = center.x + p.x;
            float z = center.z + p.y;
            if (World.IsLand(x, z) && !World.IsBlocked(x, z))
                return new Vector3(x, World.HeightAt(x, z) + 1f, z);
        }
        return new Vector3(center.x, World.HeightAt(center.x, center.z) + 1f, center.z);
    }

    public string StatusText
    {
        get
        {
            if (!active)
                return "";
            if (finished)
                return "Son bölge!";
            int secs = Mathf.CeilToInt(Mathf.Max(0f, phaseTimer));
            string time = (secs / 60) + ":" + (secs % 60).ToString("00");
            return shrinking ? "Bölge daralıyor " + time : "Bölge " + time + " sonra daralacak";
        }
    }

    private static void DrawRing(LineRenderer lr, Vector3 c, float r)
    {
        for (int i = 0; i < RingSegments; i++)
        {
            float a = i * Mathf.PI * 2f / RingSegments;
            float x = c.x + Mathf.Cos(a) * r;
            float z = c.z + Mathf.Sin(a) * r;
            lr.SetPosition(i, new Vector3(x, Mathf.Max(0.2f, World.HeightAt(x, z)) + 0.5f, z));
        }
    }
}
