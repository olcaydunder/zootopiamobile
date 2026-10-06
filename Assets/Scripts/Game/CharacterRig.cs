using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public enum RigPose
{
    Normal,
    Freefall,
    Parachute,
    Driving,
    Dead,
    Swim,
    Prone      // lying on the stomach, crawling
}

/// <summary>
/// Character visuals. Uses an animated 3D model from Resources/Models/Characters when available
/// (idle, run, shoot, hit and death clips played through the Playables API), otherwise a
/// simple blocky humanoid (head, torso, arms, legs, helmet, backpack) with code-driven animation:
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
    private Renderer canopyCloth;
    private Material canopyDefault;
    private string canopyCamoShown = "";

    /// <summary>Parachute camo id (Cosmetics.ParachuteCamos); applied when the canopy opens.</summary>
    public string parachuteCamo = "";
    private Vector3 lastPos;
    private float speed;
    private float phase;
    private float deathT;

    // Model mode
    public Transform weaponHold;      // moved to the right hand every frame
    public Transform aimReference;    // rotation the gun should point along
    private GameObject model;
    private Transform rightHand;
    private Animator animator;
    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private AnimationClipPlayable[] states;
    private float[] weights;
    private bool[] looping;
    private float hitTimer;
    private bool deathStarted;
    private Animation legacy;
    private string legacyState;
    private const int Idle = 0, Run = 1, Shoot = 2, Death = 3, Hit = 4;

    // Procedural layer on top of the clips: walk/run blend synced to the ground speed, hips turned towards the
    // way you move (strafing, walking backwards), knees bent when crouching, upper body following the aim,
    // the gun shouldered (or held at the ready) with both hands on it (two-bone IK), and footstep events.
    /// <summary>Raised when a foot touches the ground while walking or running (true = left foot).</summary>
    public System.Action<bool> footstep;
    /// <summary>The owner is airborne / swimming / sliding: no footsteps.</summary>
    public bool grounded = true;
    /// <summary>Menu showcase (lobby): the gun held at the ready across the body, whatever the camera does.</summary>
    public bool showcase;
    private Transform hips, spine, chest, upLegL, shinL, footL, upLegR, shinR, footR, upperArmL, foreL, handL, upperArmR, foreR;
    private Transform yawBone;          // carries both the legs and the spine (Mixamo: Hips; Quaternius: Body)
    private Transform headBone, maskMount;   // mount: centre of the head, facing forward, world scale = head size
    private Transform neckBone, clavL, clavR;   // neck and collarbones (shoulders follow the hands, the neck the head)
    private GameObject mask;
    private string maskShown = "";
    private bool feetFree;              // feet are not children of the shins (Quaternius rigs): moved along by hand
    private Transform[] posedBones;     // everything PoseBody changes, to undo it if the animator skipped a frame
    private Quaternion[] baseRot, posedRot;
    private Vector3[] basePos, posedPos;
    private bool posedValid;
    private float thighLen, shinLen, upperArmLen, foreArmLen;
    private Quaternion gripRel = Quaternion.identity;   // right hand in the one-handed aim clip, relative to the body
    private bool gripRelKnown;
    private SkinnedMeshRenderer[] skins;
    private float strideSpeed = 3.5f;
    private Vector3 velocity;                           // smoothed ground velocity (world)
    private float crouchK, hipYaw, aimK;
    private float verticalSpeed, airTime, landDip;   // falls: a leaping pose in the air, knees giving on landing
    private float crawlPhase;

    /// <summary>Falling or jumping for a moment (not a step down a kerb).</summary>
    private bool Airborne { get { return airTime > 0.18f; } }
    private double lastRunPhase = -1;
    private WeaponController heldWeapon;
    private WeaponData anchorsFor;
    private GunAnchors anchors;
    private static readonly string[] StateNames = { "Idle", "Run", "Shoot", "Death", "RecieveHit" };

    public bool HasModel { get { return model != null; } }

    public static CharacterRig Build(GameObject owner, Color shirt, Color pants, Color skin, Color helmet, Color pack)
    {
        return Build(owner, shirt, pants, skin, helmet, pack, null);
    }

    public static CharacterRig Build(GameObject owner, Color shirt, Color pants, Color skin, Color helmet, Color pack, string modelSkin)
    {
        var rig = owner.AddComponent<CharacterRig>();
        rig.Create(shirt, pants, skin, helmet, pack, modelSkin);
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

    private void Create(Color shirt, Color pants, Color skin, Color helmet, Color pack, string modelSkin)
    {
        root = Pivot(transform, "Rig", Vector3.zero);
        if (string.IsNullOrEmpty(modelSkin) || !TryCreateModel(modelSkin, skin))
            CreateBlocky(shirt, pants, skin, helmet, pack);
        CreateParachute(helmet);
        lastPos = transform.position;
    }

    private void CreateBlocky(Color shirt, Color pants, Color skin, Color helmet, Color pack)
    {
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
    }

    private void CreateParachute(Color helmet)
    {
        // Parachute (hidden until used)
        canopy = new GameObject("Parachute");
        canopy.transform.SetParent(transform, false);
        var cloth = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(cloth.GetComponent<Collider>());
        cloth.transform.SetParent(canopy.transform, false);
        cloth.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        cloth.transform.localScale = new Vector3(3.4f, 0.9f, 2.6f);
        canopyCloth = cloth.GetComponent<Renderer>();
        canopyDefault = MaterialCache.Lit(helmet * 0.6f + new Color(0.4f, 0.35f, 0.3f));
        canopyCloth.sharedMaterial = canopyDefault;
        canopyCamoShown = "";
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
    }

    private void ApplyCanopyCamo()
    {
        canopyCamoShown = parachuteCamo ?? "";
        if (canopyCloth == null)
            return;
        var mat = WeaponDressing.CamoMaterial(Cosmetics.FindParachuteCamo(canopyCamoShown), 1.73f, 1.6f);
        canopyCloth.sharedMaterial = mat != null ? mat : canopyDefault;
    }

    /// <summary>Puts an animal mask (Gear "k_…", Resources/Models/Masks) on the head; null or "" takes it off.</summary>
    public void SetMask(string id)
    {
        id = id ?? "";
        if (id == maskShown)
            return;
        maskShown = id;
        if (mask != null)
            Destroy(mask);
        mask = null;
        if (id.Length == 0 || maskMount == null || NetGame.IsServer)
            return;
        mask = ModelLibrary.Spawn("Models/Masks/" + id, maskMount);   // keeps the FBX's own axis rotation
        if (mask == null)
            return;
        ModelLibrary.ShareMaterials(mask, true);
        if (root != null)
            ModelLibrary.SetLayer(mask, root.gameObject.layer);
    }

    public string MaskShown { get { return maskShown; } }

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
        deathStarted = false;
        hitTimer = 0f;
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        if (states != null)
        {
            for (int i = 0; i < states.Length; i++)
            {
                weights[i] = i == Idle ? 1f : 0f;
                if (states[i].IsValid())
                    states[i].SetTime(0);
            }
        }
        lastPos = transform.position;
        velocity = Vector3.zero;
        speed = 0f;
        crouchK = hipYaw = aimK = 0f;
        lastRunPhase = -1;
        posedValid = false;
        SetVisible(true);
    }

    private void LateUpdate()
    {
        if (root == null || !root.gameObject.activeSelf)
            return;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 delta = transform.position - lastPos;
        float dy = delta.y;
        delta.y = 0f;
        lastPos = transform.position;
        if (delta.sqrMagnitude > 25f || Mathf.Abs(dy) > 5f)
        {
            delta = Vector3.zero;   // teleported (respawn, vehicle exit): not a step
            dy = 0f;
        }
        velocity = Vector3.Lerp(velocity, delta / dt, Mathf.Min(1f, dt * 10f));
        speed = velocity.magnitude;
        verticalSpeed = Mathf.Lerp(verticalSpeed, dy / dt, Mathf.Min(1f, dt * 12f));
        // In the air on foot (jumped, or stepped off a roof): count the time; landing bends the knees.
        bool inAir = !grounded && pose == RigPose.Normal && (verticalSpeed < -1.5f || verticalSpeed > 1.5f);
        if (inAir)
            airTime += dt;
        else
        {
            if (airTime > 0.3f && grounded)
                landDip = Mathf.Clamp01(airTime * 1.1f);
            airTime = 0f;
        }
        landDip = Mathf.MoveTowards(landDip, 0f, dt * 2.6f);

        canopy.SetActive(pose == RigPose.Parachute);
        if (pose == RigPose.Parachute && canopyCamoShown != (parachuteCamo ?? ""))
            ApplyCanopyCamo();

        if (model != null)
        {
            AnimateModel(dt);
            return;
        }

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
            case RigPose.Swim:
                {
                    float stroke = Mathf.Sin(Time.time * 4f) * 60f;
                    SetLimbs(-150f + stroke, -150f - stroke, 15f, -15f, 10f, 0f);
                    root.localPosition = new Vector3(0f, 0.15f, 0f);
                    root.localRotation = Quaternion.Euler(75f, 0f, 0f);
                    head.localRotation = Quaternion.Euler(-60f, 0f, 0f);
                    return;
                }
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
        float before = phase;
        phase += speed * dt * 2.4f;
        if (footstep != null && grounded && speed > 0.5f && Mathf.Floor(before / Mathf.PI) != Mathf.Floor(phase / Mathf.PI))
            footstep(Mathf.FloorToInt(phase / Mathf.PI) % 2 == 0);
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

    // ----- Animated model -----

    private static readonly Dictionary<Mesh, Mesh> bootMeshes = new Dictionary<Mesh, Mesh>();

    /// <summary>
    /// The low-poly pack's feet are part of its "Skin" material, so with a real skin tone those characters looked
    /// barefoot. The skin triangles below the ankle move to a sub-mesh of their own with dark boot leather.
    /// </summary>
    private static void SplitBoots(GameObject model)
    {
        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var src = smr.sharedMesh;
            var mats = smr.sharedMaterials;
            if (src == null || mats.Length != src.subMeshCount)
                continue;
            int skin = -1;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].mainTexture == null && mats[i].name.StartsWith("Skin"))
                    skin = i;
            if (skin < 0)
                continue;
            Mesh split;
            if (!bootMeshes.TryGetValue(src, out split))
            {
                split = null;
                if (src.isReadable)
                {
                    var verts = src.vertices;
                    Vector3 up = smr.transform.InverseTransformDirection(Vector3.up).normalized;
                    float lo = float.MaxValue, hi = float.MinValue;
                    foreach (var v in verts)
                    {
                        float h = Vector3.Dot(v, up);
                        lo = Mathf.Min(lo, h);
                        hi = Mathf.Max(hi, h);
                    }
                    float ankle = lo + (hi - lo) * 0.09f;
                    var tris = src.GetTriangles(skin);
                    var rest = new List<int>(tris.Length);
                    var boots = new List<int>();
                    for (int t = 0; t + 2 < tris.Length; t += 3)
                    {
                        bool low = Vector3.Dot(verts[tris[t]], up) < ankle && Vector3.Dot(verts[tris[t + 1]], up) < ankle && Vector3.Dot(verts[tris[t + 2]], up) < ankle;
                        var into = low ? boots : rest;
                        into.Add(tris[t]);
                        into.Add(tris[t + 1]);
                        into.Add(tris[t + 2]);
                    }
                    if (boots.Count > 0 && rest.Count > 0)
                    {
                        split = Object.Instantiate(src);
                        split.name = src.name + "_boots";
                        int n = src.subMeshCount;
                        split.subMeshCount = n + 1;
                        split.SetTriangles(rest, skin);
                        split.SetTriangles(boots, n);
                    }
                }
                bootMeshes[src] = split;
            }
            if (split == null)
                continue;
            smr.sharedMesh = split;
            var withBoots = new Material[mats.Length + 1];
            mats.CopyTo(withBoots, 0);
            withBoots[mats.Length] = MaterialCache.Lit(new Color(0.11f, 0.095f, 0.085f));
            smr.sharedMaterials = withBoots;
        }
    }

    private bool TryCreateModel(string skin, Color skinTone)
    {
        string path = ModelLibrary.CharacterPath(skin);
        model = ModelLibrary.Spawn(path, root);
        if (model == null)
            return false;

        // The low-poly pack's characters come with a near-black "Skin" (head, neck, hands) and plain white "Face"
        // (the eyes). Give them a real, slightly deeper skin tone (a pale tone reads as white under the bright sun)
        // and dark eyes, so faces have features instead of a blank pale mask.
        Color tone = new Color(skinTone.r * 0.86f, skinTone.g * 0.79f, skinTone.b * 0.74f);
        SplitBoots(model);
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || mats[i].mainTexture != null)
                    continue;
                if (mats[i].name.StartsWith("Skin"))
                {
                    mats[i] = MaterialCache.Lit(tone);
                    changed = true;
                }
                else if (mats[i].name.StartsWith("Face") && mats[i].color.grayscale > 0.8f)
                {
                    mats[i] = MaterialCache.Lit(new Color(0.09f, 0.075f, 0.07f));
                    changed = true;
                }
            }
            if (changed)
                r.sharedMaterials = mats;
        }

        ModelLibrary.ShareMaterials(model, true);
        ModelLibrary.ApplyVariant(model, skin);

        // Normalise height to 1.8 m with the feet at the bottom of the CharacterController.
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        Bounds b = ModelLibrary.RenderBounds(model);
        float height = Mathf.Max(0.01f, b.size.y);
        float scale = 1.8f / height;
        model.transform.localScale = model.transform.localScale * scale;
        b = ModelLibrary.RenderBounds(model);
        float feetOffset = b.min.y - transform.position.y;
        model.transform.localPosition = new Vector3(0f, -0.9f - feetOffset, 0f);

        skins = model.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (var smr in skins)
            smr.updateWhenOffscreen = false;

        rightHand = ModelLibrary.FindDeep(model.transform, "Fist.R");
        if (rightHand == null)
            rightHand = FindMixamoHand(model.transform);   // Mixamo rigs: "mixamorig:RightHand" (maybe with a _NN suffix)
        FindBones();
        strideSpeed = ModelLibrary.RunStrideSpeed(path);

        AnimationClip[] clips = ModelLibrary.Clips(path);
        var found = new AnimationClip[StateNames.Length];
        foreach (var clip in clips)
        {
            if (clip == null || clip.name.StartsWith("__preview__"))
                continue;
            for (int i = 0; i < StateNames.Length; i++)
                if (found[i] == null && clip.name.Contains(StateNames[i]))
                    found[i] = clip;
        }
        if (found[Idle] == null)
            return true;    // static model: still better than blocks

        if (found[Idle].legacy)
        {
            legacy = model.GetComponent<Animation>();
            if (legacy == null)
                legacy = model.AddComponent<Animation>();
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null)
                    continue;
                legacy.AddClip(found[i], StateNames[i]);
                legacy[StateNames[i]].wrapMode = (i == Idle || i == Run || i == Shoot) ? WrapMode.Loop : WrapMode.ClampForever;
            }
            legacy.Play(StateNames[Idle]);
            legacyState = StateNames[Idle];
            return true;
        }

        animator = model.GetComponent<Animator>();
        if (animator == null)
            animator = model.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        graph = PlayableGraph.Create("Character_" + skin);
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        mixer = AnimationMixerPlayable.Create(graph, StateNames.Length);
        states = new AnimationClipPlayable[StateNames.Length];
        weights = new float[StateNames.Length];
        looping = new bool[StateNames.Length];
        for (int i = 0; i < StateNames.Length; i++)
        {
            AnimationClip clip = found[i] != null ? found[i] : found[Idle];
            states[i] = AnimationClipPlayable.Create(graph, clip);
            states[i].SetApplyFootIK(false);
            graph.Connect(states[i], 0, mixer, i);
            looping[i] = i == Idle || i == Run || i == Shoot;
            weights[i] = i == Idle ? 1f : 0f;
            mixer.SetInputWeight(i, weights[i]);
        }
        var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
        output.SetSourcePlayable(mixer);
        graph.Play();
        CalibrateGrip(found[Shoot], found[Idle]);
        return true;
    }

    // ----- Bones (Quaternius "CharacterArmature" and Mixamo names) -----

    private static string BoneName(string n)
    {
        int colon = n.LastIndexOf(':');
        if (colon >= 0)
            n = n.Substring(colon + 1);
        int us = n.LastIndexOf('_');
        if (us > 0 && us < n.Length - 1)
        {
            bool digits = true;
            for (int i = us + 1; i < n.Length; i++)
                digits &= char.IsDigit(n[i]);
            if (digits)
                n = n.Substring(0, us);
        }
        return n;
    }

    private static void Collect(Transform t, System.Collections.Generic.Dictionary<string, Transform> into)
    {
        string n = BoneName(t.name);
        if (!into.ContainsKey(n))
            into[n] = t;
        foreach (Transform c in t)
            Collect(c, into);
    }

    private static Transform Pick(System.Collections.Generic.Dictionary<string, Transform> b, params string[] names)
    {
        Transform t;
        foreach (var n in names)
            if (b.TryGetValue(n, out t))
                return t;
        return null;
    }

    private void FindBones()
    {
        var b = new System.Collections.Generic.Dictionary<string, Transform>();
        Collect(model.transform, b);
        hips = Pick(b, "Hips");
        spine = Pick(b, "Abdomen", "Spine");
        chest = Pick(b, "Torso", "Spine2", "Spine1");
        upLegL = Pick(b, "UpperLeg.L", "LeftUpLeg");
        shinL = Pick(b, "LowerLeg.L", "LeftLeg");
        footL = Pick(b, "Foot.L", "LeftFoot");
        upLegR = Pick(b, "UpperLeg.R", "RightUpLeg");
        shinR = Pick(b, "LowerLeg.R", "RightLeg");
        footR = Pick(b, "Foot.R", "RightFoot");
        upperArmL = Pick(b, "UpperArm.L", "LeftArm");
        foreL = Pick(b, "LowerArm.L", "LeftForeArm");
        handL = Pick(b, "Fist.L", "LeftHand");
        upperArmR = Pick(b, "UpperArm.R", "RightArm");
        foreR = Pick(b, "LowerArm.R", "RightForeArm");
        headBone = Pick(b, "Head");
        if (headBone != null && root != null)
        {
            // Still in the bind pose here (upright head): mount masks at the centre of the head, facing forward,
            // scaled to the head (bone to top of the model; hats make it a little bigger, which is fine).
            float top = ModelLibrary.RenderBounds(model).max.y;
            float headH = Mathf.Clamp(top - headBone.position.y, 0.16f, 0.42f);
            maskMount = new GameObject("MaskMount").transform;
            maskMount.SetPositionAndRotation(headBone.position + root.up * headH * 0.48f + root.forward * headH * 0.06f, root.rotation);
            maskMount.localScale = Vector3.one * (headH * 1.22f / 0.25f);   // the mask shell is 0.25 m tall
            maskMount.SetParent(headBone, true);
        }
        if (upLegL != null && shinL != null && footL != null)
        {
            thighLen = Vector3.Distance(upLegL.position, shinL.position);
            shinLen = Vector3.Distance(shinL.position, footL.position);
        }
        if (upperArmR != null && foreR != null && rightHand != null)
        {
            upperArmLen = Vector3.Distance(upperArmR.position, foreR.position);
            foreArmLen = Vector3.Distance(foreR.position, rightHand.position);
        }
        yawBone = CommonAncestor(upLegL, spine) ?? hips;
        feetFree = footL != null && shinL != null && !footL.IsChildOf(shinL);
        // collarbones: the upper arms' own parents (Shoulder.L, mixamorig:LeftShoulder), when they are not the chest
        clavL = upperArmL != null && upperArmL.parent != chest && upperArmL.parent != spine ? upperArmL.parent : null;
        clavR = upperArmR != null && upperArmR.parent != chest && upperArmR.parent != spine ? upperArmR.parent : null;
        neckBone = headBone != null && headBone.parent != chest && headBone.parent != spine ? headBone.parent : null;
        posedBones = new[] { yawBone, spine, chest, upLegL, shinL, footL, upLegR, shinR, footR, upperArmL, foreL, handL, upperArmR, foreR, rightHand, headBone,
                             clavL, clavR, neckBone };
        baseRot = new Quaternion[posedBones.Length];
        posedRot = new Quaternion[posedBones.Length];
        basePos = new Vector3[posedBones.Length];
        posedPos = new Vector3[posedBones.Length];
    }

    private Transform CommonAncestor(Transform a, Transform b)
    {
        if (a == null || b == null)
            return null;
        for (var t = a.parent; t != null && t != transform; t = t.parent)
            if (b.IsChildOf(t))
                return t;
        return null;
    }

    /// <summary>The animator normally rewrites every bone each frame. When it didn't (paused, culled), last frame's pose
    /// would be posed again on top of itself: put the clean animated pose back first.</summary>
    private void BeginPose()
    {
        for (int i = 0; i < posedBones.Length; i++)
        {
            var t = posedBones[i];
            if (t == null)
                continue;
            if (posedValid && t.localRotation == posedRot[i] && t.localPosition == posedPos[i])
            {
                t.localRotation = baseRot[i];
                t.localPosition = basePos[i];
            }
            baseRot[i] = t.localRotation;
            basePos[i] = t.localPosition;
        }
    }

    private void EndPose()
    {
        for (int i = 0; i < posedBones.Length; i++)
        {
            var t = posedBones[i];
            if (t == null)
                continue;
            posedRot[i] = t.localRotation;
            posedPos[i] = t.localPosition;
        }
        posedValid = true;
    }

    private bool CanPose
    {
        get
        {
            return hips != null && yawBone != null && spine != null && chest != null && upperArmR != null && foreR != null && rightHand != null &&
                   upperArmL != null && foreL != null && handL != null && upperArmLen > 0.01f && foreArmLen > 0.01f;
        }
    }

    /// <summary>How this rig's right hand holds a gun: its rotation (relative to the body) in the one-handed aim clip,
    /// where the arm points a pistol straight ahead. Later the hand gets this rotation relative to the gun.</summary>
    private void CalibrateGrip(AnimationClip shoot, AnimationClip idle)
    {
        if (shoot == null || rightHand == null || NetGame.IsServer || states == null)
            return;
        Quaternion bind = rightHand.localRotation;
        var culling = animator.cullingMode;
        try
        {
            // Evaluate the graph once with only the aim clip, read the hand, then put the idle pose back.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            for (int i = 0; i < states.Length; i++)
                mixer.SetInputWeight(i, i == Shoot ? 1f : 0f);
            states[Shoot].SetTime(shoot.length * 0.3f);
            graph.Evaluate(0f);
            if (Quaternion.Angle(rightHand.localRotation, bind) < 0.01f)
                shoot.SampleAnimation(model, shoot.length * 0.3f);   // the graph did not write: sample the clip directly
            gripRelKnown = Quaternion.Angle(rightHand.localRotation, bind) >= 0.01f;
            gripRel = Quaternion.Inverse(transform.rotation) * rightHand.rotation;
        }
        catch (System.Exception)
        {
            gripRelKnown = false;
        }
        finally
        {
            for (int i = 0; i < states.Length; i++)
                mixer.SetInputWeight(i, weights[i]);
            states[Shoot].SetTime(0);
            animator.cullingMode = culling;
        }
    }

    private static Transform FindMixamoHand(Transform t)
    {
        string n = t.name;
        int colon = n.LastIndexOf(':');
        if (colon >= 0)
            n = n.Substring(colon + 1);
        if (n == "RightHand" || (n.StartsWith("RightHand_") && char.IsDigit(n[n.Length - 1])))
            return t;
        foreach (Transform c in t)
        {
            var f = FindMixamoHand(c);
            if (f != null)
                return f;
        }
        return null;
    }

    /// <summary>Short flinch when taking damage.</summary>
    public void PlayHit()
    {
        if (model == null || pose == RigPose.Dead)
            return;
        hitTimer = 0.3f;
        if (states != null && states[Hit].IsValid())
            states[Hit].SetTime(0);
    }

    private void AnimateModel(float dt)
    {
        // Body orientation for the special poses (no dedicated clips for these).
        Quaternion bodyRot = Quaternion.identity;
        Vector3 bodyPos = Vector3.zero;
        bool normal = pose == RigPose.Normal;
        bool posed = normal && CanPose && !NetGame.IsServer;   // arms on the gun by IK: the run/idle clips drive the rest
        int target;
        switch (pose)
        {
            case RigPose.Dead:
                target = Death;
                break;
            case RigPose.Freefall:
                target = Idle;
                bodyRot = Quaternion.Euler(70f, 0f, 0f);
                break;
            case RigPose.Swim:
                target = speed > 0.6f ? Run : Idle;   // running arms read as strokes when lying flat
                bodyRot = Quaternion.Euler(75f, 0f, 0f);
                bodyPos = new Vector3(0f, 0.15f, 0f);
                break;
            case RigPose.Parachute:
                target = Idle;
                break;
            case RigPose.Driving:
                target = Idle;
                bodyPos = new Vector3(0f, -0.45f, 0f);
                break;
            case RigPose.Prone:
            {
                // flat on the stomach, head forward; crawling rocks the body side to side and pushes it along
                // (a stride clip would swing the legs into the ground when lying flat)
                target = Idle;
                crawlPhase += dt * Mathf.Min(speed, 1.6f) * 4.5f;
                float k = Mathf.Clamp01(speed / 0.6f);
                bodyRot = Quaternion.Euler(RigTune.Current.pronePitch, 0f, Mathf.Sin(crawlPhase) * 6f * k);
                bodyPos = new Vector3(Mathf.Sin(crawlPhase) * 0.04f * k, RigTune.Current.proneDrop, -0.05f + Mathf.Abs(Mathf.Cos(crawlPhase)) * 0.06f * k);
                break;
            }
            default:
                target = speed > 0.6f || Airborne ? Run : (aiming && !posed ? Shoot : Idle);
                if (Airborne)
                {
                    // in the air: a held mid-stride leap, leaning into the fall
                    float lean = Mathf.Clamp(-verticalSpeed * 1.2f, -8f, 14f);
                    bodyRot = Quaternion.Euler(lean, 0f, 0f);
                }
                break;
        }

        // Crouching: knees bend and the body lowers by exactly what bent legs lose (feet stay on the ground).
        float crouchGoal = crouched && normal ? 1f : 0f;
        crouchGoal = Mathf.Max(crouchGoal, landDip * 0.85f);   // knees give when landing from a fall
        crouchK = Mathf.MoveTowards(crouchK, crouchGoal, dt * (landDip > crouchK ? 14f : 5f));
        float kneeAngle = 0f;
        if (normal && posed && thighLen > 0f)
        {
            float k = Mathf.SmoothStep(0f, 1f, crouchK);
            kneeAngle = RigTune.Current.kneeMax * k;
            bodyPos.y = -(thighLen + shinLen) * (1f - Mathf.Cos(kneeAngle * Mathf.Deg2Rad));
        }
        else if (normal && (crouched || landDip > 0.05f))
        {
            bodyPos.y = -0.25f * Mathf.Max(crouched ? 1f : 0f, landDip);
        }
        root.localRotation = Quaternion.Slerp(root.localRotation, bodyRot, dt * 6f);
        root.localPosition = Vector3.Lerp(root.localPosition, bodyPos, dt * (posed ? 20f : 10f));
        root.localScale = new Vector3(1f, crouched && normal && !posed ? 0.82f : 1f, 1f);

        // Walking: how far forward / sideways relative to where the body faces.
        float fwd = Vector3.Dot(velocity, transform.forward), side = Vector3.Dot(velocity, transform.right);
        bool backward = speed > 0.4f && fwd < -0.35f * speed;
        float runAmount = Mathf.Clamp01((speed - 0.3f) / 3.2f);   // half-strides (a walk) at low speed, full strides from 3.5 m/s

        if (legacy != null)
        {
            string wanted = StateNames[target];
            if (wanted != legacyState && legacy.GetClip(wanted) != null)
            {
                legacy.CrossFade(wanted, 0.15f);
                legacyState = wanted;
            }
        }
        else if (states != null)
        {
            if (target == Death && !deathStarted)
            {
                deathStarted = true;
                states[Death].SetTime(0);
            }

            hitTimer = Mathf.Max(0f, hitTimer - dt);
            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                float goal = i == target ? 1f : 0f;
                if (normal && target != Death && (i == Run || i == Idle) && target != Shoot)
                    goal = i == Run ? runAmount : 1f - runAmount;   // walk = run clip blended with idle, smaller steps
                if (normal && Airborne && (i == Run || i == Idle))
                    goal = i == Run ? 1f : 0f;   // full stride held in the air
                if (i == Hit)
                    goal = hitTimer > 0f && target != Death ? 0.7f : 0f;
                weights[i] = Mathf.MoveTowards(weights[i], goal, dt * (target == Death ? 6f : 9f));
                total += weights[i];
            }
            for (int i = 0; i < weights.Length; i++)
                mixer.SetInputWeight(i, total > 0.001f ? weights[i] / total : (i == Idle ? 1f : 0f));

            // Steps as long as the ground moves under the feet: shorter steps (walk blend) are played faster.
            float rate = speed / (strideSpeed * Mathf.Max(runAmount, 0.35f));
            rate = Mathf.Clamp(rate, 0.4f, 2f);
            if (pose == RigPose.Swim)
                rate = Mathf.Clamp(speed / 3f, 0.6f, 1.4f);
            if (normal && Airborne)
                rate = 0.12f;                                    // legs drift slowly while falling
            states[Run].SetSpeed(backward && normal ? -rate : rate);

            for (int i = 0; i < states.Length; i++)
            {
                if (!looping[i])
                    continue;
                float length = states[i].GetAnimationClip().length;
                double t = states[i].GetTime();
                if (length > 0.01f && (t > length || t < 0))
                {
                    t %= length;
                    if (t < 0)
                        t += length;
                    states[i].SetTime(t);
                }
            }
            StepEvents(normal);
        }

        bool prone = pose == RigPose.Prone;
        if ((!normal && !prone) || weaponHold == null || rightHand == null)
            return;
        if (!(posed || (prone && CanPose && !NetGame.IsServer)) || !Visible())
        {
            // Not posing (server, simple rigs, off screen): the gun simply follows the right hand.
            Quaternion aim = AimRotation();
            weaponHold.position = rightHand.position + aim * new Vector3(0f, 0.02f, 0.05f);
            weaponHold.rotation = aim;
            return;
        }
        PoseBody(dt, fwd, side, backward, kneeAngle, prone);
    }

    private bool Visible()
    {
        if (skins == null)
            return false;
        foreach (var r in skins)
            if (r != null && r.isVisible)
                return true;
        return false;
    }

    private Quaternion AimRotation()
    {
        if (showcase)
            return transform.rotation * Quaternion.Euler(RigTune.Current.showcaseAim);   // menus: gun at the ready across the body
        if (aimReference != null)
            return aimReference.rotation;
        return transform.rotation * Quaternion.Euler(Mathf.Clamp(aimPitch, -60f, 60f), 0f, 0f);
    }

    /// <summary>Footsteps when the run clip's feet touch down (also off screen: you hear people behind you).</summary>
    private void StepEvents(bool normal)
    {
        float length = states[Run].GetAnimationClip().length;
        if (length < 0.01f)
            return;
        double p = states[Run].GetTime() / length;
        p -= System.Math.Floor(p);
        double prev = lastRunPhase;
        lastRunPhase = p;
        if (prev < 0 || footstep == null || !normal || !grounded || speed < 0.5f || weights[Run] < 0.2f)
            return;
        bool right = Crossed(prev, p, ModelLibrary.RunStepRight), left = Crossed(prev, p, ModelLibrary.RunStepLeft);
        if (right)
            footstep(false);
        if (left)
            footstep(true);
    }

    /// <summary>Did the phase pass the mark between two frames (either direction, across the loop)?</summary>
    private static bool Crossed(double a, double b, double mark)
    {
        double d = b - a;
        if (d > 0.5) d -= 1.0;
        if (d < -0.5) d += 1.0;
        if (System.Math.Abs(d) < 1e-6)
            return false;
        double lo = d > 0 ? a : a + d, hi = d > 0 ? a + d : a;   // the swept interval (may leave [0,1))
        for (int k = -1; k <= 1; k++)
            if (mark + k > lo && mark + k <= hi)
                return true;
        return false;
    }

    /// <summary>
    /// After the clips: hips towards the walking direction with the chest still facing the aim, bent knees when
    /// crouching, the spine and head following the aim up and down, a slightly bladed stance with a long gun (left
    /// shoulder forward, cheek on the stock), the gun shouldered at the aim (or held lower at the ready), the
    /// collarbones reaching a little towards the hands, and both arms put on the gun by two-bone IK.
    /// Prone: up on the elbows, chest raised, head back to look ahead, legs a little apart, the gun out in front.
    /// </summary>
    private void PoseBody(float dt, float fwd, float side, bool backward, float kneeAngle, bool prone)
    {
        BeginPose();
        var T = RigTune.Current;
        Vector3 up = Vector3.up;
        Vector3 bodyRight = transform.right, bodyFwd = transform.forward;
        // Hips turn towards where the legs run (up to 65 degrees; walking backwards: away from it).
        float want = 0f;
        if (speed > 0.6f && !prone)
        {
            float a = Mathf.Atan2(side, fwd) * Mathf.Rad2Deg;
            if (backward)
                a = Mathf.DeltaAngle(180f, a);
            want = Mathf.Clamp(a, -65f, 65f);
        }
        hipYaw = Mathf.MoveTowardsAngle(hipYaw, want, dt * 300f);
        if (!prone)
        {
            Quaternion yaw = Quaternion.AngleAxis(hipYaw, up);
            yawBone.rotation = yaw * yawBone.rotation;
            if (feetFree && Mathf.Abs(hipYaw) > 0.01f)
            {
                Vector3 pivot = yawBone.position;
                TurnAbout(footL, pivot, yaw);
                TurnAbout(footR, pivot, yaw);
            }
            spine.rotation = Quaternion.AngleAxis(-hipYaw * T.hipSpine, up) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(-hipYaw * T.hipChest, up) * chest.rotation;

            // Crouch: the body went down; each leg reaches back to where its foot was (knees bend forward), feet stay put.
            if (kneeAngle > 0.5f && upLegL != null && shinL != null && footL != null && upLegR != null && shinR != null && footR != null)
            {
                float drop = Mathf.Max(0f, -root.localPosition.y);
                Vector3 knees = yaw * bodyFwd;
                CrouchLeg(upLegL, shinL, footL, drop, knees);
                CrouchLeg(upLegR, shinR, footR, drop, knees);
                spine.rotation = Quaternion.AngleAxis(kneeAngle * T.crouchLean, bodyRight) * spine.rotation;
            }
        }
        else
        {
            // On the stomach (the whole body is tipped forward): up on the elbows, legs a little apart.
            spine.rotation = Quaternion.AngleAxis(-T.proneSpineLift, bodyRight) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(-T.proneChestLift, bodyRight) * chest.rotation;
            if (upLegL != null && upLegR != null && T.proneLegSpread > 0f)
            {
                Quaternion l = Quaternion.AngleAxis(T.proneLegSpread, up), r = Quaternion.AngleAxis(-T.proneLegSpread, up);
                Vector3 hl = upLegL.position, hr = upLegR.position;
                upLegL.rotation = l * upLegL.rotation;
                upLegR.rotation = r * upLegR.rotation;
                if (feetFree && footL != null && footR != null)
                {
                    TurnAbout(footL, hl, l);
                    TurnAbout(footR, hr, r);
                }
            }
        }

        // The chest follows the aim up and down (lying down: only a little), the head a little more.
        Quaternion aim = AimRotation();
        Vector3 aimFwd = aim * Vector3.forward;
        float pitch = -Mathf.Asin(Mathf.Clamp(aimFwd.y, -1f, 1f)) * Mathf.Rad2Deg;   // + = looking down
        float follow = prone ? T.proneAimFollow : 1f;
        spine.rotation = Quaternion.AngleAxis(pitch * follow * (pitch > 0f ? T.spineDown : T.spineUp), bodyRight) * spine.rotation;
        chest.rotation = Quaternion.AngleAxis(pitch * follow * (pitch > 0f ? T.chestDown : T.chestUp), bodyRight) * chest.rotation;

        bool hasGun = weaponHold.gameObject.activeInHierarchy;
        if (hasGun && (heldWeapon == null || heldWeapon.transform != weaponHold))
            heldWeapon = weaponHold.GetComponent<WeaponController>();
        WeaponData data = hasGun && heldWeapon != null ? heldWeapon.weaponData : null;
        bool pistol = data != null && data.weaponType == WeaponType.Pistol;
        aimK = Mathf.MoveTowards(aimK, aiming && !showcase && hasGun ? 1f : 0f, dt * (aiming ? 9f : 3f));
        float k = Mathf.SmoothStep(0f, 1f, aimK);

        // Bladed stance with a long gun: the chest turns right (left shoulder forward), the spine taking part of it.
        float twist = !hasGun || pistol || prone ? 0f : Mathf.Lerp(T.readyTwist, T.aimTwist, k);
        if (Mathf.Abs(twist) > 0.01f)
        {
            spine.rotation = Quaternion.AngleAxis(twist * T.twistSpine, up) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(twist * (1f - T.twistSpine), up) * chest.rotation;
        }

        // Head: back towards the aim after the twist, the rest of the aim pitch, the cheek down on the stock when aiming
        // a long gun. Lying down: lifted back so the eyes look ahead instead of into the ground.
        if (headBone != null)
        {
            float lift = prone ? -T.proneHeadLift : 0f;
            float hp = prone ? 0f : pitch * (pitch > 0f ? T.headDown : T.headUp);
            float cheek = hasGun && !pistol && !prone ? k : 0f;
            Quaternion q = Quaternion.AngleAxis(-T.cheekTilt * cheek, aimFwd) *
                           Quaternion.AngleAxis(hp + T.cheekPitch * cheek, bodyRight) *
                           Quaternion.AngleAxis(-twist * T.headFollow, up);
            if (neckBone != null && Mathf.Abs(lift) > 0.01f)
            {
                neckBone.rotation = Quaternion.AngleAxis(lift * 0.45f, bodyRight) * neckBone.rotation;
                lift *= 0.55f;
            }
            headBone.rotation = Quaternion.AngleAxis(lift, bodyRight) * q * headBone.rotation;
        }

        if (!hasGun)
        {
            EndPose();
            return;   // no gun in hand (knocked down, item in use): the arms keep the clip's pose
        }

        // The gun: shouldered on the aim when aiming or shooting, otherwise at the ready (muzzle lower, a little inwards).
        if (data != anchorsFor)
        {
            anchorsFor = data;
            anchors = GunAnchors.For(data);
        }
        GunAnchors A = anchors;
        float gs = heldWeapon != null && heldWeapon.ModelScale > 0f ? heldWeapon.ModelScale : 1f;
        Vector3 grip = A != null ? A.Grip * gs : Vector3.zero;
        Vector3 support = A != null ? A.Support * gs : new Vector3(0f, 0.04f, pistol ? 0f : 0.3f);
        Vector3 butt = A != null ? A.Butt * gs : new Vector3(0f, 0.03f, -0.2f);
        if (A != null && data.attachments != null)
            foreach (var id in data.attachments)
                if (id == "vgrip" || id == "tgrip" || id == "hgrip" || id == "agrip")
                    support = new Vector3(A.cx, A.underY - 0.04f, A.underZ) * gs;   // hold the foregrip

        Quaternion gunRot;
        Vector3 rightTarget, leftTarget;
        HoldGun(aim, k, pistol, prone, grip, support, butt, out gunRot, out rightTarget, out leftTarget);
        // The collarbones reach a little towards the hands (shoulders come forward and up with the arms), then the gun
        // is placed again against the shoulder that moved.
        bool moved = false;
        if (clavR != null && T.clavRight > 0f)
            moved |= ReachClavicle(clavR, upperArmR, rightTarget, T.clavRight, T.clavMax);
        if (clavL != null && T.clavLeft > 0f)
            moved |= ReachClavicle(clavL, upperArmL, leftTarget, T.clavLeft, T.clavMax);
        if (moved)
            HoldGun(aim, k, pistol, prone, grip, support, butt, out gunRot, out rightTarget, out leftTarget);

        // Hands on the gun: right on the grip (wrist behind and below it, as the one-handed clip held it), left on the
        // handguard or foregrip (pistol: wrapped round the right hand). Elbows down and out (lying down: on the ground).
        Vector3 shoulderR = upperArmR.position, shoulderL = upperArmL.position;
        // Short arms (the cartoon characters): the hand goes further back along the handguard, where it reaches.
        float reach = (upperArmLen + foreArmLen) * T.reach, over = Vector3.Distance(shoulderL, leftTarget) - reach;
        if (!pistol && over > 0f)
            leftTarget -= (gunRot * Vector3.forward) * Mathf.Min(over * 1.3f, T.maxSlide);
        Vector3 rp = prone ? T.pronePoleR : T.rightPole, lp = prone ? T.pronePoleL : T.leftPole;
        SolveLimb(upperArmR, foreR, rightHand.position, rightTarget, shoulderR + bodyRight * rp.x + up * rp.y + bodyFwd * rp.z, upperArmLen, foreArmLen);
        SolveLimb(upperArmL, foreL, handL.position, leftTarget, shoulderL + bodyRight * lp.x + up * lp.y + bodyFwd * lp.z, upperArmLen, foreArmLen);
        if (gripRelKnown)
            rightHand.rotation = gunRot * gripRel;
        EndPose();
    }

    /// <summary>Places the gun for the current shoulders and returns where the hands go.</summary>
    private void HoldGun(Quaternion aim, float k, bool pistol, bool prone, Vector3 grip, Vector3 support, Vector3 butt,
                         out Quaternion gunRot, out Vector3 rightTarget, out Vector3 leftTarget)
    {
        var T = RigTune.Current;
        Vector3 up = Vector3.up;
        float readyPitch = prone ? T.proneReadyPitch : pistol ? T.pistolReadyPitch : T.readyPitch;
        float readyYaw = pistol ? T.pistolReadyYaw : T.readyYaw;
        Quaternion ready = Quaternion.AngleAxis(readyPitch, aim * Vector3.right) * Quaternion.AngleAxis(readyYaw, up) * aim;
        gunRot = Quaternion.Slerp(ready, aim, k);
        Vector3 shoulderR = upperArmR.position, shoulderL = upperArmL.position;
        Vector3 gunPos;
        if (pistol)
        {
            // Both arms forward, the pistol in front of the chin; at the ready lower and closer.
            Vector3 mid = (shoulderR + shoulderL) * 0.5f;
            Vector3 g = Vector3.Lerp(mid + aim * T.pistolReadyGrip, mid + aim * T.pistolAimGrip, k);
            gunPos = g - gunRot * grip;
        }
        else
        {
            // Butt in the shoulder pocket (a little inside and below the shoulder joint).
            Vector3 pocket = shoulderR + aim * T.pocket - up * T.readyDrop * (1f - k);
            gunPos = pocket - gunRot * butt;
        }
        weaponHold.position = gunPos;
        weaponHold.rotation = gunRot;
        rightTarget = weaponHold.TransformPoint(grip) - gunRot * T.rightHandOffset;
        leftTarget = pistol ? weaponHold.TransformPoint(grip) + gunRot * T.pistolLeftOffset
                            : weaponHold.TransformPoint(support) - gunRot * T.leftHandOffset;
    }

    /// <summary>Turns a collarbone part of the way from where it points towards the hand's target (capped).</summary>
    private static bool ReachClavicle(Transform clav, Transform upper, Vector3 target, float follow, float maxDeg)
    {
        Vector3 from = upper.position - clav.position, to = target - clav.position;
        if (from.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f)
            return false;
        float angle;
        Vector3 axis;
        Quaternion.FromToRotation(from, to).ToAngleAxis(out angle, out axis);
        if (angle > 180f)
            angle -= 360f;
        float a = Mathf.Clamp(angle * follow, -maxDeg, maxDeg);
        if (Mathf.Abs(a) < 0.01f || axis.sqrMagnitude < 0.5f)
            return false;
        clav.rotation = Quaternion.AngleAxis(a, axis) * clav.rotation;
        return true;
    }

    private static void TurnAbout(Transform t, Vector3 pivot, Quaternion q)
    {
        t.position = pivot + q * (t.position - pivot);
        t.rotation = q * t.rotation;
    }

    /// <summary>A crouching leg: the ankle back where it was before the body went down, the foot keeping its angle.</summary>
    private void CrouchLeg(Transform thigh, Transform shin, Transform foot, float drop, Vector3 kneeDir)
    {
        Vector3 ankle = foot.position;
        Vector3 target = ankle + Vector3.up * drop;
        Quaternion footRot = foot.rotation;
        SolveLimb(thigh, shin, ankle, target, thigh.position + kneeDir - Vector3.up * 0.2f, thighLen, shinLen);
        if (feetFree)
            foot.position = target;
        foot.rotation = footRot;
    }

    /// <summary>Two-bone IK: turns the upper and lower bone so the end (now at <paramref name="end"/>, carried by the lower
    /// bone) reaches the target, bending towards the pole (elbows down and out, knees forward).</summary>
    private void SolveLimb(Transform upper, Transform lower, Vector3 end, Vector3 target, Vector3 pole, float la, float lb)
    {
        Vector3 endLocal = Quaternion.Inverse(lower.rotation) * (end - lower.position);
        Vector3 a = upper.position;
        Vector3 toTarget = target - a;
        float d = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(la - lb) + 0.01f, (la + lb) * 0.999f);
        Vector3 dir = toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized : transform.forward;
        float x = (la * la - lb * lb + d * d) / (2f * d);
        float h = Mathf.Sqrt(Mathf.Max(0f, la * la - x * x));
        Vector3 bend = Vector3.ProjectOnPlane(pole - a, dir);
        if (bend.sqrMagnitude < 1e-6f)
            bend = Vector3.ProjectOnPlane(Vector3.down, dir);
        Vector3 elbow = a + dir * x + bend.normalized * h;
        Vector3 b = lower.position;
        upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * upper.rotation;
        b = lower.position;
        Vector3 c = b + lower.rotation * endLocal;
        lower.rotation = Quaternion.FromToRotation(c - b, (a + dir * d) - b) * lower.rotation;
    }

    /// <summary>Removes this rig and everything it created (used when changing skins).</summary>
    public void Teardown()
    {
        enabled = false;
        states = null;
        model = null;
        if (graph.IsValid())
            graph.Destroy();
        if (root != null)
            Destroy(root.gameObject);
        if (canopy != null)
            Destroy(canopy);
        Destroy(this);
    }

    private void OnDestroy()
    {
        if (graph.IsValid())
            graph.Destroy();
    }
}
