using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRTD.Core
{
    [DisallowMultipleComponent]
    public sealed class LevelSelectController : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Button[] levels;
        [SerializeField] private UnityEngine.UI.Button backButton;

        private void Start()
        {
            int unlocked = SceneFlow.HighestUnlockedStage;
            for (int i = 0; i < levels.Length; i++)
            {
                int stage = i + 1;
                var button = levels[i];
                button.interactable = stage <= unlocked;
                if (!button.interactable) button.transform.Find("Status").GetComponent<TMP_Text>().text = "LOCKED";
                button.onClick.AddListener(() => SelectStage(stage));
            }
            backButton.onClick.AddListener(Back);
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true) Back();
        }

        private void SelectStage(int stage)
        {
            SoundManager.Play(Sfx.Confirm);
            SceneFlow.LoadSelectedStage(stage);
        }

        private void Back()
        {
            SoundManager.Play(Sfx.Confirm);
            SceneFlow.LeaveLevelSelect();
        }
    }
}
