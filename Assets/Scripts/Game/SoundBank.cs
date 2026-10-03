using UnityEngine;

/// <summary>
/// Every sound in the game is synthesised at startup (no audio files needed):
/// gunshots per weapon, explosion, footsteps, hit ticks, pickups, beeps and looping engine/wind/plane hums.
/// </summary>
public static class SoundBank
{
    private const int Rate = 22050;
    private static readonly System.Random rng = new System.Random(4242);

    private static AudioClip pistol, smg, rifle, shotgun, sniper;
    private static AudioClip explosion, footstep, hit, kill, pickup, beep, reload, land, whoosh;
    private static AudioClip planeLoop, windLoop, engineLoop;

    public static AudioClip Gunshot(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Pistol: return pistol ?? (pistol = Shot("Pistol", 0.35f, 20f, 160f, 0.55f));
            case WeaponType.SMG: return smg ?? (smg = Shot("SMG", 0.28f, 26f, 190f, 0.6f));
            case WeaponType.Shotgun: return shotgun ?? (shotgun = Shot("Shotgun", 0.7f, 9f, 70f, 0.25f));
            case WeaponType.Sniper: return sniper ?? (sniper = Shot("Sniper", 1.1f, 6f, 90f, 0.3f));
            default: return rifle ?? (rifle = Shot("Rifle", 0.45f, 15f, 120f, 0.4f));
        }
    }

    public static AudioClip Explosion { get { return explosion ?? (explosion = MakeExplosion()); } }
    public static AudioClip Footstep { get { return footstep ?? (footstep = Noise("Step", 0.09f, 40f, 0.12f, 0.5f)); } }
    public static AudioClip Land { get { return land ?? (land = Noise("Land", 0.25f, 14f, 0.08f, 0.9f)); } }
    public static AudioClip Hit { get { return hit ?? (hit = Tone("Hit", 0.06f, 1500f, 1500f, 60f, 0.5f)); } }
    public static AudioClip Kill { get { return kill ?? (kill = TwoTone("Kill", 900f, 1350f)); } }
    public static AudioClip Pickup { get { return pickup ?? (pickup = Tone("Pickup", 0.14f, 600f, 1300f, 18f, 0.45f)); } }
    public static AudioClip Beep { get { return beep ?? (beep = Tone("Beep", 0.25f, 880f, 880f, 10f, 0.35f)); } }
    public static AudioClip Reload { get { return reload ?? (reload = MakeReload()); } }
    public static AudioClip Whoosh { get { return whoosh ?? (whoosh = Noise("Whoosh", 0.5f, 5f, 0.04f, 0.6f)); } }
    public static AudioClip PlaneLoop { get { return planeLoop ?? (planeLoop = Hum("Plane", 2f, new[] { 70f, 140f, 210f, 280f }, new[] { 1f, 0.6f, 0.35f, 0.2f }, 0.5f)); } }
    public static AudioClip EngineLoop { get { return engineLoop ?? (engineLoop = Hum("Engine", 1f, new[] { 55f, 110f, 165f, 330f }, new[] { 1f, 0.7f, 0.4f, 0.15f }, 0.45f)); } }
    public static AudioClip WindLoop { get { return windLoop ?? (windLoop = MakeWind()); } }

    private static float Rnd()
    {
        return (float)rng.NextDouble() * 2f - 1f;
    }

    private static AudioClip Create(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// <summary>Noise crack + low thump, filtered so bigger guns sound duller.</summary>
    private static AudioClip Shot(string name, float length, float decay, float thumpHz, float brightness)
    {
        int n = (int)(length * Rate);
        var data = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float env = Mathf.Exp(-t * decay);
            float attack = Mathf.Clamp01(t * 800f);
            lp += brightness * (Rnd() - lp);
            float crack = lp * env;
            float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t * (1f - t * 0.6f)) * Mathf.Exp(-t * decay * 0.6f);
            float tail = lp * Mathf.Exp(-t * decay * 0.25f) * 0.12f;
            data[i] = Mathf.Clamp((crack * 0.9f + thump * 0.55f + tail) * attack, -1f, 1f) * 0.9f;
        }
        return Create(name, data);
    }

    private static AudioClip MakeExplosion()
    {
        int n = (int)(1.8f * Rate);
        var data = new float[n];
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            lp += 0.15f * (Rnd() - lp);
            lp2 += 0.03f * (lp - lp2);
            float env = Mathf.Exp(-t * 2.6f) * Mathf.Clamp01(t * 300f);
            float boom = Mathf.Sin(2f * Mathf.PI * 42f * t) * Mathf.Exp(-t * 3.5f);
            data[i] = Mathf.Clamp((lp * 0.6f + lp2 * 3f + boom * 0.7f) * env, -1f, 1f);
        }
        return Create("Explosion", data);
    }

    private static AudioClip Noise(string name, float length, float decay, float filter, float gain)
    {
        int n = (int)(length * Rate);
        var data = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            lp += filter * (Rnd() - lp);
            float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 400f);
            data[i] = Mathf.Clamp(lp * env * gain * 4f, -1f, 1f);
        }
        return Create(name, data);
    }

    public static AudioClip MakeUiTone(string name, float length, float startHz, float endHz, float decay, float gain)
    {
        return Tone(name, length, startHz, endHz, decay, gain);
    }

    private static AudioClip Tone(string name, float length, float startHz, float endHz, float decay, float gain)
    {
        int n = (int)(length * Rate);
        var data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float hz = Mathf.Lerp(startHz, endHz, t / length);
            phase += 2f * Mathf.PI * hz / Rate;
            float env = Mathf.Exp(-t * decay) * Mathf.Clamp01(t * 600f) * Mathf.Clamp01((length - t) * 200f);
            data[i] = Mathf.Sin(phase) * env * gain;
        }
        return Create(name, data);
    }

    private static AudioClip TwoTone(string name, float a, float b)
    {
        int half = (int)(0.07f * Rate);
        var data = new float[half * 2];
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)(i % half) / Rate;
            float hz = i < half ? a : b;
            float env = Mathf.Exp(-t * 30f) * Mathf.Clamp01(t * 600f);
            data[i] = Mathf.Sin(2f * Mathf.PI * hz * t) * env * 0.5f;
        }
        return Create(name, data);
    }

    private static AudioClip MakeReload()
    {
        int n = (int)(0.5f * Rate);
        var data = new float[n];
        float lp = 0f;
        float[] clicks = { 0f, 0.28f };
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            lp += 0.5f * (Rnd() - lp);
            float v = 0f;
            foreach (float c in clicks)
            {
                float dt = t - c;
                if (dt >= 0f)
                    v += lp * Mathf.Exp(-dt * 90f) + Mathf.Sin(2f * Mathf.PI * 2200f * dt) * Mathf.Exp(-dt * 120f) * 0.4f;
            }
            data[i] = Mathf.Clamp(v * 0.6f, -1f, 1f);
        }
        return Create("Reload", data);
    }

    /// <summary>Seamless loop: frequencies must complete whole cycles in the clip length.</summary>
    private static AudioClip Hum(string name, float length, float[] freqs, float[] amps, float gain)
    {
        int n = (int)(length * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float v = 0f;
            for (int k = 0; k < freqs.Length; k++)
                v += Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * amps[k];
            float wobble = 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 4f * t);
            data[i] = v * wobble * gain * 0.35f;
        }
        return Create(name, data);
    }

    private static AudioClip MakeWind()
    {
        int n = (int)(2f * Rate);
        int fade = (int)(0.25f * Rate);
        var raw = new float[n + fade];
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            lp += 0.06f * (Rnd() - lp);
            lp2 += 0.2f * (lp - lp2);
            raw[i] = lp2 * 3f;
        }
        var data = new float[n];
        for (int i = 0; i < n; i++)
            data[i] = raw[i];
        // Cross-fade the overflow into the start so the loop has no click.
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            data[i] = Mathf.Lerp(raw[n + i], raw[i], k);
        }
        return Create("Wind", data);
    }
}

