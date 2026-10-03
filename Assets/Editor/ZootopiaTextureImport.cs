using UnityEditor;
using UnityEngine;

/// <summary>Import settings for the photo textures in Resources/Textures: normal maps as normal maps,
/// 1K max, mipmaps, trilinear, repeat, compressed (ETC2/ASTC on Android).</summary>
public class ZootopiaTextureImport : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("Resources/Textures/"))
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
