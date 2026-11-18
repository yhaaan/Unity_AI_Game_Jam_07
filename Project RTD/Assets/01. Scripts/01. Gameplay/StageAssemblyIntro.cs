using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class StageAssemblyIntro : MonoBehaviour
    {
        [SerializeField] private StageRuntime stage;
        [SerializeField] private PlayerPartLoadout loadout;
        [SerializeField] private TMP_Text result;
        [SerializeField] private GameObject actions;
        [SerializeField] private UnityEngine.UI.Button rerollButton;
        [SerializeField] private UnityEngine.UI.Button enterButton;
        [SerializeField] private Material rollMaterial;

        private ChimeraRollPresentation presentation;
        private TMP_Text enterLabel;
        private bool waitingForChoice;

        public bool IsPlaying { get; private set; } = true;

        private IEnumerator Start()
        {
            var visual = loadout.GetComponentInChildren<PlayerPartVisual>(true);
            enterLabel = enterButton.GetComponentInChildren<TMP_Text>();
            presentation = new ChimeraRollPresentation((RectTransform)transform, (RectTransform)rerollButton.transform,
                result.font, visual ? visual.PartArt : null, rollMaterial);
            rerollButton.interactable = false;
            rerollButton.onClick.AddListener(Reroll);
            enterButton.onClick.AddListener(Finish);
            yield return null; // The stage's loadout rolls its initial parts in Start.
            yield return Reveal();
        }

        private void LateUpdate() => presentation?.UpdateLayout();

        private void OnDisable()
        {
            presentation?.Dispose();
            presentation = null;
        }

        private void Update()
        {
            if (!waitingForChoice || Keyboard.current == null) return;
            if (Keyboard.current.rKey.wasPressedThisFrame)
                Reroll();
            else if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                Finish();
        }

        private void Reroll()
        {
            if (!waitingForChoice) return;
            if (loadout.TryResummon()) StartCoroutine(Reveal());
        }

        private IEnumerator Reveal()
        {
            waitingForChoice = false;
            rerollButton.interactable = false;
            actions.SetActive(false);
            result.text = "ASSEMBLING";
            PlayerPartBuild build = loadout.Current;
            presentation.Play(build);
            while (!presentation.Finished) yield return null;

            bool ready = stage.CanClearWith(build);
            result.text = ready ? "READY" : $"NEEDS {stage.Requirement}";
            if (enterLabel) enterLabel.text = ready ? "ENTER STAGE" : "ENTER ANYWAY";
            waitingForChoice = true;
            rerollButton.interactable = true;
            actions.SetActive(true);
        }

        private void Finish()
        {
            if (!waitingForChoice) return;
            IsPlaying = false;
            waitingForChoice = false;
            gameObject.SetActive(false);
            loadout.RevealCurrent();
        }
    }
}
