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

    private static readonly float[] WaitTimes = { 45f, 35f, 30f, 25f, 20f, 15f };
    private static readonly float[] ShrinkTimes = { 30f, 25f, 22f, 18f, 15f, 12f };
    private static readonly float[] RadiusFractions = { 0.62f, 0.4f, 0.25f, 0.14f, 0.06f, 0f };
    private static readonly float[] DamagePerSecond = { 2f, 4f, 6f, 9f, 13f, 18f };

    private float initialRadius;
    private int phase;
    private bool shrinking;
    private bool finished;
    private float phaseTimer;

    private Vector3 startCenter;
    private float startRadius;
    private Vector3 nextCenter;
    private float nextRadius;

    private LineRenderer ring;
    private LineRenderer nextRing;
    private const int RingSegments = 72;

    private void Awake()
    {
        ring = CreateRing("ZoneRing", new Color(0.3f, 0.6f, 1f, 0.9f), 0.6f);
        nextRing = CreateRing("NextZoneRing", new Color(1f, 1f, 1f, 0.8f), 0.3f);
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
        DrawRing(ring, center, radius);
        DrawRing(nextRing, nextCenter, nextRadius);
    }

    public void Stop()
    {
        active = false;
        if (ring != null) ring.enabled = false;
        if (nextRing != null) nextRing.enabled = false;
    }

    private void PrepareNextCircle()
    {
        nextRadius = initialRadius * RadiusFractions[phase];
        Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, radius - nextRadius) * 0.8f;
        nextCenter = center + new Vector3(offset.x, 0f, offset.y);

        // Keep the final circles on land.
        Vector3 flat = new Vector3(nextCenter.x, 0f, nextCenter.z);
        float maxDist = Mathf.Max(0f, GameBootstrap.IslandRadius - nextRadius - 3f);
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

        if (!finished)
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

        ApplyDamage(gm);
    }

    private void ApplyDamage(GameManager gm)
    {
        float dmg = DamagePerSecond[phase] * Time.deltaTime;

        var player = gm.player;
        if (player != null && !player.isDead && IsOutside(player.transform.position))
            player.TakeDamage(dmg, -1);

        for (int i = gm.bots.Count - 1; i >= 0; i--)
        {
            var bot = gm.bots[i];
            if (bot != null && !bot.isDead && IsOutside(bot.transform.position))
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
        float r = Mathf.Min(radius, GameBootstrap.IslandRadius - 3f) * fraction;
        Vector2 p = Random.insideUnitCircle * r;
        return new Vector3(center.x + p.x, 1f, center.z + p.y);
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
            lr.SetPosition(i, new Vector3(c.x + Mathf.Cos(a) * r, 0.4f, c.z + Mathf.Sin(a) * r));
        }
    }
}
