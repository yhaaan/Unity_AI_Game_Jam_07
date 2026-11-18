using System.Collections.Generic;
using ChimeraGroove.Plants;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Gameplay
{
    /// <summary>
    /// Dresses every stage when it loads (no scene edits, so nothing to merge):
    ///   * the exit (StageGoal) becomes the lab shutter door that opens as the player walks up
    ///   * "Jump Wall" blocks get the lab-barrier pillar art at their exact size
    ///   * island surfaces get plants as decoration (behind the player, a few low grass tufts in front),
    ///     kept clear of the spawn, the door, walls, gates and other gameplay pieces; they sway and react to the player
    /// Placement is seeded by the scene name, so a stage always looks the same.
    /// </summary>
    public static class StageDressing
    {
        const string Marker = "Stage Dressing";
        const int PlantOrder = 3, FrontOrder = 15, DoorOrder = 3, WallOrder = 2;
        const float DoorScale = .33f;

        static readonly string[] BackPlants =
        {
            "grass-tall", "bush", "wheat", "clover", "grass-wild", "grass-clover", "grass-dry",
            "flower-pink", "flower-buck", "bellflower", "calla-tongue", "flower-nose", "flower-tooth", "fern-eyes", "pitcher-ear",
        };
        static readonly float[] BackWeights = { 3, 2, 2, 2, 3, 2, 2, 1.2f, 1, 1, 1, 1, .8f, 1, .8f };
        static readonly string[] FrontPlants = { "grass-low", "grass-wild", "grass-dry" };

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
            var rng = new System.Random(StableHash(scene.name));
            var blocked = new List<Vector2>();   // x-intervals where nothing decorative may stand

            foreach (var goal in stage.GetComponentsInChildren<StageGoal>(true)) DressGoal(goal, holder.transform, player, blocked);
            foreach (var col in stage.GetComponentsInChildren<BoxCollider2D>(true))
                if (col.name == "Jump Wall") DressJumpWall(col, holder.transform, player, blocked);

            // gameplay pieces standing on the ground keep their space
            foreach (var col in stage.GetComponentsInChildren<Collider2D>(true))
            {
                if (IsSurface(col) || col.GetComponent<RespawnZone>() || col.transform == player) continue;
                var b = col.bounds;
                if (b.size.x > 40f) continue;
                blocked.Add(new Vector2(b.min.x - .25f, b.max.x + .25f));
            }
            // plants behind the player may stand at the spawn; ones in front of it may not
            var frontBlocked = new List<Vector2>(blocked);
            if (stage.SpawnPoint) frontBlocked.Add(new Vector2(stage.SpawnPoint.position.x - .8f, stage.SpawnPoint.position.x + .8f));

            foreach (var col in stage.GetComponentsInChildren<BoxCollider2D>(true))
                if (IsSurface(col)) Plant(col, holder.transform, player, blocked, frontBlocked, rng);
        }

        static bool IsSurface(Collider2D col) =>
            !col.isTrigger && !col.attachedRigidbody && col.name.Contains("Surface");

        static void DressGoal(StageGoal goal, Transform holder, Transform player, List<Vector2> blocked)
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
            blocked.Add(new Vector2(entrance.transform.position.x - 1.5f, spawn.x + .6f));
            goal.BindDoors(stage, entrance, exit);
            blocked.Add(new Vector2(b.center.x - 1.4f, b.center.x + 1.4f));
        }

        static void DressJumpWall(BoxCollider2D col, Transform holder, Transform player, List<Vector2> blocked)
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
            blocked.Add(new Vector2(b.min.x - .3f, b.max.x + .3f));
        }

        static void Plant(BoxCollider2D surface, Transform holder, Transform player, List<Vector2> blocked, List<Vector2> frontBlocked, System.Random rng)
        {
            var b = surface.bounds;
            float x = b.min.x + .25f + (float)rng.NextDouble() * .4f, end = b.max.x - .25f;
            int n = 0;
            while (x < end)
            {
                bool front = n % 4 == 3;
                string name = front ? FrontPlants[rng.Next(FrontPlants.Length)] : Pick(rng);
                // sized against the ~1.4 unit tall player: grass knee-high, flowers about shoulder-high
                float scale = front ? .3f + (float)rng.NextDouble() * .06f
                    : name.StartsWith("grass") || name == "bush" ? .42f + (float)rng.NextDouble() * .1f
                    : .4f + (float)rng.NextDouble() * .08f;
                float half = Approx(name) * scale * .5f;
                if (Free(x - half, x + half, front ? frontBlocked : blocked) && x - half > b.min.x - .1f && x + half < b.max.x + .1f)
                {
                    var go = new GameObject("Plant " + name);
                    go.transform.SetParent(holder, false);
                    go.transform.position = new Vector3(x, b.max.y - .02f, 0f);
                    float flip = rng.NextDouble() < .5 ? -1f : 1f;
                    go.transform.localScale = new Vector3(scale * flip, scale, 1f);
                    var p = go.AddComponent<PlantRig>();
                    p.plantName = name;
                    p.sortingOrder = front ? FrontOrder : PlantOrder;
                    p.reactToBodies = false;
                    p.watch = player;
                    p.phase = (float)rng.NextDouble();
                    p.wind = .7f + (float)rng.NextDouble() * .5f;
                    var used = new Vector2(x - half * .45f, x + half * .45f);
                    blocked.Add(used); frontBlocked.Add(used);
                    x += half * 1.3f + .1f + (float)rng.NextDouble() * .5f;   // neighbours may overlap a little, like a real verge
                }
                else x += .3f;
                n++;
            }
        }

        static string Pick(System.Random rng)
        {
            float total = 0f; foreach (var w in BackWeights) total += w;
            float r = (float)rng.NextDouble() * total;
            for (int i = 0; i < BackPlants.Length; i++) { r -= BackWeights[i]; if (r <= 0f) return BackPlants[i]; }
            return BackPlants[0];
        }

        /// <summary>rough plant width in world units at scale 1 (png px / 100)</summary>
        static float Approx(string name) => name switch
        {
            "flower-nose" or "flower-tooth" or "fern-eyes" => 3.9f,
            "pitcher-ear" => 3.5f,
            _ => 3.1f,
        };

        static bool Free(float a, float b, List<Vector2> blocked)
        {
            foreach (var r in blocked) if (a < r.y && b > r.x) return false;
            return true;
        }

        static int StableHash(string s)
        {
            unchecked { int h = 23; foreach (char c in s) h = h * 31 + c; return h; }
        }
    }
}
