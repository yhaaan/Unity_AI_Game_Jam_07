using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChimeraGroove.Plants
{
    /// <summary>
    /// Breakable brick wall (Resources/Walls/brick). Position = bottom centre on the ground.
    ///   Intact → Hit() → Cracked (bricks swap to cracked art, knock + shake + dust)
    ///   Cracked → Hit() → Breaking (every brick flies off spinning, bounces, crumbles) → Open (rubble on the ground)
    /// The wall's BoxCollider2D (added if missing) blocks until it breaks. Reset() rebuilds it.
    /// </summary>
    public class BrickWall : MonoBehaviour
    {
        public const float PPU = 100f;
        public enum State { Intact, Cracked, Breaking, Open }

        [Tooltip("world units")] public float width = 1.3f;
        public int hitsToBreak = 2;
        public string sortingLayerName = "Default";
        public int sortingOrder = 2;
        public Color dustColor = new Color32(0xe8, 0xd6, 0xbd, 0xff);
        public bool addCollider = true;

        public event Action<State> StateChanged;
        public State Current { get; private set; }

#pragma warning disable 0649
        [Serializable] class BrickDef { public string piece; public float x, y, w, h; }
        [Serializable] class Rig { public float width, height; public BrickDef[] bricks; }
#pragma warning restore 0649

        class Brick { public BrickDef d; public SpriteRenderer sr; public Vector2 home, pos, vel; public Vector3 baseScale; public float rot, spin, knock, life; public bool grounded; }
        class Puff { public SpriteRenderer sr; public Vector2 p, v; public float t0, life, r; }

        Rig rig;
        float k, time, shakeAt = -10f;
        int hits;
        Transform root;
        readonly List<Brick> bricks = new List<Brick>();
        readonly List<SpriteRenderer> rubble = new List<SpriteRenderer>();
        readonly List<Puff> puffs = new List<Puff>();
        BoxCollider2D box;
        static Sprite disc;

        public float Height => rig == null ? 0f : rig.height / PPU * k;

        void Start() { Build(); }

        Sprite Piece(string n) => Resources.Load<Sprite>("Walls/brick/" + n);

        void Build()
        {
            var text = Resources.Load<TextAsset>("Walls/brick/rig");
            if (text == null) { Debug.LogError("BrickWall: missing Resources/Walls/brick/rig.json"); return; }
            rig = JsonUtility.FromJson<Rig>(text.text);
            k = width / (rig.width / PPU);
            if (addCollider)
            {
                box = GetComponent<BoxCollider2D>();
                if (box == null) box = gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(width, Height);
                box.offset = new Vector2(0f, Height / 2f);
            }
            Reset();
        }

        public void Reset()
        {
            if (rig == null) return;
            if (root != null) Destroy(root.gameObject);
            foreach (var r in rubble) if (r != null) Destroy(r.gameObject);
            foreach (var p in puffs) if (p.sr != null) Destroy(p.sr.gameObject);
            bricks.Clear(); rubble.Clear(); rubbleScale.Clear(); puffs.Clear();
            root = new GameObject("Bricks").transform;
            root.SetParent(transform, false);
            for (int i = 0; i < rig.bricks.Length; i++)
            {
                var d = rig.bricks[i];
                var sr = new GameObject(d.piece + " " + i).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(root, false);
                sr.sprite = Piece(d.piece);
                sr.sortingLayerName = sortingLayerName; sr.sortingOrder = sortingOrder + i;
                var home = Px(d.x + d.w / 2f, d.y + d.h / 2f);
                var size = sr.sprite != null ? sr.sprite.rect.size : new Vector2(d.w, d.h);
                sr.transform.localScale = new Vector3(d.w / size.x * k, d.h / size.y * k, 1f);
                bricks.Add(new Brick { d = d, sr = sr, home = home, pos = home, baseScale = sr.transform.localScale });
            }
            hits = 0;
            if (box != null) box.enabled = true;
            SetState(State.Intact);
        }

        Vector2 Px(float x, float y) => new Vector2((x - rig.width / 2f) / PPU * k, (rig.height - y) / PPU * k);

        void SetState(State s) { Current = s; StateChanged?.Invoke(s); }

        /// <summary>A hit from the side `fromDir` points to (+1 = hit from the left, bricks fly right).</summary>
        public void Hit(float fromDir = 1f)
        {
            if (Current == State.Breaking || Current == State.Open || rig == null) return;
            hits++;
            if (hits >= hitsToBreak) { Break(fromDir); return; }
            foreach (var b in bricks) { b.sr.sprite = Piece(b.d.piece + "-cracked"); b.knock = UnityEngine.Random.Range(.5f, 1f) * Mathf.Sign(fromDir); }
            shakeAt = time;
            Dust(new Vector2(-Mathf.Sign(fromDir) * width * .5f, Height * .5f), 5, .6f);
            SetState(State.Cracked);
        }

        public void Break(float fromDir = 1f)
        {
            if (rig == null || Current == State.Breaking || Current == State.Open) return;
            float dir = Mathf.Sign(fromDir == 0f ? 1f : fromDir);
            foreach (var b in bricks)
            {
                b.sr.sprite = Piece(b.d.piece + "-cracked");
                float up = b.home.y / Mathf.Max(.01f, Height);
                b.vel = new Vector2(dir * UnityEngine.Random.Range(1.5f, 5f) + b.home.x * 2.5f, UnityEngine.Random.Range(3f, 7f) * (.4f + up));
                b.spin = UnityEngine.Random.Range(180f, 720f) * (UnityEngine.Random.value < .5f ? -1f : 1f);
                b.life = UnityEngine.Random.Range(.9f, 1.4f);
            }
            if (box != null) box.enabled = false;
            Dust(new Vector2(0f, Height * .4f), 12, 1.3f);
            SetState(State.Breaking);
            breakAt = time;
        }
        float breakAt;

        void Update()
        {
            if (rig == null) return;
            float dt = Time.deltaTime;
            time += dt;
            if (Current == State.Breaking)
            {
                bool any = false;
                foreach (var b in bricks)
                {
                    float a = time - breakAt;
                    if (a > b.life) { b.sr.enabled = false; continue; }
                    any = true;
                    b.vel.y -= 22f * dt;
                    b.pos += b.vel * dt;
                    b.rot += b.spin * dt;
                    float floor = b.d.h / PPU * k * .3f;
                    if (b.pos.y < floor && b.vel.y < 0f)
                    {
                        b.pos.y = floor; b.vel.y *= -.3f; b.vel.x *= .6f; b.spin *= .5f;
                        if (!b.grounded) { b.grounded = true; Dust(b.pos, 2, .5f); }
                    }
                    float shrink = Mathf.Clamp01((b.life - a) / .25f);   // crumbles away at the end
                    b.sr.transform.localPosition = b.pos;
                    b.sr.transform.localRotation = Quaternion.Euler(0f, 0f, b.rot);
                    b.sr.transform.localScale = b.baseScale * shrink;
                }
                if (time - breakAt > .45f && rubble.Count == 0) SpawnRubble();
                if (!any) SetState(State.Open);
            }
            else if (Current != State.Open)
            {
                float s = time - shakeAt, sh = s < .4f ? .06f * Mathf.Exp(-8f * s) * Mathf.Sin(2f * Mathf.PI * s * 12f) : 0f;
                foreach (var b in bricks)
                {
                    float kn = s < .5f ? b.knock * .05f * Mathf.Exp(-9f * s) * Mathf.Sin(2f * Mathf.PI * s * 7f) : 0f;
                    b.sr.transform.localPosition = b.home + new Vector2(sh + kn, 0f);
                    b.sr.transform.localRotation = Quaternion.Euler(0f, 0f, kn * 60f);
                }
            }
            for (int i = rubble.Count - 1; i >= 0; i--)          // rubble pops in
            {
                float a = time - breakAt - .45f, e = Mathf.Clamp01(a / .25f);
                float pop = e < 1f ? 1f + Mathf.Sin(e * Mathf.PI) * .25f : 1f;
                rubble[i].transform.localScale = rubbleScale[i] * e * pop;
            }
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                var p = puffs[i]; float a = time - p.t0;
                if (a > p.life) { Destroy(p.sr.gameObject); puffs.RemoveAt(i); continue; }
                p.sr.enabled = a >= 0f;
                if (a < 0f) continue;
                float u = a / p.life, r = p.r * (u < .25f ? u / .25f : 1f - (u - .25f) / .75f);
                p.sr.transform.localPosition = p.p + p.v * a * (1f - u * .5f);
                p.sr.transform.localScale = Vector3.one * Mathf.Max(0f, r) * 2f;
            }
        }

        readonly List<Vector3> rubbleScale = new List<Vector3>();
        void SpawnRubble()
        {
            // two little heaps either side of where the wall stood
            var set = new[] { ("rubble-big", -.55f, 1f, false), ("rubble-small", -.15f, .9f, true), ("rubble-big", .45f, .9f, true), ("rubble-small", .8f, .8f, false) };
            int n = 0;
            foreach (var (piece, x, s, flip) in set)
            {
                var sr = new GameObject(piece).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = Piece(piece);
                sr.sortingLayerName = sortingLayerName; sr.sortingOrder = sortingOrder + 20 + n++;
                float sc = width / (rig.width / PPU) * .45f * s;
                var size = sr.sprite != null ? sr.sprite.rect.size / PPU : Vector2.one;
                sr.transform.localPosition = new Vector3(x * width, size.y * sc / 2f, 0f);
                var scale = new Vector3(flip ? -sc : sc, sc, 1f);
                rubbleScale.Add(scale);
                sr.transform.localScale = Vector3.zero;
                rubble.Add(sr);
            }
        }

        static Sprite Disc
        {
            get
            {
                if (disc != null) return disc;
                const int N = 48; var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float dx = x + .5f - N / 2f, dy = y + .5f - N / 2f;
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(N / 2f - Mathf.Sqrt(dx * dx + dy * dy))));
                }
                tex.SetPixels32(px); tex.Apply();
                return disc = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), N);
            }
        }

        /// <summary>outline-free round dust, one colour</summary>
        void Dust(Vector2 at, int count, float power)
        {
            for (int i = 0; i < count; i++)
            {
                var sr = new GameObject("dust").AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = Disc; sr.color = dustColor;
                sr.sortingLayerName = sortingLayerName; sr.sortingOrder = sortingOrder + 40;
                float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                puffs.Add(new Puff
                {
                    sr = sr, p = at + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * .1f,
                    v = new Vector2(Mathf.Cos(ang), Mathf.Abs(Mathf.Sin(ang)) * .6f) * UnityEngine.Random.Range(.8f, 2f) * power,
                    t0 = time + i * .02f, life = UnityEngine.Random.Range(.45f, .8f), r = UnityEngine.Random.Range(.12f, .26f) * Mathf.Min(1.3f, power) * width / 1.3f,
                });
            }
        }
    }
}
