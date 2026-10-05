using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads the 3D models shipped in Resources/Models (Quaternius CC0 packs, see ASSETS.md).
/// Every caller falls back to the procedural shapes if a model is missing.
/// </summary>
public static class ModelLibrary
{
    public const string PlayerSkin = "SoldierMale";
    public static readonly string[] EnemySkins = {
        "WorkerMale", "WorkerFemale", "CowboyMale", "NinjaSand", "DoctorMaleYoung", "SoldierMale", "LadyButcher",
        "Operator", "OperatorDesert", "Infantry", "InfantryWoodland", "Mercenary", "MercenaryUrban", "Masked",
        "SwatOperator", "SwatElite", "Asuna", "AsunaRed", "AsunaBlack" };
    /// <summary>Characters the player can pick: the basic ones are free, the others are bought in the store
    /// with Kredi (ShopPrices), won from gift boxes or received as gifts.</summary>
    public static readonly string[] ShopSkins = {
        "SoldierMale", "Operator", "OperatorDesert", "OperatorNight", "Infantry", "InfantryWoodland", "Mercenary", "MercenaryUrban", "Masked",
        "LadyButcher", "WorkerMale", "WorkerFemale", "CowboyMale", "NinjaSand", "DoctorMaleYoung",
        "SwatOperator", "SwatElite", "Asuna", "AsunaRed", "AsunaBlack", "AsunaGreen" };
    public static readonly string[] ShopNames = {
        "Asker", "Operatör", "Çöl Operatörü", "Gece Operatörü", "Piyade", "Orman Piyadesi", "Paralı Asker", "Kent Komandosu", "Maskeli",
        "Kasap Leydi", "İşçi", "İşçi (K)", "Kovboy", "Ninja", "Doktor",
        "SWAT", "Özel Tim", "Nova", "Nova Kızıl", "Nova Gece", "Nova Orman" };
    public static readonly string[] ShopRoles = {
        "Dengeli piyade", "Özel harekât", "Çöl harekâtı", "Gece baskını", "Hücum piyadesi", "Orman keşifçisi", "Ağır zırhlı", "Şehir çatışması", "Hızlı baskıncı",
        "Yakın dövüş uzmanı", "Mühendis", "Mühendis", "Keskin nişancı", "Sızma uzmanı", "Sıhhiyeci",
        "Baskın timi", "Gece görüşlü komando", "Siber ajan", "Siber ajan", "Siber ajan", "Siber ajan" };
    public static readonly int[] ShopPrices = { 0, 1500, 1800, 2200, 0, 1200, 2000, 2400, 2800, 3500, 0, 0, 0, 3000, 0,
        2600, 3200, 2400, 2700, 2900, 2200 };

    /// <summary>
    /// Colour variants: same model and animations as the base character, with the textures in
    /// Resources/Models/Characters/Variants/&lt;skin&gt;/&lt;texture name&gt; swapped in (see ApplyVariant).
    /// </summary>
    private static readonly Dictionary<string, string> VariantBase = new Dictionary<string, string>
    {
        { "OperatorDesert", "Operator" },
        { "OperatorNight", "Operator" },
        { "InfantryWoodland", "Infantry" },
        { "MercenaryUrban", "Mercenary" },
        { "AsunaRed", "Asuna" },
        { "AsunaBlack", "Asuna" },
        { "AsunaGreen", "Asuna" },
    };

    /// <summary>
    /// Ground speed (m/s) at which each character's run clip, played at normal speed, keeps the planted foot still
    /// (measured from the clips at 1.8 m height). The rig plays the clip at speed / this, so feet don't slide.
    /// </summary>
    public static float RunStrideSpeed(string characterPath)
    {
        string skin = characterPath != null ? characterPath.Substring(characterPath.LastIndexOf('/') + 1) : "";
        switch (skin)
        {
            case "CowboyMale": return 2.6f;
            case "DoctorMaleYoung": return 2.9f;
            case "NinjaSand": return 3.1f;
            case "SoldierMale": return 3.0f;
            case "WorkerFemale": return 2.9f;
            case "WorkerMale": return 2.9f;
            case "LadyButcher": return 3.4f;
            case "Masked": return 4.2f;
            case "Operator": return 4.4f;
            case "Mercenary": return 4.5f;
            case "Infantry": return 4.6f;
            case "SwatOperator": return 5.1f;
            case "SwatElite": return 4.9f;
            case "Asuna": return 4.5f;
            default: return 3.5f;
        }
    }

    /// <summary>Where the feet touch down in the run clips (fraction of the cycle): right, then left.</summary>
    public const float RunStepRight = 0.29f, RunStepLeft = 0.79f;

    // ----- Weapon model variants ("" = the standard model) -----

