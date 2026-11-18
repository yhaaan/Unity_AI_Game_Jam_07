#if UNITY_EDITOR
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    internal static class ChimeraComposite
    {
        // Source-over on straight-alpha PNG pixels, preserving transparent margins.
        internal static void Over(Color32[] canvas, Color32[] layer)
        {
            for (int i = 0; i < layer.Length; ++i)
            {
                Color32 src = layer[i], dst = canvas[i];
                if (src.a == 0) continue;
                if (src.a == 255 || dst.a == 0) { canvas[i] = src; continue; }
                int s = src.a * 255, d = dst.a * (255 - src.a), a = s + d;
                canvas[i] = new Color32((byte)((src.r*s+dst.r*d)/a), (byte)((src.g*s+dst.g*d)/a),
                    (byte)((src.b*s+dst.b*d)/a), (byte)((a+127)/255));
            }
        }

        internal static Sprite Save(AssetImportContext ctx, string key, Color32[] canvas, int w, int h, Vector2 pivot, float ppu)
        {
            var tex = new Texture2D(w,h,TextureFormat.RGBA32,false) {name = key + " texture", wrapMode = TextureWrapMode.Clamp};
            tex.SetPixels32(canvas); tex.Apply(false,true);
            var sprite = Sprite.Create(tex,new Rect(0,0,w,h),pivot,ppu,0,SpriteMeshType.FullRect);
            sprite.name = key;
            ctx.AddObjectToAsset(key+"/texture",tex); ctx.AddObjectToAsset(key,sprite);
            return sprite;
        }
    }
}
#endif
