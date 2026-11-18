using System.Collections.Generic;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // A slot lives in the original canvas space; nested joints use manifest-relative pivots.
    internal sealed class ChimeraPartRig
    {
        private struct Joint { public Transform t; public Vector3 rest; public string role; public bool left; }
        private readonly List<Joint> joints = new();
        private readonly GameObject root;
        public Transform Transform => root.transform;
        public static Vector3 Pixel(float x,float y) => new Vector3(x,-y,0) * .01f;
        public static Vector3 Canvas(float x,float y) => Pixel(x-256,y-256);

        public ChimeraPartRig(SpriteRenderer slot, ChimeraLayerLibrary.Part part, int order)
        {
            root = new GameObject("Animated " + part.id);
            root.transform.SetParent(slot.transform,false);
            var nodes = new Dictionary<string,Transform>();
            var pivots = new Dictionary<string,Vector3>();
            foreach (var l in part.layers)
            {
                var node = new GameObject(l.id).transform;
                node.SetParent(root.transform,false);
                var sr = node.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = l.sprite; sr.sharedMaterial = slot.sharedMaterial;
                sr.sortingLayerID = slot.sortingLayerID; sr.sortingOrder = order++;
                nodes.Add(l.id,node); pivots.Add(l.id,Canvas(l.pivot[0],l.pivot[1]));

            }
            foreach (var l in part.layers)
            {
                var node = nodes[l.id]; var pos = pivots[l.id];
                if (!string.IsNullOrEmpty(l.parent))
                {
                    node.SetParent(nodes[l.parent],false); pos -= pivots[l.parent];
                }
                node.localPosition = pos;
                joints.Add(new Joint {t=node,rest=pos,role=l.role,left=l.id.Contains("left")});
            }
        }

        public void Dispose() { root.SetActive(false); Object.Destroy(root); }

        public void Pose(float beat, bool run, bool air, float ascent, float surprise, ChimeraGroove.BodyPose flight = default)
        {
            float wave = Mathf.Sin(2*Mathf.PI*beat*(run ? 2 : .5f));
            float blinkTime = Time.time % 3.1f;
            float blink = blinkTime < .16f ? Mathf.Sin(Mathf.PI*blinkTime/.16f) : 0;
            foreach (var j in joints)
            {
                float angle;
                switch (j.role)
                {
                    case "lower_leg": angle = run ? 18 * Mathf.Max(0, j.left ? wave : -wave) : 0; break;
                    case "upper_leg": case "leg": angle = (j.left ? 1 : -1) * (air ? 20 : run ? 34*wave : 0); break;
                    case "tail": angle = air ? 26*ascent : run ? -10+14*wave : 8*wave; break;
                    case "wing":
                        bool flying = flight.wingSx > 0;
                        angle = flying ? flight.wing : air ? -38 : run ? -18+14*wave : 3*wave;
                        j.t.localScale = flying ? new Vector3(flight.wingSx,flight.wingSy,1) : Vector3.one;
                        j.t.localPosition = j.rest + Pixel(flight.wingX,flight.wingY);
                        break;
                    case "ear": angle = air ? (j.left ? -16 : 16)*ascent : run ? 12+8*wave : 2*wave; break;
                    case "eyelid": angle = 3*blink; break;
                    case "eye": case "sclera":
                        float eye = 1 + .45f * surprise;
                        j.t.localScale = new Vector3(eye,eye-.9f*blink*(1-Mathf.Clamp01(surprise)),1); continue;
                    default: continue;
                }
                j.t.localRotation = Quaternion.Euler(0,0,-angle);
            }
        }
    }
}
