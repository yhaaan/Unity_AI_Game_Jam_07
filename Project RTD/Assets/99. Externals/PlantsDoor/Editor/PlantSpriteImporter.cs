using UnityEditor;
using UnityEngine;

namespace ChimeraGroove.Plants
{
    /// <summary>Resources/Plants, Resources/LabDoor 안의 PNG를 스프라이트(100 PPU, 가운데 피벗, Full Rect)로 자동 설정합니다.</summary>
    public class PlantSpriteImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Plants/") && !assetPath.Contains("/Resources/LabDoor/") && !assetPath.Contains("/Resources/Walls/") && !assetPath.Contains("/Resources/Islands/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.npotScale = TextureImporterNPOTScale.None;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Center;
            s.spriteMeshType = SpriteMeshType.FullRect;   // bend meshes sample the whole texture rect
            ti.SetTextureSettings(s);
        }
    }
}
