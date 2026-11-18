using UnityEngine;

namespace ChimeraGroove.Prologue
{
    /// <summary>
    /// Full-screen dust wipe (scene 06), same timing as drawDust() in prologue-anim.js.
    /// Outline-free ivory puffs roll in from the left, the ivory cover with ink swirls holds,
    /// then the puffs shrink away to the right. Swap the cut underneath at <see cref="SwitchAt"/>.
    /// </summary>
    public class PrologueDust : MonoBehaviour
    {
        public const float Duration = 1.5f;
        public const float SwitchAt = .5f; // fraction of Duration
        public static readonly Color Ivory = new Color32(0xfd, 0xf3, 0xe0, 0xff);

        public int sortingOrder = 30000;

        struct Puff { public float x, y, r, d; }
        static Puff[] puffs;
        static Sprite disc;
        SpriteRenderer[] puffSr;
        PrologueCut cover;

        static Puff[] MakePuffs()
        {
            var list = new Puff[40];
            long s = 7;
            float Rnd() { s = (s * 16807L) % 2147483647L; return s / 2147483647f; }
            int k = 0;
            for (int gy = 0; gy < 5; gy++)
                for (int gx = 0; gx < 8; gx++)
                {
                    var p = new Puff();
                    p.x = (gx + .2f + Rnd() * .6f) / 8f * 1.1f - .05f;
                    p.y = (gy + .15f + Rnd() * .7f) / 5f * 1.1f - .05f;
                    p.r = .16f + Rnd() * .1f;
                    p.d = Rnd() * .08f;
                    list[k++] = p;
                }
            return list;
        }

        public static Sprite Disc
        {
            get
            {
                if (disc != null) return disc;
                const int N = 256;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[N * N];
                float c = (N - 1) / 2f, R = N / 2f - 1.5f;
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                        byte a = (byte)(Mathf.Clamp01(R - d + .5f) * 255f);
                        px[y * N + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px); tex.Apply();
                disc = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), N);
                return disc;
            }
        }

        void Build()
        {
            if (puffSr != null) return;
            if (puffs == null) puffs = MakePuffs();
            puffSr = new SpriteRenderer[puffs.Length];
            for (int i = 0; i < puffs.Length; i++)
            {
                var sr = new GameObject("puff" + i).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(transform, false);
                sr.sprite = Disc; sr.color = Ivory; sr.sortingOrder = sortingOrder;
                puffSr[i] = sr;
            }
            var go = new GameObject("transition-dust");
            go.transform.SetParent(transform, false);
            cover = go.AddComponent<PrologueCut>();
            cover.cutName = "transition-dust";
            cover.playOnStart = false;
            cover.sortingOrderBase = sortingOrder + 10;
            cover.Build();
            Show(false);
        }

        public void Show(bool on)
        {
            Build();
            foreach (var sr in puffSr) sr.gameObject.SetActive(on);
            cover.gameObject.SetActive(on);
        }

        /// <summary>p = 0..1 over <see cref="Duration"/>. viewW/viewH = visible area in world units, centred on this transform.</summary>
        public void Evaluate(float p, float viewW, float viewH)
        {
            Build();
            float t = p * Duration, grow, sweep = 1f, coverA = 0f;
            if (t < .55f) grow = t / .55f;
            else if (t < .95f) { grow = 1f; coverA = Mathf.Clamp01((t - .5f) / .12f) * Mathf.Clamp01((.95f - t) / .12f); }
            else { grow = 1f - (t - .95f) / .55f; sweep = -1f; }

            float big = Mathf.Max(viewW, viewH);
            for (int i = 0; i < puffs.Length; i++)
            {
                var pf = puffs[i];
                float lead = sweep > 0f ? pf.x : 1f - pf.x;
                float g = Mathf.Clamp01((grow * 1.45f - lead * .45f - pf.d) / .55f);
                float e = sweep > 0f ? 1f - (1f - g) * (1f - g) : g * g * (3f - 2f * g);
                float r = pf.r * big * e;
                var sr = puffSr[i];
                sr.enabled = r > .005f;
                sr.transform.localPosition = new Vector3((pf.x - .5f) * viewW, (.5f - pf.y) * viewH, 0f);
                sr.transform.localScale = Vector3.one * (2f * r);
            }

            cover.gameObject.SetActive(coverA > 0f);
            if (coverA > 0f)
            {
                float s = Mathf.Max(viewW / (cover.SizePx.x / PrologueCut.PPU), viewH / (cover.SizePx.y / PrologueCut.PPU));
                cover.transform.localScale = new Vector3(s / PrologueCut.Overscan, s / PrologueCut.Overscan, 1f);
                cover.fade = coverA;
                cover.time = t;
                cover.Apply();
            }
        }
    }
}
