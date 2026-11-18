using UnityEngine;

namespace ProjectRTD.Gameplay
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
    public sealed class StageLadder2D : MonoBehaviour
    {
        [SerializeField] private BoxCollider2D[] landings;

        internal float CenterX => transform.position.x;
        internal float LowestSurface => landings[0].bounds.max.y;
        internal float HighestSurface => landings[landings.Length - 1].bounds.max.y;

        internal bool CanGrab(Bounds reach, float x, float feet) =>
            x >= reach.min.x && x <= reach.max.x &&
            feet >= LowestSurface - .15f && feet <= HighestSurface + .25f;

        internal bool TryGetLanding(float feet, float tolerance, out float surface)
        {
            foreach (BoxCollider2D landing in landings)
            {
                surface = landing.bounds.max.y;
                if (Mathf.Abs(feet - surface) <= tolerance) return true;
            }
            surface = 0f;
            return false;
        }

        internal void SetClimbing(Collider2D player, bool climbing)
        {
            foreach (BoxCollider2D landing in landings)
                if (landing && landing.enabled) Physics2D.IgnoreCollision(landing, player, climbing);
        }
    }
}
