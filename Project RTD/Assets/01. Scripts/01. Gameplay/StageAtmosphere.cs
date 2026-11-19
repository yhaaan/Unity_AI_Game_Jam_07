using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectRTD.Gameplay
{
    /// <summary>
    /// 스테이지 분위기 연출을 카메라 화면에 맞춰 유지한다.
    ///   * fog: 메인 플랫폼과 맨앞 어두운 전경 사이에서 흐르는 바닥 연기 (RTD/Stage Fog)
    ///   * shafts: 천장에서 대각선으로 내리쬐는 빛줄기 (RTD/Light Shafts)
    ///   * ambientShade: 빛줄기 근처만 밝게 남기고 나머지를 어둡게 (RTD/Shaft Ambient)
    ///   * worldBend: 화면 전체를 좌우로 갈수록 살짝 내려가게 구부려 둥근 행성 위를 걷는 느낌 (RTD/Curved World)
    ///   * dust: 공중에 떠다니는 먼지. 빛줄기 안에서만 반짝인다 (RTD/Shaft Dust)
    /// 카메라 줌이 바뀌어도 화면 전체를 덮도록 렌더 직전에 크기·위치를 다시 잡는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageAtmosphere : MonoBehaviour
    {
        static readonly int ShaftViewId = Shader.PropertyToID("_ShaftView");

        [SerializeField] SpriteRenderer fog;
        [Tooltip("연기가 차지하는 화면 높이 비율(아래에서부터).")]
        [SerializeField, Range(0.1f, 1f)] float fogHeight = 0.62f;

        [SerializeField] SpriteRenderer shafts;
        [Tooltip("빛줄기 근처만 남기고 화면을 어둡게 덮는 층 (RTD/Shaft Ambient).")]
        [SerializeField] SpriteRenderer ambientShade;
        [Tooltip("먼지 파티클. 월드 공간에서 시뮬레이션되고, 발생 영역만 카메라를 따라간다.")]
        [SerializeField] ParticleSystem dust;

        static readonly int WorldBendId = Shader.PropertyToID("_WorldBend");

        [Tooltip("둥근 행성 위를 걷는 느낌: 화면 좌우 끝이 화면 높이의 이 비율만큼 내려간다 (RTD/Curved World, 0이면 평평).")]
        [SerializeField, Range(0f, 0.15f)] float worldBend = 0.06f;

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            Shader.SetGlobalFloat(WorldBendId, worldBend);
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Shader.SetGlobalFloat(WorldBendId, 0f);   // menus and cutscenes stay flat
        }

        void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled) Shader.SetGlobalFloat(WorldBendId, worldBend);
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam || !dust) return;
            // the emitter box follows the view; motes already spawned stay put in the world
            var p = cam.transform.position;
            dust.transform.position = new Vector3(p.x, p.y, 0f);
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam.cameraType != CameraType.Game || cam != Camera.main || !cam.orthographic) return;

            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;
            Shader.SetGlobalVector(ShaftViewId, new Vector4(c.x, c.y + halfH, halfH * 2f, 0f));

            if (fog && fog.sprite)
            {
                float h = halfH * 2f * fogHeight;
                Fit(fog, new Vector2(halfW * 2f * 1.15f, h), new Vector3(c.x, c.y - halfH + h * 0.5f - 0.05f, 0f));
            }

            var fullView = new Vector2(halfW * 2f + 1f, halfH * 2f + 1f);
            if (shafts && shafts.sprite) Fit(shafts, fullView, new Vector3(c.x, c.y, 0f));
            if (ambientShade && ambientShade.sprite) Fit(ambientShade, fullView, new Vector3(c.x, c.y, 0f));
        }

        static void Fit(SpriteRenderer r, Vector2 size, Vector3 position)
        {
            Vector2 native = r.sprite.bounds.size;
            r.transform.position = position;
            r.transform.localScale = new Vector3(size.x / native.x, size.y / native.y, 1f);
        }
    }
}
