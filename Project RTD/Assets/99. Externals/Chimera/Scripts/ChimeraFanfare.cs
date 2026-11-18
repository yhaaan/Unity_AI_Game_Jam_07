using System;
using UnityEngine;

namespace ChimeraGroove
{
    /// <summary>
    /// 조합 완료 이펙트(빵빠레). 뒤에는 빙글 도는 빛줄기 12개 + 망점 동그라미 6개 + 번지는 링 3개, 앞에는 흩어지는 둥근 점과 제자리에서 피었다 지는 통통한 반짝이.
    /// 그림 파일 없이 실행 중에 텍스처를 만들어 쓰므로 스크립트만 있으면 됩니다.
    /// ChimeraAnimator.PlayCombine()이 자동으로 만들어 재생하지만, 빈 오브젝트에 붙여 Play()만 불러도 됩니다.
    /// </summary>
    public class ChimeraFanfare : MonoBehaviour
    {
        [Tooltip("뒤쪽 원·링 색. 어두운 배경이면 흰색, 밝은 배경이면 노란색 계열이 잘 보입니다.")]
        public Color glow = Color.white;
        [Tooltip("흩어지는 둥근 점의 색. 알록달록하지 않게 한 색 계열만 넣으세요.")]
        public Color[] colors = { Color.white };
        [Tooltip("반짝이(네 갈래 별) 색.")]
        public Color sparkle = Color.white;
        public float pixelsPerUnit = 100f;
        public string sortingLayerName = "Default";
        public int backOrder = -10, frontOrder = 1000;

        const float Duration = 2.2f;
        const int RayCount = 12, PetalCount = 6, RingCount = 3, PartCount = 36, SparkCount = 7;

        struct Part { public SpriteRenderer sr; public float vx, vy, size; public Color col; }
        struct Spark { public SpriteRenderer sr; public float x, y, delay, size, rot; }

        static Sprite raySprite, discSprite, halftoneSprite, ringSprite, sparkSprite;

        Transform root;
        SpriteRenderer glowSr;
        SpriteRenderer[] petals, rings, rays;
        Transform rayRoot;
        Part[] parts;
        Spark[] sparks;
        float t0 = float.NegativeInfinity;

