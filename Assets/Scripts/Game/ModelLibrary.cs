using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads the 3D models shipped in Resources/Models (Quaternius CC0 packs, see ASSETS.md).
/// Every caller falls back to the procedural shapes if a model is missing.
/// </summary>
public static class ModelLibrary
{
    public const string PlayerSkin = "SoldierMale";
    public static readonly string[] EnemySkins = { "WorkerMale", "WorkerFemale", "CowboyMale", "NinjaSand", "DoctorMaleYoung", "SoldierMale", "LadyButcher" };
    /// <summary>Characters the player can pick (all free).</summary>
    public static readonly string[] ShopSkins = { "SoldierMale", "LadyButcher", "WorkerMale", "WorkerFemale", "CowboyMale", "NinjaSand", "DoctorMaleYoung" };
    public static readonly string[] ShopNames = { "Asker", "Kasap Leydi", "İşçi", "İşçi (K)", "Kovboy", "Ninja", "Doktor" };
    public static readonly string[] ShopRoles = { "Dengeli piyade", "Yakın dövüş uzmanı", "Mühendis", "Mühendis", "Keskin nişancı", "Sızma uzmanı", "Sıhhiyeci" };
    public static readonly int[] ShopPrices = { 0, 0, 0, 0, 0, 0, 0 };

    // ----- Weapon model variants ("" = the standard model) -----

    public static string[] GunSkins(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Pistol: return new[] { "", "Flame" };
            case WeaponType.Rifle: return new[] { "", "AK19" };
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
        return "Models/Characters/" + skin;
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
        m = new Material(MaterialCache.Lit(Color.white));
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