    public static string[] GunSkins(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Pistol: return new[] { "", "Flame", "Engraved" };
            case WeaponType.Rifle: return new[] { "", "AK19", "AR15" };
            case WeaponType.Sniper: return new[] { "", "Shadow" };
            default: return new[] { "" };
        }
    }

    public static string GunSkinName(WeaponType type, string skin)
    {
        switch (skin)
        {
            case "Flame": return "Alev Kartalı";
            case "AK19": return "AK-19 Taktik";
            case "AR15": return "AR-15 Saha";
            case "Engraved": return "Gravürlü 1911";
            case "Shadow": return "Gölge Avcı";
            default: return "Standart";
        }
    }

    public static string SelectedGunSkin(WeaponType type)
    {
        string s = PlayerPrefs.GetString("zm_gunskin_" + type, "");
        return System.Array.IndexOf(GunSkins(type), s) >= 0 ? s : "";
    }

    public static void SelectGunSkin(WeaponType type, string skin)
    {
        PlayerPrefs.SetString("zm_gunskin_" + type, skin ?? "");
        PlayerPrefs.Save();
    }

    public static readonly string[] CoverProps = { "SackTrench_Small", "SackTrench", "Barrier_Single", "Container_Small", "Container_Long", "ExplodingBarrel", "GasTank", "TrashContainer", "CardboardBoxes_4", "Debris_Tires", "WaterTank_Floor" };
    public static readonly string[] SmallProps = { "Crate", "Pallet", "CardboardBoxes_2", "TrafficCone" };

    private static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
    private static readonly Dictionary<string, AnimationClip[]> clips = new Dictionary<string, AnimationClip[]>();

    public static GameObject Prefab(string path)
    {
        GameObject p;
        if (!prefabs.TryGetValue(path, out p))
        {
            p = Resources.Load<GameObject>(path);
            prefabs[path] = p;
        }
        return p;
    }

    public static AnimationClip[] Clips(string path)
    {
        AnimationClip[] c;
        if (!clips.TryGetValue(path, out c))
        {
            c = Resources.LoadAll<AnimationClip>(path);
            clips[path] = c;
        }
        return c;
    }

    public static GameObject Spawn(string path, Transform parent)
    {
        var p = Prefab(path);
        if (p == null)
            return null;
        var go = Object.Instantiate(p, parent, false);
        go.name = p.name;
        return go;
    }

    public static string CharacterPath(string skin)
    {
        string baseSkin;
        if (skin != null && VariantBase.TryGetValue(skin, out baseSkin))
            skin = baseSkin;
        return "Models/Characters/" + skin;
    }

    private static readonly Dictionary<string, Material> variantMaterials = new Dictionary<string, Material>();

    /// <summary>
    /// For colour-variant skins: swaps each material's texture for the variant's texture of the same name
    /// (Resources/Models/Characters/Variants/&lt;skin&gt;/). Call after ShareMaterials. No-op for normal skins.
    /// </summary>
    public static void ApplyVariant(GameObject root, string skin)
    {
        if (skin == null || !VariantBase.ContainsKey(skin))
            return;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || mats[i].mainTexture == null)
                    continue;
                string key = skin + "/" + mats[i].mainTexture.name;
                Material m;
                if (!variantMaterials.TryGetValue(key, out m) || m == null)
                {
                    var tex = Resources.Load<Texture2D>("Models/Characters/Variants/" + key);
                    if (tex == null)
                        continue;
                    m = new Material(mats[i]);
                    m.name = mats[i].name + "_" + skin;
                    m.mainTexture = tex;
                    m.enableInstancing = true;
                    variantMaterials[key] = m;
                }
                mats[i] = m;
                changed = true;
            }
            if (changed)
                r.sharedMaterials = mats;
        }
    }

    public static string PropPath(string prop)
    {
        return "Models/Props/" + prop;
    }

    /// <summary>Gun model path for a variant, falling back to the standard model if it's missing.</summary>
    public static string GunPath(WeaponType type, string skin)
    {
        if (!string.IsNullOrEmpty(skin))
        {
            string p = GunPath(type) + "_" + skin;
            if (Prefab(p) != null)
                return p;
        }
        return GunPath(type);
    }

    public static string GunPath(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Pistol: return "Models/Guns/Pistol";
            case WeaponType.SMG: return "Models/Guns/SMG";
            case WeaponType.Shotgun: return "Models/Guns/Shotgun";
            case WeaponType.Sniper: return "Models/Guns/Sniper";
            default: return "Models/Guns/Rifle";
        }
    }

    /// <summary>Distance from the grip to the muzzle of each gun model (metres, +Z forward).</summary>
    public static float MuzzleDistance(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Pistol: return 0.28f;
            case WeaponType.SMG: return 0.49f;
            case WeaponType.Shotgun: return 0.86f;
            case WeaponType.Sniper: return 0.86f;
            default: return 0.63f;
        }
    }

    /// <summary>World-space bounds of all renderers under a root.</summary>
    public static Bounds RenderBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(root.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    /// <summary>
    /// Replaces each imported material with the shared per-colour material, so identical colours
    /// across all model instances batch/instance together (big draw-call saving on phones).
    /// </summary>
    public static void ShareMaterials(GameObject root, bool castShadows)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null)
                    continue;
                // Textured models (Sketchfab assets) keep their texture; plain-colour ones share by colour.
                mats[i] = mats[i].mainTexture != null ? Textured(mats[i]) : MaterialCache.Lit(mats[i].color);
            }
            r.sharedMaterials = mats;
            if (!castShadows)
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private static readonly Dictionary<Material, Material> textured = new Dictionary<Material, Material>();

    /// <summary>Same texture on the game's own Standard material (guaranteed to be in the build).</summary>
    private static Material Textured(Material source)
    {
        Material m;
        if (textured.TryGetValue(source, out m) && m != null)
            return m;
        // "..._cutout" textures (character atlases with hair cards) are drawn alpha-tested.
        bool cutout = source.mainTexture.name.EndsWith("_cutout");
        m = new Material(cutout ? MaterialCache.CutoutBase : MaterialCache.Lit(Color.white));
        m.name = source.name + "_zm";
        m.mainTexture = source.mainTexture;
        m.color = source.HasProperty("_Color") ? source.color : Color.white;
        m.enableInstancing = true;
        textured[source] = m;
        return m;
    }

    public static void SetLayer(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayer(child.gameObject, layer);
    }

    public static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;
        foreach (Transform child in root)
        {
            var found = FindDeep(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}
