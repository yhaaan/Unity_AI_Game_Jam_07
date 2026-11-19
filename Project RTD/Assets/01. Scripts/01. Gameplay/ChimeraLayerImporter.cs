#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Opt in only the player recipe; do not claim other YAML files.
    [ScriptedImporter(3, null, new[] { "yaml" })]
    public sealed class ChimeraLayerImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var line = File.ReadLines(ctx.assetPath).Single(l => l.StartsWith("source: ", StringComparison.Ordinal));
            var root = line.Substring(8).Trim();
            if (ctx.assetPath.EndsWith("Enemy.yaml", StringComparison.Ordinal))
            {
                ctx.SetMainObject(ChimeraEnemyArt.Import(ctx,root));
                return;
            }
            var lib = ScriptableObject.CreateInstance<ChimeraLayerLibrary>();
            lib.parts = JsonUtility.FromJson<ChimeraLayerLibrary.Manifest>(File.ReadAllText(Path.Combine(root, "manifest.json"))).parts;
            var playable = Enum.GetNames(typeof(PartAnimal));
            ChimeraLayerLibrary.Part[] Import(ChimeraLayerLibrary.Part[] parts)
            {
                var selected = parts.Where(p => playable.Contains(p.id, StringComparer.OrdinalIgnoreCase)).ToArray();
                if (selected.Length != playable.Length) throw new InvalidDataException("Missing playable parts");
                foreach (var part in selected)
                {
                var canvas = new Color32[512*512];
                foreach (var l in part.layers)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(File.ReadAllBytes(Path.Combine(root, l.path))) || tex.width != 512 || tex.height != 512)
                        throw new InvalidDataException(l.path);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    // Crop transparent margins offline. The normalized pivot retains its canvas registration.
                    var pixels = tex.GetPixels32(); ChimeraComposite.Over(canvas,pixels); int x0 = 512, y0 = 512, x1 = -1, y1 = -1;
                    for (int i = 0; i < pixels.Length; i++) if (pixels[i].a != 0)
                    {
                        int x = i % 512, y = i / 512;
                        x0 = Math.Min(x0,x); x1 = Math.Max(x1,x); y0 = Math.Min(y0,y); y1 = Math.Max(y1,y);
                    }
                    if (x1 < 0) throw new InvalidDataException("Empty layer: " + l.path);
                    // Include the joint even when it lies outside the visible pixels.
                    x0 = Math.Max(0, Math.Min(x0, l.pivot[0])-1); y0 = Math.Max(0, Math.Min(y0,512-l.pivot[1])-1);
                    x1 = Math.Min(511,Math.Max(x1,l.pivot[0])+1); y1 = Math.Min(511,Math.Max(y1,512-l.pivot[1])+1);
                    int w=x1-x0+1, h=y1-y0+1;
                    var tile = new Texture2D(w,h,TextureFormat.RGBA32,false) { wrapMode = TextureWrapMode.Clamp, name = l.path + " texture" };
                    var cropped = new Color32[w*h];
                    for (int y=0; y<h; y++) Array.Copy(pixels,(y+y0)*512+x0,cropped,y*w,w);
                    tile.SetPixels32(cropped); tile.Apply(false,true);
                    UnityEngine.Object.DestroyImmediate(tex);
                    l.sprite = Sprite.Create(tile,new Rect(0,0,w,h),new Vector2((l.pivot[0]-x0)/(float)w,(512-l.pivot[1]-y0)/(float)h),100,0,SpriteMeshType.FullRect);
                    l.sprite.name = l.path;
                    ctx.AddObjectToAsset("tex/"+l.path,tile); ctx.AddObjectToAsset("sprite/"+l.path,l.sprite);
                }
                    part.preview = ChimeraComposite.Save(ctx,"preview/"+part.layers[0].path,canvas,512,512,new Vector2(.5f,.5f),100);
                }
                return selected;
            }
            lib.parts.heads = Import(lib.parts.heads); lib.parts.bodies = Import(lib.parts.bodies); lib.parts.legs = Import(lib.parts.legs);
            ctx.AddObjectToAsset("PlayerLayers",lib); ctx.SetMainObject(lib);
        }
    }
}
#endif
