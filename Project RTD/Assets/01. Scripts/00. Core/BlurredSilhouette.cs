using UnityEngine;

namespace ProjectRTD.Core
{
    /// <summary>
    /// 전경용 흐릿한 실루엣. 스프라이트를 여백이 있는 FullRect 스프라이트로 바꿔
    /// RTD/Sprite Foreground Blur 셰이더의 번짐이 윤곽 밖으로 퍼질 수 있게 한다.
    /// 시트에서 잘라낸 스프라이트도 이웃 그림이 섞이지 않도록 원래 영역(_UVRect)만 샘플링한다.
    /// 블러 반경은 월드 단위라서 스프라이트 해상도·스케일이 달라도 같은 정도로 흐려진다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    [DisallowMultipleComponent]
    public sealed class BlurredSilhouette : MonoBehaviour
    {
        static readonly int UVRectId = Shader.PropertyToID("_UVRect");
        static readonly int BlurTexelsId = Shader.PropertyToID("_BlurTexelsXY");

        [Tooltip("블러 반경(월드 유닛).")]
        [SerializeField] float blurRadius = .14f;

        Sprite padded;

        void Awake()
        {
            var sr = GetComponent<SpriteRenderer>();
            var source = sr.sprite;
            if (!source || !source.texture) return;

            var tex = source.texture;
            Vector3 scale = transform.lossyScale;
            var blurTexels = new Vector2(
                blurRadius * source.pixelsPerUnit / Mathf.Max(1e-4f, Mathf.Abs(scale.x)),
                blurRadius * source.pixelsPerUnit / Mathf.Max(1e-4f, Mathf.Abs(scale.y)));
            int padX = Mathf.CeilToInt(blurTexels.x * 1.5f), padY = Mathf.CeilToInt(blurTexels.y * 1.5f);

            Rect rect = source.textureRect;
            float xMin = Mathf.Max(0, rect.xMin - padX), yMin = Mathf.Max(0, rect.yMin - padY);
            float xMax = Mathf.Min(tex.width, rect.xMax + padX), yMax = Mathf.Min(tex.height, rect.yMax + padY);
            var grown = Rect.MinMaxRect(xMin, yMin, xMax, yMax);

            Vector2 pivotPx = source.pivot + (rect.position - grown.position);
            padded = Sprite.Create(tex, grown, new Vector2(pivotPx.x / grown.width, pivotPx.y / grown.height),
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            padded.name = source.name + " (blur)";
            sr.sprite = padded;

            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            block.SetVector(UVRectId, new Vector4(rect.xMin / tex.width, rect.yMin / tex.height, rect.xMax / tex.width, rect.yMax / tex.height));
            block.SetVector(BlurTexelsId, blurTexels);
            sr.SetPropertyBlock(block);
        }

        void OnDestroy()
        {
            if (padded) Destroy(padded);
        }
    }
}
