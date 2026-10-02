using UnityEngine;

public enum RigPose
{
    Normal,
    Freefall,
    Parachute,
    Driving,
    Dead
}

/// <summary>
/// Simple blocky humanoid (head, torso, arms, legs, helmet, backpack) with code-driven animation:
/// walking, aiming, crouching, skydiving, parachuting, driving and falling over when killed.
/// Built around a CharacterController of height 1.8 centred on the transform (feet at y = -0.9).
/// </summary>
public class CharacterRig : MonoBehaviour
{
    public RigPose pose = RigPose.Normal;
    public bool crouched;
    public bool aiming;
    public float aimPitch;          // degrees, positive = looking down

    private Transform root;
    private Transform torso;
    private Transform head;
    private Transform armL, armR, legL, legR;
    private GameObject canopy;
    private Vector3 lastPos;
    private float speed;
    private float phase;
    private float deathT;

    public static CharacterRig Build(GameObject owner, Color shirt, Color pants, Color skin, Color helmet, Color pack)
    {
        var rig = owner.AddComponent<CharacterRig>();
        rig.Create(shirt, pants, skin, helmet, pack);
        return rig;
    }

    private Transform Part(Transform parent, string partName, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = partName;
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(color);
        return go.transform;
    }

    private Transform Pivot(Transform parent, string pivotName, Vector3 pos)
    {
        var t = new GameObject(pivotName).transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    private void Create(Color shirt, Color pants, Color skin, Color helmet, Color pack)
    {
        root = Pivot(transform, "Rig", Vector3.zero);
        Color boots = new Color(0.18f, 0.15f, 0.12f);

        // Legs (pivot at hip)
        legL = Pivot(root, "LegL", new Vector3(-0.12f, -0.12f, 0f));
        Part(legL, "Thigh", PrimitiveType.Cube, new Vector3(0f, -0.36f, 0f), new Vector3(0.17f, 0.72f, 0.19f), pants);
        Part(legL, "Boot", PrimitiveType.Cube, new Vector3(0f, -0.72f, 0.04f), new Vector3(0.18f, 0.1f, 0.27f), boots);
        legR = Pivot(root, "LegR", new Vector3(0.12f, -0.12f, 0f));
        Part(legR, "Thigh", PrimitiveType.Cube, new Vector3(0f, -0.36f, 0f), new Vector3(0.17f, 0.72f, 0.19f), pants);
        Part(legR, "Boot", PrimitiveType.Cube, new Vector3(0f, -0.72f, 0.04f), new Vector3(0.18f, 0.1f, 0.27f), boots);

        // Torso, belt, vest
        torso = Pivot(root, "Torso", new Vector3(0f, -0.1f, 0f));
        Part(torso, "Chest", PrimitiveType.Cube, new Vector3(0f, 0.33f, 0f), new Vector3(0.48f, 0.62f, 0.27f), shirt);
        Part(torso, "Belt", PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.5f, 0.08f, 0.29f), boots);
        Part(torso, "Vest", PrimitiveType.Cube, new Vector3(0f, 0.38f, 0.02f), new Vector3(0.5f, 0.4f, 0.3f), shirt * 0.75f);
        Part(torso, "Pack", PrimitiveType.Cube, new Vector3(0f, 0.36f, -0.22f), new Vector3(0.36f, 0.42f, 0.18f), pack);

        // Head + helmet
        head = Pivot(torso, "Head", new Vector3(0f, 0.72f, 0f));
        Part(head, "Face", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0f), new Vector3(0.3f, 0.32f, 0.3f), skin);
        Part(head, "Helmet", PrimitiveType.Sphere, new Vector3(0f, 0.2f, -0.01f), new Vector3(0.36f, 0.24f, 0.37f), helmet);
        Part(head, "Visor", PrimitiveType.Cube, new Vector3(0f, 0.13f, 0.14f), new Vector3(0.22f, 0.05f, 0.04f), new Color(0.1f, 0.1f, 0.12f));

        // Arms (pivot at shoulder)
        armL = Pivot(torso, "ArmL", new Vector3(-0.3f, 0.58f, 0f));
        Part(armL, "Arm", PrimitiveType.Cube, new Vector3(0f, -0.28f, 0f), new Vector3(0.13f, 0.58f, 0.14f), shirt);
        Part(armL, "Hand", PrimitiveType.Cube, new Vector3(0f, -0.6f, 0f), new Vector3(0.11f, 0.1f, 0.11f), skin);
        armR = Pivot(torso, "ArmR", new Vector3(0.3f, 0.58f, 0f));
        Part(armR, "Arm", PrimitiveType.Cube, new Vector3(0f, -0.28f, 0f), new Vector3(0.13f, 0.58f, 0.14f), shirt);
        Part(armR, "Hand", PrimitiveType.Cube, new Vector3(0f, -0.6f, 0f), new Vector3(0.11f, 0.1f, 0.11f), skin);

