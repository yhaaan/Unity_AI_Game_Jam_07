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
            var art = SharedMenuArt.Loaded;
            if (art)
            {
                var canvas = backButton.GetComponentInParent<Canvas>().transform;
                var backdrop = MenuArtView.Art(canvas, art.labIllustration, 0,0,1,1,false);
                backdrop.color = new Color(.35f,.35f,.35f,1);
                backdrop.transform.parent.SetAsFirstSibling();
                MenuArtView.Art(canvas, art.brand, .025f,.72f,.19f,.19f);
                MenuArtView.Art(canvas, art.subtitle, .025f,.62f,.19f,.1f);
                MenuArtView.Art(backButton.transform, art.backPanel, 0,0,1,1,false).transform.parent.SetAsFirstSibling();
            }
            int unlocked = SceneFlow.HighestUnlockedStage;
            for (int i = 0; i < levels.Length; i++)
            {
                int stage = i + 1;
                var button = levels[i];
                button.interactable = stage <= unlocked;
                if (!button.interactable) button.transform.Find("Status").GetComponent<TMP_Text>().text = "LOCKED";
                if (art)
                {
                    var face = MenuArtView.Art(button.transform, button.interactable ? art.available : art.locked, 0,0,1,1,false);
                    face.transform.parent.SetAsFirstSibling();
                    button.targetGraphic = face;
                    MenuArtView.Art(button.transform, i % 2 == 0 ? art.leftPrint : art.rightPrint, .04f,.12f,.16f,.26f);
                }
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
