using UnityEngine;

public enum StepSurface
{
    Hard,     // asphalt, pavement, concrete
    Grass,
    Gravel,   // dirt, gravel, forest floor
    Floor,    // inside buildings, roofs, stairs
    Water     // wading
}

/// <summary>
/// Footsteps: what the ground is under a foot, and synthesised step sounds for each surface (a heel strike and
/// the roll onto the toes, plus the surface itself: a hard click on asphalt, a soft swish on grass, a granular
/// crunch on gravel, a hollow knock on floors, a splash in water), several variations each so steps don't repeat.
/// The rig raises a step when a foot touches down in the walk/run animation.
/// </summary>
public static class Footsteps
{
    private const int Rate = 22050;
    private const int Variants = 5;
    private static readonly AudioClip[,] clips = new AudioClip[5, Variants];
    private static readonly int[] last = new int[5];

    /// <summary>The ground under these feet (feet = the bottom of the character).</summary>
    public static StepSurface SurfaceAt(Vector3 feet)
    {
        if (PlayerController.WaterDepthAt(feet) > 0.05f)
            return StepSurface.Water;
        float terrain = World.HeightAt(feet.x, feet.z);
        if (feet.y > terrain + 0.3f)
            return StepSurface.Floor;   // on a floor, a roof or stairs
        if (!MapData.Loaded)
            return StepSurface.Grass;
        switch (MapData.Ground(feet.x, feet.z))
        {
            case MapData.GroundGrass:
            case MapData.GroundPark:
            case MapData.GroundPitch:
            case MapData.GroundCemetery:
                return StepSurface.Grass;
            case MapData.GroundDirt:
            case MapData.GroundIndustrial:
            case MapData.GroundForest:
                return StepSurface.Gravel;
            case MapData.GroundBuilding:
                return StepSurface.Floor;
            default:
                return StepSurface.Hard;
        }
    }

    /// <summary>A step sound for the surface (never the same variation twice in a row).</summary>
    public static AudioClip Clip(StepSurface s)
    {
        int si = (int)s;
        int v = Random.Range(0, Variants - 1);
        if (v >= last[si])
            v++;
        last[si] = v;
        if (clips[si, v] == null)
            clips[si, v] = Make(s, v);
        return clips[si, v];
    }

    /// <summary>How loud a surface is compared to the others (grass is soft, floors and water carry).</summary>
    public static float Loudness(StepSurface s)
    {
        switch (s)
        {
            case StepSurface.Grass: return 0.75f;
            case StepSurface.Gravel: return 1f;
            case StepSurface.Floor: return 1f;
            case StepSurface.Water: return 1.1f;
            default: return 0.9f;
        }
    }

    /// <summary>Plays a step: 2D for the local player, 3D (positioned) for everyone else.</summary>
    public static void Play(Vector3 feet, float volume, bool local)
    {
        if (NetGame.IsServer)
            return;
        var s = SurfaceAt(feet);
        float v = volume * Loudness(s);
        float pitch = Random.Range(0.93f, 1.07f);
        if (local)
            Sfx.Play(Clip(s), v, pitch);
        else
            Sfx.PlayAt(Clip(s), feet, v, pitch);
    }

    // ----- Synthesis -----

    private class Noise
    {
        private System.Random rng;
        public Noise(int seed) { rng = new System.Random(seed); }
        public float Next() { return (float)rng.NextDouble() * 2f - 1f; }
        public float Range(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
    }

    /// <summary>Two-pole resonant band-pass (state-variable filter).</summary>
    private struct Band
    {
        private float low, band, f, q;
        public Band(float hz, float damping) { low = band = 0f; f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(hz, Rate * 0.2f) / Rate); q = damping; }   // stable below ~fs/5
        public float Process(float x)
        {
            low += f * band;
            float high = x - low - q * band;
            band += f * high;
            return band;
        }
    }

    private static AudioClip Make(StepSurface s, int variant)
    {
        var n = new Noise(7919 * ((int)s + 1) + 104729 * (variant + 1));
        float toeAt = n.Range(0.055f, 0.085f);
        float length = s == StepSurface.Water ? 0.32f : s == StepSurface.Gravel ? 0.26f : 0.2f;
        int count = (int)(length * Rate);
        var d = new float[count];
        switch (s)
        {
            case StepSurface.Hard: Hard(d, n, toeAt); break;
            case StepSurface.Grass: Grass(d, n, toeAt); break;
            case StepSurface.Gravel: Gravel(d, n, toeAt); break;
            case StepSurface.Floor: Floor(d, n, toeAt); break;
            default: Water(d, n); break;
        }
        // Gentle fade-out and peak normalisation.
        float peak = 0.0001f;
        for (int i = 0; i < count; i++)
        {
            float tail = Mathf.Clamp01((count - i) / (Rate * 0.02f));
            d[i] *= tail;
            peak = Mathf.Max(peak, Mathf.Abs(d[i]));
        }
        float g = 0.9f / peak;
        for (int i = 0; i < count; i++)
            d[i] *= g;
        var clip = AudioClip.Create("Step_" + s + "_" + variant, count, 1, Rate, false);
        clip.SetData(d, 0);
        return clip;
    }