        // Parachute (hidden until used)
        canopy = new GameObject("Parachute");
        canopy.transform.SetParent(transform, false);
        var cloth = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(cloth.GetComponent<Collider>());
        cloth.transform.SetParent(canopy.transform, false);
        cloth.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        cloth.transform.localScale = new Vector3(3.4f, 0.9f, 2.6f);
        cloth.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(helmet * 0.6f + new Color(0.4f, 0.35f, 0.3f));
        for (int i = 0; i < 4; i++)
        {
            float sx = i % 2 == 0 ? -1f : 1f;
            float sz = i < 2 ? -1f : 1f;
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(line.GetComponent<Collider>());
            line.transform.SetParent(canopy.transform, false);
            Vector3 top = new Vector3(sx * 1.4f, 3.0f, sz * 1.0f);
            Vector3 bottom = new Vector3(sx * 0.25f, 0.5f, 0f);
            line.transform.localPosition = (top + bottom) * 0.5f;
            line.transform.localScale = new Vector3(0.02f, Vector3.Distance(top, bottom), 0.02f);
            line.transform.localRotation = Quaternion.FromToRotation(Vector3.up, top - bottom);
            line.GetComponent<Renderer>().sharedMaterial = MaterialCache.Lit(new Color(0.85f, 0.85f, 0.8f));
        }
        canopy.SetActive(false);

        lastPos = transform.position;
    }

    public void SetVisible(bool visible)
    {
        if (root != null)
            root.gameObject.SetActive(visible);
        if (!visible && canopy != null)
            canopy.SetActive(false);
    }

    public void ResetPose()
    {
        pose = RigPose.Normal;
        crouched = false;
        deathT = 0f;
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        lastPos = transform.position;
        SetVisible(true);
    }

    private void LateUpdate()
    {
        if (root == null || !root.gameObject.activeSelf)
            return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 delta = transform.position - lastPos;
        delta.y = 0f;
        lastPos = transform.position;
        speed = Mathf.Lerp(speed, delta.magnitude / dt, dt * 10f);

        canopy.SetActive(pose == RigPose.Parachute);

        switch (pose)
        {
            case RigPose.Dead:
                AnimateDeath(dt);
                return;
            case RigPose.Freefall:
                SetLimbs(-160f, -160f, 25f, 25f, 15f, 0f);
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.Euler(70f, 0f, 0f);
                head.localRotation = Quaternion.Euler(-55f, 0f, 0f);
                return;
            case RigPose.Parachute:
                SetLimbs(-170f, -170f, 10f, -10f, 0f, 0f);
                root.localPosition = Vector3.zero;
                root.localRotation = Quaternion.identity;
                head.localRotation = Quaternion.identity;
                return;
            case RigPose.Driving:
                SetLimbs(-70f, -70f, -85f, -85f, 0f, 0f);
                root.localPosition = new Vector3(0f, -0.35f, 0f);
                root.localRotation = Quaternion.identity;
                head.localRotation = Quaternion.identity;
                return;
        }

        root.localRotation = Quaternion.identity;
        head.localRotation = Quaternion.Euler(Mathf.Clamp(aimPitch * 0.5f, -30f, 30f), 0f, 0f);

        float move = Mathf.Clamp01(speed / 5f);
        phase += speed * dt * 2.4f;
        float swing = Mathf.Sin(phase) * 38f * move;

        float crouchLeg = crouched ? -55f : 0f;
        root.localPosition = new Vector3(0f, crouched ? -0.3f : 0f, 0f);
        torso.localRotation = Quaternion.Euler(crouched ? 18f : 4f * move, 0f, 0f);

        float armPitch = -82f + Mathf.Clamp(aimPitch, -60f, 60f);
        if (aiming || move < 0.15f)
        {
            armR.localRotation = Quaternion.Euler(armPitch, 0f, 0f);
            armL.localRotation = Quaternion.Euler(armPitch + 8f, 32f, 0f);
        }
        else
        {
            armR.localRotation = Quaternion.Euler(-swing * 0.8f - 10f, 0f, 0f);
            armL.localRotation = Quaternion.Euler(swing * 0.8f - 10f, 0f, 0f);
        }
        legL.localRotation = Quaternion.Euler(swing + crouchLeg, 0f, 0f);
        legR.localRotation = Quaternion.Euler(-swing + crouchLeg, 0f, 0f);
    }

    private void SetLimbs(float armLX, float armRX, float legLX, float legRX, float armSpread, float legSpread)
    {
        armL.localRotation = Quaternion.Euler(armLX, 0f, -armSpread);
        armR.localRotation = Quaternion.Euler(armRX, 0f, armSpread);
        legL.localRotation = Quaternion.Euler(legLX, 0f, -legSpread);
        legR.localRotation = Quaternion.Euler(legRX, 0f, legSpread);
        torso.localRotation = Quaternion.identity;
    }

    private void AnimateDeath(float dt)
    {
        deathT = Mathf.Min(1f, deathT + dt * 2.2f);
        float k = deathT * deathT;
        root.localRotation = Quaternion.Euler(-88f * k, 0f, 0f);
        root.localPosition = new Vector3(0f, -0.7f * k, -0.3f * k);
        armL.localRotation = Quaternion.Euler(-150f * k, 0f, -20f * k);
        armR.localRotation = Quaternion.Euler(-150f * k, 0f, 20f * k);
    }
}
