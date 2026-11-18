using UnityEngine;

namespace ProjectRTD.Gameplay
{
    /// <summary>
    /// floor.png 바닥 띠(9-slice)와 바닥 콜라이더("Island Surface")를 서로 맞춰 둔다. 편집 모드에서도 돈다.
    ///   바닥(pit 꺼짐): 둘 중 하나를 Scene 뷰에서 옮기거나 늘이면 다른 쪽이 따라온다.
    ///                   그림은 Rect 툴(T)로 좌우·아래 끝을 끌면 되고, 윗면 = 밟는 면이다.
    ///   구덩이(pit 켜짐): from 오른쪽 끝 ~ to 왼쪽 끝 사이를 pitDrop만큼 낮춰 채운다(양쪽 바닥을 따라감).
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class LabFloorArt : MonoBehaviour
    {
        public BoxCollider2D from, to;
        public bool pit;
        [Tooltip("바닥 윗면에서 그림이 내려가는 깊이(월드 단위). 카메라가 보는 가장 아래보다 길게.")]
        public float depth = 6f;
        [Tooltip("구덩이 바닥이 양쪽 바닥보다 낮은 정도")]
        public float pitDrop = .9f;

        SpriteRenderer view;
        bool synced;
        Vector3 artPosition, artScale;
        Vector2 artSize;
        Bounds colliderBounds;

        void OnEnable() => synced = false;

        void LateUpdate()
        {
            if (!from || (pit && !to)) return;
            if (!view) view = GetComponent<SpriteRenderer>();
            view.drawMode = SpriteDrawMode.Sliced;
            if (pit) FitPit();
            else if (synced && ArtChanged() && !Application.isPlaying) MoveColliderToArt();
            else if (!synced || from.bounds != colliderBounds) FitArt(from.bounds.min.x, from.bounds.max.x, from.bounds.max.y);
            Remember();
        }

        bool ArtChanged() => transform.position != artPosition || transform.lossyScale != artScale || view.size != artSize;

        void FitPit()
        {
            float left = from.bounds.max.x, right = to.bounds.min.x;
            view.enabled = right - left > .05f;
            if (view.enabled) FitArt(left, right, to.bounds.max.y - pitDrop);
        }

        void FitArt(float left, float right, float top)
        {
            var scale = transform.lossyScale;
            var size = new Vector2((right - left) / Mathf.Max(.0001f, Mathf.Abs(scale.x)), depth / Mathf.Max(.0001f, Mathf.Abs(scale.y)));
            var position = new Vector3((left + right) * .5f, top - depth * .5f, transform.position.z);
            if (view.size == size && transform.position == position) return;
            Record(view, transform);
            view.size = size;
            transform.position = position;
        }

        // The art was dragged in the Scene view: the walkable collider takes its left/right ends and its top.
        void MoveColliderToArt()
        {
            var art = view.bounds;
            depth = art.size.y;
            var b = from.bounds;
            var t = from.transform;
            float sx = Mathf.Max(.0001f, Mathf.Abs(t.lossyScale.x));
            Vector3 center = new(art.center.x, art.max.y - b.extents.y, b.center.z);
            Record(from, this);
            from.offset = (Vector2)t.InverseTransformPoint(center);
            from.size = new Vector2(art.size.x / sx, from.size.y);
        }

        void Remember()
        {
            synced = true;
            artPosition = transform.position;
            artScale = transform.lossyScale;
            artSize = view.size;
            colliderBounds = from.bounds;
        }

        static void Record(params Object[] targets)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RecordObjects(targets, "Lab Floor");
#endif
        }
    }
}
