using UnityEngine;

namespace ChimeraGroove
{
    /// <summary>
    /// 빈 오브젝트에 붙이고 Play 하면 조합 6개가 같은 박자로 춤춥니다(동작 확인용).
    /// 컴포넌트 우클릭 → Shuffle (구 Input Manager를 쓰는 프로젝트는 스페이스바도 가능): 전부 무작위로 다시 조합하고 조합 완료 연출을 재생.
    /// </summary>
    public class ChimeraShowcase : MonoBehaviour
    {
        public string resourcesRoot = "ChimeraParts";
        [Range(70, 160)] public float bpm = 112f;
        public ChimeraState state = ChimeraState.Groove;
        [Tooltip("시간을 초당 12장으로 끊어 손그림 카툰처럼 보이게 합니다.")]
        public bool handDrawn = false;
        public float scale = 0.6f;
        public Vector2 spacing = new Vector2(3.6f, 3.6f);

        static readonly string[][] Combos =
        {
            new[] { "pig", "sheep", "chicken" },
            new[] { "donkey", "chicken", "pig" },
            new[] { "cow", "duck", "rabbit" },
            new[] { "rabbit", "bear", "horse" },
            new[] { "bear", "gumiho", "kangaroo" },
            new[] { "goat", "phoenix", "unicorn" },
        };

        ChimeraAnimator[] dancers;

        void Start()
        {
            dancers = new ChimeraAnimator[Combos.Length];
            for (int i = 0; i < Combos.Length; i++)
            {
                var go = new GameObject("Chimera " + i);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3((i % 3 - 1) * spacing.x, i < 3 ? 0.6f : 0.6f - spacing.y, 0);
                go.transform.localScale = Vector3.one * scale;
                var c = go.AddComponent<ChimeraAnimator>();
                c.resourcesRoot = resourcesRoot;
                c.sortingOrderBase = i * 100;
                c.SetCombo(Combos[i][0], Combos[i][1], Combos[i][2]);
                dancers[i] = c;
            }
        }

        void Update()
        {
            ChimeraBeat.Bpm = bpm;
            ChimeraBeat.StepsPerSecond = handDrawn ? 12f : 0f;
            foreach (var c in dancers) c.state = state;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space)) Shuffle();
#endif
        }

        [ContextMenu("Shuffle")]
        public void Shuffle()
        {
            var m = ChimeraAnimator.LoadManifest(resourcesRoot);
            if (m == null) return;
            foreach (var c in dancers)
                c.Combine(
                    m.parts.heads[Random.Range(0, m.parts.heads.Length)].id,
                    m.parts.bodies[Random.Range(0, m.parts.bodies.Length)].id,
                    m.parts.legs[Random.Range(0, m.parts.legs.Length)].id);
        }
    }
}
