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
        [Tooltip("Groove=춤, Idle=아이들, Run=빨리 달리기, Jump=반복 점프(확인용). 한 번만 뛰려면 Jump() 호출.")]
        public ChimeraState state = ChimeraState.Groove;

        [Header("리소스")]
        [Tooltip("Resources 폴더 기준 경로. 이 안에 manifest.json, heads/, bodies/, legs/ 가 있어야 합니다.")]
        public string resourcesRoot = "ChimeraParts";
        public float pixelsPerUnit = 100f;

        [Header("정렬")]
        public string sortingLayerName = "Default";
        public int sortingOrderBase = 0;

        /// <summary>지금 공중에 떠 있는 높이(유닛). 그림자 크기 조절 등에 쓰세요.</summary>
        public float Lift { get; private set; }

        const float HipX = 262f, HipY = 331f, NeckX = 185f, NeckY = 250f, BodyPivotX = 255f, BodyPivotY = 295f;
        const float Canvas = 512f;

        class Node
        {
            public Transform t; public Vector3 rest; public string role;
            public int i, n; public bool left, top;
        }

        static readonly Dictionary<string, ChimeraManifest> manifests = new Dictionary<string, ChimeraManifest>();

        Transform rig, bodyRot, bodyScale, headBase;
        Vector3 bodyRotRest, bodyScaleRest;
        readonly List<Node> legNodes = new List<Node>(), bodyNodes = new List<Node>(), headNodes = new List<Node>();
        float groundY, seed;
        float jumpAt = -1f;
        int order;

        void Start() { if (rig == null) Build(); }

        public void SetCombo(string headId, string bodyId, string legsId)
        {
            head = headId; body = bodyId; legs = legsId;
            Build();
        }

        /// <summary>한 번 점프하고 원래 상태로 돌아옵니다.</summary>
        public void Jump() { jumpAt = Time.time; }

        /// <summary>Jump()로 뛴 점프가 아직 진행 중인지.</summary>
        public bool IsJumping { get { return jumpAt >= 0 && Time.time - jumpAt < ChimeraMoves.JumpSeconds; } }

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
            BuildPart(L, rig, HipX, groundY, legNodes);
            BuildPart(B, bodyScale, HipX, HipY, bodyNodes);
            BuildPart(H, headBase, NeckX, NeckY, headNodes);
            Animate();
        }

        void BuildPart(ChimeraAnimal part, Transform baseT, float baseX, float baseY, List<Node> nodes)
        {
            var byId = new Dictionary<string, Node>();
            var count = new Dictionary<string, int>();
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
            }
        }

        // r: 시계 방향 +(도), x·y: 캔버스 픽셀(아래 +)
        void Set(Transform t, Vector3 rest, float r, float x = 0, float y = 0, float sx = 1, float sy = 1)
        {
            t.localPosition = rest + U(x, y);
            t.localRotation = Quaternion.Euler(0, 0, -r);
            t.localScale = new Vector3(sx, sy, 1);
        }

        void Update() { if (rig != null) Animate(); }

        void Animate()
        {
            float b = ChimeraBeat.Beat;
            // 지금 재생할 동작 고르기: 한 번 점프 > 반복 점프(중간엔 아이들) > state
            var st = state; float p = 0;
            if (IsJumping) { st = ChimeraState.Jump; p = (Time.time - jumpAt) / ChimeraMoves.JumpSeconds; }
            else if (state == ChimeraState.Jump)
            {
                p = (Time.time % ChimeraMoves.JumpLoopSeconds) / ChimeraMoves.JumpSeconds;
                if (p >= 1) st = ChimeraState.Idle;
            }
            bool groove = st == ChimeraState.Groove;

            LegPose L; BodyPose B; HeadPose H;
            if (groove) { L = ChimeraMoves.Legs(legs, b); B = ChimeraMoves.Body(body, b); H = ChimeraMoves.Head(head, b); }
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

            // 약 3초마다 깜빡 + 머리 동작의 윙크
            float t = (Time.time + seed) % 3.1f;
            float blink = Mathf.Max(t < .16f ? Mathf.Sin(Mathf.PI * t / .16f) : 0, H.wink);

            foreach (var n in legNodes)
            {
                float r = n.left ? L.Lr : L.Rr, y = n.left ? L.Ly : L.Ry;
                if (n.role == "leg") Set(n.t, n.rest, r, 0, y);
                else if (n.role == "foot") Set(n.t, n.rest, -.6f * r + L.foot * (n.left ? 1 : -1));
                else if (n.role == "fire") { var f = ChimeraMoves.Flicker(b, n.i); Set(n.t, n.rest, f.r, 0, 0, f.sx, f.sy); }
            }

            foreach (var n in bodyNodes)
            {
                if (n.role == "tail") Set(n.t, n.rest, groove ? ChimeraMoves.Tail(body, b, n.i, n.n) : ChimeraMoves.StateTail(st, b, p, n.i));
                else if (n.role == "wing") Set(n.t, n.rest, B.wing);
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
                        if (n.top) Set(n.t, n.rest, 0, 0, 0, 1, 1 - .9f * blink);      // 눈 전체를 납작하게 = 깜빡
                        break;
                    case "pupil":
                        if (n.top) Set(n.t, n.rest, 0, 0, 0, 1, 1 - .9f * blink);
                        else Set(n.t, n.rest, 0, H.px, H.py);                          // 흰자 안에서 눈동자 굴리기
                        break;
                }
            }
        }
    }
}
