using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChimeraGroove
{
    [Serializable] public class SciManifest { public SciCharacter[] characters; }
    [Serializable] public class SciCharacter { public string id, ko; public int[] canvas_px; public SciLayer[] layers; }
    [Serializable] public class SciLayer { public string id, role, parent, path; public int[] pivot; }

    /// <summary>
    /// 적: 미친 과학자. id는 ray(광선총) / pipe(쇠파이프) / net(포획망) / egg(달걀 발사기) / magnet(자석).
    /// 이 오브젝트의 위치가 발밑 중앙입니다. 다섯 명 모두 같은 키로 맞춰 그립니다.
    /// state로 아이들·걷기를 고르고, 공격은 Attack()을 한 번 호출하세요(state = Attack은 확인용 반복).
    /// </summary>
    public class ScientistAnimator : MonoBehaviour
    {
        public string id = "ray";
        public ScientistState state = ScientistState.Idle;
        [Tooltip("Resources 폴더 기준 경로. 이 안에 manifest.json과 PNG/ 가 있어야 합니다.")]
        public string resourcesRoot = "ChimeraEnemies/scientists";
        public float pixelsPerUnit = 100f;
        public string sortingLayerName = "Default";
        public int sortingOrderBase = 0;
        public Color dustColor = new Color(.95f, .92f, .85f, 1f);
        [Tooltip("휘두르는 궤적과 충격 섬광의 색.")]
        public Color fxColor = Color.white;

        class Node { public Transform t; public Vector3 rest; public string role, id; public int leg = -1, arm = -1; }

        static readonly Dictionary<string, SciManifest> manifests = new Dictionary<string, SciManifest>();
        SciConfig cfg;
        Transform fit, rig, weaponT;
        Vector2 weaponPivot;
        readonly List<Node> nodes = new List<Node>();
        float unit, seed, attackAt = float.NegativeInfinity;
        int lastStep = int.MinValue; bool lastImpact;
        ChimeraDust dust;
        // 이펙트용 그림들 (모두 선 없이 면)
        SpriteRenderer beam, beamCore, glow, glowCore, burst, starA, starB, egg, eggBack;
        SpriteRenderer[] waves, bits;
        Transform armT; MeshFilter swoosh; MeshRenderer swooshR;
        struct TrailPt { public float t; public Vector3 tip, hub; }
        readonly List<TrailPt> trail = new List<TrailPt>();
        bool lastFired;

        void Start() { if (rig == null) Build(); }

        public void SetScientist(string scientistId) { id = scientistId; Build(); }

        /// <summary>한 번 공격하고 원래 상태로 돌아옵니다.</summary>
        public void Attack() { attackAt = Time.time; }

        public bool IsAttacking { get { float d = Time.time - attackAt; return d >= 0 && d < EnemyMoves.AttackSeconds; } }

        Vector3 U(float dx, float dy) { return new Vector3(dx / pixelsPerUnit, -dy / pixelsPerUnit, 0); }

        public void Build()
        {
            if (fit != null) Destroy(fit.gameObject);
            nodes.Clear(); fit = rig = weaponT = armT = null; trail.Clear();
            cfg = EnemyMoves.Config(id);
            SciManifest m;
            if (!manifests.TryGetValue(resourcesRoot, out m))
            {
                var text = Resources.Load<TextAsset>(resourcesRoot + "/manifest");
                if (text == null) { Debug.LogError("manifest를 찾을 수 없습니다: Resources/" + resourcesRoot + "/manifest.json"); return; }
                m = JsonUtility.FromJson<SciManifest>(text.text); manifests[resourcesRoot] = m;
            }
            var c = Array.Find(m.characters, x => x.id == id);
            if (c == null || cfg == null) { Debug.LogError("없는 과학자입니다: " + id); return; }

            unit = EnemyMoves.SciHeight / cfg.height;
            seed = id.Length * .53f;
            var torso = Array.Find(c.layers, x => x.role == "torso");
            float ox = torso.pivot[0], oy = cfg.ground; // 발밑 중앙

            fit = new GameObject("Fit").transform; fit.SetParent(transform, false); fit.localScale = Vector3.one * unit; // 키 맞춤
            rig = new GameObject("Rig").transform; rig.SetParent(fit, false);

            var byId = new Dictionary<string, Node>();
            int order = sortingOrderBase, legs = 0, arms = 0;
            foreach (var l in c.layers)
            {
                var n = new Node { role = l.role, id = l.id, t = new GameObject(l.id).transform };
                if (l.role == "thigh") n.leg = legs++;
                if (l.role == "upper_arm") n.arm = arms++;
                byId[l.id] = n; nodes.Add(n);
                var sprite = Resources.Load<Sprite>(resourcesRoot + "/" + l.path.Replace(".png", ""));
                if (sprite == null) { Debug.LogWarning("스프라이트 없음(Texture Type이 Sprite인지 확인): " + l.path); continue; }
                var gfx = new GameObject("gfx"); gfx.transform.SetParent(n.t, false);
                gfx.transform.localPosition = U(sprite.pivot.x - l.pivot[0], (c.canvas_px[1] - sprite.pivot.y) - l.pivot[1]);
                var sr = gfx.AddComponent<SpriteRenderer>();
                sr.sprite = sprite; sr.sortingLayerName = sortingLayerName; sr.sortingOrder = order++;
            }
            foreach (var l in c.layers)
            {
                var n = byId[l.id];
                // 하체(골반·허벅지)는 몸통이 들썩여도 땅에 붙어 있도록 몸통을 따라가지 않게 함
                bool lower = l.role == "pelvis" || l.role == "thigh";
                Node p = null;
                if (!lower && !string.IsNullOrEmpty(l.parent)) byId.TryGetValue(l.parent, out p);
                var pl = p != null ? Array.Find(c.layers, x => x.id == l.parent) : null;
                n.t.SetParent(p != null ? p.t : rig, false);
                n.rest = pl != null ? U(l.pivot[0] - pl.pivot[0], l.pivot[1] - pl.pivot[1]) : U(l.pivot[0] - ox, l.pivot[1] - oy);
                n.t.localPosition = n.rest;
                if ((l.role == "shin" || l.role == "boot") && p != null) n.leg = p.leg; // 정강이·신발은 부모 다리 번호를 물려받음
                if (cfg.weapon != null && l.id == cfg.weapon) { weaponT = n.t; weaponPivot = new Vector2(l.pivot[0], l.pivot[1]); }
                if (cfg.arm != null && l.id == cfg.arm) armT = n.t; // 휘두르는 팔의 어깨
            }
            // shin → boot 순서로 다리 번호가 내려가도록 한 번 더
            foreach (var l in c.layers) if (l.role == "boot" && !string.IsNullOrEmpty(l.parent)) byId[l.id].leg = byId[l.parent].leg;
            Animate();
        }

        void Set(Transform t, Vector3 rest, float r, float x = 0, float y = 0, float sx = 1, float sy = 1)
        {
            t.localPosition = rest + U(x, y);
            t.localRotation = Quaternion.Euler(0, 0, -r);
            t.localScale = new Vector3(sx, sy, 1);
        }

        void Update() { if (rig != null) Animate(); }

        void Animate()
        {
            float b = ChimeraBeat.Beat, now = ChimeraBeat.Now;
            // 지금 재생할 동작: 한 번 공격 > 확인용 반복 공격 > 아이들·걷기
            SciPose M; bool attacking = false;
            if (now >= attackAt && now - attackAt < EnemyMoves.AttackSeconds) { M = EnemyMoves.Attack((now - attackAt) / EnemyMoves.AttackSeconds, cfg); attacking = true; }
            else if (state == ScientistState.Attack && now % EnemyMoves.AttackLoopSeconds >= 1f) { M = EnemyMoves.Attack((now % EnemyMoves.AttackLoopSeconds - 1f) / EnemyMoves.AttackSeconds, cfg); attacking = true; }
            else M = state == ScientistState.Walk ? EnemyMoves.Walk(b, cfg) : EnemyMoves.Idle(b, cfg);

            Set(rig, Vector3.zero, M.rootR, M.rootX, M.rootY, 1 + .06f * M.sq, 1 - .08f * M.sq);
            float t = (now + seed) % 3.4f;
            float blink = Mathf.Max(t < .16f ? Mathf.Sin(Mathf.PI * t / .16f) : 0, M.wink);

            foreach (var n in nodes)
            {
                float lt = n.leg == 0 ? M.leg0t : M.leg1t, ls = n.leg == 0 ? M.leg0s : M.leg1s;
                switch (n.role)
                {
                    case "torso": Set(n.t, n.rest, M.torsoR, M.torsoDx, M.torsoDy + 5 * M.sq, 1, M.torsoSy == 0 ? 1 : M.torsoSy); break;
                    case "thigh": Set(n.t, n.rest, lt); break;
                    case "shin": Set(n.t, n.rest, ls); break;
                    case "boot": Set(n.t, n.rest, -.8f * (lt + ls)); break; // 신발은 바닥과 평평하게
                    case "upper_arm":
                        Set(n.t, n.rest, M.swinging && n.id == cfg.arm ? M.arm : M.swinging ? 0 : (n.arm == 0 ? M.armAll0 : M.armAll1));
                        break;
                    case "head": Set(n.t, n.rest, M.headR, 0, M.headDy); break;
                    case "hair": case "clothing": Set(n.t, n.rest, M.hair); break;
                    case "moustache": Set(n.t, n.rest, .4f * M.hair); break;
                    case "sclera": case "eye": Set(n.t, n.rest, 0, 0, 0, 1, 1 - .9f * blink); break;
                    case "pupil": Set(n.t, n.rest, 0, M.px, 0); break;
                    case "mouth": Set(n.t, n.rest, 0, 0, 0, 1 + .15f * M.mouth, 1 + .45f * M.mouth); break;
                }
            }

            if (!Application.isPlaying) return;
            // 먼지: 걸을 때 발 디딜 때마다, 내려칠 때 앞쪽에서
            float u = 1f / unit;
            if (M.walking && M.step != lastStep) { lastStep = M.step; Puff(-cfg.face * (40 + 30 * R()) * u, -cfg.face * (50 + 30 * R()) * u, -25 * u, (20 + 10 * R()) * u); }
            if (M.impact && !lastImpact) for (int i = 0; i < 5; i++) Puff(cfg.face * (150 + 90 * R()) * u, (R() - .5f) * 240 * u, -40 * u, (26 + 16 * R()) * u);
            lastImpact = M.impact;
            ShowFx(attacking ? M : default(SciPose));
        }

        static float R() { return UnityEngine.Random.value; }

        void Puff(float x, float vx, float vy, float size)
        {
            if (dust == null) dust = new GameObject("ChimeraDust").AddComponent<ChimeraDust>();
            dust.color = dustColor; dust.sortingLayerName = sortingLayerName; dust.sortingOrder = sortingOrderBase + 95;
            dust.Emit(fit.TransformPoint(U(x, 0)), fit.TransformVector(U(vx, vy)), size / pixelsPerUnit * Mathf.Abs(fit.lossyScale.y));
        }

        SpriteRenderer Fx(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.color = color; sr.sortingLayerName = sortingLayerName; sr.sortingOrder = sortingOrderBase + order; sr.enabled = false;
            return sr;
        }

        // ── 무기 이펙트 (선 없이 면으로) ──
        // 발사: 모으는 빛 → 섬광 + 번지는 빛 + 광선(또는 달걀) + 총구 연기
        // 휘두르기: 무기 끝이 쓸고 간 부채꼴 궤적 + 내려친 순간의 섬광
        // 끌어당기기: 자석으로 빨려 드는 고리와 알갱이
        void ShowFx(SciPose M)
        {
            if (beam == null)
            {
                var hot = new Color(.55f, 1f, .31f); var core = new Color(.96f, 1f, .88f);
                glow = Fx("Glow", ChimeraFanfare.Disc, hot, 96); glowCore = Fx("GlowCore", ChimeraFanfare.Disc, core, 97);
                burst = Fx("Burst", ChimeraFanfare.Disc, hot, 95);
                beam = Fx("Beam", ChimeraFanfare.Square, hot, 96); beamCore = Fx("BeamCore", ChimeraFanfare.Square, core, 97);
                starA = Fx("StarA", ChimeraFanfare.SparkSprite, hot, 98); starB = Fx("StarB", ChimeraFanfare.SparkSprite, core, 99);
                eggBack = Fx("EggBack", ChimeraFanfare.Disc, new Color(.72f, .62f, .45f), 98); egg = Fx("Egg", ChimeraFanfare.Disc, new Color(.98f, .95f, .87f), 99);
                waves = new SpriteRenderer[3];
                for (int i = 0; i < 3; i++) waves[i] = Fx("Wave", ChimeraFanfare.Ring, new Color(.35f, .72f, 1f), 96);
                bits = new SpriteRenderer[7];
                for (int i = 0; i < 7; i++) bits[i] = Fx("Bit", ChimeraFanfare.Disc, new Color(.75f, .89f, 1f), 97);
                var sg = new GameObject("Swoosh"); sg.transform.SetParent(transform, false);
                swoosh = sg.AddComponent<MeshFilter>(); swoosh.mesh = new Mesh();
                swooshR = sg.AddComponent<MeshRenderer>();
                swooshR.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                swooshR.sortingLayerName = sortingLayerName; swooshR.sortingOrder = sortingOrderBase + 94;
            }
            foreach (var sr in new[] { beam, beamCore, glow, glowCore, burst, starA, starB, egg, eggBack }) sr.enabled = false;
            foreach (var sr in waves) sr.enabled = false;
            foreach (var sr in bits) sr.enabled = false;

            float now = ChimeraBeat.Now, k = 1f / pixelsPerUnit, f = cfg.face;
            Vector3 tip = Vector3.zero;
            if (weaponT != null) tip = transform.InverseTransformPoint(weaponT.TransformPoint(U(cfg.muzzle.x - weaponPivot.x, cfg.muzzle.y - weaponPivot.y)));

            // 휘두른 궤적: 무기 끝과 어깨의 위치를 기억해 부채꼴 면으로 칠함
            if (M.trail && weaponT != null && armT != null)
            {
                var cur = new TrailPt { t = now, tip = tip, hub = transform.InverseTransformPoint(armT.position) };
                if (trail.Count > 0 && now - trail[trail.Count - 1].t < .1f)
                {   // 워낙 빨리 휘둘러서 프레임 사이를 잘게 나눠 넣어야 궤적이 매끈함
                    var prev = trail[trail.Count - 1];
                    for (int j = 1; j < 4; j++)
                    {
                        float u = j / 4f; Vector3 hub = Vector3.Lerp(prev.hub, cur.hub, u);
                        trail.Add(new TrailPt { t = Mathf.Lerp(prev.t, now, u), hub = hub, tip = hub + Vector3.Slerp(prev.tip - prev.hub, cur.tip - cur.hub, u) });
                    }
                }
                trail.Add(cur);
            }
            trail.RemoveAll(q => now - q.t >= .16f || now < q.t);
            BuildSwoosh();

            if (M.fx == null || weaponT == null) { lastFired = false; return; }
            if (M.fx == "swing")
            {   // 내려친 순간 무기 끝에서 번쩍
                float a = Mathf.Clamp01(1 - M.hitT / .16f);
                if (M.hitT >= 0 && a > 0) Star(starA, tip, (70 + 90 * (1 - a)) * 2 * k, M.hitT * 9, fxColor, a);
                return;
            }
            if (M.fx == "ray" || M.fx == "egg")
            {
                Color hot = M.fx == "ray" ? new Color(.55f, 1f, .31f) : new Color(1f, .82f, .4f), core = M.fx == "ray" ? new Color(.96f, 1f, .88f) : new Color(1f, .96f, .86f);
                if (M.fxCh > 0)
                {   // 모으는 동안 총구에서 빛이 커지며 떨림
                    Disc(glow, tip, (18 + 44 * M.fxCh) * 2 * k * (1 + .12f * Mathf.Sin(now * 60)), hot, .55f * M.fxCh);
                    Disc(glowCore, tip, (8 + 20 * M.fxCh) * 2 * k, core, .8f * M.fxCh);
                }
                float t = M.fxT; bool fired = t >= 0;
                if (fired && !lastFired) // 발사하는 순간 총구에서 연기
                    for (int i = 0; i < 4; i++)
                    {
                        if (dust == null) dust = new GameObject("ChimeraDust").AddComponent<ChimeraDust>();
                        dust.color = dustColor; dust.sortingLayerName = sortingLayerName; dust.sortingOrder = sortingOrderBase + 95;
                        dust.Emit(transform.TransformPoint(tip + new Vector3(f * 20 * R() * k, 0, 0)), transform.TransformVector(new Vector3(f * (60 + 140 * R()) * k, (30 + 60 * R()) * k, 0)), (22 + 14 * R()) * k * Mathf.Abs(transform.lossyScale.y));
                    }
                lastFired = fired;
                if (!fired) return;
                float fa = Mathf.Clamp01(1 - t / .14f); // 총구 섬광
                if (fa > 0) { Star(starA, tip, (90 + 60 * (1 - fa)) * 2 * k, t * 12, hot, fa); Star(starB, tip, (50 + 30 * (1 - fa)) * 2 * k, -t * 9 + .785f, core, fa); }
                float ra = Mathf.Clamp01(1 - t / .3f);   // 번지는 빛
                if (ra > 0) Disc(burst, tip, (60 + 260 * (1 - ra)) * 2 * k, hot, .22f * ra);
                if (M.fx == "ray")
                {   // 초록 광선: 굵게 뻗었다 가늘어지며 사라지고, 안에 밝은 속빛
                    float b = Mathf.Clamp01(1 - t / .32f); if (b <= 0) return;
                    float len = 700 * k, pulse = 1 + .15f * Mathf.Sin(now * 90);
                    beam.enabled = beamCore.enabled = true;
                    beam.transform.localPosition = beamCore.transform.localPosition = tip + new Vector3(f * len / 2, 0, 0);
                    beam.transform.localScale = new Vector3(len, 2 * 58 * k * b * pulse * .65f, 1);
                    beamCore.transform.localScale = new Vector3(len, 2 * 26 * k * b * pulse * .65f, 1);
                    hot.a = .8f * b; beam.color = hot; core.a = b; beamCore.color = core;
                }
                else if (t < .55f)
                {   // 달걀: 날아가는 방향으로 길쭉하게 늘어난 채 포물선
                    float st = 1 + .25f * Mathf.Clamp01(1 - t / .2f);
                    var pos = tip + new Vector3(f * 820 * k * t, (170 * t - 640 * t * t) * k, 0);
                    float rot = Mathf.Atan2(170 - 1280 * t, f * 820) * Mathf.Rad2Deg;
                    egg.enabled = eggBack.enabled = true;
                    egg.transform.localPosition = eggBack.transform.localPosition = pos;
                    egg.transform.localRotation = eggBack.transform.localRotation = Quaternion.Euler(0, 0, rot);
                    egg.transform.localScale = new Vector3(76 * k * st, 60 * k / st, 1);
                    eggBack.transform.localScale = new Vector3(86 * k * st, 70 * k / st, 1); // 짙은 바탕으로 윤곽을 대신함
                }
                return;
            }
            // 자력: 고리가 자석 쪽으로 빨려 들고, 작은 알갱이들이 딸려 옴
            for (int i = 0; i < 3; i++)
            {
                float u = ChimeraMoves.Fr(M.fxP * 4 + i / 3f), d = 300 * k * (1 - u), rad = (34 + 80 * (1 - u)) * k;
                waves[i].enabled = true;
                waves[i].transform.localPosition = tip + new Vector3(f * d, 0, 0);
                waves[i].transform.localScale = new Vector3(rad * .9f, rad * 2.2f, 1);
                var col = waves[i].color; col.a = M.fxOn * Mathf.Sin(Mathf.PI * u) * .8f; waves[i].color = col;
            }
            for (int i = 0; i < 7; i++)
            {
                float u = ChimeraMoves.Fr(M.fxP * 3 + i * .37f), d = 320 * k * (1 - u) * (1 - u);
                Disc(bits[i], tip + new Vector3(f * d, -Mathf.Sin(i * 2.4f) * 90 * k * (1 - u), 0), 2 * (5 + 4 * Mathf.Sin(i)) * k, bits[i].color, M.fxOn * Mathf.Sin(Mathf.PI * u));
            }
            Disc(glow, tip, 92 * k, new Color(.35f, .72f, 1f), .35f * M.fxOn * (1 + .3f * Mathf.Sin(now * 70)));
        }

        static void Disc(SpriteRenderer sr, Vector3 pos, float diameter, Color c, float alpha)
        {
            sr.enabled = true; sr.transform.localPosition = pos; sr.transform.localScale = Vector3.one * diameter;
            c.a = Mathf.Clamp01(alpha); sr.color = c;
        }

        static void Star(SpriteRenderer sr, Vector3 pos, float diameter, float rotRad, Color c, float alpha)
        {
            Disc(sr, pos, diameter, c, alpha);
            sr.transform.localRotation = Quaternion.Euler(0, 0, -rotRad * Mathf.Rad2Deg);
        }

        // 궤적 점들로 부채꼴 면을 만듦: 무기 끝을 잇는 바깥 선과, 어깨 쪽으로 조금 들어온 안쪽 선 사이를 채움
        void BuildSwoosh()
        {
            var mesh = swoosh.mesh; mesh.Clear();
            int n = trail.Count;
            if (n < 2) return;
            var verts = new Vector3[n * 2]; var cols = new Color[n * 2]; var tris = new int[(n - 1) * 6];
            var col = fxColor; col.a *= .6f;
            for (int i = 0; i < n; i++)
            {
                float wd = .12f + .6f * i / (n - 1); // 방금 지나간 쪽이 넓고 오래된 쪽은 가늘게
                verts[i * 2] = trail[i].tip; verts[i * 2 + 1] = Vector3.Lerp(trail[i].tip, trail[i].hub, wd);
                cols[i * 2] = cols[i * 2 + 1] = col;
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2, t = i * 6;
                tris[t] = a; tris[t + 1] = a + 1; tris[t + 2] = a + 2; tris[t + 3] = a + 1; tris[t + 4] = a + 3; tris[t + 5] = a + 2;
            }
            mesh.vertices = verts; mesh.colors = cols; mesh.triangles = tris; mesh.RecalculateBounds();
        }

        void OnDestroy() { if (dust != null) Destroy(dust.gameObject); }
    }
}
