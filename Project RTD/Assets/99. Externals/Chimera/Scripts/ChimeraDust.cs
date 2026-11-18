using System.Collections.Generic;
using UnityEngine;

namespace ChimeraGroove
{
    /// <summary>
    /// 발밑 먼지. 테두리 없는 둥근 구름 덩이가 커지면서 옅어집니다.
    /// ChimeraAnimator가 자동으로 만들어 쓰므로 직접 붙일 필요는 없습니다.
    /// </summary>
    public class ChimeraDust : MonoBehaviour
    {
        public Color color = new Color(.95f, .92f, .85f, 1f);
        public string sortingLayerName = "Default";
        public int sortingOrder = 95;

        const float Life = 0.55f;
        // 구름 한 덩이 = 겹친 원 3개 (x, y, 크기 비율)
        static readonly Vector3[] Blobs = { new Vector3(0, 0, 1f), new Vector3(.7f, -.2f, .7f), new Vector3(-.6f, -.25f, .65f) };

        class Puff { public Transform t; public SpriteRenderer[] sr; public float t0, size; public Vector3 pos, vel; public bool streak; }
        readonly List<Puff> pool = new List<Puff>();

        /// <summary>pos: 월드 위치(발밑), vel: 월드 속도, size: 월드 단위 반지름. streak=true면 바람 줄기(길쭉한 면).</summary>
        public void Emit(Vector3 pos, Vector3 vel, float size, bool streak = false)
        {
            Puff p = pool.Find(x => !x.t.gameObject.activeSelf);
            if (p == null)
            {
                p = new Puff { t = new GameObject("Puff").transform, sr = new SpriteRenderer[Blobs.Length] };
                p.t.SetParent(transform, false);
                for (int i = 0; i < Blobs.Length; i++)
                {
                    var go = new GameObject("Blob");
                    go.transform.SetParent(p.t, false);
                    go.transform.localPosition = new Vector3(Blobs[i].x, Blobs[i].y, 0);
                    go.transform.localScale = Vector3.one * (2 * Blobs[i].z); // 지름
                    p.sr[i] = go.AddComponent<SpriteRenderer>();
                    p.sr[i].sprite = ChimeraFanfare.Disc;
                }
                pool.Add(p);
            }
            foreach (var sr in p.sr) { sr.sortingLayerName = sortingLayerName; sr.sortingOrder = sortingOrder; }
            p.t0 = Time.time; p.size = size; p.vel = vel; p.streak = streak;
            for (int i = 0; i < p.sr.Length; i++)
            {
                p.sr[i].gameObject.SetActive(!streak || i == 0);
                p.sr[i].transform.localPosition = streak ? Vector3.zero : new Vector3(Blobs[i].x, Blobs[i].y, 0);
                p.sr[i].transform.localScale = Vector3.one * (streak ? 1f : 2 * Blobs[i].z);
            }
            p.pos = pos + Vector3.up * (size * .3f);
            p.t.localRotation = streak ? Quaternion.identity : Quaternion.Euler(0, 0, Random.value * 360);
            p.t.gameObject.SetActive(true);
            Step(p);
        }

        void Update() { foreach (var p in pool) if (p.t.gameObject.activeSelf) Step(p); }

        void Step(Puff p)
        {
            float age = (Time.time - p.t0) / Life;
            if (age >= 1) { p.t.gameObject.SetActive(false); return; }
            p.t.position = p.pos + p.vel * (age * .5f * (2 - age) * Life); // 처음엔 빠르게, 점점 느리게 퍼짐
            if (p.streak) p.t.localScale = new Vector3(p.size * (3.2f - 1.2f * age), p.size * .3f * (1 - .5f * age), 1); // 바람 줄기
            else p.t.localScale = Vector3.one * (p.size * (.6f + .8f * age));
            var c = color; c.a *= Mathf.Min(1, (1 - age) * 1.6f) * .9f;
            foreach (var sr in p.sr) sr.color = c;
        }
    }
}
