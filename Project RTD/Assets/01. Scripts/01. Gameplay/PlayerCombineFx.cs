using System.Collections.Generic;
using ChimeraGroove;
using ProjectRTD.Core;
using TMPro;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // "Rumble-tumble" merge whenever the player's parts are (re)assembled, in the prologue's dust-cloud style.
    // Beats: crouch & gather light → cloud SLAM → escalating brawl (limbs, stars, lightning, BONK!/CLANG!) →
    // hit-stop + white flash + shockwaves + glowing rays → the new chimera springs out in a yellow aura.
    // Runs on unscaled time so the hit-stop can freeze the game for a beat.
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerPartLoadout))]
    public sealed class PlayerCombineFx : MonoBehaviour
    {
        [Header("Prologue dust-cloud art")]
        [SerializeField] private Sprite lettering;
        [SerializeField] private Sprite lightning;
        [SerializeField] private Sprite[] stars = new Sprite[0];
        [SerializeField] private Sprite[] limbs = new Sprite[0];
        [SerializeField] private Sprite[] puffs = new Sprite[0];
        [SerializeField] private Sprite[] speedLines = new Sprite[0];
        [SerializeField] private TMP_FontAsset font;

        [Header("Look")]
        [SerializeField] private Color cream = new Color32(0xfb, 0xf3, 0xe2, 0xff);
        [SerializeField] private Color ink = new Color32(0x1d, 0x1b, 0x26, 0xff);
        [SerializeField] private Color glow = new Color32(0xff, 0xd1, 0x66, 0xff);
        [SerializeField] private Vector2 cloudRadius = new(0.82f, 0.86f);
        [SerializeField] private int sortingOrder = 70;
        [SerializeField, Tooltip("Glow, aura and rays sit behind the player's parts (which start at 10).")] private int behindOrder = 6;
        [SerializeField] private Transform visual;

        [Header("Timing (seconds)")]
        [SerializeField] private float slamAt = 0.18f;
        [SerializeField] private float burstAt = 0.8f;
        [SerializeField] private float duration = 1.35f;
        [SerializeField] private float hitStop = 0.07f;

        public float BurstAt => burstAt;

        private static readonly string[] Words = { "BONK!", "CLANG!", "WHAM!", "POW!", "THUD!", "BOING!" };
        private const int CloudBalls = 13;
        private static Sprite softGlow;

        private PlayerPartLoadout loadout;
        private CapsuleCollider2D capsule;
        private PlayerOutline outline;
        private Transform root;
        private float startedAt, visualBaseY = 1f, nextBump, frozenUntil = -1f, savedTimeScale = 1f;
        private bool active, burstDone;
        private Material wordMaterial;

        private readonly List<Ball> balls = new();
        private readonly List<Bit> bits = new();

        private sealed class Ball { public SpriteRenderer inkSr, creamSr; public Vector2 home; public float size, seed, bump; }
        private sealed class Bit
        {
            public SpriteRenderer sr; public TextMeshPro text;
            public Vector2 dir; public float start, end, size, spin, seed; public Kind kind;
        }
        private enum Kind { Limb, FlyingLimb, Star, Lightning, SpeedIn, SpeedOut, Lettering, Word, Puff, Spark, Ring, Ray, Flash, Aura, Gather }

        private void Awake()
        {
            loadout = GetComponent<PlayerPartLoadout>();
            capsule = GetComponent<CapsuleCollider2D>();
            if (!visual) { var v = GetComponentInChildren<PlayerPartVisual>(true); if (v) visual = v.transform; }
            if (visual) { visualBaseY = visual.localScale.y; outline = visual.GetComponent<PlayerOutline>(); }
        }

        private void OnEnable() => loadout.Changed += Play;
        private void OnDisable() { loadout.Changed -= Play; Stop(); }

        private Vector3 Center => capsule ? (Vector3)capsule.offset : Vector3.up * 0.8f;
        private float Now => Time.unscaledTime;

        private void Play(PlayerPartBuild _)
        {
            Stop();
            SoundManager.Play(Sfx.Summon);
            startedAt = Now;
            nextBump = slamAt;
            burstDone = false;
            active = true;
            root = new GameObject("Combine FX").transform;
            root.SetParent(transform, false);
            root.localPosition = Center;

            // Gathering light and inward speed lines during the crouch.
            Add(Kind.Gather, SoftGlow, Vector2.zero, 0f, slamAt + 0.1f, 2.2f, 0f, behindOrder, glow);
            for (int i = 0; i < 6 && speedLines.Length > 0; i++)
                Add(Kind.SpeedIn, speedLines[i % speedLines.Length], Dir(i / 6f + 0.08f), i * 0.02f, slamAt, 0.7f, 0f, sortingOrder + 2, ink);

            for (int i = 0; i < CloudBalls; i++)
            {
                float a = i / (float)CloudBalls * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
                float r = i == 0 ? 0f : Random.Range(0.35f, 0.78f);
                balls.Add(new Ball
                {
                    inkSr = Sr("Cloud Ink", ChimeraFanfare.Disc, ink, sortingOrder),
                    creamSr = Sr("Cloud", ChimeraFanfare.Disc, cream, sortingOrder + 1),
                    home = new Vector2(Mathf.Cos(a) * cloudRadius.x * r, Mathf.Sin(a) * cloudRadius.y * r),
                    size = Random.Range(0.62f, 0.95f) * (i == 0 ? 1.4f : 1f),
                    seed = Random.value * 10f,
                });
            }

            // The brawl: limbs, stars, lightning, outward speed lines, words.
            var limbOrder = Shuffled(limbs);
            for (int i = 0; i < 6 && limbOrder.Count > 0; i++)
            {
                float s = slamAt + 0.04f + i * 0.09f;
                bool flies = i == 2 || i == 5; // every so often a part gets flung clean out of the cloud
                Add(flies ? Kind.FlyingLimb : Kind.Limb, limbOrder[i % limbOrder.Count], Dir(Random.value), s, s + (flies ? 0.45f : 0.26f),
                    0.55f, Random.Range(500f, 900f) * (Random.value < 0.5f ? -1 : 1), flies ? sortingOrder + 4 : sortingOrder - 1, Color.white);
            }
            for (int i = 0; i < stars.Length * 2 && stars.Length > 0; i++)
                Add(Kind.Star, stars[i % stars.Length], Dir(i / (stars.Length * 2f)), slamAt + i * 0.06f, burstAt, 0.4f, 420f * (i % 2 == 0 ? 1 : -1), sortingOrder + 3, Color.white);
            if (lightning)
                for (int i = 0; i < 3; i++)
                {
                    float s = slamAt + 0.1f + i * 0.17f;
                    Add(Kind.Lightning, lightning, new Vector2(i % 2 == 0 ? -1f : 1f, Random.Range(-0.2f, 0.7f)).normalized, s, s + 0.12f, 0.62f, 0f, sortingOrder + 2, Color.white);
                }
            for (int i = 0; i < speedLines.Length * 2 && speedLines.Length > 0; i++)
                Add(Kind.SpeedOut, speedLines[i % speedLines.Length], Dir(Random.value), slamAt + i * 0.04f, burstAt, 0.8f, 0f, sortingOrder - 2, Color.white);
            for (int i = 0; i < 3; i++)
            {
                float s = slamAt + 0.02f + i * 0.17f;
                var dir = new Vector2(i % 2 == 0 ? -0.9f : 0.9f, Random.Range(0.2f, 0.9f)).normalized;
                AddWord(Words[Random.Range(0, Words.Length)], dir, s, s + 0.32f);
            }
            if (lettering) Add(Kind.Lettering, lettering, new Vector2(0.55f, 1f).normalized, burstAt - 0.2f, duration, 1.6f, 0f, sortingOrder + 6, Color.white);
        }

        private void Burst()
        {
            burstDone = true;
            SoundManager.Play(Sfx.Land);
            float t = burstAt;
            Add(Kind.Flash, SoftGlow, Vector2.zero, t, t + 0.22f, 3.4f, 0f, sortingOrder + 8, Color.white);
            Add(Kind.Aura, SoftGlow, Vector2.zero, t, duration, 2.6f, 0f, behindOrder, glow);
            for (int i = 0; i < 2; i++)
                Add(Kind.Ring, ChimeraFanfare.Ring, Vector2.zero, t + i * 0.08f, t + 0.42f + i * 0.08f, 3.2f + i, 0f, sortingOrder + 7, i == 0 ? Color.white : glow);
            for (int i = 0; i < 10; i++)
                Add(Kind.Ray, ChimeraFanfare.Wedge, Dir(i / 10f), t, duration, 2.1f, 70f, behindOrder, glow);
            for (int i = 0; i < 12; i++)
                Add(Kind.Spark, ChimeraFanfare.SparkSprite, Dir(i / 12f + Random.Range(-0.03f, 0.03f)), t, t + Random.Range(0.35f, 0.55f), Random.Range(0.18f, 0.32f), Random.Range(-400f, 400f), sortingOrder + 7, i % 3 == 0 ? Color.white : glow);
            for (int i = 0; i < 9 && puffs.Length > 0; i++)
                Add(Kind.Puff, puffs[Random.Range(0, puffs.Length)], Dir(i / 9f + Random.Range(-0.04f, 0.04f)), t, t + 0.42f, Random.Range(0.2f, 0.36f), Random.Range(-260f, 260f), sortingOrder + 1, Color.white);

            if (outline) outline.Pulse(0.55f);
            if (hitStop > 0f && Time.timeScale > 0f)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                frozenUntil = Now + hitStop;
            }
        }

        private void LateUpdate()
        {
            if (frozenUntil > 0f && Now >= frozenUntil) { Time.timeScale = savedTimeScale; frozenUntil = -1f; }
            if (!active) return;
            float t = Now - startedAt;
            if (t >= duration + 0.1f) { Stop(); return; }
            if (!burstDone && t >= burstAt) Burst();

            // The shake ramps up through the brawl, then snaps still on the burst.
            float brawl = Mathf.InverseLerp(slamAt, burstAt, t);
            float rumble = t < slamAt || t >= burstAt ? 0f : Mathf.Lerp(0.025f, 0.085f, brawl * brawl);
            root.localPosition = Center + new Vector3(Mathf.Sin(t * 61f) * rumble, Mathf.Sin(t * 47f + 1f) * rumble, 0f);
            root.localRotation = Quaternion.Euler(0f, 0f, rumble * 60f * Mathf.Sin(t * 38f));

            if (t >= nextBump && t < burstAt && balls.Count > 0)
            {
                balls[Random.Range(0, balls.Count)].bump = 1f; // a fist hits the inside of the cloud
                nextBump = t + Random.Range(0.06f, 0.11f);
            }

            float burst = Mathf.Clamp01((t - burstAt) / 0.3f);
            foreach (var b in balls)
            {
                float grow = t < slamAt ? 0f : BackOut(Mathf.Clamp01((t - slamAt) / 0.13f), 3f);
                b.bump = Mathf.MoveTowards(b.bump, 0f, Time.unscaledDeltaTime * 7f);
                float boil = 1f + 0.08f * Mathf.Sin(t * 26f + b.seed * 3f) + 0.3f * b.bump;
                float s = b.size * grow * boil * (1f + 0.6f * burst);
                var pos = b.home * (1f + 1.3f * burst) + 0.03f * new Vector2(Mathf.Sin(t * 33f + b.seed), Mathf.Cos(t * 29f + b.seed));
                Place(b.inkSr, pos, s + 0.12f, 1f - burst);
                Place(b.creamSr, pos, s, 1f - burst);
            }

            foreach (var bit in bits) Animate(bit, t);

            if (visual)
            {
                float y;
                if (t < slamAt) y = Mathf.Lerp(1f, 0.72f, t / slamAt) + 0.04f * Mathf.Sin(t * 70f); // crouch & tremble
                else if (t < burstAt) y = 0.85f;
                else y = Mathf.LerpUnclamped(0.55f, 1f, BackOut(Mathf.Clamp01((t - burstAt) / 0.34f), 3.2f));
                var scale = visual.localScale; scale.y = visualBaseY * y; visual.localScale = scale;
            }
        }

        private void Animate(Bit bit, float t)
        {
            bool on = t >= bit.start && t <= bit.end;
            if (bit.sr) bit.sr.enabled = on;
            if (bit.text) bit.text.enabled = on;
            if (!on) return;
            float life = Mathf.InverseLerp(bit.start, bit.end, t);
            float age = t - bit.start;
            Vector2 edge = new(bit.dir.x * cloudRadius.x, bit.dir.y * cloudRadius.y);
            float angle = Mathf.Atan2(bit.dir.y, bit.dir.x) * Mathf.Rad2Deg;
            var tr = bit.sr ? bit.sr.transform : bit.text.transform;

            switch (bit.kind)
            {
                case Kind.Gather:
                    Place(bit.sr, Vector2.zero, bit.size * (1.2f - 0.6f * life), 0.55f * Mathf.Sin(Mathf.PI * life));
                    break;
                case Kind.SpeedIn: // lines rush toward the centre
                    Place(bit.sr, edge * Mathf.Lerp(2.3f, 0.6f, Ease(life)), bit.size, Mathf.Sin(Mathf.PI * life));
                    tr.localRotation = Quaternion.Euler(0f, 0f, angle);
                    break;
                case Kind.Limb:
                {
                    float out_ = Mathf.Sin(Mathf.PI * life);
                    Place(bit.sr, edge * (0.7f + 0.5f * out_), bit.size * (0.5f + 0.5f * out_), 1f);
                    tr.localRotation = Quaternion.Euler(0f, 0f, angle - 90f + 35f * Mathf.Sin(t * 34f + bit.seed));
                    break;
                }
                case Kind.FlyingLimb: // flung out spinning, arcs and drops
                {
                    var p = edge * (0.8f + 2.2f * life) + Vector2.down * (2.2f * life * life);
                    Place(bit.sr, p, bit.size, 1f - Mathf.Pow(life, 3f));
                    tr.localRotation = Quaternion.Euler(0f, 0f, bit.spin * age);
                    break;
                }
                case Kind.Star:
                {
                    float a = angle * Mathf.Deg2Rad + age * 6f;
                    var p = new Vector2(Mathf.Cos(a) * cloudRadius.x * 1.3f, Mathf.Sin(a) * cloudRadius.y * 1.22f);
                    Place(bit.sr, p, bit.size * BackOut(Mathf.Clamp01(age * 6f), 2.4f), 1f);
                    tr.localRotation = Quaternion.Euler(0f, 0f, bit.spin * age);
                    break;
                }
                case Kind.Lightning:
                    Place(bit.sr, edge * 1.2f, bit.size * (1f + 0.2f * (1f - life)), Mathf.Sin(t * 120f) > -0.4f ? 1f : 0.15f);
                    tr.localRotation = Quaternion.Euler(0f, 0f, angle - 30f);
                    break;
                case Kind.SpeedOut:
                    Place(bit.sr, edge * (1.2f + 0.25f * Mathf.Sin(t * 22f + bit.seed)), bit.size * (0.8f + 0.3f * Mathf.Sin(t * 24f + bit.seed)), 0.9f);
                    tr.localRotation = Quaternion.Euler(0f, 0f, angle + 180f);
                    break;
                case Kind.Word:
                {
                    float pop = BackOut(Mathf.Clamp01(age / 0.12f), 3f);
                    tr.localPosition = edge * 1.35f + 0.03f * new Vector2(Mathf.Sin(t * 70f), Mathf.Cos(t * 63f));
                    tr.localScale = Vector3.one * bit.size * pop;
                    tr.localRotation = Quaternion.Euler(0f, 0f, bit.spin + 6f * Mathf.Sin(t * 30f));
                    bit.text.alpha = Mathf.Clamp01((1f - life) * 4f);
                    break;
                }
                case Kind.Lettering:
                {
                    float pop = BackOut(Mathf.Clamp01(age / 0.14f), 3f);
                    var p = new Vector2(cloudRadius.x * 0.85f, cloudRadius.y * 1.35f) + 0.03f * new Vector2(Mathf.Sin(t * 70f), Mathf.Cos(t * 64f));
                    Place(bit.sr, p, bit.size * pop, Mathf.Clamp01((1f - life) * 3f));
                    tr.localRotation = Quaternion.Euler(0f, 0f, 8f + 4f * Mathf.Sin(t * 20f));
                    break;
                }
                case Kind.Flash: // white pop on the burst
                    Place(bit.sr, Vector2.zero, bit.size * (0.6f + 0.6f * life), 1f - life);
                    break;
                case Kind.Ring: // shockwave
                    Place(bit.sr, Vector2.zero, bit.size * Ease(life), (1f - life) * 0.9f);
                    break;
                case Kind.Ray: // glowing rays turning slowly behind the reveal
                {
                    float fade = Mathf.Min(1f, life * 6f) * (1f - life);
                    Place(bit.sr, Vector2.zero, bit.size * (0.7f + 0.3f * Ease(Mathf.Min(1f, life * 3f))), 0.55f * fade);
                    tr.localRotation = Quaternion.Euler(0f, 0f, angle + bit.spin * age);
                    break;
                }
                case Kind.Aura: // soft yellow glow around the new chimera
                    Place(bit.sr, Vector2.zero, bit.size * (0.9f + 0.1f * Mathf.Sin(age * 14f)), 0.6f * Mathf.Min(1f, life * 8f) * (1f - life));
                    break;
                case Kind.Spark:
                    Place(bit.sr, edge * (0.6f + 2.1f * Mathf.Sqrt(life)), bit.size * (1f - 0.6f * life), 1f - life * life);
                    tr.localRotation = Quaternion.Euler(0f, 0f, bit.spin * age);
                    break;
                case Kind.Puff:
                    Place(bit.sr, edge * (0.8f + 1.6f * Mathf.Sqrt(life)), bit.size * (1f - 0.5f * life), 1f - life);
                    tr.localRotation = Quaternion.Euler(0f, 0f, bit.spin * life);
                    break;
            }
        }

        private void Add(Kind kind, Sprite sprite, Vector2 dir, float start, float end, float size, float spin, int order, Color color)
        {
            if (!sprite) return;
            var sr = Sr(kind.ToString(), sprite, color, order);
            sr.enabled = false;
            bits.Add(new Bit { sr = sr, dir = dir, start = start, end = end, size = size, spin = spin, seed = Random.value * 10f, kind = kind });
        }

        private void AddWord(string word, Vector2 dir, float start, float end)
        {
            var text = new GameObject(word).AddComponent<TextMeshPro>();
            text.transform.SetParent(root, false);
            if (font) text.font = font;
            if (!wordMaterial)
            {
                wordMaterial = new Material(text.fontSharedMaterial) { name = "Combine Word (outlined)" };
                wordMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
                wordMaterial.SetColor(ShaderUtilities.ID_OutlineColor, ink);
                wordMaterial.EnableKeyword(ShaderUtilities.Keyword_Outline);
            }
            text.fontSharedMaterial = wordMaterial;
            text.text = word;
            text.fontSize = 4.4f;
            text.fontStyle = FontStyles.Bold | FontStyles.Italic;
            text.color = glow;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = new Vector2(3f, 1f);
            text.sortingOrder = sortingOrder + 6;
            text.enabled = false;
            bits.Add(new Bit { text = text, dir = dir, start = start, end = end, size = 1f, spin = Random.Range(-14f, 14f), kind = Kind.Word });
        }

        private SpriteRenderer Sr(string name, Sprite sprite, Color color, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(root, false);
            sr.sprite = sprite; sr.color = color; sr.sortingOrder = order;
            return sr;
        }

        private static Vector2 Dir(float turns) => new(Mathf.Cos(turns * Mathf.PI * 2f), Mathf.Sin(turns * Mathf.PI * 2f));

        private static List<Sprite> Shuffled(Sprite[] source)
        {
            var list = new List<Sprite>(source);
            list.RemoveAll(s => !s);
            for (int i = list.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (list[i], list[j]) = (list[j], list[i]); }
            return list;
        }

        // Size is the sprite's longest side in world units.
        private static void Place(SpriteRenderer sr, Vector2 pos, float size, float alpha)
        {
            sr.transform.localPosition = pos;
            float longest = sr.sprite ? Mathf.Max(sr.sprite.bounds.size.x, sr.sprite.bounds.size.y) : 1f;
            sr.transform.localScale = Vector3.one * (Mathf.Max(0f, size) / Mathf.Max(0.0001f, longest));
            var c = sr.color; c.a = Mathf.Clamp01(alpha); sr.color = c;
        }

        private static float Ease(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

        private static float BackOut(float x, float c)
        {
            float u = x - 1f;
            return 1f + (c + 1f) * u * u * u + c * u * u;
        }

        // Soft radial glow (white, fades to clear), 1 unit across.
        private static Sprite SoftGlow
        {
            get
            {
                if (softGlow) return softGlow;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + .5f) / n * 2 - 1, v = (y + .5f) / n * 2 - 1;
                        float d = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * d * d));
                    }
                tex.SetPixels32(px); tex.Apply();
                softGlow = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(.5f, .5f), n);
                return softGlow;
            }
        }

        private void Stop()
        {
            active = false;
            if (frozenUntil > 0f) { Time.timeScale = savedTimeScale; frozenUntil = -1f; }
            if (visual) { var s = visual.localScale; s.y = visualBaseY; visual.localScale = s; }
            if (root) Destroy(root.gameObject);
            root = null;
            balls.Clear(); bits.Clear();
        }

        private void OnDestroy()
        {
            if (wordMaterial) Destroy(wordMaterial);
        }
    }
}
