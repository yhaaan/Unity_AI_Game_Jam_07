using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRTD.Core
{
    /// <summary>
    /// 카메라 이동량에 비례해 배경 레이어를 움직여 원근감을 만든다.
    /// 편집 모드에서는 referenceCameraPosition에 카메라가 있을 때의 모습으로 배치한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("카메라 이동을 따라가는 비율. 0이면 월드에 고정(가까움), 1이면 화면에 고정(무한히 멂).")]
        [SerializeField] Vector2 followFactor = new(0.5f, 0.5f);

        [Tooltip("이 레이어가 편집된 위치 그대로 보이는 카메라 위치.")]
        [SerializeField] Vector2 referenceCameraPosition;

        [Tooltip("0보다 크면 자식 타일이 이 폭 간격으로 반복된다고 보고, 카메라 주변으로 순환시킨다.")]
        [SerializeField] float loopWidth;

        public enum ViewEdge { None, Bottom, Top }

        [Tooltip("전경용: 세로 위치를 화면 아래/위 가장자리에 붙인다. 편집 위치의 y가 그 가장자리로부터의 오프셋이 되고 followFactor.y는 무시된다. 카메라 줌이 바뀌어도 가장자리에 붙어 있다.")]
        [SerializeField] ViewEdge pinToViewEdge;

        Vector3 authoredPosition;
        Camera targetCamera;

        public Vector2 FollowFactor => followFactor;

        void Awake()
        {
            authoredPosition = transform.position;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            transform.position = authoredPosition;
        }

        // Cinemachine가 LateUpdate에서 카메라를 옮긴 뒤에 위치를 계산해야 떨림이 없다.
        void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera.cameraType != CameraType.Game)
                return;

            if (targetCamera == null)
                targetCamera = Camera.main;

            if (renderingCamera != targetCamera)
                return;

            Vector2 cameraPosition = renderingCamera.transform.position;
            Vector2 delta = cameraPosition - referenceCameraPosition;
            Vector3 position = authoredPosition + new Vector3(delta.x * followFactor.x, delta.y * followFactor.y, 0f);

            if (pinToViewEdge != ViewEdge.None && renderingCamera.orthographic)
            {
                float halfHeight = renderingCamera.orthographicSize;
                position.y = cameraPosition.y + (pinToViewEdge == ViewEdge.Bottom ? -halfHeight : halfHeight) + authoredPosition.y;
            }

            if (loopWidth > 0f)
                position.x += Mathf.Round((cameraPosition.x - position.x) / loopWidth) * loopWidth;

            transform.position = position;
        }
    }
}
