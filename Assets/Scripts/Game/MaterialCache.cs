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
