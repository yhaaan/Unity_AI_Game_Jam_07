using UnityEditor;
using UnityEngine;

namespace ChimeraGroove.Bubble
{
    /// <summary>Resources/ThoughtBubble 조각: 100 PPU, 가운데 피벗, Full Rect, Clamp (이어 붙여도 경계에 실선이 안 생기게).</summary>
    public class BubbleSpriteImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/ThoughtBubble/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Center;
            s.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);
        }
    }
}
