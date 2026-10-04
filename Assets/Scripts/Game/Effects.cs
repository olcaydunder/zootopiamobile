using UnityEngine;

/// <summary>Particle effects built in code: bullet impacts, explosions, landing dust.</summary>
public static class Effects
{
    private static ParticleSystem sparks;
    private static ParticleSystem smoke;

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
        Ensure();
        Emit(smoke, point + Random.insideUnitSphere * 0.3f, new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(2f, 3.5f), Random.Range(-0.3f, 0.3f)), Random.Range(0.8f, 1.4f), Random.Range(2.5f, 3.5f), color);
    }

    public static void Dust(Vector3 point, int count)
    {
        Ensure();
        for (int i = 0; i < count; i++)
        {
            Vector3 dir = Random.insideUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.3f;
            Emit(smoke, point, dir * Random.Range(1f, 3f), Random.Range(0.4f, 0.9f), Random.Range(0.6f, 1.1f), new Color(0.7f, 0.64f, 0.52f, 0.6f));
        }
    }
}
