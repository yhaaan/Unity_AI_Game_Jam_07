using ProjectRTD.Gameplay;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

namespace ProjectRTD.Debugging
{
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerPartLoadout))]
    public sealed class PlayerPartsDebugHud : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private PlayerPartLoadout loadout;
        private PlayerMotor2D motor;
        private bool rerollFailed;
        private bool visible = true;

        private void Awake()
        {
            loadout = GetComponent<PlayerPartLoadout>();
            motor = GetComponent<PlayerMotor2D>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f1Key.wasPressedThisFrame) visible = !visible;
            if (keyboard.f8Key.wasPressedThisFrame) Reroll();
        }

        private void Reroll() => rerollFailed = !loadout.TryResummon();

        private void OnGUI()
        {
            if (!visible || Screen.width <= 20 || Screen.height <= 20) return;

            float scale = Mathf.Min(2f, Mathf.Min((Screen.width - 20f) / 330f, (Screen.height - 20f) / 270f));
            var previousMatrix = GUI.matrix;
            var previousFont = GUI.skin.font;
            try
            {
                GUI.skin.font = ProjectRTD.Core.GameTypography.GuiFont;
                GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(10f, 10f));
                DrawPanel();
            }
            finally
            {
                GUI.skin.font = previousFont;
                GUI.matrix = previousMatrix;
            }
        }

        private void DrawPanel()
        {
            var build = loadout.Current;
            GUILayout.BeginArea(new Rect(10f, 10f, 330f, 270f));
            GUILayout.BeginVertical(GUI.skin.window);
            GUILayout.Label("플레이어 파츠 디버그");
            if (build == null)
            {
                GUILayout.Label("장착된 파츠 없음");
            }
            else
            {
                GUILayout.Label($"머리: {build.Head.DisplayName}\n몸통: {build.Body.DisplayName}\n다리: {build.Legs.DisplayName}");
                GUILayout.Label($"키 {build.Height}   |   무게 {build.Weight}   |   체력 단계 {build.Health}/5\n" +
                    $"이동속도 {build.Speed}/5   ({motor.MoveSpeed:0.##} 유닛/초)\n" +
                    $"파워 {build.Power}/3   |   점프력 {build.Jump}/3   (초속 {motor.JumpVelocity:0.##} 유닛/초)");
            }
            if (GUILayout.Button("전체 파츠 랜덤 재추첨 [F8]")) Reroll();
            if (rerollFailed) GUILayout.Label("재추첨 실패: 카탈로그와 슬롯 후보를 확인하세요.");
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
#endif
    }
}