/// <summary>Pooled sound playback: 2D for the local player, 3D for everything in the world.</summary>
public static class Sfx
{
    private static AudioSource[] pool2D;
    private static AudioSource[] pool3D;
    private static int next2D, next3D;
    private static GameObject root;

    private static void Ensure()
    {
        if (root != null)
            return;
        root = new GameObject("Sfx");
        Object.DontDestroyOnLoad(root);

        pool2D = new AudioSource[8];
        for (int i = 0; i < pool2D.Length; i++)
        {
            var src = new GameObject("2D").AddComponent<AudioSource>();
            src.transform.SetParent(root.transform, false);
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            pool2D[i] = src;
        }

        pool3D = new AudioSource[20];
        for (int i = 0; i < pool3D.Length; i++)
        {
            var src = new GameObject("3D").AddComponent<AudioSource>();
            src.transform.SetParent(root.transform, false);
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 5f;
            src.maxDistance = 110f;
            src.dopplerLevel = 0f;
            pool3D[i] = src;
        }
    }

    public static void Play(AudioClip clip, float volume, float pitch)
    {
        if (clip == null)
            return;
        Ensure();
        var src = pool2D[next2D];
        next2D = (next2D + 1) % pool2D.Length;
        src.pitch = pitch;
        src.PlayOneShot(clip, volume);
    }

    public static void Play(AudioClip clip, float volume)
    {
        Play(clip, volume, 1f);
    }

    public static void PlayAt(AudioClip clip, Vector3 position, float volume, float pitch)
    {
        if (clip == null)
            return;
        Ensure();
        var src = pool3D[next3D];
        next3D = (next3D + 1) % pool3D.Length;
        src.transform.position = position;
        src.pitch = pitch;
        src.clip = clip;
        src.volume = volume;
        src.Play();
    }

    /// <summary>Looping source attached to an object (engines, plane, wind).</summary>
    public static AudioSource CreateLoop(Transform parent, AudioClip clip, float volume, bool spatial)
    {
        var src = parent.gameObject.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.volume = volume;
        src.playOnAwake = false;
        src.spatialBlend = spatial ? 1f : 0f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 10f;
        src.maxDistance = 160f;
        src.dopplerLevel = 0f;
        return src;
    }
}
