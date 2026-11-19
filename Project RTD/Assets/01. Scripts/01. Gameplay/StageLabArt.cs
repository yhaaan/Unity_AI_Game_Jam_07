using System.Collections.Generic;
using ChimeraGroove.Plants;
using UnityEngine;

namespace ProjectRTD.Gameplay
{
    /// <summary>
    /// The lab-room look, laid over the baked stage layout when a stage starts (Play only, never saved):
    ///   * the room itself: rail floors on a solid floor mass, dark shafts in gaps, pillar end walls and a rail
    ///     ceiling (LabRoomArt) in place of the baked floor band art
    ///   * Jump Wall blocks as 9-sliced lab pillars sunk into the rail
    ///   * pools as bubbling toon toxic sludge
    ///   * plants behind the player and a dense grass verge in front of it that rustles when walked through
    ///   * a light touch of overgrowth (StageDecor)
    /// The baked layout (walls, spawn, doors, colliders) is used as is; nothing here changes gameplay.
    /// </summary>
    public static class StageLabArt
    {
        const string Marker = "Lab Art";
        const int PlantOrder = 3, FrontOrder = 15, VergeOrder = 16, PillarOrder = 2;
        const float DoorLift = .08f;   // baked doors are seated .12 into the old floor lip; the rail wants .04

        static readonly string[] BackPlants =
        {
            "grass-tall", "bush", "wheat", "clover", "grass-wild", "grass-clover", "grass-dry",
            "flower-pink", "flower-buck", "bellflower", "calla-tongue", "flower-nose", "flower-tooth", "fern-eyes", "pitcher-ear",
        };
        static readonly float[] BackWeights = { 3, 2, 2, 2, 3, 2, 2, 1.2f, 1, 1, 1, 1, .8f, 1, .8f };
        static readonly string[] FrontPlants = { "grass-low", "grass-wild", "grass-dry" };
        static readonly string[] VergeGrass = { "grass-low", "grass-low", "grass-wild", "grass-dry", "grass-clover" };

        public static void Apply(StageRuntime stage)
        {
            if (!Application.isPlaying || !stage || stage.transform.Find(Marker)) return;
            var room = stage.transform.Find("Course Boundaries");
            var leftWall = room ? room.Find("Left Boundary") : null;
            var rightWall = room ? room.Find("Right Boundary") : null;
            if (!leftWall || !rightWall) return;
            float left = leftWall.GetComponent<Collider2D>().bounds.max.x;
            float right = rightWall.GetComponent<Collider2D>().bounds.min.x;

            var holder = new GameObject(Marker).transform;
            holder.SetParent(stage.transform, false);
            var rng = new System.Random(StableHash(stage.gameObject.scene.name));
            Transform player = stage.Player;

            // ---- room: our floors/walls/ceiling replace the baked floor band ----
            foreach (var t in room.GetComponentsInChildren<Transform>(true))
                if (t.name is "Lab Floor" or "Pit Floor") t.gameObject.SetActive(false);
            var floors = Surfaces(stage);
            var spans = new List<LabRoomArt.Span>();
            foreach (var f in floors) spans.Add(new LabRoomArt.Span(f.bounds.min.x, f.bounds.max.x, f.bounds.max.y));
            var pools = new List<Bounds>();
            int water = LayerMask.NameToLayer("Water");
            foreach (var col in stage.GetComponentsInChildren<Collider2D>())
                if (col.gameObject.layer == water) pools.Add(col.bounds);
            LabRoomArt.BuildRoom(holder, spans, left, right, 2.4f, pools);

            // ---- toxic pools ----
            if (pools.Count > 0)
            {
                foreach (var sr in stage.GetComponentsInChildren<SpriteRenderer>())
                    if (sr.name is "Water Basin" or "Waterline") sr.enabled = false;
                foreach (var pool in pools) LabRoomArt.ToxicPool(holder, pool);
            }

            // ---- jump walls: our sliced pillar instead of the baked barrier ----
            foreach (var col in stage.GetComponentsInChildren<BoxCollider2D>(true))
            {
                if (col.name != "Jump Wall") continue;
                foreach (var old in col.GetComponentsInChildren<JumpWall>(true)) old.gameObject.SetActive(false);
                var b = col.bounds;
                float top = b.max.y;
                if (col.transform.parent)
                    foreach (Transform sib in col.transform.parent)
                        if (sib.name == "Wall Top" && sib.TryGetComponent(out SpriteRenderer strip)) top = Mathf.Max(top, strip.bounds.max.y);
                LabRoomArt.Pillar(holder, "Jump Wall Art", new Vector2(b.center.x, b.min.y - LabRoomArt.SinkIntoDeck), b.size.x,
                    top - b.min.y + LabRoomArt.SinkIntoDeck, PillarOrder, Color.white, contactShadow: true);
            }

            // ---- doors stand on the rail ----
            var blocked = new List<Vector2>();   // x-intervals where nothing decorative may stand
            foreach (var door in stage.GetComponentsInChildren<StageGimmickArt>(true))
            {
                if (door.name is not ("Entrance Door" or "Exit Door")) continue;
                door.transform.position += Vector3.up * DoorLift;
                if (door.TryGetComponent(out SpriteRenderer dr)) blocked.Add(new Vector2(dr.bounds.min.x, dr.bounds.max.x));
            }

            // ---- plants ----
            foreach (var col in stage.GetComponentsInChildren<Collider2D>(true))
            {
                if (IsSurface(col) || col.GetComponent<RespawnZone>() || col.transform == player) continue;
                var cb = col.bounds;
                if (cb.size.x > 40f) continue;
                blocked.Add(new Vector2(cb.min.x - .25f, cb.max.x + .25f));
            }
            var frontBlocked = new List<Vector2>(blocked);
            if (stage.SpawnPoint) frontBlocked.Add(new Vector2(stage.SpawnPoint.position.x - .8f, stage.SpawnPoint.position.x + .8f));
            var vergeBlocked = new List<Vector2>(frontBlocked);
            foreach (var f in floors) Plant(f, holder, player, blocked, frontBlocked, rng);

            var verge = new List<Vector3>();
            foreach (var f in floors) Verge(f, holder, player, vergeBlocked, rng, verge);
            if (player && verge.Count > 0) holder.gameObject.AddComponent<GrassRustle>().Init(player, verge);

            StageDecor.Dress(holder, stage, vergeBlocked, rng);
        }

