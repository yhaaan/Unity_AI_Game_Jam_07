using ChimeraGroove.Plants;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Gameplay
{
    /// <summary>
    /// Stage art that stands in for the grey placeholders:
    ///   * the spawn and the exit (StageGoal) get the lab shutter doors (children of Spawn Point / Goal)
    ///   * "Jump Wall" blocks get the lab-barrier pillar art at their exact size (child of the Jump Wall)
    ///   * crates, pressure plates, the cave roof and the press door get their art (StageGimmickArt.Install)
    /// Built once in the editor and saved into the scene (Tools/RTD/Bake Stage Layout), so it shows and can be
    /// moved in the Scene view. On load in Play it is only bound to the goal; a stage that was never baked
    /// is still dressed on the fly.
    /// Stages are indoor lab rooms, so no plants or other natural props are added.
    /// </summary>
    public static class StageDressing
    {
        const string EntranceName = "Entrance Door", ExitName = "Exit Door";
        const int WallOrder = 2;
        // Half of the visible door frame, plus a little air, kept inside the room's walls.
        const float DoorHalfWidth = 1.95f;

        static readonly string[] Signs =
        {
            "Start Marker", "Exit Marker", "Jump Analysis", "Glide Instructions",
            "Weight Limit", "Swimming Requirement", "Height Limit", "Power Limit",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            Dress(SceneManager.GetActiveScene());
        }

        static void OnLoaded(Scene scene, LoadSceneMode mode) => Dress(scene);

        static StageRuntime Find(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var stage = root.GetComponentInChildren<StageRuntime>(true);
                if (stage) return stage;
            }
            return null;
        }

        public static bool IsDressed(StageRuntime stage) =>
            stage.SpawnPoint && stage.SpawnPoint.Find(EntranceName);

        // Play: bind the saved doors to the goal (dressing first if the scene was never baked).
        static void Dress(Scene scene)
        {
            var stage = Find(scene);
            if (!stage || !Application.isPlaying) return;
            if (!IsDressed(stage)) Build(stage);
            else StageGimmickArt.Upgrade(stage);
            var entrance = stage.SpawnPoint ? stage.SpawnPoint.Find(EntranceName) : null;
            foreach (var goal in stage.GetComponentsInChildren<StageGoal>(true))
            {
                var group = goal.transform.parent ? goal.transform.parent : goal.transform;
                var exit = group.Find(ExitName);
                if (entrance && exit)
                    goal.BindDoors(stage, entrance.GetComponent<StageGimmickArt>(), exit.GetComponent<StageGimmickArt>());
            }
        }

        /// <summary>Creates the stage art (editor bake, or Play for a stage that was never baked).</summary>
        public static void Build(StageRuntime stage)
        {
            if (IsDressed(stage)) return;
            // Static world signs only. The drone's live target label and canvas HUD are gameplay UI.
            foreach (var sign in stage.GetComponentsInChildren<TMPro.TextMeshPro>(true))
                if (System.Array.IndexOf(Signs, sign.name) >= 0) StageArt.Remove(sign.gameObject);

            StageGimmickArt.Install(stage);
            Transform player = stage.Player;
            foreach (var goal in stage.GetComponentsInChildren<StageGoal>(true)) DressGoal(goal, player);
            foreach (var col in stage.GetComponentsInChildren<BoxCollider2D>(true))
                if (col.name == "Jump Wall") DressJumpWall(col, player);
        }

        static bool IsSurface(Collider2D col) =>
            !col.isTrigger && !col.attachedRigidbody && col.name.Contains("Surface");

        static void DressGoal(StageGoal goal, Transform player)
        {
            var col = goal.GetComponent<Collider2D>();
            if (!col) return;
            // the pole + flag placeholder make way for the door
            var group = goal.transform.parent ? goal.transform.parent : goal.transform;
            // Keep the authored marker visible if a custom stage has no door art.

            var stage = goal.GetComponentInParent<StageRuntime>();
            var art = ProjectRTD.Core.SharedMenuArt.Loaded;
            if (!stage || !player || !stage.SpawnPoint || !art || !art.doorSheet || art.doorGrid.x < 1 || art.doorGrid.y < 1) return;
            // keep the whole exit door inside the room's right wall
            var rightWall = stage.transform.Find("Course Boundaries/Right Boundary");
            if (rightWall && rightWall.TryGetComponent(out BoxCollider2D wall))
            {
                float over = col.bounds.center.x + DoorHalfWidth - wall.bounds.min.x;
                if (over > 0) { group.position += Vector3.left * over; Physics2D.SyncTransforms(); }
            }
            var b = col.bounds;
            foreach (var r in group.GetComponentsInChildren<Renderer>(true)) StageArt.Retire(r);
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
            StageGimmickArt.Portal(group, Ground(b.center.x, b.min.y), art, ExitName);
            StageGimmickArt.Portal(stage.SpawnPoint, Ground(spawn.x - .6f, spawn.y - .7f), art, EntranceName);
        }

        static void DressJumpWall(BoxCollider2D col, Transform player)
        {
            var b = col.bounds;
            float top = b.max.y;
            // retire the grey placeholder (its own sprite + the "Wall Top" strip next to it)
            foreach (var sr in col.GetComponentsInChildren<SpriteRenderer>(true)) StageArt.Retire(sr);
            if (col.transform.parent)
                foreach (var strip in col.transform.parent.GetComponentsInChildren<SpriteRenderer>(true))
                    if (strip.name == "Wall Top" && strip.transform.parent == col.transform.parent)
                    {
                        top = Mathf.Max(top, strip.bounds.max.y);
                        StageArt.Retire(strip);
                    }
            var go = new GameObject("Jump Wall Art");
            go.transform.SetParent(col.transform, false);
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
