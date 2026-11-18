#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    internal static class ChimeraEnemyArt
    {
        [Serializable] private class Drone { public int[] canvas_px; public string[] layers; }
        [Serializable] private class Scientists { public Scientist[] characters; }
        [Serializable] private class Scientist { public string id; public int[] canvas_px; public ChimeraLayerLibrary.Layer[] layers; }
        internal static Sprite Import(AssetImportContext ctx, string root)
        {
            bool drone = Path.GetFileName(root) == "drone";
            int[] size; string[] paths;
            if (drone)
            {
                var spec = JsonUtility.FromJson<Drone>(File.ReadAllText(Path.Combine(root,"drone.json")));
                size = spec.canvas_px; paths = spec.layers;
            }
            else
            {
                var spec = JsonUtility.FromJson<Scientists>(File.ReadAllText(Path.Combine(root,"manifest.json"))).characters.Single(c => c.id == "ray");
                size = spec.canvas_px; paths = spec.layers.Select(l => l.path).ToArray();
            }
            int w = size[0], h = size[1]; var canvas = new Color32[w*h];
            var layer = new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                foreach (var path in paths)
                {
                    if (!layer.LoadImage(File.ReadAllBytes(Path.Combine(root,path))) || layer.width != w || layer.height != h) throw new InvalidDataException(path);
                    ChimeraComposite.Over(canvas,layer.GetPixels32());
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(layer); }
            int bottom = Array.FindIndex(canvas,p => p.a > 32)/w;
            return ChimeraComposite.Save(ctx,drone ? "Drone" : "Scientist",canvas,w,h,new Vector2(.5f,(float)bottom/h),drone ? 480f : 450f);
        }
    }
}
#endif