        static List<BoxCollider2D> Surfaces(StageRuntime stage)
        {
            var list = new List<BoxCollider2D>();
            foreach (var col in stage.GetComponentsInChildren<BoxCollider2D>())
                if (col.name == "Island Surface" && !col.isTrigger && !col.attachedRigidbody) list.Add(col);
            list.Sort((a, b) => a.bounds.min.x.CompareTo(b.bounds.min.x));
            return list;
        }

        static bool IsSurface(Collider2D col) =>
            !col.isTrigger && !col.attachedRigidbody && col.name.Contains("Surface");

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
                    x += half * 1.3f + .1f + (float)rng.NextDouble() * .5f;
                }
                else x += .3f;
                n++;
            }
        }

        /// <summary>Overlapping low grass along the front lip of a surface, drawn over the character's legs.</summary>
        static void Verge(BoxCollider2D surface, Transform holder, Transform player, List<Vector2> keepClear, System.Random rng, List<Vector3> tufts)
        {
            var b = surface.bounds;
            float x = b.min.x + .2f + (float)rng.NextDouble() * .3f;
            while (x < b.max.x - .2f)
            {
                string name = VergeGrass[rng.Next(VergeGrass.Length)];
                float scale = .3f + (float)rng.NextDouble() * .1f;
                float half = Approx(name) * scale * .5f;
                // gaps of varied length so the verge reads as clumps, not a hedge
                if (Free(x - half * .5f, x + half * .5f, keepClear) && rng.NextDouble() > .12)
                {
                    var go = new GameObject("Verge " + name);
                    go.transform.SetParent(holder, false);
                    go.transform.position = new Vector3(x, b.max.y - .1f, 0f);
                    float flip = rng.NextDouble() < .5 ? -1f : 1f;
                    go.transform.localScale = new Vector3(scale * flip, scale * (.85f + (float)rng.NextDouble() * .3f), 1f);
                    var p = go.AddComponent<PlantRig>();
                    p.plantName = name;
                    p.sortingOrder = VergeOrder;
                    p.bendRows = 12;
                    p.reactToBodies = false;
                    p.watch = player;
                    p.watchHalfWidth = .25f;
                    p.phase = (float)rng.NextDouble();
                    p.wind = .5f + (float)rng.NextDouble() * .4f;
                    tufts.Add(new Vector3(x - half * .6f, x + half * .6f, b.max.y));
                }
                x += half * .55f + (float)rng.NextDouble() * .35f;
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
