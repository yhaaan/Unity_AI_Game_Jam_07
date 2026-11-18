using Unity.Cinemachine;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    // Blend the final shot, not the tracking target: the normal composer keeps following underneath.
    public sealed class StageOverviewCamera : CinemachineExtension
    {
        Bounds course;
        public float Weight { get; set; }

        public static StageOverviewCamera Begin(StageRuntime stage)
        {
            var limits = stage.transform.Find("Course Boundaries");
            if (!limits) return null;
            var boxes = limits.GetComponentsInChildren<BoxCollider2D>();
            if (boxes.Length == 0) return null;
            foreach (var root in stage.gameObject.scene.GetRootGameObjects())
            {
                var camera = root.GetComponentInChildren<CinemachineCamera>();
                if (!camera) continue;
                var overview = camera.gameObject.AddComponent<StageOverviewCamera>();
                overview.course = boxes[0].bounds;
                foreach (var box in boxes) overview.course.Encapsulate(box.bounds);
                overview.Weight = 1;
                return overview;
            }
            return null;
        }

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize || Weight <= 0) return;
            var lens = state.Lens;
            float fit = Mathf.Max(course.extents.y, course.extents.x / Mathf.Max(.01f, lens.Aspect)) * 1.1f;
            lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, Mathf.Max(fit, lens.OrthographicSize), Weight);
            state.Lens = lens;
            var pos = state.RawPosition + state.PositionCorrection;
            state.PositionCorrection += Weight * new Vector3(course.center.x - pos.x, course.center.y - pos.y, 0);
        }
    }
}
