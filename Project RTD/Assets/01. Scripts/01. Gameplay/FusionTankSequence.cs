using System;
using System.Collections.Generic;
using System.Linq;
using ProjectRTD.Core;
using TMPro;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    /// <summary>
    /// Fusion tank: every head / body / leg part whirls round the water tank in its own ring (blub-blub bubbles),
    /// "STOP!!" slams in and the rings brake one by one onto the picked parts, then "FUSION!!" snaps the three
    /// together into the chimera.  Art: Resources/FusionTank (wall, floor, tank, banners) + the part catalog sprites.
    ///   Play(build, done)  plays it for a given result,  Play() picks a random one.
    /// Everything is drawn in this object's local space (16:9, 19.2 × 10.8 units around its position).
    /// </summary>
    public sealed class FusionTankSequence : MonoBehaviour
    {
        [SerializeField] private PartCatalog catalog;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool loop = false;
        [SerializeField] private float spinSeconds = 1.8f;
        [SerializeField] private string sortingLayer = "Default";
        [SerializeField] private int sortingOrder = 0;

        public bool Playing { get; private set; }
        public event Action<PlayerPartBuild> Fused;

        // layout (local units)
        const float PartScale = .9f, FusedScale = 1.45f, RingRadius = 3.0f, TankY = -.35f;
        static readonly float[] RowY = { 1.75f, .35f, -1.15f };        // head, body, legs ring heights while separated
        static readonly float[] RowSpeed = { 1.25f, -1.05f, 1.45f };   // turns per second while whirling
        const float StopAt = 0f, BrakeSeconds = .9f, BrakeStagger = .28f;

        enum Phase { Idle, Spin, Brake, Fuse, Done }

        class Item { public AnimalPartDefinition def; public SpriteRenderer sr; public float angle0; public int row; public bool chosen; }
        class Ring { public List<Item> items = new List<Item>(); public float angle, speed, brakeFrom, brakeTo, brakeStart; public bool stopped; public Item pick; }
        class Bubble { public SpriteRenderer sr; public Vector2 p; public float r, vy, phase, t0, life; }

        readonly Ring[] rings = { new Ring(), new Ring(), new Ring() };
        readonly List<Bubble> bubbles = new List<Bubble>();
        Transform stage, partsRoot;
        SpriteRenderer flash;
        (Transform root, SpriteRenderer sr, TextMeshPro label) stopBanner, fusionBanner;
        Phase phase;
        float t, phaseAt, nextBubble;
        PlayerPartBuild result;
        Action onDone;
        static Sprite disc;

        void Start()
        {
            Build();
            if (playOnStart) Play();
        }

        // ------------------------------------------------------------------ build
        static Sprite Art(string n) => Resources.Load<Sprite>("FusionTank/" + n);

        static Sprite Disc
        {
            get
            {
                if (disc) return disc;
                const int N = 64; var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
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

        SpriteRenderer Sr(string name, Sprite s, Transform parent, Vector2 pos, float scale, int order)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent, false);
            sr.transform.localPosition = pos;
            sr.transform.localScale = Vector3.one * scale;
            sr.sprite = s; sr.sortingLayerName = sortingLayer; sr.sortingOrder = sortingOrder + order;
            return sr;
        }

        void Build()
        {
            if (stage) return;
            stage = new GameObject("Fusion Stage").transform;
            stage.SetParent(transform, false);
            var wall = Art("wall"); var floor = Art("floor"); var tank = Art("tank");
            if (wall) Sr("wall", wall, stage, new Vector2(0f, 1.55f), 19.8f / wall.rect.width * 100f, 0);
            if (floor) Sr("floor", floor, stage, new Vector2(0f, -3.6f), 21f / floor.rect.width * 100f, 1);
            // a soft cyan water body sits behind the parts, the glass goes over them
            var water = Sr("water", Disc, stage, new Vector2(0f, TankY + .3f), 1f, 2);
            water.transform.localScale = new Vector3(9.2f, 6.4f, 1f);
            water.color = new Color(.45f, .9f, 1f, .22f);
            partsRoot = new GameObject("Parts").transform;
            partsRoot.SetParent(stage, false);
            if (tank) Sr("tank", tank, stage, new Vector2(0f, TankY), 10.6f / tank.rect.width * 100f, 3);   // parts float in front of the glass, crisp
            flash = Sr("flash", Disc, stage, new Vector2(0f, .2f), 0f, 70);
            flash.color = new Color(1f, 1f, 1f, 0f);
            stopBanner = Banner("STOP!!", "banner-stop", new Color32(0xff, 0xff, 0xff, 0xff));
            fusionBanner = Banner("FUSION!!", "banner-fusion", new Color32(0xff, 0xff, 0xff, 0xff));
        }

        (Transform, SpriteRenderer, TextMeshPro) Banner(string text, string art, Color textColor)
        {
            var root = new GameObject("Banner " + text).transform;
            root.SetParent(stage, false);
            root.localPosition = new Vector3(0f, -3.55f, 0f);
            var sp = Art(art);
            var sr = Sr("plate", sp, root, Vector2.zero, sp ? 6.6f / sp.rect.width * 100f : 1f, 80);
            var tm = new GameObject("text").AddComponent<TextMeshPro>();
            tm.transform.SetParent(root, false);
            tm.transform.localPosition = new Vector3(.1f, .1f, 0f);
            tm.transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            tm.text = text; tm.fontSize = 9f; tm.color = textColor; tm.fontStyle = FontStyles.Bold;
            tm.alignment = TextAlignmentOptions.Center; tm.textWrappingMode = TextWrappingModes.NoWrap;
            tm.rectTransform.sizeDelta = new Vector2(6f, 2f);
            tm.sortingLayerID = SortingLayer.NameToID(sortingLayer); tm.sortingOrder = sortingOrder + 81;
            root.gameObject.SetActive(false);
            return (root, sr, tm);
        }

        void Fill()
        {
            foreach (var ring in rings)
            {
                foreach (var it in ring.items) if (it.sr) Destroy(it.sr.gameObject);
                ring.items.Clear(); ring.stopped = false; ring.pick = null;
            }
            for (int row = 0; row < 3; row++)
            {
                var slot = (PartSlot)row;
                var defs = catalog ? catalog.ForSlot(slot).Where(d => d && d.Sprite).ToList() : new List<AnimalPartDefinition>();
                var want = row == 0 ? result.Head : row == 1 ? result.Body : result.Legs;
                if (want && !defs.Contains(want)) defs.Add(want);
                for (int i = 0; i < defs.Count; i++)
                {
                    var sr = Sr(defs[i].name, defs[i].Sprite, partsRoot, Vector2.zero, PartScale, 10);
                    var it = new Item { def = defs[i], sr = sr, row = row, angle0 = i * Mathf.PI * 2f / defs.Count, chosen = defs[i] == want };
                    rings[row].items.Add(it);
                    if (it.chosen) rings[row].pick = it;
                }
                rings[row].angle = UnityEngine.Random.value * Mathf.PI * 2f;
                rings[row].speed = RowSpeed[row] * Mathf.PI * 2f;
            }
        }

        // ------------------------------------------------------------------ control
        public void Play()
        {
            if (!catalog) { Debug.LogWarning("FusionTankSequence: no part catalog", this); return; }
            AnimalPartDefinition Pick(PartSlot s) { var a = catalog.ForSlot(s).ToArray(); return a.Length == 0 ? null : a[UnityEngine.Random.Range(0, a.Length)]; }
            Play(new PlayerPartBuild(Pick(PartSlot.Head), Pick(PartSlot.Body), Pick(PartSlot.Legs)), null);
        }

        public void Play(PlayerPartBuild build, Action done)
        {
            Build();
            result = build; onDone = done;
            Fill();
            stopBanner.root.gameObject.SetActive(false);
            fusionBanner.root.gameObject.SetActive(false);
            Playing = true;
            Enter(Phase.Spin);
        }

        void Enter(Phase p) { phase = p; phaseAt = t; }

        void Update()
        {
            if (!stage) return;
            float dt = Time.deltaTime;
            t += dt;
            float a = t - phaseAt;
            switch (phase)
            {
                case Phase.Spin:
                    foreach (var r in rings) r.angle += r.speed * dt;
                    if (a > spinSeconds) BeginBrake();
                    break;
                case Phase.Brake:
                    bool all = true;
                    for (int i = 0; i < 3; i++)
                    {
                        var r = rings[i];
                        float u = (t - r.brakeStart) / BrakeSeconds;
                        if (u < 0f) { r.angle += r.speed * dt; all = false; continue; }
                        if (u < 1f) { r.angle = Mathf.Lerp(r.brakeFrom, r.brakeTo, 1f - Mathf.Pow(1f - u, 3f)); all = false; }
                        else if (!r.stopped)
                        {
                            r.stopped = true; r.angle = r.brakeTo;
                            SoundManager.Play(Sfx.Land, .7f);
                            BurstBubbles(new Vector2(0f, RowY[i]), 5);
                        }
                    }
                    if (all && a > BrakeSeconds + BrakeStagger * 2f + .35f) { Enter(Phase.Fuse); SoundManager.Play(Sfx.Summon); }
                    break;
                case Phase.Fuse:
                    if (a > 1.8f)
                    {
                        Enter(Phase.Done);
                        Playing = false;
                        Fused?.Invoke(result);
                        onDone?.Invoke();
                        if (loop) Invoke(nameof(Replay), 1.2f);
                    }
                    break;
            }
            Pose();
            Banners();
            Bubbles(dt);
        }

        void Replay() => Play();

        void BeginBrake()
        {
            Enter(Phase.Brake);
            SoundManager.Play(Sfx.Confirm);
            for (int i = 0; i < 3; i++)
            {
                var r = rings[i];
                r.brakeStart = t + i * BrakeStagger;
                float extra = r.speed * (i * BrakeStagger);           // it keeps spinning until its brake starts
                r.brakeFrom = r.angle + extra;
                // land so that pick.angle0 + angle == StopAt (front), at least ~3/4 turn of travel in the spin direction
                float target = StopAt - (r.pick != null ? r.pick.angle0 : 0f);
                float dir = Mathf.Sign(r.speed), travel = Mathf.PI * 1.5f;
                float k = Mathf.Ceil((dir * (r.brakeFrom - target) + travel) / (Mathf.PI * 2f));
                r.brakeTo = target + dir * k * Mathf.PI * 2f;
                if (dir * (r.brakeTo - r.brakeFrom) < travel) r.brakeTo += dir * Mathf.PI * 2f;
            }
        }

        // ------------------------------------------------------------------ pose
        void Pose()
        {
            float fuseU = phase == Phase.Fuse ? Mathf.Clamp01((t - phaseAt - .25f) / .45f) : phase == Phase.Done ? 1f : 0f;
            float snap = Back(fuseU);
            for (int row = 0; row < 3; row++)
            {
                var r = rings[row];
                foreach (var it in r.items)
                {
                    float ang = it.angle0 + r.angle;
                    float depth = Mathf.Cos(ang);                         // 1 = front of the tank, -1 = back
                    float x = Mathf.Sin(ang) * RingRadius;
                    float bob = Mathf.Sin(t * 3.1f + it.angle0 * 2.3f + row) * .12f;   // blub-blub drift
                    float s = PartScale * (.72f + .28f * (depth * .5f + .5f));
                    float alpha = .45f + .55f * (depth * .5f + .5f);
                    float rot = Mathf.Sin(t * 2.2f + it.angle0 * 3f) * 8f;
                    var p = new Vector2(x, RowY[row] + bob);

                    if (r.stopped && !it.chosen)
                    {
                        // the others sink back into the murk
                        float g = Mathf.Clamp01((t - (r.brakeStart + BrakeSeconds)) / .35f);
                        alpha *= 1f - g; s *= 1f - .4f * g;
                    }
                    if (it.chosen && (phase == Phase.Fuse || phase == Phase.Done))
                    {
                        // separated rows close up so the three sprites (shared 512 frame) meet as one chimera
                        float sep = (1f - snap);
                        p = new Vector2(x * sep, Mathf.LerpUnclamped(RowY[row] + bob, (row - 1) * 0f + FusedY(row), snap));
                        float sq = phase == Phase.Fuse ? Mathf.Sin(Mathf.Clamp01((t - phaseAt - .7f) / .35f) * Mathf.PI) * .14f : 0f;
                        s = Mathf.Lerp(PartScale, FusedScale, snap) * (1f + sq); alpha = 1f; rot *= 1f - snap;
                        it.sr.transform.localScale = new Vector3(s * (1f + sq * .6f), s * (1f - sq), 1f);
                        if (phase == Phase.Done) p.y += Mathf.Sin(t * 3f) * .08f;
                    }
                    else it.sr.transform.localScale = new Vector3(s, s, 1f);
                    it.sr.transform.localPosition = p;
                    it.sr.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
                    var c = it.sr.color; c.a = alpha; it.sr.color = c;
                    // front of the ring draws over the back; legs < body < head so the chimera stacks right
                    it.sr.sortingOrder = sortingOrder + 10 + (int)((depth + 1f) * 10f) + (it.chosen && fuseU > 0f ? 30 + (row == 0 ? 2 : row == 1 ? 1 : 0) : 0);
                }
            }
            // white flash when the parts lock together
            float fa = phase == Phase.Fuse ? t - phaseAt - .7f : -1f;
            if (fa >= 0f && fa < .5f)
            {
                float k = fa / .5f;
                flash.transform.localScale = Vector3.one * (1f + 7f * Mathf.Sqrt(k));
                flash.color = new Color(1f, 1f, 1f, .85f * (1f - k));
                if (fa < Time.deltaTime * 1.5f) BurstBubbles(new Vector2(0f, .2f), 18);
            }
            else flash.color = new Color(1f, 1f, 1f, 0f);
        }

        // the three parts share one 512 px frame: at rest they overlap exactly (local y = body row)
        static float FusedY(int row) => RowY[1] + .1f;

        void Banners()
        {
            bool showStop = phase == Phase.Brake, showFusion = phase == Phase.Fuse || phase == Phase.Done;
            Slam(stopBanner.root, showStop, -1f);
            Slam(fusionBanner.root, showFusion, 1f);
        }

        void Slam(Transform b, bool on, float side)
        {
            if (b.gameObject.activeSelf != on) b.gameObject.SetActive(on);
            if (!on) return;
            float a = t - phaseAt, u = Mathf.Clamp01(a / .28f);
            float e = Back(u);
            b.localPosition = new Vector3((1f - e) * side * -9f, -3.55f + Mathf.Sin(t * 9f) * .03f * u, 0f);
            float s = Mathf.LerpUnclamped(1.5f, 1f, e) * (1f + .03f * Mathf.Sin(t * 14f));
            b.localScale = new Vector3(s, s, 1f);
            b.localRotation = Quaternion.Euler(0f, 0f, (1f - e) * side * 12f);
        }

        // ------------------------------------------------------------------ bubbles (round, outline-free, one colour)
        void Bubbles(float dt)
        {
            if (Playing && t >= nextBubble)
            {
                nextBubble = t + (phase == Phase.Spin ? .05f : .12f);
                Spawn(new Vector2(UnityEngine.Random.Range(-4.2f, 4.2f), -2.6f), UnityEngine.Random.Range(.06f, .2f), UnityEngine.Random.Range(1.4f, 2.6f));
            }
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var b = bubbles[i]; float a = t - b.t0;
                if (a > b.life) { Destroy(b.sr.gameObject); bubbles.RemoveAt(i); continue; }
                b.p.y += b.vy * dt;
                float wob = Mathf.Sin(t * 6f + b.phase) * .08f;
                b.sr.transform.localPosition = new Vector2(b.p.x + wob, b.p.y);
                float pop = a > b.life - .12f ? 1f + (a - (b.life - .12f)) * 4f : Mathf.Clamp01(a / .1f);
                b.sr.transform.localScale = Vector3.one * b.r * 2f * pop;
                b.sr.color = new Color(1f, 1f, 1f, a > b.life - .12f ? .55f * (1f - (a - (b.life - .12f)) / .12f) : .55f);
            }
        }

        void BurstBubbles(Vector2 at, int n)
        {
            for (int i = 0; i < n; i++)
                Spawn(at + UnityEngine.Random.insideUnitCircle * .8f, UnityEngine.Random.Range(.07f, .22f), UnityEngine.Random.Range(1.8f, 3.4f));
        }

        void Spawn(Vector2 p, float r, float vy)
        {
            var sr = Sr("bubble", Disc, partsRoot, p, 0f, 50);
            bubbles.Add(new Bubble { sr = sr, p = p, r = r, vy = vy, phase = UnityEngine.Random.value * 6f, t0 = t, life = Mathf.Max(.4f, (2.9f - p.y) / vy) });
        }

        static float Back(float u) { const float c1 = 1.7f, c3 = c1 + 1f; float v = u - 1f; return 1f + c3 * v * v * v + c1 * v * v; }
    }
}
