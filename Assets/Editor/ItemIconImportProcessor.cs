#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Keep line icons crisp and retain alpha on every build target.</summary>
public sealed class ItemIconImportProcessor : AssetPostprocessor
{
    public override uint GetVersion()=>1;
    private void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/ItemIcons/"))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;
        importer.mipmapEnabled=false;
        importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.wrapMode=TextureWrapMode.Clamp;
    }
}
#endif
