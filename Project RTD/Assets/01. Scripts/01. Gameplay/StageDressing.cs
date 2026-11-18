using ChimeraGroove.Plants;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Gameplay
{
    /// <summary>
    /// Dresses every stage when it loads (no scene edits, so nothing to merge):
    ///   * the exit (StageGoal) becomes the lab shutter door that opens as the player walks up
    ///   * "Jump Wall" blocks get the lab-barrier pillar art at their exact size
    /// Stages are indoor lab rooms, so no plants or other natural props are added.
    /// </summary>
    public static class StageDressing
    {
        const string Marker = "Stage Dressing";
        const int WallOrder = 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            Dress(SceneManager.GetActiveScene());
        }

        static void OnLoaded(Scene scene, LoadSceneMode mode) => Dress(scene);

        public static void Dress(Scene scene)
        {
            StageRuntime stage = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == Marker) return;   // already dressed
                if (!stage) stage = root.GetComponentInChildren<StageRuntime>(true);
            }
            if (!stage) return;

            var holder = new GameObject(Marker);
            SceneManager.MoveGameObjectToScene(holder, scene);
            // Remove only static world signs. The drone's live target label and canvas HUD are gameplay UI.
            foreach (var sign in stage.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                if (sign.name is "Start Marker" or "Exit Marker" or "Jump Analysis" or "Glide Instructions"
                    or "Weight Limit" or "Swimming Requirement" or "Height Limit" or "Power Limit")
                    Object.Destroy(sign.gameObject);

            StageGimmickArt.Install(stage);
            Transform player = stage.Player;
            foreach (var goal in stage.GetComponentsInChildren<StageGoal>(true)) DressGoal(goal, holder.transform, player);
            foreach (var col in stage.GetComponentsInChildren<BoxCollider2D>(true))
                if (col.name == "Jump Wall") DressJumpWall(col, holder.transform, player);
        }

        static bool IsSurface(Collider2D col) =>
            !col.isTrigger && !col.attachedRigidbody && col.name.Contains("Surface");

        static void DressGoal(StageGoal goal, Transform holder, Transform player)
        {
            var col = goal.GetComponent<Collider2D>();
            if (!col) return;
            var b = col.bounds;
            // the pole + flag placeholder make way for the door
            var group = goal.transform.parent ? goal.transform.parent : goal.transform;
            // Keep the authored marker visible if a custom stage has no door art.

            var stage = goal.GetComponentInParent<StageRuntime>();
            var art = ProjectRTD.Core.SharedMenuArt.Loaded;
            if (!stage || !player || !stage.SpawnPoint || !art || !art.doorSheet || art.doorGrid.x < 1 || art.doorGrid.y < 1) return;
            foreach (var r in group.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            Vector3 spawn = stage.SpawnPoint.position;
            Vector3 Ground(float x, float fallback)
            {
                float nearest = float.PositiveInfinity, ground = fallback;
                foreach (var surface in stage.GetComponentsInChildren<BoxCollider2D>())
                {
                    if (!IsSurface(surface)) continue;
                    var bounds = surface.bounds;
                    if (x < bounds.min.x || x > bounds.max.x) continue;
                    float distance = Mathf.Abs(fallback - bounds.max.y);
                    if (distance < nearest) { ground = bounds.max.y; nearest = distance; }
                }
                // Seat the bottom of the frame slightly into the illustrated island lip.
                return new Vector3(x, ground - .12f, 0f);
            }
            var exit = StageGimmickArt.Portal(holder, Ground(b.center.x, b.min.y), art, "Exit Door");
            var entrance = StageGimmickArt.Portal(holder, Ground(spawn.x - .6f, spawn.y - .7f), art, "Entrance Door");
            goal.BindDoors(stage, entrance, exit);
        }

        static void DressJumpWall(BoxCollider2D col, Transform holder, Transform player)
        {
            var b = col.bounds;
            float top = b.max.y;
            // hide the grey placeholder (its own sprite + the "Wall Top" strip next to it)
            foreach (var sr in col.GetComponentsInChildren<SpriteRenderer>(true)) sr.enabled = false;
            if (col.transform.parent)
                foreach (Transform sib in col.transform.parent)
                    if (sib.name == "Wall Top" && sib.TryGetComponent(out SpriteRenderer strip))
                    {
                        strip.enabled = false;
                        top = Mathf.Max(top, strip.bounds.max.y);
                    }
            var go = new GameObject("Jump Wall Art");
            go.transform.SetParent(holder, false);
            go.transform.position = new Vector3(b.center.x, b.min.y, 0f);
            var wall = go.AddComponent<JumpWall>();
            wall.style = JumpWall.Style.LabBarrier;
            wall.width = b.size.x;
            wall.height = top - b.min.y;
            wall.sortingOrder = WallOrder;
            wall.watch = player;
        }
    }
}
