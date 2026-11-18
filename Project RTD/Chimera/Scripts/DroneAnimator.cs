using System;
using UnityEngine;

namespace ChimeraGroove
{
    [Serializable]
    public class DroneInfo
    {
        public int[] canvas_px, hub_left, hub_right, pupil_center, eye, dome, center;
        public int pupil_radius, bottom_y;
    }

    public enum DroneState { Scan, Alert }

    /// <summary>
    /// 적: 감시 드론. 이 오브젝트의 위치가 드론 바로 아래 바닥입니다.
    /// Scan(감시): 둥실둥실 뜬 채 눈알을 여기저기 굴리고 프로펠러를 세게 돌립니다. 경광등은 천천히 깜빡입니다.
    /// Alert(발각): 화들짝 튀어 올라 부들부들 떨고, 머리 위 경광등이 삐용삐용 돕니다.
    /// </summary>
    public class DroneAnimator : MonoBehaviour
    {
        public DroneState state = DroneState.Scan;
        [Tooltip("Resources 폴더 기준 경로. body, blades_left, blades_right, eye_mask 그림과 drone.json이 있어야 합니다.")]
        public string resourcesRoot = "ChimeraEnemies/drone";
        public float pixelsPerUnit = 100f;
        [Tooltip("바닥에서 떠 있는 높이(픽셀).")]
        public float hover = 170f;
        public string sortingLayerName = "Default";
        public int sortingOrderBase = 0;
        [Tooltip("머리 위 경광등 불빛 색.")]
        public Color sirenColor = new Color(1f, .18f, .22f, 1f);

        DroneInfo I;
        Transform body, bladesL, bladesR, pupil, domeRoot, wedges;
        SpriteRenderer pupilSr, glint, blurL, blurR, domeGlow;
        SpriteRenderer[] wedgeSr;
        DroneState was = DroneState.Scan;
        float alertAt = float.NegativeInfinity;

        Vector3 U(float dx, float dy) { return new Vector3(dx / pixelsPerUnit, -dy / pixelsPerUnit, 0); }
        static float Hash(float n) { float x = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return x - Mathf.Floor(x); }

        SpriteRenderer Sr(string name, Transform parent, Sprite sprite, int order, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.color = color; sr.sortingLayerName = sortingLayerName; sr.sortingOrder = sortingOrderBase + order;
            return sr;
        }

        // 그림 한 장을 body 관절 아래에, 캔버스 좌표가 그대로 맞도록 놓음
        Transform Layer(string file, Transform parent, Vector2 pivot, int order)
        {
            var joint = new GameObject(file).transform; joint.SetParent(parent, false);
            joint.localPosition = U(pivot.x - I.center[0], pivot.y - I.center[1]);
            var sprite = Resources.Load<Sprite>(resourcesRoot + "/" + file);
            if (sprite == null) { Debug.LogWarning("스프라이트 없음(Texture Type이 Sprite인지 확인): " + file); return joint; }
            var sr = Sr("gfx", joint, sprite, order, Color.white);
            sr.transform.localPosition = U(sprite.pivot.x - pivot.x, (I.canvas_px[1] - sprite.pivot.y) - pivot.y);
            return joint;
        }

        void Start()
        {
            var text = Resources.Load<TextAsset>(resourcesRoot + "/drone");
            if (text == null) { Debug.LogError("drone.json을 찾을 수 없습니다: Resources/" + resourcesRoot); enabled = false; return; }
            I = JsonUtility.FromJson<DroneInfo>(text.text);
            var center = new Vector2(I.center[0], I.center[1]);

            body = new GameObject("Body").transform; body.SetParent(transform, false);
            bladesL = Layer("blades_left", body, new Vector2(I.hub_left[0], I.hub_left[1]), 10);
            bladesR = Layer("blades_right", body, new Vector2(I.hub_right[0], I.hub_right[1]), 10);
            var tan = new Color(.83f, .68f, .43f, .34f);
            blurL = Sr("BlurL", body, ChimeraFanfare.Disc, 11, tan); blurL.transform.localPosition = U(I.hub_left[0] - center.x, I.hub_left[1] - 8 - center.y);
            blurR = Sr("BlurR", body, ChimeraFanfare.Disc, 11, tan); blurR.transform.localPosition = U(I.hub_right[0] - center.x, I.hub_right[1] - 8 - center.y);
            blurL.transform.localScale = blurR.transform.localScale = new Vector3(300 / pixelsPerUnit, 52 / pixelsPerUnit, 1);
            Layer("body", body, center, 12);

            // 눈동자: 흰자 모양 마스크 안에서만 보이게 해서 밖으로 삐져나가지 않게 함
            var maskT = Layer("eye_mask", body, center, 13);
            var maskSr = maskT.GetComponentInChildren<SpriteRenderer>();
            if (maskSr != null)
            {
                var mask = maskSr.gameObject.AddComponent<SpriteMask>();
                mask.sprite = maskSr.sprite;
                mask.isCustomRangeActive = true; // 다른 드론의 눈과 섞이지 않게 이 드론의 눈동자 순서만 가림
                mask.frontSortingLayerID = mask.backSortingLayerID = SortingLayer.NameToID(sortingLayerName);
                mask.frontSortingOrder = sortingOrderBase + 15; mask.backSortingOrder = sortingOrderBase + 13;
                maskSr.enabled = false;
            }
            pupil = new GameObject("Pupil").transform; pupil.SetParent(body, false);
            pupilSr = Sr("gfx", pupil, ChimeraFanfare.Disc, 14, new Color(.1f, .08f, .055f));
            glint = Sr("glint", pupil, ChimeraFanfare.Disc, 15, Color.white);
            pupilSr.maskInteraction = glint.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            // 머리 위 경광등 불빛
            domeRoot = new GameObject("Dome").transform; domeRoot.SetParent(body, false);
            domeRoot.localPosition = U(I.dome[0] - center.x, I.dome[1] - center.y);
            domeGlow = Sr("Glow", domeRoot, ChimeraFanfare.Disc, 20, sirenColor);
            wedges = new GameObject("Siren").transform; wedges.SetParent(domeRoot, false);
            wedgeSr = new SpriteRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                wedgeSr[i] = Sr("Beam", wedges, ChimeraFanfare.Wedge, 21, sirenColor);
                wedgeSr[i].transform.localRotation = Quaternion.Euler(0, 0, i * 180);
                wedgeSr[i].transform.localScale = Vector3.one * (300 / pixelsPerUnit);
            }

        }

