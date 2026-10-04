using UnityEngine;

/// <summary>Drop plane that crosses the island at the start of a match.</summary>
public class AirPlane : MonoBehaviour
{
    public const float Altitude = 170f;
    public const float Speed = 22f;

    public Vector3 start;
    public Vector3 end;
    public bool Finished { get; private set; }

    private float travelled;
    private float length;
    private AudioSource hum;

    public float Progress
    {
        get { return length > 0f ? Mathf.Clamp01(travelled / length) : 1f; }
    }

    public Vector3 Direction
    {
        get { return (end - start).normalized; }
    }

    public static AirPlane Launch()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        Vector3 side = new Vector3(-dir.z, 0f, dir.x) * Random.Range(-30f, 30f);
        float half = World.IslandRadius + 70f;
        return Launch(-dir * half + side + Vector3.up * Altitude, dir * half + side + Vector3.up * Altitude);
    }

    /// <summary>A plane on a given path (online: the server picks it, every phone flies the same one).</summary>
    public static AirPlane Launch(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1f)
            dir = Vector3.forward;
        var go = new GameObject("DropPlane");
        var plane = go.AddComponent<AirPlane>();
        plane.start = from;
        plane.end = to;
        plane.length = Vector3.Distance(plane.start, plane.end);
        go.transform.position = plane.start;
        go.transform.rotation = Quaternion.LookRotation(dir.normalized);
        plane.Build();
        return plane;
    }

    private void Part(PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Color color)
    {
        var p = GameObject.CreatePrimitive(type);
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(transform, false);
        p.transform.localPosition = pos;
        p.transform.localScale = scale;
        p.transform.localRotation = Quaternion.Euler(euler);
        p.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(color);
    }

    private void Build()
    {
        Color hull = new Color(0.75f, 0.77f, 0.78f);
        Color dark = new Color(0.3f, 0.32f, 0.34f);
        Part(PrimitiveType.Capsule, Vector3.zero, new Vector3(3.2f, 9f, 3.2f), new Vector3(90f, 0f, 0f), hull);
        Part(PrimitiveType.Cube, new Vector3(0f, 0.6f, 1f), new Vector3(26f, 0.35f, 3.2f), Vector3.zero, hull);
        Part(PrimitiveType.Cube, new Vector3(0f, 0.8f, -7.5f), new Vector3(9f, 0.25f, 2f), Vector3.zero, hull);
        Part(PrimitiveType.Cube, new Vector3(0f, 2.4f, -7.8f), new Vector3(0.3f, 3.4f, 2.2f), Vector3.zero, hull);
        Part(PrimitiveType.Cube, new Vector3(0f, 0.6f, 8.2f), new Vector3(2.2f, 0.9f, 0.4f), Vector3.zero, dark);
        for (int s = -1; s <= 1; s += 2)
        {
            Part(PrimitiveType.Cylinder, new Vector3(s * 5.5f, 0f, 2f), new Vector3(1.1f, 1.6f, 1.1f), new Vector3(90f, 0f, 0f), dark);
            Part(PrimitiveType.Cylinder, new Vector3(s * 10f, 0f, 1.8f), new Vector3(1f, 1.4f, 1f), new Vector3(90f, 0f, 0f), dark);
        }

        hum = Sfx.CreateLoop(transform, SoundBank.PlaneLoop, 0.9f, true);
        hum.minDistance = 30f;
        hum.maxDistance = 300f;
        hum.Play();
    }

    private void Update()
    {
        travelled += Speed * Time.deltaTime;
        transform.position = start + Direction * travelled;
        if (!Finished && travelled >= length)
        {
            Finished = true;
            Destroy(gameObject, 6f);
        }
    }
}
