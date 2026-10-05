using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared lit materials for everything built from code.
/// The scene is empty, so Unity would strip the Standard shader from builds unless
/// something references it: Resources/ZootopiaLit.mat does that (otherwise objects render pink).
/// Materials are cached per color so identical objects can batch together.
/// </summary>
public static class MaterialCache
{
    private static Material baseMaterial;
    private static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();

    private static Material Base
    {
        get
        {
            if (baseMaterial == null)
            {
                baseMaterial = Resources.Load<Material>("ZootopiaLit");
                if (baseMaterial == null)
                {
                    Shader shader = Shader.Find("Standard");
                    if (shader == null)
                        shader = Shader.Find("Legacy Shaders/Diffuse");
                    baseMaterial = new Material(shader);
                }
            }
            return baseMaterial;
        }
    }

    private static Material cutoutBase;

    /// <summary>Alpha-tested version of the lit material (hair cards, lashes): Resources/ZootopiaLitCutout.mat keeps
    /// the Standard shader's _ALPHATEST_ON variant in the build.</summary>
    public static Material CutoutBase
    {
        get
        {
            if (cutoutBase == null)
            {
                cutoutBase = Resources.Load<Material>("ZootopiaLitCutout");
                if (cutoutBase == null)
                {
                    cutoutBase = new Material(Base);
                    cutoutBase.SetFloat("_Mode", 1f);
                    cutoutBase.SetFloat("_Cutoff", 0.5f);
                    cutoutBase.EnableKeyword("_ALPHATEST_ON");
                    cutoutBase.SetOverrideTag("RenderType", "TransparentCutout");
                    cutoutBase.renderQueue = 2450;
                }
            }
            return cutoutBase;
        }
    }

    public static Material Lit(Color color)
    {
        Material mat;
        if (!cache.TryGetValue(color, out mat) || mat == null)
        {
            mat = new Material(Base);
            mat.color = color;
            mat.enableInstancing = true;
            cache[color] = mat;
        }
        return mat;
    }
}
