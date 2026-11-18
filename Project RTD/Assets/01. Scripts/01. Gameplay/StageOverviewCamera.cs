using Unity.Cinemachine;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Keeps the shot inside the lab room: its left/right edges are the invisible course walls, so the
    // screen border reads as the wall. Weight blends in the whole-room overview used when a stage starts.
    // Works on the final shot, not the tracking target, so the normal composer keeps following underneath.
    public sealed class StageOverviewCamera : CinemachineExtension
    {
        // How far below the walkway the camera may look, and how high the room goes above it.
        public const float FloorView = 1.6f, RoomHeight = 12.35f;

        Rect room;
        public float Weight { get; set; }

        public static StageOverviewCamera Install(StageRuntime stage, float left, float right, float ground)
        {
            foreach (var root in stage.gameObject.scene.GetRootGameObjects())
            {
                var camera = root.GetComponentInChildren<CinemachineCamera>();
                if (!camera) continue;
                if (!camera.TryGetComponent(out StageOverviewCamera bounds)) bounds = camera.gameObject.AddComponent<StageOverviewCamera>();
                bounds.room = Rect.MinMaxRect(left, ground - FloorView, right, ground + RoomHeight);
                return bounds;
            }
            return null;
        }

        public static StageOverviewCamera Begin(StageRuntime stage)
        {
            foreach (var root in stage.gameObject.scene.GetRootGameObjects())
            {
                var camera = root.GetComponentInChildren<CinemachineCamera>();
                if (!camera || !camera.TryGetComponent(out StageOverviewCamera overview)) continue;
                overview.Weight = 1;
                return overview;
            }
            return null;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize || room.width <= 0) return;
            var lens = state.Lens;
            float aspect = Mathf.Max(.01f, lens.Aspect);
            if (Weight > 0)
            {
                // the largest view that still fits inside the room
                float fit = Mathf.Min(room.width / 2f / aspect, room.height / 2f);
                lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, fit, Weight);
            }
            float halfH = Mathf.Min(lens.OrthographicSize, room.height / 2f, room.width / 2f / aspect);
            lens.OrthographicSize = halfH;
            state.Lens = lens;
            float halfW = halfH * aspect;

            var pos = state.RawPosition + state.PositionCorrection;
            var target = Vector2.Lerp(pos, room.center, Weight);
            target.x = Mathf.Clamp(target.x, room.xMin + halfW, room.xMax - halfW);
            target.y = Mathf.Clamp(target.y, room.yMin + halfH, room.yMax - halfH);
            state.PositionCorrection += new Vector3(target.x - pos.x, target.y - pos.y, 0);
        }
    }
}
