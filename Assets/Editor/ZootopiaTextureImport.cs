using UnityEditor;
using UnityEngine;

/// <summary>Import settings for the photo textures in Resources/Textures: normal maps as normal maps,
/// 2K max, mipmaps, trilinear, repeat, compressed (ETC2/ASTC on Android). UI icons, the logo and the
/// map pictures get UI settings.</summary>
public class ZootopiaTextureImport : AssetPostprocessor
{
    /// <summary>Bump when these rules change so Unity re-imports the affected assets.</summary>
    public override uint GetVersion() { return 5; }

    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (path.Contains("Resources/UI/Icons/"))
        {
            // UI icons: crisp, transparent edges, no mipmaps, high-quality compression.
            var ui = (TextureImporter)assetImporter;
            ui.textureType = TextureImporterType.Default;
            ui.alphaIsTransparency = true;
            ui.mipmapEnabled = false;
            ui.npotScale = TextureImporterNPOTScale.None;
            ui.wrapMode = TextureWrapMode.Clamp;
            ui.filterMode = FilterMode.Bilinear;
            ui.maxTextureSize = 256;
            ui.textureCompression = TextureImporterCompression.CompressedHQ;
            return;
        }
        if (path.Contains("Resources/UI/") || (path.Contains("Resources/Map/") && path.EndsWith("preview.png")))
        {
            // Logo and map pictures: shown as UI, smooth alpha edges, no mipmaps, clamped.
            var pic = (TextureImporter)assetImporter;
            pic.textureType = TextureImporterType.Default;
            pic.alphaIsTransparency = true;
            pic.mipmapEnabled = false;
            pic.wrapMode = TextureWrapMode.Clamp;
            pic.filterMode = FilterMode.Bilinear;
            pic.maxTextureSize = 1024;
            pic.textureCompression = TextureImporterCompression.CompressedHQ;
            return;
        }
        if (!path.Contains("Resources/Textures/"))
            return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = assetPath.EndsWith("_nor.jpg") ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.maxTextureSize = 2048;   // 2K photo textures (the DÜŞÜK preset shows them at half size)
        ti.mipmapEnabled = true;
        ti.anisoLevel = 4;
        ti.filterMode = FilterMode.Trilinear;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.textureCompression = TextureImporterCompression.Compressed;
    }

    /// <summary>Vehicle models keep their Blender material names ("Paint" is swapped for camouflage).</summary>
    private void OnPreprocessModel()
    {
        if (!assetPath.Replace('\\', '/').Contains("Models/Vehicles/"))
            return;
        var mi = (ModelImporter)assetImporter;
        mi.materialName = ModelImporterMaterialName.BasedOnMaterialName;
    }
}
