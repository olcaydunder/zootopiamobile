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
    Swim
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

    // ----- Animated model -----

    private bool TryCreateModel(string skin, Color skinTone)
    {
        string path = ModelLibrary.CharacterPath(skin);
        model = ModelLibrary.Spawn(path, root);
        if (model == null)
            return false;

        // The pack's characters have near-black faces; give them a real skin tone.
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name.StartsWith("Skin"))
                {
                    mats[i] = MaterialCache.Lit(skinTone);
                    changed = true;
                }
            }
            if (changed)
                r.sharedMaterials = mats;
        }

        ModelLibrary.ShareMaterials(model, true);

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

        foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.updateWhenOffscreen = false;

        rightHand = ModelLibrary.FindDeep(model.transform, "Fist.R");
        if (rightHand == null)
            rightHand = FindMixamoHand(model.transform);   // Mixamo rigs: "mixamorig:RightHand" (maybe with a _NN suffix)

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
        return true;
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
            default:
                target = speed > 0.6f ? Run : (aiming ? Shoot : Idle);
                if (crouched)
                    bodyPos = new Vector3(0f, -0.25f, 0f);
                break;
        }
        root.localRotation = Quaternion.Slerp(root.localRotation, bodyRot, dt * 6f);
        root.localPosition = Vector3.Lerp(root.localPosition, bodyPos, dt * 10f);
        root.localScale = new Vector3(1f, crouched && pose == RigPose.Normal ? 0.82f : 1f, 1f);

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
                if (i == Hit)
                    goal = hitTimer > 0f && target != Death ? 0.7f : 0f;
                weights[i] = Mathf.MoveTowards(weights[i], goal, dt * (target == Death ? 6f : 9f));
                total += weights[i];
            }
            for (int i = 0; i < weights.Length; i++)
                mixer.SetInputWeight(i, total > 0.001f ? weights[i] / total : (i == Idle ? 1f : 0f));

            states[Run].SetSpeed(Mathf.Clamp(speed / 4.5f, 0.6f, 1.6f));

            for (int i = 0; i < states.Length; i++)
            {
                if (!looping[i])
                    continue;
                float length = states[i].GetAnimationClip().length;
                double t = states[i].GetTime();
                if (length > 0.01f && t > length)
                    states[i].SetTime(t % length);
            }
        }

        // Keep the gun in the right hand, pointing where the character aims.
        if (weaponHold != null && rightHand != null && pose == RigPose.Normal)
        {
            Quaternion aim = aimReference != null ? aimReference.rotation : transform.rotation;
            weaponHold.position = rightHand.position + aim * new Vector3(0f, 0.02f, 0.05f);
            weaponHold.rotation = aim;
        }
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