        // u,v ∈ [-1,1] 에서 불투명도(0~1)를 돌려주는 함수로 흰색 스프라이트를 만듦. 한 변이 1유닛.
        static Sprite Make(int n, Func<float, float, float> alpha, float pivotX = .5f)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + .5f) / n * 2 - 1, v = (y + .5f) / n * 2 - 1;
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.Clamp01(alpha(u, v))));
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(pivotX, .5f), n);
        }

        /// <summary>가장자리가 부드러운 흰 원 스프라이트(지름 1유닛). 먼지 이펙트도 같이 씁니다.</summary>
        public static Sprite Disc { get { EnsureSprites(); return discSprite; } }

        /// <summary>끝으로 갈수록 벌어지며 옅어지는 쐐기(왼쪽 끝이 피벗). 빛줄기·레이저에 씁니다.</summary>
        public static Sprite Wedge { get { EnsureSprites(); return raySprite; } }
        /// <summary>가는 원형 링(지름 1유닛).</summary>
        public static Sprite Ring { get { EnsureSprites(); return ringSprite; } }
        /// <summary>네 갈래 반짝이(지름 1유닛). 섬광에 씁니다.</summary>
        public static Sprite SparkSprite { get { EnsureSprites(); return sparkSprite; } }
        static Sprite squareSprite;
        /// <summary>흰 정사각형(한 변 1유닛). 광선·선에 씁니다.</summary>
        public static Sprite Square { get { if (squareSprite == null) squareSprite = Make(4, (u, v) => 1f); return squareSprite; } }

        static void EnsureSprites()
        {
            if (discSprite != null) return;
            // 빛줄기: 왼쪽 끝(피벗)에서 오른쪽으로 벌어지며 옅어지는 쐐기
            raySprite = Make(128, (u, v) =>
            {
                float d = (u + 1) / 2;
                return Mathf.Clamp01((.22f * d - Mathf.Abs(v)) * 64) * (1 - d);
            }, 0f);
            discSprite = Make(128, (u, v) => (1 - Mathf.Sqrt(u * u + v * v)) * 64);
            // 망점: 격자 위의 점이 가장자리로 갈수록 작아짐
            halftoneSprite = Make(256, (u, v) =>
            {
                const float cell = .12f;
                float cu = Mathf.Round(u / cell) * cell, cv = Mathf.Round(v / cell) * cell;
                float dotR = .0352f * (1 - Mathf.Sqrt(cu * cu + cv * cv));
                float d = Mathf.Sqrt((u - cu) * (u - cu) + (v - cv) * (v - cv));
                return (dotR - d) * 128;
            });
            ringSprite = Make(256, (u, v) => (.03f - Mathf.Abs(Mathf.Sqrt(u * u + v * v) - .95f)) * 128);
            // 끝이 둥글고 통통한 네 갈래 별
            sparkSprite = Make(128, (u, v) => (.86f - Mathf.Pow(Mathf.Abs(u), .72f) - Mathf.Pow(Mathf.Abs(v), .72f)) * 30);
        }

        SpriteRenderer NewSr(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sortingLayerName = sortingLayerName; sr.sortingOrder = order;
            return sr;
        }

        void Build()
        {
            EnsureSprites();
            root = new GameObject("FanfareRoot").transform;
            root.SetParent(transform, false);
            rayRoot = new GameObject("Rays").transform;
            rayRoot.SetParent(root, false);
            rays = new SpriteRenderer[RayCount];
            for (int i = 0; i < RayCount; i++)
            {
                rays[i] = NewSr("Ray", raySprite, backOrder);
                rays[i].transform.SetParent(rayRoot, false);
                rays[i].transform.localRotation = Quaternion.Euler(0, 0, i * 360f / RayCount);
            }
            glowSr = NewSr("Glow", discSprite, backOrder);
            petals = new SpriteRenderer[PetalCount];
            for (int i = 0; i < PetalCount; i++) petals[i] = NewSr("Petal", halftoneSprite, backOrder + 1);
            rings = new SpriteRenderer[RingCount];
            for (int i = 0; i < RingCount; i++) rings[i] = NewSr("Ring", ringSprite, backOrder + 2);
            parts = new Part[PartCount];
            for (int i = 0; i < PartCount; i++)
                parts[i] = new Part { sr = NewSr("Dot", discSprite, frontOrder) };
            sparks = new Spark[SparkCount];
            for (int i = 0; i < SparkCount; i++) sparks[i] = new Spark { sr = NewSr("Sparkle", sparkSprite, frontOrder + 1) };
        }

        /// <summary>delay초 뒤에 이펙트를 터뜨립니다.</summary>
        public void Play(float delay = 0f)
        {
            if (root != null) Destroy(root.gameObject); // 색·정렬 설정이 바뀌었을 수 있으니 새로 만듦
            Build();
            t0 = Time.time + delay;
            for (int i = 0; i < PartCount; i++)
            {
                float ang = UnityEngine.Random.value * Mathf.PI * 2, sp = 300 + UnityEngine.Random.value * 500;
                parts[i].vx = Mathf.Cos(ang) * sp;
                parts[i].vy = Mathf.Sin(ang) * sp - 60;
                parts[i].size = 7 + UnityEngine.Random.value * 7;
                parts[i].col = colors[i % colors.Length];
            }
            // 반짝이: 캐릭터 둘레에 하나씩 시차를 두고 피어남
            for (int i = 0; i < SparkCount; i++)
            {
                float ang = (i + UnityEngine.Random.value * .7f) * Mathf.PI * 2 / SparkCount, rad = 130 + UnityEngine.Random.value * 100;
                sparks[i].x = Mathf.Cos(ang) * rad; sparks[i].y = Mathf.Sin(ang) * rad;
                sparks[i].delay = UnityEngine.Random.value * .6f;
                sparks[i].size = 16 + UnityEngine.Random.value * 14;
                sparks[i].rot = (UnityEngine.Random.value - .5f) * 30;
            }
            Step();
        }

        void Update() { if (root != null) Step(); }

        static Color A(Color c, float a) { c.a *= Mathf.Clamp01(a); return c; }

        // 캔버스 픽셀(아래 +) → 유닛(위 +)
        Vector3 U(float x, float y) { return new Vector3(x / pixelsPerUnit, -y / pixelsPerUnit, 0); }

        void Step()
        {
            float t = Time.time - t0;
            bool on = t >= 0 && t <= Duration;
            if (root.gameObject.activeSelf != on) root.gameObject.SetActive(on);
            if (!on) { if (t > Duration) { Destroy(root.gameObject); root = null; } return; }

            float fade = t < 1.1f ? 1 : 1 - (t - 1.1f) / .5f;
            float pop = ChimeraMoves.BackOut(Mathf.Min(1, t / .35f));
            float px = 1f / pixelsPerUnit;

            // 빙글 도는 빛줄기: 길고 짧은 줄기가 번갈아
            float rayA = Mathf.Clamp01(t / .2f) * Mathf.Clamp01((Duration - t) / .5f);
            rayRoot.localRotation = Quaternion.Euler(0, 0, -t * .9f * Mathf.Rad2Deg);
            for (int i = 0; i < RayCount; i++)
            {
                rays[i].transform.localScale = Vector3.one * ((i % 2 == 1 ? 270 : 380) * pop * px);
                rays[i].color = A(glow, .6f * rayA);
            }

            // 가운데 은은한 빛
            glowSr.transform.localScale = Vector3.one * (340 * pop * px);
            glowSr.color = A(glow, .28f * fade);

            // 망점 동그라미 6개가 꽃잎처럼 퍼지며 천천히 회전
            for (int i = 0; i < PetalCount; i++)
            {
                float ang = i * Mathf.PI * 2 / PetalCount + t * .25f;
                petals[i].transform.localPosition = U(Mathf.Cos(ang) * 150 * pop, Mathf.Sin(ang) * 150 * pop);
                petals[i].transform.localScale = Vector3.one * (250 * pop * px);
                petals[i].color = A(glow, .75f * fade);
            }

            // 번져 나가는 링 3개
            for (int j = 0; j < RingCount; j++)
            {
                float u = (t - j * .12f) / .7f;
                bool show = u > 0 && u < 1;
                rings[j].enabled = show;
                if (!show) continue;
                float r = 60 + 220 * (1 - Mathf.Pow(1 - u, 3));
                rings[j].transform.localScale = Vector3.one * (2 * r / .95f * px);
                rings[j].color = A(glow, (1 - u) * .9f);
            }

            // 흩어지는 둥근 점
            float drag = (1 - Mathf.Exp(-3 * t)) / 3;
            for (int i = 0; i < PartCount; i++)
            {
                var p = parts[i];
                var tr = p.sr.transform;
                tr.localPosition = U(p.vx * drag, p.vy * drag + 60 * t * t);
                tr.localScale = Vector3.one * (.9f * p.size * px); // 둥근 점
                p.sr.color = A(p.col, fade);
            }

            // 반짝이: 부드럽게 커졌다 사라짐
            for (int i = 0; i < SparkCount; i++)
            {
                var q = sparks[i];
                float u = (t - q.delay) / .7f;
                bool show = u > 0 && u < 1;
                q.sr.enabled = show;
                if (!show) continue;
                q.sr.transform.localPosition = U(q.x, q.y);
                q.sr.transform.localRotation = Quaternion.Euler(0, 0, q.rot - u * 35);
                q.sr.transform.localScale = Vector3.one * (2 * q.size * Mathf.Sin(Mathf.PI * u) * px);
                q.sr.color = sparkle;
            }
        }
    }
}
