using UnityEditor;
using UnityEngine;

/// <summary>Import settings for the photo textures in Resources/Textures: normal maps as normal maps,
/// 1K max, mipmaps, trilinear, repeat, compressed (ETC2/ASTC on Android).</summary>
public class ZootopiaTextureImport : AssetPostprocessor
{
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
        if (!path.Contains("Resources/Textures/"))
            return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = assetPath.EndsWith("_nor.jpg") ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.maxTextureSize = 1024;
        ti.mipmapEnabled = true;
        ti.anisoLevel = 4;
        ti.filterMode = FilterMode.Trilinear;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.textureCompression = TextureImporterCompression.Compressed;
    }
}
