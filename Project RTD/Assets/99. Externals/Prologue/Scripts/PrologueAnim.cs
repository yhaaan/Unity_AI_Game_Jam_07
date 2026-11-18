using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChimeraGroove.Prologue
{
    // ───────── JSON shapes (Resources/Prologue/<cut>/rig.json, anim.json) ─────────
    [Serializable] public class RigLayer { public string id, file, parent, role; public float[] pivot; public int[] rect; }
    [Serializable] public class Rig { public string cut; public int[] size; public RigLayer[] layers; }
    [Serializable] public class PivotOverride { public string id; public float x, y; }
    [Serializable]
    public class Track
    {
        public string[] t; public string ch, f, e;
        public float amp, per, ph, frm, ramp, dur, rep, speed, rate, seed;
        public float[] k;
    }
    [Serializable] public class Anim { public string cut; public float intro; public PivotOverride[] pivots; public Track[] tracks; }

    public struct LayerPose
    {
        public float x, y, r, sx, sy, a;
        public static readonly LayerPose Rest = new LayerPose { sx = 1, sy = 1, a = 1 };
    }

    /// <summary>Same maths as prologue-anim.js — keep the two in step.</summary>
    public static class PrologueMath
    {
        const float TAU = Mathf.PI * 2f;

        public static float Ease(string e, float u)
        {
            switch (e)
            {
                case "lin": return u;
                case "in": return u * u;
                case "out": return 1f - (1f - u) * (1f - u);
                case "back": { const float c1 = 1.9f, c3 = c1 + 1f; float v = u - 1f; return 1f + c3 * v * v * v + c1 * v * v; }
                default: return u * u * (3f - 2f * u);
            }
        }

        public static float Noise(float x) =>
            Mathf.Sin(x) * .6f + Mathf.Sin(x * 2.31f + 1.7f) * .3f + Mathf.Sin(x * 4.13f + .4f) * .1f;

        public static float KeysAt(float[] k, float u, string e)
        {
            int n = k.Length / 2;
            if (n == 0) return 0f;
            if (u <= k[0]) return k[1];
            for (int i = 0; i < n - 1; i++)
            {
                float t0 = k[2 * i], t1 = k[2 * i + 2];
                if (u <= t1)
                {
                    if (e == "step") return k[2 * i + 1];
                    float f = Ease(e, t1 > t0 ? (u - t0) / (t1 - t0) : 1f);
                    return k[2 * i + 1] + (k[2 * i + 3] - k[2 * i + 1]) * f;
                }
            }
            return k[2 * n - 1];
        }

        public static float Value(Track tr, float t)
        {
            float u = t - tr.frm;
            float env = tr.ramp > 0f ? Mathf.Clamp01(u / tr.ramp) : 1f;
            switch (tr.f)
            {
                case "wave": return u < 0f ? 0f : tr.amp * Mathf.Sin(TAU * (u / tr.per + tr.ph)) * env;
                case "hop": return u < 0f ? 0f : tr.amp * Mathf.Abs(Mathf.Sin(Mathf.PI * (u / tr.per + tr.ph))) * env;
                case "jit": return u < 0f ? 0f : tr.amp * Noise(u * tr.rate + tr.seed) * env;
                case "spin": return u < 0f ? 0f : tr.speed * u;
                case "keys": return KeysAt(tr.k, tr.rep > 0f && u > 0f ? u % tr.rep : u, tr.e);
                case "pop": return u <= 0f ? 0f : u >= tr.dur ? 1f : Ease("back", u / tr.dur);
                case "blink":
                {
                    if (u < 0f) return 1f;
                    float c = (u / tr.per + tr.ph) % 1f; if (c < 0f) c += 1f;
                    float p = c * tr.per; const float d = .14f;
                    return p < d ? 1f - .9f * Mathf.Sin(Mathf.PI * p / d) : 1f;
                }
            }
            return 0f;
        }

        /// <summary>Evaluates every track at time t. poses[i] = layer i, poses[poses.Length-1] = camera.</summary>
        public static void Evaluate(Track[] tracks, int[][] targets, float t, LayerPose[] poses)
        {
            for (int i = 0; i < poses.Length; i++) poses[i] = LayerPose.Rest;
            int cam = poses.Length - 1;
            for (int ti = 0; ti < tracks.Length; ti++)
            {
                var tr = tracks[ti];
                float v = Value(tr, t);
                bool additive = tr.ch == "x" || tr.ch == "y" || tr.ch == "r";
                bool relative = tr.f == "wave" || tr.f == "hop" || tr.f == "jit";
                float f = additive ? v : relative ? 1f + v : v;
                foreach (int raw in targets[ti])
                {
                    int i = raw < 0 ? cam : raw;
                    switch (tr.ch)
                    {
                        case "x": poses[i].x += f; break;
                        case "y": poses[i].y += f; break;
                        case "r": poses[i].r += f; break;
                        case "s": poses[i].sx *= f; poses[i].sy *= f; break;
                        case "sx": poses[i].sx *= f; break;
                        case "sy": poses[i].sy *= f; break;
                        case "a": poses[i].a *= f; break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// One prologue cut built from Resources/Prologue/&lt;cutName&gt;/ (rig.json + anim.json + parts/*.png).
    /// The cut is centred on this transform, 100 px = 1 unit (1920 px wide = 19.2 units).
    /// Time runs from 0 when the cut appears; everything after <see cref="Intro"/> loops forever.
    /// </summary>
    public class PrologueCut : MonoBehaviour
    {
        public const float PPU = 100f;
        public const float Overscan = 1.02f; // hides the canvas edge while the camera shakes

        public string cutName = "cut1-breakout";
        public string resourcesRoot = "Prologue";
        public int sortingOrderBase = 0;
        public bool playOnStart = true;
        [Range(0f, 1f)] public float fade = 1f;

        public float time;
        public bool playing;

        public Vector2 SizePx { get; private set; }
        public float Intro { get; private set; }
        public bool IntroDone => time >= Intro;
        public bool Built => layers != null;

        RigLayer[] layers;
        Track[] tracks;
        int[][] targets;
        int[] parent;
        Vector2[] piv;
        Transform cam;
        Transform[] nodes;
        SpriteRenderer[] renderers;
        Dictionary<string, int> layerIndices;
        LayerPose[] poses;
        float[] alpha;
        int[] order; // parents before children

        void Start()
        {
            if (!Built) Build();
            if (playOnStart) Play();
        }

        public void Play(float from = 0f) { time = from; playing = true; Apply(); }

        void Update()
        {
            if (!Built) return;
            if (playing) time += Time.deltaTime;
            Apply();
        }

        public void Build()
        {
            if (Built) return;
            string dir = resourcesRoot + "/" + cutName + "/";
            var rigText = Resources.Load<TextAsset>(dir + "rig");
            var animText = Resources.Load<TextAsset>(dir + "anim");
            if (rigText == null || animText == null) { Debug.LogError("PrologueCut: missing " + dir + "rig/anim"); return; }
            var rig = JsonUtility.FromJson<Rig>(rigText.text);
            var anim = JsonUtility.FromJson<Anim>(animText.text);

            layers = rig.layers;
            int n = layers.Length;
            SizePx = new Vector2(rig.size[0], rig.size[1]);
            Intro = anim.intro;
            var index = layerIndices = new Dictionary<string, int>();
            for (int i = 0; i < n; i++) index[layers[i].id] = i;

            parent = new int[n];
            piv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                parent[i] = !string.IsNullOrEmpty(layers[i].parent) && index.TryGetValue(layers[i].parent, out int p) ? p : -1;
                piv[i] = new Vector2(layers[i].pivot[0], layers[i].pivot[1]);
            }
            if (anim.pivots != null)
                foreach (var po in anim.pivots)
                    if (index.TryGetValue(po.id, out int i)) piv[i] = new Vector2(po.x, po.y);

            tracks = anim.tracks ?? new Track[0];
            targets = new int[tracks.Length][];
            for (int t = 0; t < tracks.Length; t++)
            {
                var list = new List<int>();
                foreach (var id in tracks[t].t)
                    if (id == "@cam") list.Add(-1);
                    else if (index.TryGetValue(id, out int i)) list.Add(i);
                targets[t] = list.ToArray();
            }

            // parents first
            var done = new bool[n];
            var ord = new List<int>(n);
            void Visit(int i) { if (done[i]) return; if (parent[i] >= 0) Visit(parent[i]); done[i] = true; ord.Add(i); }
            for (int i = 0; i < n; i++) Visit(i);
            order = ord.ToArray();

            // camera node at the canvas centre, canvas origin (top-left) under it
            cam = new GameObject("Camera").transform;
            cam.SetParent(transform, false);
            var canvas = new GameObject("Canvas").transform;
            canvas.SetParent(cam, false);
            canvas.localPosition = new Vector3(-SizePx.x / 2f / PPU, SizePx.y / 2f / PPU, 0f);

            nodes = new Transform[n];
            renderers = new SpriteRenderer[n];
            foreach (int i in order)
            {
                var L = layers[i];
                var node = new GameObject(L.id).transform;
                node.SetParent(parent[i] >= 0 ? nodes[parent[i]] : canvas, false);
                nodes[i] = node;
                var sprGo = new GameObject("sprite");
                sprGo.transform.SetParent(node, false);
                var cx = L.rect[0] + L.rect[2] / 2f; var cy = L.rect[1] + L.rect[3] / 2f;
                sprGo.transform.localPosition = ToUnity(new Vector2(cx, cy) - piv[i]);
                var sr = sprGo.AddComponent<SpriteRenderer>();
                string file = L.file.EndsWith(".png") ? L.file.Substring(0, L.file.Length - 4) : L.file;
                sr.sprite = Resources.Load<Sprite>(dir + file);
                if (sr.sprite == null) Debug.LogWarning("PrologueCut: missing sprite " + dir + file);
                sr.sortingOrder = sortingOrderBase + i;
                renderers[i] = sr;
            }
            poses = new LayerPose[n + 1];
            alpha = new float[n];
        }

        static Vector3 ToUnity(Vector2 px) => new Vector3(px.x / PPU, -px.y / PPU, 0f);

        public SpriteRenderer GetLayerRenderer(string id)
        {
            if (!Built) Build();
            return layerIndices != null && layerIndices.TryGetValue(id, out int index) ? renderers[index] : null;
        }

        /// <summary>Makes every renderer of this cut respect (or ignore) a SpriteMask.</summary>
        public void SetMaskInteraction(SpriteMaskInteraction m)
        {
            if (!Built) Build();
            foreach (var sr in renderers) sr.maskInteraction = m;
        }

        public void Apply()
        {
            if (!Built) return;
            PrologueMath.Evaluate(tracks, targets, time, poses);
            int n = layers.Length;
            var c = poses[n];
            cam.localPosition = new Vector3(c.x / PPU, -c.y / PPU, 0f);
            cam.localRotation = Quaternion.Euler(0f, 0f, -c.r);
            cam.localScale = new Vector3(c.sx * Overscan, c.sy * Overscan, 1f);
            foreach (int i in order)
            {
                var p = poses[i];
                int pa = parent[i];
                Vector2 basePos = pa >= 0 ? piv[i] - piv[pa] : piv[i];
                var node = nodes[i];
                node.localPosition = ToUnity(basePos + new Vector2(p.x, p.y));
                node.localRotation = Quaternion.Euler(0f, 0f, -p.r);
                node.localScale = new Vector3(p.sx, p.sy, 1f);
                alpha[i] = (pa >= 0 ? alpha[pa] : fade) * p.a;
                var sr = renderers[i];
                float a = Mathf.Clamp01(alpha[i]);
                sr.enabled = a > .003f;
                var col = sr.color; col.a = a; sr.color = col;
            }
        }
    }
}
