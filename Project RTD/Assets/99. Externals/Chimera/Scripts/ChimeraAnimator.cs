using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChimeraGroove
{
    [Serializable] public class ChimeraManifest { public ChimeraParts parts; }
    [Serializable] public class ChimeraParts { public ChimeraAnimal[] heads, bodies, legs; }
    [Serializable] public class ChimeraAnimal { public string id, ko, rarity; public int ground_y_px; public ChimeraLayer[] layers; }
    [Serializable] public class ChimeraLayer { public string id, role, parent, path; public int[] pivot; }

    /// <summary>
    /// 머리·몸통·다리 id를 받아 스프라이트 계층을 만들고 박자에 맞춰 춤추게 합니다.
    /// 이 오브젝트의 위치가 곧 "발밑 중앙"입니다. 왼쪽을 보는 그림이므로 오른쪽을 보게 하려면 scale.x = -1.
    /// </summary>
    public class ChimeraAnimator : MonoBehaviour
    {
        [Header("조합 (manifest.json의 id)")]
        public string head = "pig";
        public string body = "sheep";
        public string legs = "chicken";

        [Header("동작")]
        [Tooltip("Groove=춤, Idle=아이들, Run=빨리 달리기, Jump=반복 점프(확인용), Skid=달리다 급정지 반복(확인용), Hurt=피격 반복(확인용), Scare=놀람 반복(확인용), Fly=날기, Glide=활강(날개 달린 몸통만). 한 번만 하려면 Jump() / Skid() / Hurt() / Scare() 호출.")]
        public ChimeraState state = ChimeraState.Groove;

        [Header("리소스")]
        [Tooltip("Resources 폴더 기준 경로. 이 안에 manifest.json, heads/, bodies/, legs/ 가 있어야 합니다.")]
        public string resourcesRoot = "ChimeraParts";
        public float pixelsPerUnit = 100f;

        [Header("정렬")]
        public string sortingLayerName = "Default";
        public int sortingOrderBase = 0;

        [Header("먼지")]
        [Tooltip("달릴 때·점프(도약, 착지)·급정지 때 발밑에서 나는 먼지. 테두리 없이 채움만 씁니다.")]
        public bool dustEnabled = true;
        public Color dustColor = new Color(.95f, .92f, .85f, 1f);

        /// <summary>지금 공중에 떠 있는 높이(유닛). 그림자 크기 조절 등에 쓰세요.</summary>
        public float Lift { get; private set; }

        const float HipX = 262f, HipY = 331f, NeckX = 185f, NeckY = 250f, BodyPivotX = 255f, BodyPivotY = 295f;
        const float Canvas = 512f;

        class Node
        {
            public Transform t; public Vector3 rest; public string role;
            public int i, n; public bool left, top;
            public Node far; // 날개: 날 때 몸 뒤에 그리는 반대편 날개
        }

        static readonly Dictionary<string, ChimeraManifest> manifests = new Dictionary<string, ChimeraManifest>();

        Transform rig, bodyRot, bodyScale, headBase, legsPop, bodyPop, headPop;
        Vector3 legsPopRest, bodyPopRest, headPopRest;
        ChimeraFanfare fanfare;
        Vector3 bodyRotRest, bodyScaleRest;
        readonly List<Node> legNodes = new List<Node>(), bodyNodes = new List<Node>(), headNodes = new List<Node>();
        float groundY, seed;
        bool knees; // 윗다리·아랫다리로 나뉜 파츠인지
        float jumpAt = float.NegativeInfinity, combineAt = float.NegativeInfinity, skidAt = float.NegativeInfinity, hurtAt = float.NegativeInfinity, scareAt = float.NegativeInfinity;
        ChimeraDust dust;
        int dustStride = int.MinValue, dustFlap = int.MinValue;
        float dustGlideAt = float.NegativeInfinity;
        bool canFly;
        float dustJumpP = -1f, dustHurtP = -1f, dustScareP = -1f, dustSkidAt = float.NegativeInfinity;
        int order;

        void Start() { if (rig == null) Build(); }

        public void SetCombo(string headId, string bodyId, string legsId)
        {
            head = headId; body = bodyId; legs = legsId;
            Build();
        }

        /// <summary>한 번 점프하고 원래 상태로 돌아옵니다.</summary>
        public void Jump() { jumpAt = Time.time; }

        /// <summary>급정지 한 번 하고 원래 상태로 돌아옵니다. 달리다 멈추는 순간에 부르세요.</summary>
        public void Skid() { skidAt = Time.time; }

        /// <summary>공격당해 뒤로 밀려나는 동작을 한 번 하고 원래 상태로 돌아옵니다. 맞는 순간에 부르세요.</summary>
        public void Hurt() { hurtAt = Time.time; }

        /// <summary>'꺅!' 깜짝 놀라 쭈욱 늘어나는 동작을 한 번 하고 원래 상태로 돌아옵니다.</summary>
        public void Scare() { scareAt = Time.time; }

        /// <summary>날개 달린 몸통(오리·닭·용·불사조)이라 Fly·Glide를 쓸 수 있는지. 아니면 Fly·Glide는 Idle로 보입니다.</summary>
        public bool CanFly { get { return canFly; } }

        /// <summary>Jump()로 뛴 점프가 아직 진행 중인지.</summary>
        public bool IsJumping { get { float d = Time.time - jumpAt; return d >= 0 && d < ChimeraMoves.JumpSeconds; } }

        /// <summary>부위를 바꾸고 조합 완료 연출까지 재생합니다.</summary>
        public void Combine(string headId, string bodyId, string legsId)
        {
            SetCombo(headId, bodyId, legsId);
            PlayCombine();
        }

        /// <summary>조합 완료 연출: 부위가 차례로 '뿅' 붙고, 이펙트가 터지며 기뻐 점프합니다.</summary>
        public void PlayCombine()
        {
            combineAt = Time.time;
            jumpAt = Time.time + ChimeraMoves.CelebrateAt;
            if (fanfare == null)
            {
                var go = new GameObject("Fanfare");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0, 200f / pixelsPerUnit, 0); // 몸통 높이쯤
                fanfare = go.AddComponent<ChimeraFanfare>();
            }
            fanfare.pixelsPerUnit = pixelsPerUnit;
            fanfare.sortingLayerName = sortingLayerName;
            fanfare.backOrder = sortingOrderBase - 10;
            fanfare.frontOrder = sortingOrderBase + 90;
            fanfare.Play(ChimeraMoves.CelebrateAt);
            if (rig != null) Animate();
        }

        public static ChimeraManifest LoadManifest(string root)
        {
            ChimeraManifest m;
            if (manifests.TryGetValue(root, out m)) return m;
            var text = Resources.Load<TextAsset>(root + "/manifest");
            if (text == null) { Debug.LogError("manifest를 찾을 수 없습니다: Resources/" + root + "/manifest.json"); return null; }
            m = JsonUtility.FromJson<ChimeraManifest>(text.text);
            manifests[root] = m;
            return m;
        }

        // 캔버스 픽셀 차이(오른쪽+, 아래+) → 유니티 유닛(오른쪽+, 위+)
        Vector3 U(float dx, float dy) { return new Vector3(dx / pixelsPerUnit, -dy / pixelsPerUnit, 0); }

        Transform MakeJoint(string name, Transform parent, Vector3 rest)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = rest;
            return go.transform;
        }

        public void Build()
        {
            if (rig != null) Destroy(rig.gameObject);
            legNodes.Clear(); bodyNodes.Clear(); headNodes.Clear();
            rig = null;

            var m = LoadManifest(resourcesRoot);
            if (m == null) return;
            var H = Array.Find(m.parts.heads, a => a.id == head);
            var B = Array.Find(m.parts.bodies, a => a.id == body);
            var L = Array.Find(m.parts.legs, a => a.id == legs);
            if (H == null || B == null || L == null) { Debug.LogError("없는 부위입니다: " + head + " / " + body + " / " + legs); return; }

            groundY = L.ground_y_px;
            knees = Array.Exists(L.layers, x => x.role == "lower_leg");
            canFly = Array.Exists(B.layers, x => x.role == "wing");
            seed = (head + body + legs).Length * 0.37f;
            order = sortingOrderBase;

            // rig(발밑) → 다리 레이어들
            //           → bodyRot(몸통 회전축) → bodyScale(엉덩이 기준 찌그러짐) → 몸통 레이어들
            //           → headBase(목) → 머리 레이어들
            rig = MakeJoint("Rig", transform, Vector3.zero);
            bodyRotRest = U(BodyPivotX - HipX, BodyPivotY - groundY);
            bodyRot = MakeJoint("BodyRot", rig, bodyRotRest);
            bodyScaleRest = U(HipX - BodyPivotX, HipY - BodyPivotY);
            bodyScale = MakeJoint("BodyScale", bodyRot, bodyScaleRest);
            headBase = MakeJoint("HeadBase", rig, U(NeckX - HipX, NeckY - groundY));

            // 그리는 순서: 다리 → 몸통 → 머리
            // 조합 완료 연출용 관절: 부위 전체를 한 점 기준으로 키우고 날아오게 함
            const float LegsPopY = 380f, HeadPopY = 210f;
            legsPopRest = U(0, LegsPopY - groundY);
            legsPop = MakeJoint("LegsPop", rig, legsPopRest);
            bodyPopRest = U(BodyPivotX - HipX, BodyPivotY - HipY);
            bodyPop = MakeJoint("BodyPop", bodyScale, bodyPopRest);
            headPopRest = U(0, HeadPopY - NeckY);
            headPop = MakeJoint("HeadPop", headBase, headPopRest);

            BuildPart(L, legsPop, HipX, LegsPopY, legNodes);
            BuildPart(B, bodyPop, BodyPivotX, BodyPivotY, bodyNodes);
            BuildPart(H, headPop, NeckX, HeadPopY, headNodes);
            Animate();
        }

        void BuildPart(ChimeraAnimal part, Transform baseT, float baseX, float baseY, List<Node> nodes)
        {
            var byId = new Dictionary<string, Node>();
            var count = new Dictionary<string, int>();
            int farOrder = order; // 반대편 날개는 몸통보다 뒤(다리 다음)에 그림
            foreach (var l in part.layers) if (l.role == "wing") order++;
            foreach (var l in part.layers)
            {
                var node = new Node { role = l.role, left = l.id.Contains("left") };
                int c; count.TryGetValue(l.role, out c); node.i = c; count[l.role] = c + 1;
                node.t = new GameObject(l.id).transform;
                byId[l.id] = node; nodes.Add(node);

                var sprite = Resources.Load<Sprite>(resourcesRoot + "/" + l.path.Replace(".png", ""));
                if (sprite == null) { Debug.LogWarning("스프라이트 없음(Texture Type이 Sprite인지 확인): " + l.path); continue; }
                var gfx = new GameObject("gfx");
                gfx.transform.SetParent(node.t, false);
                // 스프라이트 피벗이 캔버스의 어디든, 관절(레이어 pivot) 기준으로 원래 자리에 오도록 맞춤
                gfx.transform.localPosition = U(sprite.pivot.x - l.pivot[0], (Canvas - sprite.pivot.y) - l.pivot[1]);
                var sr = gfx.AddComponent<SpriteRenderer>();
                sr.sprite = sprite; sr.sortingLayerName = sortingLayerName; sr.sortingOrder = order++;
            }
            // 부모가 목록 뒤쪽에 있을 수 있으므로(꼬리 → 몸통) 연결은 두 번째 패스에서
            foreach (var l in part.layers)
            {
                var node = byId[l.id]; node.n = count[l.role];
                Node p = null;
                if (!string.IsNullOrEmpty(l.parent)) byId.TryGetValue(l.parent, out p);
                var pl = p != null ? Array.Find(part.layers, x => x.id == l.parent) : null;
                node.top = p == null || p.role == "core";
                node.t.SetParent(p != null ? p.t : baseT, false);
                node.rest = pl != null ? U(l.pivot[0] - pl.pivot[0], l.pivot[1] - pl.pivot[1]) : U(l.pivot[0] - baseX, l.pivot[1] - baseY);
                node.t.localPosition = node.rest;
                if (l.role == "wing" && node.t.childCount > 0)
                {   // 같은 그림을 한 장 더: 날 때만 켜지고, 어둡게, 몸통 뒤 순서
                    var farT = new GameObject(l.id + "_far").transform;
                    farT.SetParent(node.t.parent, false);
                    farT.localPosition = node.rest;
                    var src = node.t.GetChild(0);
                    var g = Instantiate(src.gameObject, farT, false);
                    g.name = "gfx";
                    var fsr = g.GetComponent<SpriteRenderer>();
                    fsr.sortingOrder = farOrder++;
                    fsr.color = new Color(.72f, .72f, .72f, 1f);
                    farT.gameObject.SetActive(false);
                    node.far = new Node { t = farT, rest = node.rest, role = "farwing" };
                }
            }
        }

        // r: 시계 방향 +(도), x·y: 캔버스 픽셀(아래 +)
        void Set(Transform t, Vector3 rest, float r, float x = 0, float y = 0, float sx = 1, float sy = 1)
        {
            t.localPosition = rest + U(x, y);
            t.localRotation = Quaternion.Euler(0, 0, -r);
            t.localScale = new Vector3(sx, sy, 1);
        }

        void Pop(Transform t, Vector3 rest, float a, float delay, float from)
        {
            float k = 1, off = 0;
            if (a < ChimeraMoves.AssembleSeconds) ChimeraMoves.PopIn(a, delay, from, out k, out off);
            Set(t, rest, 0, 0, off, k, k);
        }

        // 먼지 내기: 달릴 때 발 디딜 때마다 뒤로, 점프는 도약·착지 때 양옆으로, 급정지는 앞발에서 계속
        void EmitDust(ChimeraState st, float p, float b, float now)
        {
            if (!dustEnabled || !Application.isPlaying) return;
            if (st == ChimeraState.Run)
            {
                int k = Mathf.FloorToInt(b * 4);
                if (k != dustStride) { dustStride = k; Puff(55 + R() * 30, 70 + R() * 40, -30, 24 + R() * 14); }
            }
            else if (st == ChimeraState.Jump)
            {
                if (dustJumpP < .2f && p >= .2f)
                    for (int i = 0; i < 4; i++) { int d = i < 2 ? -1 : 1; Puff(d * (40 + 30 * R()), d * (60 + 60 * R()), -30, 28 + 14 * R()); }
                if (dustJumpP < .75f && p >= .75f)
                    for (int i = 0; i < 6; i++) { int d = i < 3 ? -1 : 1; Puff(d * (50 + 40 * R()), d * (90 + 80 * R()), -30, 34 + 18 * R()); }
            }
            else if (st == ChimeraState.Skid)
            {
                if (p < .65f && now - dustSkidAt > .035f) { dustSkidAt = now; Puff(-70 - 40 * R(), -140 * (1 - p) - 40 * R(), -50, 28 + 22 * R()); }
            }
            else if (st == ChimeraState.Hurt)
            {   // 밀리는 순간 발밑에서 앞쪽으로 먼지
                if (dustHurtP < .04f && p >= .04f)
                    for (int i = 0; i < 4; i++) Puff(-10 - 60 * R(), -50 - 70 * R(), -40, 26 + 16 * R());
            }
            else if (st == ChimeraState.Scare)
            {   // 털썩 내려앉을 때 양옆으로 먼지
                if (dustScareP < .85f && p >= .85f)
                    for (int i = 0; i < 4; i++) { int d = i < 2 ? -1 : 1; Puff(d * (40 + 30 * R()), d * (70 + 60 * R()), -30, 26 + 14 * R()); }
            }
            else if (st == ChimeraState.Fly)
            {   // 날개를 내려칠 때마다 날개 밑으로 바람 덩이가 '훅'
                int k = Mathf.FloorToInt(b * 2 - .75f);
                if (k != dustFlap) { dustFlap = k; float lf = Lift * pixelsPerUnit; for (int i = 0; i < 2; i++) Puff(20 + 50 * R(), 30 + 40 * R(), 90 + 40 * R(), 20 + 10 * R(), -(lf + 70)); }
            }
            else if (st == ChimeraState.Glide)
            {   // 몸 뒤로 바람 줄기가 쓱쓱
                if (now - dustGlideAt > .09f) { dustGlideAt = now; float lf = Lift * pixelsPerUnit; Puff(70 + 30 * R(), 300 + 120 * R(), 0, 22 + 10 * R(), -(lf + 40 + 130 * R()), true); }
            }
            dustScareP = st == ChimeraState.Scare ? p : -1f;
            dustJumpP = st == ChimeraState.Jump ? p : -1f;
            dustHurtP = st == ChimeraState.Hurt ? p : -1f;
        }

        static float R() { return UnityEngine.Random.value; }

        // x: 발밑 중앙 기준 가로 위치(왼쪽이 앞), vx·vy: 퍼지는 속도, size: 크기 — 모두 512 캔버스 픽셀
        void Puff(float x, float vx, float vy, float size, float y0 = 0, bool streak = false)
        {
            if (dust == null)
            {   // 캐릭터가 움직여도 먼지는 제자리에 남도록 부모 없이 월드에 둠
                dust = new GameObject("ChimeraDust").AddComponent<ChimeraDust>();
            }
            dust.color = dustColor;
            dust.sortingLayerName = sortingLayerName;
            dust.sortingOrder = sortingOrderBase + 95;
            dust.Emit(transform.TransformPoint(U(x, y0)), transform.TransformVector(U(vx, vy)), size / pixelsPerUnit * Mathf.Abs(transform.lossyScale.y), streak);
        }

        void OnDestroy() { if (dust != null) Destroy(dust.gameObject); }

        void Update() { if (rig != null) Animate(); }

        void Animate()
        {
            float b = ChimeraBeat.Beat;
            // 지금 재생할 동작 고르기: 한 번 피격 > 한 번 급정지 > 한 번 점프 > 확인용 반복 > state
            var st = state; float p = 0;
            float now = ChimeraBeat.Now, sinceCombine = now - combineAt;
            if (now >= hurtAt && now - hurtAt < ChimeraMoves.HurtSeconds) { st = ChimeraState.Hurt; p = (now - hurtAt) / ChimeraMoves.HurtSeconds; }
            else if (now >= scareAt && now - scareAt < ChimeraMoves.ScareSeconds) { st = ChimeraState.Scare; p = (now - scareAt) / ChimeraMoves.ScareSeconds; }
            else if (now >= skidAt && now - skidAt < ChimeraMoves.SkidSeconds) { st = ChimeraState.Skid; p = (now - skidAt) / ChimeraMoves.SkidSeconds; }
            else if (now < jumpAt) st = ChimeraState.Idle; // 아직 붙는 중
            else if (now - jumpAt < ChimeraMoves.JumpSeconds) { st = ChimeraState.Jump; p = (now - jumpAt) / ChimeraMoves.JumpSeconds; }
            else if (state == ChimeraState.Jump)
            {
                p = (now % ChimeraMoves.JumpLoopSeconds) / ChimeraMoves.JumpSeconds;
                if (p >= 1) st = ChimeraState.Idle;
            }
            else if (state == ChimeraState.Skid)
            {   // 확인용: 달리다 → 급정지 → 잠깐 아이들
                float c = now % ChimeraMoves.SkidLoopSeconds;
                if (c < 1f) st = ChimeraState.Run;
                else { p = (c - 1f) / ChimeraMoves.SkidSeconds; if (p >= 1) st = ChimeraState.Idle; }
            }
            else if (state == ChimeraState.Hurt)
            {   // 확인용: 가만히 있다가 → 피격
                float c = now % ChimeraMoves.HurtLoopSeconds;
                if (c < 1f) st = ChimeraState.Idle;
                else p = (c - 1f) / ChimeraMoves.HurtSeconds;
            }
            else if (state == ChimeraState.Scare)
            {   // 확인용: 가만히 있다가 → 깜짝
                float c = now % ChimeraMoves.ScareLoopSeconds;
                if (c < 1f) st = ChimeraState.Idle;
                else p = (c - 1f) / ChimeraMoves.ScareSeconds;
            }
            if ((st == ChimeraState.Fly || st == ChimeraState.Glide) && !canFly) st = ChimeraState.Idle; // 날개 없는 몸통
            bool groove = st == ChimeraState.Groove;
            EmitDust(st, p, b, now);

            LegPose L; BodyPose B; HeadPose H;
            if (groove) { L = ChimeraMoves.Legs(legs, b); L.sq += ChimeraMoves.GrooveBounce(b); B = ChimeraMoves.Body(body, b); H = ChimeraMoves.Head(head, b); }
            else ChimeraMoves.State(st, b, p, out L, out B, out H);

            // 전체: 발밑 기준 점프·기울기·착지 찌그러짐
            Set(rig, Vector3.zero, L.lean, L.dx, -L.lift, 1 + .08f * L.sq, 1 - .10f * L.sq);
            Lift = L.lift / pixelsPerUnit;

            // 몸통
            Set(bodyRot, bodyRotRest, B.r, B.dx, B.dy + 6 * L.sq);
            Set(bodyScale, bodyScaleRest, 0, 0, 0, B.sx, B.sy);

            // 머리는 몸통의 목 위치를 따라가되 몸통의 찌그러짐은 물려받지 않음
            headBase.position = bodyScale.TransformPoint(U(NeckX - HipX, NeckY - HipY));
            headBase.localRotation = Quaternion.Euler(0, 0, -B.r * .5f);

            // 조합 완료 연출 중이면 부위별로 '뿅' 등장 (다리는 아래에서, 몸통·머리는 위에서)
            Pop(legsPop, legsPopRest, sinceCombine, 0f, 90f);
            Pop(bodyPop, bodyPopRest, sinceCombine, .12f, -140f);
            Pop(headPop, headPopRest, sinceCombine, .24f, -180f);

            // 약 3초마다 깜빡 + 머리 동작의 윙크
            float t = (now + seed) % 3.1f;
            float blink = Mathf.Max(t < .16f ? Mathf.Sin(Mathf.PI * t / .16f) : 0, H.wink);

            foreach (var n in legNodes)
            {
                float r = n.left ? L.Lr : L.Rr, y = n.left ? L.Ly : L.Ry;
                // 무릎: 다리를 들거나 앞으로 뻗으면 저절로 굽고, 동작이 준 값을 더함. 무릎 없는 옛 파츠는 0
                float knee = knees ? 2.2f * y - .3f * Mathf.Max(0, r) + (n.left ? L.Lk : L.Rk) : 0;
                if (n.role == "leg" || n.role == "upper_leg") Set(n.t, n.rest, r, 0, y);
                else if (n.role == "lower_leg") Set(n.t, n.rest, knee);
                else if (n.role == "foot") Set(n.t, n.rest, -.6f * (r + knee) + L.foot * (n.left ? 1 : -1));
                else if (n.role == "fire") { var f = ChimeraMoves.Flicker(b, n.i); Set(n.t, n.rest, f.r, 0, 0, f.sx, f.sy); }
            }

            foreach (var n in bodyNodes)
            {
                if (n.role == "tail") Set(n.t, n.rest, groove ? ChimeraMoves.Tail(body, b, n.i, n.n) : ChimeraMoves.StateTail(st, b, p, n.i));
                else if (n.role == "wing")
                {   // 날 때는 날개를 길게 펼치고 등 위로 올림, 반대편 날개도 몸 뒤에 보임
                    float wsx = B.wingSx == 0 ? 1 : B.wingSx, wsy = B.wingSy == 0 ? 1 : B.wingSy;
                    Set(n.t, n.rest, B.wing, B.wingX, B.wingY, wsx, wsy);
                    if (n.far != null)
                    {
                        n.far.t.gameObject.SetActive(B.farWing > 0);
                        if (B.farWing > 0) Set(n.far.t, n.far.rest, B.wing - 12, B.wingX + 14, B.wingY - 12, wsx * .92f, wsy * .92f);
                    }
                }
                else if (n.role == "fin") Set(n.t, n.rest, B.fin);
                else if (n.role == "fire") { var f = ChimeraMoves.Flicker(b, n.i, groove ? ChimeraMoves.BodyFire(body, b, n.i) : ChimeraMoves.StateFire(st, b, p, n.i)); Set(n.t, n.rest, f.r, 0, 0, f.sx, f.sy); }
            }

            foreach (var n in headNodes)
            {
                switch (n.role)
                {
                    case "core": Set(n.t, n.rest, H.r, H.dx, H.dy, H.sx, H.sy); break;
                    case "ear": Set(n.t, n.rest, groove ? ChimeraMoves.Ear(head, b, n.i) : ChimeraMoves.StateEar(st, b, p, n.i)); break;
                    case "fire": { var f = ChimeraMoves.Flicker(b, n.i); Set(n.t, n.rest, f.r, 0, 0, f.sx, f.sy); } break;
                    case "eyelid":
                        if (n.top) Set(n.t, n.rest, H.browR, 0, H.browY + 3 * blink); // 눈썹
                        break;
                    case "sclera": case "eye": case "highlight":
                        if (n.top) Set(n.t, n.rest, 0, 0, 0, 1 + H.eyePop, (1 + H.eyePop) * (1 - .9f * blink)); // 납작하게 = 깜빡, 크게 = 휘둥그레
                        break;
                    case "mouth":
                        Set(n.t, n.rest, 0, 0, 2 * H.mouth, 1 + .35f * H.mouth, 1 + .6f * H.mouth); // 입 벌리기
                        break;
                    case "pupil":
                        if (n.top) Set(n.t, n.rest, 0, 0, 0, 1 + H.eyePop, (1 + H.eyePop) * (1 - .9f * blink));
                        else Set(n.t, n.rest, 0, H.px, H.py);                          // 흰자 안에서 눈동자 굴리기
                        break;
                }
            }
        }
    }
}
