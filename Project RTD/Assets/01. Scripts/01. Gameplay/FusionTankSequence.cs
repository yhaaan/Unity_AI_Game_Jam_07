using System;
using System.Collections.Generic;
using System.Linq;
using ProjectRTD.Core;
using TMPro;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    /// <summary>
    /// Fusion tank show (look-and-feel piece, not wired into gameplay):
    ///   1 Drop    – every part plops into the water one after another (splash, ripples, slows down sinking)
    ///   2 Whirl   – three rings of parts spin up into a whirlpool: they speed up, tilt, a bubble vortex forms, the view shakes
    ///   3 STOP!!  – hit-stop, red flash, each ring brakes with a springy overshoot; the pick glints, the rest get sucked down
    ///   4 Gather  – the three picks spiral round each other faster and faster into the centre
    ///   5 FUSION!!– white flash, shockwave, bubble burst, camera punch, squash-and-stretch → the chimera floats, proud
    /// Art: Resources/FusionTank (wall, floor, tank, banners) + the part catalog sprites. Local space is 19.2 × 10.8.
    /// </summary>
    public sealed class FusionTankSequence : MonoBehaviour
    {
        [SerializeField] private PartCatalog catalog;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool loop = false;
        [SerializeField] private float spinSeconds = 2.2f;
        [SerializeField] private string sortingLayer = "Default";
        [SerializeField] private int sortingOrder = 0;

        public bool Playing { get; private set; }
        public event Action<PlayerPartBuild> Fused;

        const float PartScale = .9f, FusedScale = 1.4f, RingRadius = 2.6f, TankY = -.35f, SurfaceY = 2.4f;
        static readonly float[] RowY = { 1.0f, -.3f, -1.6f };
        static readonly float[] RowDir = { 1f, -1f, 1f };
        const float TopSpeed = 2.1f;
        const float DropSeconds = 1.6f, BrakeSeconds = .95f, BrakeStagger = .3f, GatherSeconds = .9f, FuseSeconds = 2.2f;

        enum Phase { Idle, Drop, Whirl, Brake, Gather, Fuse, Done }

        class Item
        {
            public AnimalPartDefinition def; public SpriteRenderer sr; public float angle0, dropAt; public int row; public bool chosen, splashed;
        }
        class Ring { public List<Item> items = new List<Item>(); public float angle, speed, brakeFrom, brakeTo, brakeStart; public bool stopped; public Item pick; }
        class Dot { public SpriteRenderer sr; public Vector2 p, v; public float r, phase, t0, life, grow; public bool bubble; public Color c; }

        readonly Ring[] rings = { new Ring(), new Ring(), new Ring() };
        readonly List<Dot> dots = new List<Dot>();
        readonly List<SpriteRenderer> surface = new List<SpriteRenderer>(), caustics = new List<SpriteRenderer>();
        Transform cam, stage, partsRoot;
        SpriteRenderer flash, waterTint, glintA, glintB;
        (Transform root, SpriteRenderer sr, TextMeshPro label) stopBanner, fusionBanner;
        Phase phase;
        float t, phaseAt, nextBubble, stepDt, hitStop, shake, punch, surfaceKick, whirl, glintT = -10f;
        Vector2 glintAt;
        PlayerPartBuild result;
        Action onDone;
        static Sprite disc, bubbleSprite;
        Material partMat, waterMat;
        readonly List<Material> ownedMats = new List<Material>();
        readonly List<SpriteRenderer> rays = new List<SpriteRenderer>(), motes = new List<SpriteRenderer>();
        SpriteRenderer surfacePlane;

        void Start() { Build(); if (playOnStart) Play(); }

        // ================================================================== build
        static Sprite Art(string n) => Resources.Load<Sprite>("FusionTank/" + n);

        /// <summary>glossy bubble: clear middle, brighter rim, white highlight top-left</summary>
        static Sprite BubbleSprite
        {
            get
            {
                if (bubbleSprite) return bubbleSprite;
                const int N = 64; var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
                var px = new Color32[N * N];
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float dx = (x + .5f) / N * 2f - 1f, dy = (y + .5f) / N * 2f - 1f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float edge = Mathf.Clamp01((1f - d) * N * .5f);
                    float body = Mathf.Lerp(.18f, .75f, Mathf.Pow(Mathf.Clamp01(d), 4f));
                    float hx = dx + .38f, hy = dy - .38f, hl = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / .22f);
                    float a = Mathf.Max(body, hl) * edge;
                    byte v = (byte)(255 * Mathf.Lerp(.85f, 1f, hl));
                    px[y * N + x] = new Color32(v, 255, 255, (byte)(255 * a));
                }
                tex.SetPixels32(px); tex.Apply();
                return bubbleSprite = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), N);
            }
        }

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

        SpriteRenderer Sr(string name, Sprite s, Transform parent, Vector2 pos, float scale, int order, bool masked = false)
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
            cam = new GameObject("Fusion Camera Rig").transform;
            cam.SetParent(transform, false);
            stage = new GameObject("Fusion Stage").transform;
            stage.SetParent(cam, false);
            var wall = Art("wall"); var floor = Art("floor"); var tank = Art("tank");
            if (wall) Sr("wall", wall, stage, new Vector2(0f, 1.55f), 20.6f / wall.rect.width * 100f, 0);
            if (floor) Sr("floor", floor, stage, new Vector2(0f, -3.6f), 21.6f / floor.rect.width * 100f, 1);
            partsRoot = new GameObject("Parts").transform;
            partsRoot.SetParent(stage, false);
            if (tank)
            {
                // the tube is built from the tank art three times: back glass (dim) → parts → water → front glass
                float k = 10.6f / tank.rect.width * 100f;
                var back = Sr("tank back", tank, stage, new Vector2(0f, TankY), k, 3);
                back.color = new Color(.75f, .85f, 1f, 1f);
                var sh = Resources.Load<Shader>("FusionTank/UnderwaterSprite");
                if (sh)
                {
                    partMat = new Material(sh) { name = "Underwater Parts" };
                    partMat.SetFloat("_Wave", .006f); partMat.SetFloat("_Caustic", .2f); partMat.SetFloat("_TintAmt", .28f);
                    waterMat = new Material(sh) { name = "Water Body" };
                    waterMat.SetFloat("_Wave", .003f); waterMat.SetFloat("_Caustic", .55f); waterMat.SetFloat("_TintAmt", 0f);
                    waterMat.SetFloat("_CausticScale", 1.1f);
                }
                waterTint = Sr("water", tank, stage, new Vector2(0f, TankY), k, 55);            // same silhouette, tinted cyan
                if (waterMat) { waterMat.mainTexture = tank.texture; waterTint.sharedMaterial = waterMat; }
                // the water's top seen a little from above
                surfacePlane = Sr("surface plane", Disc, stage, new Vector2(0f, SurfaceY + TankY), 1f, 57);
                surfacePlane.color = new Color(.8f, .97f, 1f, .32f);
                // floating specks
                for (int i = 0; i < 26; i++)
                {
                    var m = Sr("mote", Disc, stage, Vector2.zero, .05f + (i % 3) * .025f, 54);
                    m.color = new Color(1f, 1f, 1f, .35f);
                    motes.Add(m);
                }
                var deep = Sr("water deep", tank, stage, new Vector2(0f, TankY - .5f), k * .96f, 56);
                deep.color = new Color(.05f, .35f, .75f, .16f);
                for (int i = 0; i < 24; i++) { var w = Sr("surface", Disc, stage, Vector2.zero, 1f, 57); w.color = new Color(.85f, 1f, 1f, .55f); surface.Add(w); }
                for (int i = 0; i < 6; i++) { var c = Sr("caustic", Disc, stage, Vector2.zero, 1f, 54); c.color = new Color(1f, 1f, 1f, .07f); caustics.Add(c); }
                var front = Sr("tank front", tank, stage, new Vector2(0f, TankY), k, 58);       // glass highlights over the parts
                front.color = new Color(1f, 1f, 1f, .42f);
            }
            glintA = Sr("glint", Disc, stage, Vector2.zero, 0f, 62); glintB = Sr("glint", Disc, stage, Vector2.zero, 0f, 62);
            flash = Sr("flash", Disc, stage, new Vector2(0f, .2f), 0f, 70);
            flash.color = Color.clear;
            stopBanner = Banner("STOP!!", "banner-stop");
            fusionBanner = Banner("FUSION!!", "banner-fusion");
        }

        (Transform, SpriteRenderer, TextMeshPro) Banner(string text, string art)
        {
            var root = new GameObject("Banner " + text).transform;
            root.SetParent(transform, false);                    // banners stay put while the stage shakes
            var sp = Art(art);
            var sr = Sr("plate", sp, root, Vector2.zero, sp ? 6.6f / sp.rect.width * 100f : 1f, 80);
            var tm = new GameObject("text").AddComponent<TextMeshPro>();
            tm.transform.SetParent(root, false);
            tm.transform.localPosition = new Vector3(.1f, .1f, 0f);
            tm.transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            tm.text = text; tm.fontSize = 9f; tm.color = Color.white; tm.fontStyle = FontStyles.Bold;
            tm.alignment = TextAlignmentOptions.Center; tm.textWrappingMode = TextWrappingModes.NoWrap;
            tm.rectTransform.sizeDelta = new Vector2(6f, 2f);
            tm.sortingLayerID = SortingLayer.NameToID(sortingLayer); tm.sortingOrder = sortingOrder + 81;
            root.gameObject.SetActive(false);
            return (root, sr, tm);
        }

        void Fill()
        {
            foreach (var m in ownedMats) if (m) Destroy(m);
            ownedMats.Clear();
            foreach (var ring in rings)
            {
                foreach (var it in ring.items) if (it.sr) Destroy(it.sr.gameObject);
                ring.items.Clear(); ring.stopped = false; ring.pick = null;
            }
            int n = 0;
            for (int row = 0; row < 3; row++)
            {
                var slot = (PartSlot)row;
                var defs = catalog ? catalog.ForSlot(slot).Where(d => d && d.Sprite).ToList() : new List<AnimalPartDefinition>();
                var want = row == 0 ? result.Head : row == 1 ? result.Body : result.Legs;
                if (want && !defs.Contains(want)) defs.Add(want);
                for (int i = 0; i < defs.Count; i++)
                {
                    var sr = Sr(defs[i].name, defs[i].Sprite, partsRoot, new Vector2(0f, 20f), PartScale, 10, true);
                    if (partMat)
                    {
                        // one material per sprite texture: sharing one across sprites mixes their textures up when batched
                        var m = new Material(partMat) { name = "Underwater " + defs[i].name, mainTexture = defs[i].Sprite.texture };
                        sr.sharedMaterial = m;
                        ownedMats.Add(m);
                    }
                    var it = new Item { def = defs[i], sr = sr, row = row, angle0 = i * Mathf.PI * 2f / defs.Count, chosen = defs[i] == want };
                    it.dropAt = .07f * (n++) + UnityEngine.Random.Range(0f, .05f);
                    rings[row].items.Add(it);
                    if (it.chosen) rings[row].pick = it;
                }
                rings[row].angle = UnityEngine.Random.value * Mathf.PI * 2f;
                rings[row].speed = 0f;
            }
        }

        // ================================================================== control
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
            foreach (var d in dots) if (d.sr) Destroy(d.sr.gameObject);
            dots.Clear();
            Fill();
            stopBanner.root.gameObject.SetActive(false);
            fusionBanner.root.gameObject.SetActive(false);
            Playing = true; whirl = 0f; shake = 0f; punch = 0f; hitStop = 0f;
            Enter(Phase.Drop);
        }

        void Enter(Phase p) { phase = p; phaseAt = t; }

        void Update() => Step(Time.deltaTime);

        /// <summary>advances the show by dt seconds (also used to bake previews)</summary>
        internal void Step(float dt)
        {
            if (!stage) return;
            stepDt = dt;
            if (hitStop > 0f) { hitStop -= dt; return; }        // freeze frame
            t += dt;
            float a = t - phaseAt;
            switch (phase)
            {
                case Phase.Drop:
                    whirl = Mathf.Lerp(0f, .18f, a / DropSeconds);
                    Spin(dt);
                    if (a > DropSeconds) { Enter(Phase.Whirl); SoundManager.Play(Sfx.Summon, .6f); }
                    break;
                case Phase.Whirl:
                {
                    float u = Mathf.Clamp01(a / spinSeconds);
                    whirl = Mathf.Lerp(.18f, 1f, u * u);
                    Spin(dt);
                    shake = .06f * whirl * whirl;
                    if (UnityEngine.Random.value < dt * 16f * whirl) Vortex();
                    if (a > spinSeconds) BeginBrake();
                    break;
                }
                case Phase.Brake:
                {
                    bool all = true;
                    for (int i = 0; i < 3; i++)
                    {
                        var r = rings[i];
                        float u = (t - r.brakeStart) / BrakeSeconds;
                        if (u < 0f) { r.angle += r.speed * dt; all = false; continue; }
                        if (u < 1f) { r.angle = Mathf.LerpUnclamped(r.brakeFrom, r.brakeTo, BackOut(u, 1.2f)); all = false; }
                        else if (!r.stopped)
                        {
                            r.stopped = true; r.angle = r.brakeTo;
                            SoundManager.Play(Sfx.Land, .8f);
                            shake = .12f;
                            Burst(new Vector2(0f, RowY[i]), 6, false);
                            Glint(new Vector2(0f, RowY[i] + .3f));
                        }
                    }
                    shake *= Mathf.Exp(-dt * 8f);
                    whirl = Mathf.MoveTowards(whirl, 0f, dt * 1.2f);
                    if (all && a > BrakeSeconds + BrakeStagger * 2f + .55f) { Enter(Phase.Gather); SoundManager.Play(Sfx.Glide, .8f); }
                    break;
                }
                case Phase.Gather:
                    shake = .03f + .07f * Mathf.Clamp01(a / GatherSeconds);
                    if (UnityEngine.Random.value < dt * 30f) Spiral();
                    if (a > GatherSeconds) { Enter(Phase.Fuse); Impact(); }
                    break;
                case Phase.Fuse:
                    shake *= Mathf.Exp(-dt * 6f);
                    if (a > FuseSeconds)
                    {
                        Enter(Phase.Done); Playing = false;
                        Fused?.Invoke(result); onDone?.Invoke();
                        if (loop) Invoke(nameof(Replay), 1.4f);
                    }
                    break;
                case Phase.Done:
                    shake = 0f;
                    break;
            }
            Pose();
            Effects(dt);
        }

        void Replay() => Play();

        void Spin(float dt)
        {
            for (int i = 0; i < 3; i++)
            {
                var r = rings[i];
                r.speed = RowDir[i] * TopSpeed * (1f - .15f * i) * whirl * Mathf.PI * 2f;
                r.angle += r.speed * dt;
            }
        }

        void BeginBrake()
        {
            Enter(Phase.Brake);
            hitStop = .12f; shake = .25f;
            SoundManager.Play(Sfx.Confirm);
            for (int i = 0; i < 3; i++)
            {
                var r = rings[i];
                r.brakeStart = t + i * BrakeStagger;
                r.brakeFrom = r.angle + r.speed * (i * BrakeStagger);
                float target = -(r.pick != null ? r.pick.angle0 : 0f);
                float dir = Mathf.Sign(r.speed == 0f ? 1f : r.speed), travel = Mathf.PI * 1.6f;
                float k = Mathf.Ceil((dir * (r.brakeFrom - target) + travel) / (Mathf.PI * 2f));
                r.brakeTo = target + dir * k * Mathf.PI * 2f;
            }
        }

        void Impact()
        {
            SoundManager.Play(Sfx.Goal);
            hitStop = .08f; shake = .35f; punch = 1f;
            Burst(new Vector2(0f, .3f), 26, true);
            for (int i = 0; i < 28; i++)
            {
                float ang = i / 28f * Mathf.PI * 2f;
                Add(new Vector2(0f, .3f), new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 7.5f, .16f, .55f, false, new Color(1f, 1f, 1f, .9f), -1.2f);
            }
        }

        // ================================================================== pose
        void Pose()
        {
            float a = t - phaseAt;
            float gather = phase == Phase.Gather ? Mathf.Clamp01(a / GatherSeconds) : phase >= Phase.Fuse ? 1f : 0f;
            float fuseA = phase == Phase.Fuse ? a : phase == Phase.Done ? 99f : -1f;
            float tilt = .55f * whirl;
            float radius = RingRadius * (1f - .18f * whirl);
            for (int row = 0; row < 3; row++)
            {
                var r = rings[row];
                foreach (var it in r.items)
                {
                    float ang = it.angle0 + r.angle;
                    float depth = Mathf.Cos(ang);
                    float x = Mathf.Sin(ang) * radius;
                    float bob = Mathf.Sin(t * 1.6f + it.angle0 * 2.3f + row) * .2f + Mathf.Sin(t * 3.7f + it.angle0 * 5f) * .05f;
                    var home = new Vector2(x, RowY[row] + bob - depth * tilt * (row == 1 ? .5f : 1f));
                    float s = PartScale * (.7f + .3f * (depth * .5f + .5f));
                    float alpha = .45f + .55f * (depth * .5f + .5f);
                    float rot = Mathf.Sin(t * 1.3f + it.angle0 * 3f) * 14f - r.speed * 2.2f;
                    Vector2 p = home;

                    // 1) plop in from above, splash, then sink slowly into the ring
                    if (phase == Phase.Drop)
                    {
                        float da = a - it.dropAt;
                        float surf = SurfaceY + TankY;
                        if (da < 0f) { p = new Vector2(home.x, 9f); alpha = 0f; }
                        else if (da < .28f) { float u = da / .28f; p = new Vector2(home.x, Mathf.Lerp(6.5f, surf, u * u)); }
                        else
                        {
                            if (!it.splashed) { it.splashed = true; Splash(home.x); }
                            float u = Mathf.Clamp01((da - .28f) / .7f);
                            p = new Vector2(home.x, Mathf.LerpUnclamped(surf, home.y, BackOut(u, .6f)));
                        }
                        rot += (1f - Mathf.Clamp01(da / .9f)) * 40f * (it.angle0 > Mathf.PI ? 1f : -1f);
                    }

                    // 2) the ones that lose get sucked down to the drain
                    if (r.stopped && !it.chosen)
                    {
                        float g = Mathf.Clamp01((t - (r.brakeStart + BrakeSeconds)) / .6f);
                        p = Vector2.Lerp(p, new Vector2(p.x * .3f, -3.2f), g * g);
                        alpha *= 1f - g; s *= 1f - .5f * g; rot += g * 180f;
                        if (g > 0f && g < .9f && UnityEngine.Random.value < stepDt * 6f) Add(p, new Vector2(0f, 1.6f), .08f, .9f, true, Color.white);
                    }

                    // 3) picks: spiral round each other into the centre, then lock together
                    if (it.chosen && gather > 0f && fuseA < 0f)
                    {
                        float spiralR = 2.2f * (1f - gather * gather);
                        float spin = row * Mathf.PI * 2f / 3f + gather * gather * 14f;
                        var orbit = new Vector2(Mathf.Sin(spin) * spiralR, .3f + Mathf.Cos(spin) * spiralR * .45f);
                        p = Vector2.Lerp(home, orbit, Mathf.Clamp01(gather * 3f));
                        rot = (1f - gather) * (rot + gather * 540f * (row == 1 ? -1f : 1f));
                        alpha = 1f; s = PartScale;
                    }
                    if (it.chosen && fuseA >= 0f)
                    {
                        float u = Mathf.Clamp01(fuseA / .5f);
                        float sz = Mathf.LerpUnclamped(.4f, 1f, BackOut(u, 2.2f)) * FusedScale;
                        float sq = Mathf.Exp(-fuseA * 5f) * Mathf.Sin(fuseA * 22f) * .22f;
                        p = new Vector2(0f, -.25f + Mathf.Sin(t * 2.4f) * .12f * Mathf.Clamp01(fuseA - .5f));
                        it.sr.transform.localScale = new Vector3(sz * (1f + sq), sz * (1f - sq), 1f);
                        rot = Mathf.Sin(t * 1.8f) * 4f * Mathf.Clamp01(fuseA - .5f);
                        alpha = 1f;
                    }
                    else
                    {
                        float wob = Mathf.Sin(t * 2.4f + it.angle0 * 4f) * .05f;
                        it.sr.transform.localScale = new Vector3(s * (1f + wob), s * (1f - wob), 1f);
                    }
                    p.x = Mathf.Clamp(p.x, -InnerHalf(p.y), InnerHalf(p.y));          // never poke through the glass
                    it.sr.transform.localPosition = p;
                    it.sr.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
                    float tint = it.chosen && gather > .5f ? 1f : .95f;
                    it.sr.color = new Color(tint, tint, 1f, alpha);
                    it.sr.sortingOrder = sortingOrder + 10 + (int)((depth + 1f) * 10f) + (it.chosen && gather > 0f ? 30 + (row == 0 ? 2 : row == 1 ? 1 : 0) : 0);
                    if (alpha > .5f && Playing && phase != Phase.Drop && UnityEngine.Random.value < stepDt * .9f)
                        Add(p + new Vector2(UnityEngine.Random.Range(-.3f, .3f), .5f), new Vector2(0f, UnityEngine.Random.Range(1.2f, 2f)), UnityEngine.Random.Range(.05f, .11f), 2f, true, Color.white);
                }
            }
        }

        // ================================================================== effects
        void Effects(float dt)
        {
            float a = t - phaseAt;
            Shader.SetGlobalFloat("_FusionT", t * (1f + whirl * 1.5f));
            Shader.SetGlobalFloat("_FusionWhirl", whirl * .8f);
            for (int i = 0; i < rays.Count; i++)
            {
                float ph = i * 1.9f, x = -2.8f + i * 1.4f + Mathf.Sin(t * .4f + ph) * .5f;
                rays[i].transform.localPosition = new Vector2(x, SurfaceY + TankY - 2.4f);
                rays[i].transform.localRotation = Quaternion.Euler(0f, 0f, -12f + Mathf.Sin(t * .3f + ph) * 4f);
                rays[i].transform.localScale = new Vector3(.55f + .25f * Mathf.Sin(t * .7f + ph), 5.2f, 1f);
                rays[i].color = new Color(1f, 1f, 1f, .05f + .04f * Mathf.Sin(t * 1.1f + ph * 2f));
            }
            if (surfacePlane)
            {
                surfacePlane.transform.localPosition = new Vector2(0f, SurfaceY + TankY + .12f - whirl * .2f);
                surfacePlane.transform.localScale = new Vector3(8.0f, .7f + .15f * Mathf.Sin(t * 1.7f) - whirl * .3f, 1f);
            }
            for (int i = 0; i < motes.Count; i++)
            {
                float ph = i * 2.37f;
                float mx = Mathf.Sin(t * (.13f + i * .007f) + ph) * 3.4f + Mathf.Sin(t * 1.1f + ph) * .15f;
                float my = Mathf.Repeat(-2.8f + i * .23f + t * (.12f + (i % 4) * .03f), 4.7f) - 2.9f;
                mx = Mathf.Clamp(mx + whirl * Mathf.Sin(t * 6f + ph) * 1.2f, -InnerHalf(my) - .6f, InnerHalf(my) + .6f);
                motes[i].transform.localPosition = new Vector2(mx, my);
            }
            punch = Mathf.MoveTowards(punch, 0f, dt * 2.2f);
            float z = 1f + .1f * punch * punch;
            cam.localScale = new Vector3(z, z, 1f);
            cam.localPosition = new Vector3((Mathf.PerlinNoise(t * 31f, 0f) - .5f) * 2f * shake, (Mathf.PerlinNoise(0f, t * 29f) - .5f) * 2f * shake, 0f);

            var calm = new Color(.35f, .85f, 1f, .38f);
            Color c = Color.Lerp(calm, new Color(.25f, .65f, 1f, .44f), whirl);
            if (phase == Phase.Brake) c = Color.Lerp(new Color(1f, .3f, .35f, .4f), c, Mathf.Clamp01(a / .5f));
            if (phase == Phase.Fuse) c = Color.Lerp(new Color(.85f, 1f, 1f, .55f), calm, Mathf.Clamp01(a / 1.2f));
            if (waterTint) waterTint.color = c;

            surfaceKick = Mathf.MoveTowards(surfaceKick, 0f, dt * .8f);
            for (int i = 0; i < surface.Count; i++)
            {
                float u = i / (surface.Count - 1f), x = Mathf.Lerp(-4.15f, 4.15f, u);
                float amp = .07f + .25f * surfaceKick + .18f * whirl;
                float y = SurfaceY + TankY + Mathf.Sin(t * (2.2f + 3f * whirl) + u * 9f) * amp + Mathf.Sin(t * 3.3f - u * 14f) * amp * .4f
                          - whirl * .35f * (1f - Mathf.Abs(u - .5f) * 2f);
                surface[i].transform.localPosition = new Vector2(x, y);
                surface[i].transform.localScale = new Vector3(.72f, .24f, 1f);
            }
            for (int i = 0; i < caustics.Count; i++)
            {
                float ph = i * 1.7f, sp = 1f + whirl * 3f;
                caustics[i].transform.localPosition = new Vector2(Mathf.Sin(t * .35f * sp + ph) * 2.6f, Mathf.Cos(t * .27f * sp + ph * 1.3f) * 1.8f + .3f);
                float sc = 1.1f + .35f * Mathf.Sin(t * .9f + ph);
                caustics[i].transform.localScale = new Vector3(sc * 1.4f, sc, 1f);
            }

            float ga = t - glintT;
            if (ga >= 0f && ga < .45f)
            {
                float u = ga / .45f, k = Mathf.Sin(u * Mathf.PI);
                glintA.transform.localPosition = glintB.transform.localPosition = glintAt;
                glintA.transform.localScale = new Vector3(1.4f * k, .12f * k, 1f);
                glintB.transform.localScale = new Vector3(.12f * k, 1.4f * k, 1f);
                glintA.transform.localRotation = glintB.transform.localRotation = Quaternion.Euler(0f, 0f, 45f * u);
                glintA.color = glintB.color = new Color(1f, 1f, .85f, k);
            }
            else glintA.color = glintB.color = Color.clear;

            float fa = phase == Phase.Fuse ? a : -1f;
            if (fa >= 0f && fa < .55f)
            {
                float k = fa / .55f;
                flash.transform.localScale = Vector3.one * (1.5f + 10f * Mathf.Sqrt(k));
                flash.color = new Color(1f, 1f, 1f, .95f * (1f - k));
            }
            else flash.color = Color.clear;

            if (Playing && t >= nextBubble)
            {
                nextBubble = t + Mathf.Lerp(.07f, .025f, whirl);
                float col = new[] { -3.2f, -1.1f, 1.3f, 3.4f }[UnityEngine.Random.Range(0, 4)];
                Add(new Vector2(col + UnityEngine.Random.Range(-.25f, .25f), -2.9f), new Vector2(0f, UnityEngine.Random.Range(1.3f, 2.3f)),
                    UnityEngine.Random.Range(.05f, .17f), 3f, true, Color.white);
            }

            Slam(stopBanner.root, phase == Phase.Brake, -1f);
            Slam(fusionBanner.root, phase == Phase.Fuse || phase == Phase.Done, 1f);

            for (int i = dots.Count - 1; i >= 0; i--)
            {
                var d = dots[i]; float da = t - d.t0;
                if (da > d.life || (d.bubble && d.p.y > SurfaceY + TankY + .05f)) { Destroy(d.sr.gameObject); dots.RemoveAt(i); continue; }
                if (d.bubble)
                {
                    d.v.x = Mathf.Lerp(d.v.x, (d.p.x > 0f ? -1f : 1f) * whirl * 2.2f, dt * 2f);   // the whirlpool drags them round
                    d.p += d.v * dt;
                    float lim = InnerHalf(d.p.y) + .8f;
                    if (Mathf.Abs(d.p.x) > lim) { d.p.x = Mathf.Sign(d.p.x) * lim; d.v.x = -d.v.x * .3f; }
                    float wob = Mathf.Sin(t * 5f + d.phase) * .12f;
                    d.sr.transform.localPosition = new Vector2(d.p.x + wob, d.p.y);
                    d.sr.transform.localScale = Vector3.one * d.r * 2f * Mathf.Clamp01(da / .1f);
                    d.sr.color = new Color(.9f, 1f, 1f, .9f);
                    d.sr.transform.localScale = new Vector3(d.r * 2f * (1f + Mathf.Sin(t * 9f + d.phase) * .08f), d.r * 2f * (1f - Mathf.Sin(t * 9f + d.phase) * .08f), 1f) * Mathf.Clamp01(da / .1f);
                }
                else
                {
                    d.v.y -= d.grow == 0f ? 6f * dt : 0f;      // splash drops fall back
                    d.p += d.v * dt; d.v *= Mathf.Exp(-dt * 2f);
                    float k = 1f - da / d.life;
                    d.sr.transform.localPosition = d.p;
                    d.sr.transform.localScale = Vector3.one * Mathf.Max(0f, d.r * 2f * (1f + Mathf.Max(0f, -d.grow) * da * 3f) * k);
                    var cc = d.c; cc.a *= k; d.sr.color = cc;
                }
            }
        }

        // half width of the inside of the tube at height y (the tank narrows toward the top)
        static float InnerHalf(float y) => Mathf.Lerp(4.0f, 3.4f, Mathf.InverseLerp(-3f, SurfaceY + TankY, y)) - .9f;

        void Glint(Vector2 at) { glintAt = at + new Vector2(.6f, .5f); glintT = t; }

        void Splash(float x)
        {
            surfaceKick = Mathf.Min(1f, surfaceKick + .35f);
            SoundManager.Play(Sfx.Splash, .35f);
            float y = SurfaceY + TankY;
            for (int i = 0; i < 6; i++)
                Add(new Vector2(x, y), new Vector2(UnityEngine.Random.Range(-2.2f, 2.2f), UnityEngine.Random.Range(2f, 3.6f)), UnityEngine.Random.Range(.07f, .13f), .5f, false, new Color(.9f, 1f, 1f, .9f));
            for (int i = 0; i < 5; i++)
                Add(new Vector2(x + UnityEngine.Random.Range(-.4f, .4f), y - .3f), new Vector2(0f, 1.2f), .07f, 1f, true, Color.white);
        }

        void Vortex()
        {
            float ang = t * 9f;
            var p = new Vector2(Mathf.Sin(ang) * UnityEngine.Random.Range(.6f, 2.6f), UnityEngine.Random.Range(-2.6f, 1.6f));
            Add(p, new Vector2(Mathf.Cos(ang) * 3.5f, 1.2f), UnityEngine.Random.Range(.05f, .12f), 1f, true, Color.white);
        }

        void Spiral()
        {
            float ang = t * 16f + UnityEngine.Random.value;
            var p = new Vector2(Mathf.Sin(ang) * 2.4f, .3f + Mathf.Cos(ang) * 1f);
            Add(p, -p.normalized * 4f + Vector2.up * .3f, .09f, .5f, false, new Color(.8f, 1f, 1f, .9f), 1f);
        }

        void Burst(Vector2 at, int n, bool big)
        {
            for (int i = 0; i < n; i++)
            {
                var dir = UnityEngine.Random.insideUnitCircle.normalized;
                Add(at + dir * .3f, dir * UnityEngine.Random.Range(1.5f, big ? 6f : 3f) + Vector2.up * 1.5f,
                    UnityEngine.Random.Range(.07f, big ? .25f : .16f), big ? 1.4f : .9f, true, Color.white);
            }
        }

        // grow: 0 = splash drop (falls), >0 = spark (no gravity), <0 = shockwave dot (swells)
        void Add(Vector2 p, Vector2 v, float r, float life, bool bubble, Color c, float grow = 0f)
        {
            var sr = Sr(bubble ? "bubble" : "spark", bubble ? BubbleSprite : Disc, partsRoot, p, 0f, bubble ? 50 : 61, masked: bubble);
            sr.color = c;
            dots.Add(new Dot { sr = sr, p = p, v = v, r = r, life = life, bubble = bubble, c = c, grow = grow, phase = UnityEngine.Random.value * 6f, t0 = t });
        }

        void Slam(Transform b, bool on, float side)
        {
            if (b.gameObject.activeSelf != on) b.gameObject.SetActive(on);
            if (!on) return;
            float a = t - phaseAt, u = Mathf.Clamp01(a / .28f);
            float e = BackOut(u, 1.7f);
            b.localPosition = new Vector3((1f - e) * side * -9f, -3.55f + Mathf.Sin(t * 9f) * .03f * u, 0f);
            float s = Mathf.LerpUnclamped(1.5f, 1f, e) * (1f + .03f * Mathf.Sin(t * 14f));
            b.localScale = new Vector3(s, s, 1f);
            b.localRotation = Quaternion.Euler(0f, 0f, (1f - e) * side * 12f);
        }

        static float BackOut(float u, float c1) { u = Mathf.Clamp01(u); float c3 = c1 + 1f, v = u - 1f; return 1f + c3 * v * v * v + c1 * v * v; }
    }
}
