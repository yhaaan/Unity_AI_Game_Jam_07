using System;
using TMPro;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // For long stages with several gimmicks in a row: lists which challenge starts at which x. The stage then judges
    // rerolls ("can this build clear?") against the obstacle just ahead of the player, and the goal line under the
    // stage name follows along, so the player always sees what the next obstacle needs.
    [DisallowMultipleComponent]
    public sealed class StageSections : MonoBehaviour
    {
        [Serializable]
        public struct Section
        {
            [Tooltip("이 구간의 장애물이 끝나는 월드 x. 플레이어가 여기를 지나면 다음 구간으로 넘어간다.")] public float endX;
            public StageRuntime.Challenge challenge;
        }

        [SerializeField] internal Section[] sections = Array.Empty<Section>();

        internal static StageSections Instance { get; private set; }

        int shown = -1;
        TMP_Text goal;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        int IndexAt(float x)
        {
            for (int i = 0; i < sections.Length; i++)
                if (x < sections[i].endX) return i;
            return sections.Length - 1;
        }

        internal StageRuntime.Challenge ChallengeAt(float x) =>
            sections.Length == 0 ? default : sections[IndexAt(x)].challenge;

        void Update()
        {
            var stage = StageRuntime.Instance;
            if (!stage || !stage.Player || sections.Length == 0) return;
            int index = IndexAt(stage.Player.position.x);
            if (index == shown) return;
            if (!goal)
            {
                var top = FindFirstObjectByType<StageTopHud>();
                if (!top || !top.GoalText) return; // HUD not built yet; try again next frame
                goal = top.GoalText.GetComponent<TMP_Text>();
            }
            shown = index;
            goal.text = $"{index + 1}/{sections.Length}   {StageRuntime.RequirementFor(sections[index].challenge)}";
        }
    }
}
