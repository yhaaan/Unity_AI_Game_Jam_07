using UnityEditor;
using UnityEngine;

namespace ChimeraGroove
{
    /// <summary>ChimeraParts·ChimeraEnemies 폴더 안의 PNG를 자동으로 스프라이트(100 PPU, 가운데 피벗)로 가져옵니다.</summary>
    public class ChimeraSpriteImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/ChimeraParts/") && !assetPath.Contains("/ChimeraEnemies/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteAlignment = (int)SpriteAlignment.Center;
            s.spriteMeshType = SpriteMeshType.FullRect;
            ti.SetTextureSettings(s);
        }
    }
}
