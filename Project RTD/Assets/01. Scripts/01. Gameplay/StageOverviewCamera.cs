using Unity.Cinemachine;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Keeps the shot inside the lab room: its left/right edges are the invisible course walls, so the
    // screen border reads as the wall. Weight blends in the whole-room overview used when a stage starts.
    // Works on the final shot, not the tracking target, so the normal composer keeps following underneath.
    // The room is read from the walls every frame, so moving a wall in the Scene view moves the camera limit.
    public sealed class StageOverviewCamera : CinemachineExtension
    {
        [Tooltip("투명벽. 카메라 왼쪽 끝 = 이 콜라이더의 오른쪽 면")] public BoxCollider2D leftWall;
        [Tooltip("투명벽. 카메라 오른쪽 끝 = 이 콜라이더의 왼쪽 면")] public BoxCollider2D rightWall;
        [Tooltip("바닥(벽 오브젝트의 y)보다 아래로 보여줄 높이")] public float floorView = 1.6f;
        [Tooltip("바닥 위로 천장까지의 높이")] public float roomHeight = 12.35f;

        public float Weight { get; set; }

        Rect Room
        {
            get
            {
                if (!leftWall || !rightWall) return default;
                float ground = Mathf.Min(leftWall.transform.position.y, rightWall.transform.position.y);
                return Rect.MinMaxRect(leftWall.bounds.max.x, ground - floorView, rightWall.bounds.min.x, ground + roomHeight);
            }
        }

        public static StageOverviewCamera Install(StageRuntime stage, BoxCollider2D leftWall, BoxCollider2D rightWall)
        {
            foreach (var root in stage.gameObject.scene.GetRootGameObjects())
            {
                var camera = root.GetComponentInChildren<CinemachineCamera>();
                if (!camera) continue;
                if (!camera.TryGetComponent(out StageOverviewCamera bounds)) bounds = camera.gameObject.AddComponent<StageOverviewCamera>();
                bounds.leftWall = leftWall;
                bounds.rightWall = rightWall;
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
            var room = Room;
            if (stage != CinemachineCore.Stage.Finalize || room.width <= 0 || room.height <= 0) return;
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

        // Scene view: the area the camera may show (= the room).
        void OnDrawGizmos()
        {
            var room = Room;
            if (room.width <= 0) return;
            Gizmos.color = new Color(1f, .8f, .2f, .9f);
            Gizmos.DrawWireCube(room.center, room.size);
        }
    }
}
