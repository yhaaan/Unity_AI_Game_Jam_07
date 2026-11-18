using UnityEngine;

namespace ChimeraGroove
{
    /// <summary>
    /// 빈 오브젝트에 붙이고 Play 하면 과학자 5명과 드론이 나옵니다(동작 확인용).
    /// 과학자는 아이들 → 걷기 → 공격을, 드론은 감시 → 발각을 몇 초마다 번갈아 보여 줍니다.
    /// </summary>
    public class EnemyShowcase : MonoBehaviour
    {
        public float scale = 0.45f;
        public float spacing = 3.2f;
        public float secondsPerState = 3f;

        static readonly string[] Ids = { "ray", "pipe", "net", "egg", "magnet" };
        ScientistAnimator[] sci;
        DroneAnimator drone;

        void Start()
        {
            sci = new ScientistAnimator[Ids.Length];
            for (int i = 0; i < Ids.Length; i++)
            {
                var go = new GameObject("Scientist " + Ids[i]);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3((i - 2.5f) * spacing, -2.5f, 0);
                go.transform.localScale = Vector3.one * scale;
                sci[i] = go.AddComponent<ScientistAnimator>();
                sci[i].sortingOrderBase = i * 200;
                sci[i].SetScientist(Ids[i]);
            }
            var dg = new GameObject("Drone");
            dg.transform.SetParent(transform, false);
            dg.transform.localPosition = new Vector3(2.5f * spacing, -2.5f, 0);
            dg.transform.localScale = Vector3.one * scale;
            drone = dg.AddComponent<DroneAnimator>();
            drone.sortingOrderBase = 2000;
        }

        void Update()
        {
            int k = Mathf.FloorToInt(Time.time / secondsPerState);
            var st = (ScientistState)(k % 3);
            foreach (var s in sci) s.state = st;
            drone.state = k % 2 == 0 ? DroneState.Scan : DroneState.Alert;
        }
    }
}
