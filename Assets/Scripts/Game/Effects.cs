using UnityEngine;

/// <summary>Particle effects built in code: bullet impacts, explosions, landing dust.</summary>
public static class Effects
{
    private static ParticleSystem sparks;
    private static ParticleSystem smoke;
    private static ParticleSystem clouds;   // big soft puffs (smoke and gas grenades)
    private static ParticleSystem flames;
    private static Texture2D softTex;

    private static ParticleSystem Create(string name, float gravity, bool growing)
    {
        var go = new GameObject(name);
        Object.DontDestroyOnLoad(go);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;          // stays alive; we only emit by hand (rate is 0)
        main.playOnAwake = false;
        main.maxParticles = 600;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravity;
        main.startLifetime = 0.4f;
        main.startSpeed = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = false;

        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);

        if (growing)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
        }

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var mat = UIUtil.UnlitMaterial(Color.white);
        mat.mainTexture = UIUtil.Circle.texture;
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        ps.Play();
        return ps;
    }

    private static void Ensure()
    {
        if (sparks == null)
            sparks = Create("FX_Sparks", 1.5f, false);
        if (smoke == null)
            smoke = Create("FX_Smoke", -0.05f, true);
    }

    /// <summary>A round puff that fades to nothing at its edge (clouds read as volume, not discs).</summary>
    private static Texture2D Soft()
    {
        if (softTex != null)
            return softTex;
        const int n = 64;
        softTex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        softTex.wrapMode = TextureWrapMode.Clamp;
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = Mathf.Clamp01(1f - r);
                a = a * a * (3f - 2f * a);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        softTex.SetPixels32(px);
        softTex.Apply();
        return softTex;
    }

    private static void EnsureAreas()
    {
        Ensure();
        if (clouds == null)
        {
            clouds = Create("FX_Clouds", -0.01f, true);
            var main = clouds.main;
            main.maxParticles = 900;
            clouds.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture = Soft();
        }
        if (flames == null)
        {
            flames = Create("FX_Flames", -0.6f, false);
            var main = flames.main;
            main.maxParticles = 700;
            var size = flames.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            flames.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture = Soft();
        }
    }

    /// <summary>One flame tongue of a burning patch (molotov).</summary>
    public static void Flame(Vector3 point)
    {
        if (NetGame.IsServer)
            return;
        EnsureAreas();
        Color c = Color.Lerp(new Color(1f, 0.85f, 0.3f, 0.95f), new Color(1f, 0.35f, 0.08f, 0.9f), Random.value);
        Emit(flames, point, new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(1.2f, 2.4f), Random.Range(-0.2f, 0.2f)), Random.Range(0.5f, 1.1f), Random.Range(0.45f, 0.8f), c);
    }

    /// <summary>One big puff of a smoke or gas cloud (lives a few seconds, keeps the cloud thick while it is fed).</summary>
    public static void Cloud(Vector3 point, float size, Color color)
    {
        if (NetGame.IsServer)
            return;
        EnsureAreas();
        Emit(clouds, point, Random.insideUnitSphere * 0.35f + Vector3.up * 0.1f, size * Random.Range(0.8f, 1.2f), Random.Range(3f, 4f), color);
    }

    /// <summary>The bang of a flash grenade: a white burst and sparks.</summary>
    public static void FlashBurst(Vector3 point)
    {
        if (NetGame.IsServer)
            return;
        EnsureAreas();
        Emit(clouds, point, Vector3.zero, 6f, 0.35f, new Color(1f, 1f, 0.95f, 1f));
        for (int i = 0; i < 24; i++)
            Emit(sparks, point, Random.insideUnitSphere * Random.Range(4f, 10f), Random.Range(0.05f, 0.12f), Random.Range(0.3f, 0.6f), new Color(1f, 1f, 0.85f));
    }

    private static void Emit(ParticleSystem ps, Vector3 pos, Vector3 velocity, float size, float life, Color color)
    {
        var p = new ParticleSystem.EmitParams();
        p.position = pos;
        p.velocity = velocity;
        p.startSize = size;
        p.startLifetime = life;
        p.startColor = color;
        p.applyShapeToPosition = false;
        ps.Emit(p, 1);
    }

    /// <summary>Bullet hitting something. Body hits make a small red puff, surfaces throw sparks and dust.</summary>
    public static void Impact(Vector3 point, Vector3 normal, bool body)
    {
        if (NetGame.IsServer)
            return;   // nobody watches the server
        Ensure();
        if (body)
        {
            for (int i = 0; i < 5; i++)
                Emit(sparks, point, (normal + Random.insideUnitSphere * 0.8f) * Random.Range(0.5f, 1.5f), Random.Range(0.06f, 0.12f), 0.3f, new Color(0.7f, 0.08f, 0.08f));
            return;
        }
        for (int i = 0; i < 6; i++)
            Emit(sparks, point, (normal + Random.insideUnitSphere * 0.7f) * Random.Range(2f, 5f), Random.Range(0.03f, 0.06f), 0.25f, new Color(1f, 0.8f, 0.4f));
        Emit(smoke, point + normal * 0.05f, normal * 0.4f, 0.25f, 0.6f, new Color(0.6f, 0.55f, 0.5f, 0.7f));
    }

    public static void Explosion(Vector3 point)
    {
        if (NetGame.IsServer)
            return;   // nobody watches the server
        Ensure();
        for (int i = 0; i < 30; i++)
            Emit(sparks, point, Random.insideUnitSphere * Random.Range(4f, 12f) + Vector3.up * 3f, Random.Range(0.08f, 0.2f), Random.Range(0.4f, 0.9f), new Color(1f, Random.Range(0.4f, 0.8f), 0.15f));
        for (int i = 0; i < 18; i++)
            Emit(smoke, point + Random.insideUnitSphere, Random.insideUnitSphere * 2f + Vector3.up * 2f, Random.Range(1.2f, 2.4f), Random.Range(1.2f, 2.2f), new Color(0.3f, 0.28f, 0.26f, 0.8f));
        for (int i = 0; i < 8; i++)
            Emit(smoke, point, Random.insideUnitSphere * 3f, Random.Range(1.5f, 2.5f), 0.25f, new Color(1f, 0.6f, 0.2f, 0.9f));
    }

    /// <summary>One puff of coloured marker smoke (air drops).</summary>
    public static void Smoke(Vector3 point, Color color)
    {
        if (NetGame.IsServer)
            return;   // nobody watches the server
        Ensure();
        Emit(smoke, point + Random.insideUnitSphere * 0.3f, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(2f, 3.5f), Random.Range(-0.3f, 0.3f)), Random.Range(0.8f, 1.4f), Random.Range(2.5f, 3.5f), color);
    }

    public static void Dust(Vector3 point, int count)
    {
        if (NetGame.IsServer)
            return;   // nobody watches the server
        Ensure();
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Random.insideUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.3f;
            Emit(smoke, point, dir * Random.Range(1f, 3f), Random.Range(0.4f, 0.9f), Random.Range(0.6f, 1.1f), new Color(0.7f, 0.64f, 0.52f, 0.6f));
        }
    }
}