        void Update()
        {
            if (I == null) return;
            float b = ChimeraBeat.Beat, sec = ChimeraBeat.Now, px = 1f / pixelsPerUnit;
            bool alert = state == DroneState.Alert;
            if (alert && was != DroneState.Alert) alertAt = sec; // 발각되는 순간
            was = state;
            float jolt = alert ? Mathf.Exp(-(sec - alertAt) * 5) : 0;
            float w25 = ChimeraMoves.W(b, .25f);

            // 둥실둥실 떠 있기 + 발각되면 화들짝 튀어 오르고 부들부들
            float bob = 14 * ChimeraMoves.W(b, .5f) - 46 * jolt;
            float tilt = alert ? 3 * Mathf.Sin(sec * 46) : 5 * w25;
            float shake = alert ? 3 * Mathf.Sin(sec * 71) : 0;
            float height = hover + I.bottom_y - I.center[1];
            body.localPosition = U(shake, -height + bob);
            body.localRotation = Quaternion.Euler(0, 0, -tilt);

            // 프로펠러: 날이 가로로 줄었다 늘었다 하며 빡세게 회전
            float spin = alert ? 60 : 44;
            bladesL.localScale = new Vector3(.22f + .78f * Mathf.Abs(Mathf.Cos(sec * spin)), 1, 1);
            bladesR.localScale = new Vector3(.22f + .78f * Mathf.Abs(Mathf.Cos(sec * spin + 1.3f)), 1, 1);

            // 눈동자: 여기저기 휙휙 굴림. 발각하면 작아져서 목표를 노려봄
            float ex, ey, pr = I.pupil_radius;
            if (alert) { ex = -11 + 1.5f * Mathf.Sin(sec * 50); ey = 5 + 1.5f * Mathf.Cos(sec * 43); pr *= .7f + .3f * jolt; }
            else
            {
                int i = Mathf.FloorToInt(b * 2); float f = 1 - Mathf.Pow(1 - Mathf.Clamp01(ChimeraMoves.Fr(b * 2) * 4), 3);
                ex = Mathf.Lerp((Hash(i - 1) * 2 - 1) * 26 + 14, (Hash(i) * 2 - 1) * 26 + 14, f);
                ey = Mathf.Lerp((Hash(i - .5f) * 2 - 1) * 7 + 5, (Hash(i + .5f) * 2 - 1) * 7 + 5, f);
            }
            pupil.localPosition = U(I.pupil_center[0] + ex - I.center[0], I.pupil_center[1] + ey - I.center[1]);
            pupilSr.transform.localScale = Vector3.one * (2 * pr * px);
            glint.transform.localPosition = U(-pr * .35f, -pr * .35f);
            glint.transform.localScale = Vector3.one * (.44f * pr * px);

            // 경광등: 감시 중엔 천천히 깜빡, 발각하면 삐용삐용 — 빨간 빛줄기 두 개가 돌고 번쩍
            if (alert)
            {
                float flash = .5f + .5f * Mathf.Sin(sec * 26);
                domeGlow.transform.localScale = Vector3.one * (2 * (70 + 50 * flash) * px); domeGlow.color = A(.18f + .3f * flash);
                wedges.localRotation = Quaternion.Euler(0, 0, -sec * 9 * Mathf.Rad2Deg);
                foreach (var ws in wedgeSr) { ws.enabled = true; ws.color = A(.75f); }
            }
            else
            {
                domeGlow.transform.localScale = Vector3.one * (116 * px); domeGlow.color = A(.12f + .14f * (.5f + .5f * Mathf.Sin(sec * 4)));
                foreach (var ws in wedgeSr) ws.enabled = false;
            }
        }

        Color A(float a) { var c = sirenColor; c.a = a; return c; }

    }
}
