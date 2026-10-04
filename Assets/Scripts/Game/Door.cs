using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A hinged door in a building doorway. The player opens and closes it with the KAPI button;
/// bots push nearby doors open as they walk through. Swings away from whoever opens it.
/// </summary>
public class Door : MonoBehaviour
{
    public static readonly List<Door> All = new List<Door>();

    public const float OpenAngle = 100f;
    public const float SwingSpeed = 320f;   // degrees per second

    public bool IsOpen { get; private set; }
    private float angle, target;
    private Transform hinge;

    /// <summary>World position of the door's centre (for distance checks).</summary>
    public Vector3 Center { get; private set; }

    /// <summary>
    /// Builds a door filling a doorway. <paramref name="hingeSide"/> is the doorway's left edge,
    /// <paramref name="along"/> the direction across the doorway, <paramref name="width"/> and
    /// <paramref name="height"/> its size; the floor is at hingeSide.y.
    /// </summary>
    public static Door Create(Transform parent, Vector3 hingeSide, Vector3 along, float width, float height, Material mat)
    {
        var root = new GameObject("Door");
        root.transform.SetParent(parent, false);
        root.transform.position = hingeSide;
        root.transform.rotation = Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up);

        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(root.transform, false);

        var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
        leaf.name = "Leaf";
        leaf.transform.SetParent(hinge, false);
        // Local +X runs across the doorway (root's right = along), leaf hinged at x = 0.
        leaf.transform.localPosition = new Vector3(width * 0.5f, height * 0.5f, 0f);
        leaf.transform.localScale = new Vector3(width - 0.04f, height - 0.03f, 0.06f);
        var mr = leaf.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // One handle bar through the leaf (sticks out on both sides).
        var knob = GameObject.CreatePrimitive(PrimitiveType.Cube);
        knob.name = "Handle";
        Object.Destroy(knob.GetComponent<Collider>());
        knob.transform.SetParent(leaf.transform, false);
        knob.transform.localPosition = new Vector3(0.4f, -0.04f, 0f);
        knob.transform.localScale = new Vector3(0.08f, 0.012f, 3.2f);
        var kr = knob.GetComponent<MeshRenderer>();
        kr.sharedMaterial = MaterialCache.Lit(new Color(0.75f, 0.72f, 0.65f));
        kr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var door = root.AddComponent<Door>();
        door.hinge = hinge;
        door.Center = hingeSide + along.normalized * (width * 0.5f) + Vector3.up * (height * 0.5f);
        All.Add(door);
        return door;
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    /// <summary>Opens (away from <paramref name="from"/>) or closes the door.</summary>
    public void Toggle(Vector3 from)
    {
        if (IsOpen)
            Close();
        else
            Open(from);
    }

    public void Open(Vector3 from)
    {
        if (IsOpen)
            return;
        IsOpen = true;
        // Swing away from the opener: the leaf's front (+Z of the root) faces one side of the wall.
        float side = Vector3.Dot(from - Center, transform.forward);
        target = side > 0f ? -OpenAngle : OpenAngle;
        Sfx.PlayAt(SoundBank.Footstep, Center, 0.5f, 0.55f);
        enabled = true;
        NetGame.DoorChanged(this, from);
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        target = 0f;
        Sfx.PlayAt(SoundBank.Footstep, Center, 0.6f, 0.45f);
        enabled = true;
        NetGame.DoorChanged(this, Center);
    }

    /// <summary>Back to closed instantly (new round).</summary>
    public void ResetClosed()
    {
        IsOpen = false;
        angle = target = 0f;
        hinge.localRotation = Quaternion.identity;
        enabled = false;
    }

    private void Update()
    {
        angle = Mathf.MoveTowards(angle, target, SwingSpeed * Time.deltaTime);
        hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
        if (Mathf.Approximately(angle, target))
            enabled = false;
    }

    /// <summary>Closest door within <paramref name="maxDistance"/> of a position, or null.</summary>
    public static Door Nearest(Vector3 position, float maxDistance)
    {
        Door best = null;
        float bestSq = maxDistance * maxDistance;
        for (int i = 0; i < All.Count; i++)
        {
            Vector3 d = All[i].Center - position;
            d.y *= 2f;   // a door on another storey is not "near"
            float sq = d.sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = All[i];
            }
        }
        return best;
    }

    /// <summary>Opens every closed door close to <paramref name="position"/> (bots walking through).</summary>
    public static void PushOpenNear(Vector3 position, float radius)
    {
        float r2 = radius * radius;
        for (int i = 0; i < All.Count; i++)
        {
            var door = All[i];
            if (door.IsOpen)
                continue;
            Vector3 d = door.Center - position;
            d.y = 0f;
            if (d.sqrMagnitude < r2)
                door.Open(position);
        }
    }

    public static void ResetAll()
    {
        for (int i = 0; i < All.Count; i++)
            All[i].ResetClosed();
    }
}
