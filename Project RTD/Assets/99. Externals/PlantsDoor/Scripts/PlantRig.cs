using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChimeraGroove.Plants
{
    /// <summary>
    /// A segmented, animated plant (Resources/Plants/&lt;plantName&gt;/rig.json + parts).
    /// The GameObject's position is the plant's root (bottom centre on the ground).
    ///  - bend layers (stems, grass, fronds) are row meshes that sway and fan out with the wind
    ///  - rigid layers (heads, leaves, eyes, tongue, ear, nose) ride on their stem and wobble / blink / sniff
    ///  - bodies brushing through the trigger push it with a springy gust (Poke)
    /// Same maths as web/plant-anim.js.
    /// </summary>
    public class PlantRig : MonoBehaviour
    {
        public const float PPU = 100f;
        const float BendPow = 1.6f;

        public string plantName = "flower-pink";
        public string resourcesRoot = "Plants";
        [Tooltip("seconds per sway cycle")] public float period = 2.6f;
        [Range(0f, 2f)] public float wind = 1f;
        [Tooltip("0..1, random if negative")] public float phase = -1f;
        public string sortingLayerName = "Default";
        public int sortingOrder = 0;
        [Tooltip("rows per bend mesh (more = smoother bend)")] public int bendRows = 24;
        public bool reactToBodies = true;
        [Tooltip("only colliders with this tag poke the plant (empty = any rigidbody)")] public string pokeTag = "";
        [Tooltip("collider-free alternative: this transform (e.g. the player) pokes the plant when it passes through")]
        public Transform watch;
        public float watchHalfWidth = .5f;

        // ---------- json ----------
#pragma warning disable 0649
        [Serializable] public class Bend { public float y0, y1, amp, spread, lag; }
        [Serializable] public class Layer
        {
            public string id, file, attach;
            public int[] rect;
            public Bend bend;
            public float[] anchor, wob, blink, sniff;
            public float follow = 1f, squash;
        }
        [Serializable] public class Rig { public string plant; public int[] size; public int[] @base; public Layer[] layers; }
#pragma warning restore 0649

        struct Gust { public float t0, k; }
        class Part
        {
            public Layer L; public int parent = -1; public bool isBend;
            public Transform tr; public SpriteRenderer sr;
            public Mesh mesh; public Vector3[] verts; public Rect uv;
        }

        Rig rig;
        Part[] parts;
        Dictionary<string, int> index;
        readonly List<Gust> gusts = new List<Gust>();
        float bx, by, time;
        static Material spriteMat;

        public bool Built => parts != null;
        public Vector2 SizeUnits => rig == null ? Vector2.zero : new Vector2(rig.size[0], rig.size[1]) / PPU;

        void Awake() { if (phase < 0f) phase = UnityEngine.Random.value; }
        void Start() { if (!Built) Build(); }

        public void Build()
        {
            if (Built) return;
            string dir = resourcesRoot + "/" + plantName + "/";
            var text = Resources.Load<TextAsset>(dir + "rig");
            if (text == null) { Debug.LogError("PlantRig: missing " + dir + "rig.json"); return; }
            rig = JsonUtility.FromJson<Rig>(text.text);
            bx = rig.@base[0]; by = rig.@base[1];
            if (spriteMat == null)
            {
                var probe = new GameObject("probe").AddComponent<SpriteRenderer>();
                spriteMat = probe.sharedMaterial;   // the pipeline's default sprite material
                DestroyImmediate(probe.gameObject);
            }
            index = new Dictionary<string, int>();
            for (int i = 0; i < rig.layers.Length; i++) index[rig.layers[i].id] = i;
            parts = new Part[rig.layers.Length];
            for (int i = 0; i < rig.layers.Length; i++)
            {
                var L = rig.layers[i];
                var p = new Part { L = L, isBend = L.bend != null && L.bend.y0 > 0f };  // JsonUtility fills a missing bend with zeros
                if (!string.IsNullOrEmpty(L.attach) && index.TryGetValue(L.attach, out int pi)) p.parent = pi;
                string file = L.file.EndsWith(".png") ? L.file.Substring(0, L.file.Length - 4) : L.file;
                var sprite = Resources.Load<Sprite>(dir + file);
                if (sprite == null) { Debug.LogWarning("PlantRig: missing sprite " + dir + file); }
                var go = new GameObject(L.id);
                go.transform.SetParent(transform, false);
                p.tr = go.transform;
                if (p.isBend && sprite != null)
                {
                    var mf = go.AddComponent<MeshFilter>();
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = spriteMat;
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetTexture("_MainTex", sprite.texture);
                    mr.SetPropertyBlock(mpb);
                    mr.sortingLayerName = sortingLayerName;
                    mr.sortingOrder = sortingOrder + i;
                    var tex = sprite.texture; var tr = sprite.textureRect;
                    p.uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
                    p.mesh = BuildMesh(p);
                    mf.sharedMesh = p.mesh;
                }
                else
                {
                    p.sr = go.AddComponent<SpriteRenderer>();
                    p.sr.sprite = sprite;
                    p.sr.sortingLayerName = sortingLayerName;
                    p.sr.sortingOrder = sortingOrder + i;
                }
                parts[i] = p;
            }
            if (reactToBodies && GetComponent<Collider2D>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(rig.size[0] * .8f, rig.size[1] * .9f) / PPU;
                box.offset = new Vector2((rig.size[0] / 2f - bx) / PPU, (by - rig.size[1] * .45f) / PPU);
            }
            Apply();
        }

        Mesh BuildMesh(Part p)
        {
            int rows = Mathf.Max(2, bendRows);
            var m = new Mesh { name = p.L.id };
            m.MarkDynamic();
            p.verts = new Vector3[(rows + 1) * 2];
            var uvs = new Vector2[p.verts.Length];
            var tris = new int[rows * 6];
            for (int k = 0; k <= rows; k++)
            {
                float v = (float)k / rows;              // 0 = top row of the png, 1 = bottom
                uvs[k * 2] = new Vector2(p.uv.xMin, p.uv.yMax - v * p.uv.height);
                uvs[k * 2 + 1] = new Vector2(p.uv.xMax, p.uv.yMax - v * p.uv.height);
                if (k < rows)
                {
                    int a = k * 2, t = k * 6;
                    tris[t] = a; tris[t + 1] = a + 1; tris[t + 2] = a + 2;
                    tris[t + 3] = a + 1; tris[t + 4] = a + 3; tris[t + 5] = a + 2;
                }
            }
            var cols = new Color[p.verts.Length];
            for (int k = 0; k < cols.Length; k++) cols[k] = Color.white;
            m.vertices = p.verts; m.uv = uvs; m.colors = cols; m.triangles = tris;
            return m;
        }

        /// <summary>A springy push: dir -1 (left) … 1 (right), strength ≈ 1.</summary>
        public void Poke(float dir, float strength = 1f)
        {
            gusts.Add(new Gust { t0 = time, k = Mathf.Sign(dir) * strength });
            if (gusts.Count > 6) gusts.RemoveAt(0);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!reactToBodies) return;
            if (!string.IsNullOrEmpty(pokeTag) && !other.CompareTag(pokeTag)) return;
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            float vx = rb.linearVelocity.x;
            float dir = Mathf.Abs(vx) > .1f ? Mathf.Sign(vx) : Mathf.Sign(transform.position.x - other.transform.position.x);
            Poke(dir, Mathf.Clamp(.5f + Mathf.Abs(vx) / 6f, .5f, 1.4f));
        }

        float lastWatchX = float.NaN;
        void Update()
        {
            if (!Built) return;
            time += Time.deltaTime;
            if (watch != null) WatchPass();
            Apply();
        }

        /// <summary>Pokes the plant when the watched transform crosses it (no collider needed).</summary>
        void WatchPass()
        {
            Vector3 w = watch.position, me = transform.position;
            float sx = Mathf.Abs(transform.lossyScale.x), sy = Mathf.Abs(transform.lossyScale.y);
            float halfW = rig.size[0] * .4f / PPU * sx + watchHalfWidth, top = rig.size[1] / PPU * sy;
            float dx = w.x - me.x;
            bool inside = Mathf.Abs(dx) < halfW && w.y > me.y - .5f && w.y < me.y + top + 1f;
            if (inside && !float.IsNaN(lastWatchX))
            {
                float v = (w.x - lastWatchX) / Mathf.Max(Time.deltaTime, 1e-4f);
                bool justEntered = Mathf.Abs(lastWatchX - me.x) >= halfW;
                if (justEntered && Mathf.Abs(v) > .5f) Poke(Mathf.Sign(v), Mathf.Clamp(.5f + Mathf.Abs(v) / 6f, .5f, 1.4f));
            }
            lastWatchX = w.x;
        }

        // ---------- maths (mirrors plant-anim.js) ----------
        float Wind(float t)
        {
            float u = t / period + phase;
            return wind * (Mathf.Sin(2f * Mathf.PI * u) * .75f + Mathf.Sin(2f * Mathf.PI * u * 2.31f + 1.1f) * .25f);
        }
        float GustAt(float t)
        {
            float v = 0f;
            foreach (var g in gusts) { float a = t - g.t0; if (a > 0f) v += g.k * 1.8f * Mathf.Exp(-3.1f * a) * Mathf.Sin(2f * Mathf.PI * a * 1.7f); }
            return v;
        }
        static float Soft(float w) => 1.7f * (float)Math.Tanh(w / 1.7f);

        void BendAt(Bend b, float y, float t, out float dx, out float s, out float slope)
        {
            float span = Mathf.Max(1f, b.y0 - b.y1);
            float u = Mathf.Clamp01((b.y0 - y) / span), f = Mathf.Pow(u, BendPow);
            float w = Soft(Wind(t - b.lag) + GustAt(t - b.lag));
            float w2 = Mathf.Sin(2f * Mathf.PI * ((t - b.lag) / (period * 1.37f) + phase + .3f));
            dx = b.amp * f * w; s = b.spread * f * w2;
            slope = b.amp * BendPow * Mathf.Pow(u, BendPow - 1f) / span * w;
        }

        // 2D affine in png pixel space (y down): [a b c d e f] like canvas
        static float[] Mul(float[] m, float[] n) => new[] {
            m[0] * n[0] + m[2] * n[1], m[1] * n[0] + m[3] * n[1], m[0] * n[2] + m[2] * n[3], m[1] * n[2] + m[3] * n[3],
            m[0] * n[4] + m[2] * n[5] + m[4], m[1] * n[4] + m[3] * n[5] + m[5] };
        static float[] Around(float px, float py, float deg, float sx, float sy, float tx, float ty)
        {
            float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad);
            float a = c * sx, b = s * sx, cc = -s * sy, d = c * sy;
            return new[] { a, b, cc, d, px + tx - (a * px + cc * py), py + ty - (b * px + d * py) };
        }
        static readonly float[] Identity = { 1, 0, 0, 1, 0, 0 };

        float[][] solved;
        float[] Solve(int i, float t)
        {
            if (solved[i] != null) return solved[i];
            var p = parts[i]; var L = p.L;
            if (p.isBend || p.parent < 0) return solved[i] = Identity;
            var P = parts[p.parent];
            float ax = L.anchor[0], ay = L.anchor[1], deg = 0f, sx = 1f, sy = 1f;
            float[] baseM;
            if (P.isBend)
            {
                BendAt(P.L.bend, ay, t, out float dx, out _, out float slope);
                baseM = new[] { 1f, 0f, 0f, 1f, dx, 0f };
                deg = Mathf.Atan(slope) * Mathf.Rad2Deg * L.follow + Soft(GustAt(t - .08f)) * 4f * L.follow;
            }
            else baseM = Solve(p.parent, t);
            if (L.wob != null && L.wob.Length >= 3) deg += L.wob[0] * Mathf.Sin(2f * Mathf.PI * (t / L.wob[1] + L.wob[2] + phase));
            if (L.squash != 0f) { float q = L.squash * Mathf.Sin(2f * Mathf.PI * (t / 1.3f + phase)); sy *= 1f + q; sx *= 1f - q * .6f; }
            if (L.sniff != null && L.sniff.Length >= 2)
            {
                float a = ((t / L.sniff[1] + phase) % 1f) * L.sniff[1];
                if (a < .5f) { float k = Mathf.Abs(Mathf.Sin(Mathf.PI * a / .25f) * L.sniff[0]); sx *= 1f + k; sy *= 1f + k * .5f; }
            }
            if (L.blink != null && L.blink.Length >= 2)
            {
                float per = L.blink[0], a = Mathf.Repeat(t / per + L.blink[1] + phase, 1f) * per, d = .16f;
                if (a < d) sy *= 1f - .88f * Mathf.Sin(Mathf.PI * a / d);
            }
            return solved[i] = Mul(baseM, Around(ax, ay, deg, sx, sy, 0f, 0f));
        }

        Vector3 ToLocal(float px, float py) => new Vector3((px - bx) / PPU, (by - py) / PPU, 0f);

        void Apply()
        {
            if (solved == null || solved.Length != parts.Length) solved = new float[parts.Length][];
            for (int i = 0; i < solved.Length; i++) solved[i] = null;
            float t = time;
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i]; var L = p.L;
                float rx = L.rect[0], ry = L.rect[1], rw = L.rect[2], rh = L.rect[3];
                if (p.isBend)
                {
                    if (p.mesh == null) continue;
                    int rows = p.verts.Length / 2 - 1;
                    for (int k = 0; k <= rows; k++)
                    {
                        float y = ry + rh * k / rows;
                        BendAt(L.bend, y, t, out float dx, out float s, out _);
                        float xl = bx + (rx - bx) * (1f + s) + dx, xr = bx + (rx + rw - bx) * (1f + s) + dx;
                        p.verts[k * 2] = ToLocal(xl, y);
                        p.verts[k * 2 + 1] = ToLocal(xr, y);
                    }
                    p.mesh.vertices = p.verts;
                    p.mesh.RecalculateBounds();
                }
                else
                {
                    var m = Solve(i, t);
                    float cx = rx + rw / 2f, cy = ry + rh / 2f;
                    float wx = m[0] * cx + m[2] * cy + m[4], wy = m[1] * cx + m[3] * cy + m[5];
                    float scx = Mathf.Sqrt(m[0] * m[0] + m[1] * m[1]);
                    float det = m[0] * m[3] - m[1] * m[2];
                    float ang = Mathf.Atan2(m[1], m[0]) * Mathf.Rad2Deg;
                    p.tr.localPosition = ToLocal(wx, wy);
                    p.tr.localRotation = Quaternion.Euler(0f, 0f, -ang);
                    p.tr.localScale = new Vector3(scx, scx > 1e-5f ? det / scx : 1f, 1f);
                }
            }
        }
    }
}