    /// <summary>Low body thump of the heel (shoe sole on the ground).</summary>
    private static void Thump(float[] d, float at, float hz, float decay, float amp)
    {
        int start = (int)(at * Rate);
        float phase = 0f;
        for (int i = start; i < d.Length; i++)
        {
            float t = (float)(i - start) / Rate;
            float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 2000f);
            if (env < 0.001f)
                break;
            phase += 2f * Mathf.PI * hz * (1f - t * 2f) / Rate;
            d[i] += Mathf.Sin(phase) * env * amp;
        }
    }

    /// <summary>Filtered noise burst (a click, a scuff, a swish).</summary>
    private static void Burst(float[] d, Noise n, float at, float hz, float damping, float attack, float decay, float amp)
    {
        int start = (int)(at * Rate);
        var bp = new Band(hz, damping);
        for (int i = start; i < d.Length; i++)
        {
            float t = (float)(i - start) / Rate;
            float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t / attack);
            if (t > attack && env < 0.001f)
                break;
            d[i] += bp.Process(n.Next()) * env * amp;
        }
    }

    /// <summary>Many tiny cracks (grit, gravel, leaves): random impulses rung through a band-pass.</summary>
    private static void Grains(float[] d, Noise n, float at, float span, float density, float hz, float amp)
    {
        int start = (int)(at * Rate), end = Mathf.Min(d.Length, start + (int)(span * Rate));
        var bp = new Band(hz, 0.5f);
        var bp2 = new Band(hz * 1.9f, 0.6f);
        for (int i = start; i < end; i++)
        {
            float t = (float)(i - start) / (end - start);
            float rate = density * (1f - t) * (1f - t);
            float x = n.Next() * 0.02f;
            if ((n.Next() * 0.5f + 0.5f) < rate / Rate)
                x += n.Next() * (1f - t);
            d[i] += (bp.Process(x) * 0.7f + bp2.Process(x) * 0.5f) * amp;
        }
    }

    private static void Hard(float[] d, Noise n, float toe)
    {
        Thump(d, 0f, n.Range(85f, 115f), 55f, 0.55f);
        Burst(d, n, 0f, n.Range(2600f, 3600f), 0.5f, 0.0005f, 260f, 1.1f);
        Burst(d, n, 0.004f, n.Range(900f, 1400f), 0.9f, 0.002f, 70f, 0.25f);   // scuff
        Thump(d, toe, n.Range(110f, 140f), 70f, 0.25f);
        Burst(d, n, toe, n.Range(3000f, 4200f), 0.5f, 0.0005f, 320f, 0.6f);
        Grains(d, n, 0.002f, 0.05f, 900f, 3500f, 0.25f);
    }

    private static void Grass(float[] d, Noise n, float toe)
    {
        Thump(d, 0f, n.Range(60f, 80f), 45f, 0.35f);
        Burst(d, n, 0f, n.Range(1300f, 1900f), 1.2f, 0.012f, 28f, 0.55f);       // swish
        Grains(d, n, 0.004f, 0.12f, 700f, n.Range(2500f, 3400f), 0.5f);         // blades
        Burst(d, n, toe, n.Range(1600f, 2200f), 1.2f, 0.01f, 35f, 0.3f);
    }

    private static void Gravel(float[] d, Noise n, float toe)
    {
        Thump(d, 0f, n.Range(70f, 95f), 50f, 0.45f);
        Grains(d, n, 0f, 0.16f, 3200f, n.Range(1800f, 2600f), 0.9f);            // crunch
        Grains(d, n, toe, 0.12f, 2200f, n.Range(2400f, 3200f), 0.6f);
        Burst(d, n, 0f, 900f, 1.1f, 0.006f, 40f, 0.2f);
    }

    private static void Floor(float[] d, Noise n, float toe)
    {
        // Hollow knock: a few damped modes of the floor plus the heel click, and a short room echo.
        float f0 = n.Range(160f, 210f);
        float[] modes = { f0, f0 * 2.3f, f0 * 4.6f };
        float[] amps = { 0.5f, 0.3f, 0.15f };
        for (int m = 0; m < 3; m++)
        {
            Thump(d, 0f, modes[m], 35f + m * 20f, amps[m]);
            Thump(d, toe, modes[m] * 1.06f, 45f + m * 25f, amps[m] * 0.45f);
        }
        Burst(d, n, 0f, n.Range(2200f, 3000f), 0.5f, 0.0005f, 300f, 0.8f);
        Burst(d, n, toe, n.Range(2600f, 3400f), 0.5f, 0.0005f, 340f, 0.45f);
        int echo = (int)(0.023f * Rate);
        for (int i = d.Length - 1; i >= echo; i--)
            d[i] += d[i - echo] * 0.22f;
    }

    private static void Water(float[] d, Noise n)
    {
        Burst(d, n, 0f, n.Range(700f, 1000f), 1.4f, 0.008f, 16f, 0.9f);         // splash body
        Burst(d, n, 0.01f, n.Range(2500f, 3200f), 0.8f, 0.004f, 30f, 0.35f);    // spray
        // Bubbles: short rising chirps.
        for (int b = 0; b < 6; b++)
        {
            int start = (int)(n.Range(0.02f, 0.2f) * Rate);
            float hz = n.Range(380f, 900f), phase = 0f, amp = n.Range(0.08f, 0.2f);
            for (int i = start; i < d.Length; i++)
            {
                float t = (float)(i - start) / Rate;
                float env = Mathf.Exp(-t * 60f);
                if (env < 0.002f)
                    break;
                phase += 2f * Mathf.PI * hz * (1f + t * 6f) / Rate;
                d[i] += Mathf.Sin(phase) * env * amp;
            }
        }
    }
}
