#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Gameplay
{
    // Builds the two long final stages (Stage09, Stage10) out of the gimmicks of the earlier stages:
    // Tools/RTD/Build Hard Stages. The scene files start as copies of Stage02 (an empty room with a pit);
    // this widens the room to twice the normal width, lays the floors, and copies each gimmick in.
    // Safe to re-run: it rebuilds from the scene's current floors and skips gimmicks that are already there.
    internal static class HardStageBuilder
    {
        const string Scenes = "Assets/00. Scenes/";
        const float Left = -12.4f, Right = 37.2f;   // double-width room

        struct Gimmick
        {
            public string source;      // stage number the pieces come from, e.g. "06"
            public string[] names;     // direct children of that stage's island
            public float dx;           // shift in x
            public float squeeze;      // > 0: scale x around the source island centre (pool width), then shift
        }

        [MenuItem("Tools/RTD/Build Hard Stages")]
        static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            Build("Stage09", "STAGE 09",
                new[] { new Vector2(Left, 1.5f), new Vector2(8.5f, 19f), new Vector2(19f, 27f), new Vector2(31.3f, Right) },
                new[]
                {
                    new Gimmick { source = "01", names = new[] { "Jump Wall", "Jump Level Gate" }, dx = -5f },
                    new Gimmick { source = "03", names = new[] { "Collapsing Bridge" }, dx = 5f },
                    new Gimmick { source = "04", names = new[] { "Scientist" }, dx = 13.5f },
                    new Gimmick { source = "06", names = new[] { "Cave Roof", "cupboard (1)", "cupboard (2)", "Height Gate" }, dx = 22.2f },
                    new Gimmick { source = "05", names = new[] { "Pool Water", "Water Basin", "Waterline", "Unsafe Water Crossing" }, dx = 29.15f, squeeze = 4.3f / 8.3f },
                },
                new[]
                {
                    (-4f, StageRuntime.Challenge.HighJump), (8.5f, StageRuntime.Challenge.Lightweight), (19f, StageRuntime.Challenge.Scientist),
                    (24.8f, StageRuntime.Challenge.LowCeiling), (99f, StageRuntime.Challenge.Swim),
                },
                new (float, float, float, float)[0], float.NaN);

            Build("Stage10", "STAGE 10",
                new[] { new Vector2(Left, 7f), new Vector2(13f, 19.9f), new Vector2(19.9f, 27.4f), new Vector2(27.4f, 30.2f), new Vector2(33.6f, Right) },
                new[]
                {
                    new Gimmick { source = "07", names = new[] { "Power Crate", "Powered Door", "Door Housing", "Crate Pressure Plate" }, dx = 0f },
                    new Gimmick { source = "06", names = new[] { "Cave Roof", "cupboard (1)", "cupboard (2)", "Height Gate" }, dx = 16.6f },
                    new Gimmick { source = "04", names = new[] { "Scientist" }, dx = 23.6f },
                    new Gimmick { source = "01", names = new[] { "Jump Wall", "Jump Level Gate" }, dx = 28.8f },
                    new Gimmick { source = "05", names = new[] { "Pool Water", "Water Basin", "Waterline", "Unsafe Water Crossing" }, dx = 31.9f, squeeze = 3.4f / 8.3f },
                },
                new[]
                {
                    (3.6f, StageRuntime.Challenge.Power), (13f, StageRuntime.Challenge.Glide), (19.9f, StageRuntime.Challenge.LowCeiling),
                    (27.4f, StageRuntime.Challenge.Scientist), (29.6f, StageRuntime.Challenge.HighJump), (99f, StageRuntime.Challenge.Swim),
                },
                // drones: patrol left, patrol right, height, beam length
                new[] { (5f, 15f, 5f, 5.6f), (20.5f, 27f, 4.6f, 4.8f) },
                // start to the right of the crate, like Stage07: the crate is pushed left onto the button at the wall
                -4.4f);
        }

        static void Build(string sceneName, string title, Vector2[] floors, Gimmick[] gimmicks,
            (float endX, StageRuntime.Challenge challenge)[] sections, (float left, float right, float y, float beam)[] drones, float spawnX)
        {
            var opened = new List<Scene>();
            Scene Open(string name)
            {
                var sc = SceneManager.GetSceneByPath(Scenes + name + ".unity");
                if (sc.isLoaded) return sc;
                sc = EditorSceneManager.OpenScene(Scenes + name + ".unity", OpenSceneMode.Additive);
                opened.Add(sc);
                return sc;
            }
            static Transform Island(Scene sc) => sc.GetRootGameObjects().Select(r => r.GetComponent<StageRuntime>()).First(s => s)
                .transform.Cast<Transform>().First(t => t.name.EndsWith("Island") && t.name != "Below Island");

            var scene = Open(sceneName);
            try
            {
                var stage = scene.GetRootGameObjects().Select(r => r.GetComponent<StageRuntime>()).First(s => s);
                var island = Island(scene);
                island.name = "FINAL Island";
                stage.gameObject.name = title.Replace("STAGE", "Stage");

                // ---- floors: one collider per span, each with its own lab-floor art, pits shown between gaps ----
                var surfaces = island.Cast<Transform>().Where(t => t.name == "Island Surface").Select(t => t.GetComponent<BoxCollider2D>()).ToList();
                var holder = stage.transform.Find("Course Boundaries");
                var arts = holder.GetComponentsInChildren<LabFloorArt>(true).ToList();
                var floorArt = arts.First(a => !a.pit).gameObject;
                var pitArt = arts.First(a => a.pit).gameObject;
                var template = surfaces[0].gameObject;
                var built = new List<BoxCollider2D>();
                foreach (var span in floors)
                {
                    var c = Object.Instantiate(template, island).GetComponent<BoxCollider2D>();
                    c.name = "Island Surface";
                    float sx = c.transform.lossyScale.x;
                    c.offset = new Vector2(((span.x + span.y) * .5f - c.transform.position.x) / sx, c.offset.y);
                    c.size = new Vector2((span.y - span.x) / Mathf.Abs(sx), c.size.y);
                    built.Add(c);
                    var art = Object.Instantiate(floorArt, holder).GetComponent<LabFloorArt>();
                    art.name = "Lab Floor"; art.from = c; art.to = null; art.pit = false;
                }
                for (int i = 1; i < built.Count; i++)
                {
                    if (floors[i].x - floors[i - 1].y < .05f) continue;
                    var art = Object.Instantiate(pitArt, holder).GetComponent<LabFloorArt>();
                    art.name = "Pit Floor"; art.from = built[i - 1]; art.to = built[i]; art.pit = true;
                }
                foreach (var s in surfaces) Object.DestroyImmediate(s.gameObject);
                foreach (var a in arts) Object.DestroyImmediate(a.gameObject);

                // ---- room: right wall and exit move out; kill / bounds zones stretch with it ----
                var rightWall = holder.Find("Right Boundary");
                float shift = Right - rightWall.GetComponent<BoxCollider2D>().bounds.min.x;
                rightWall.position += Vector3.right * shift;
                var goal = island.Find("Goal");
                if (goal) goal.position += Vector3.right * shift;
                foreach (var box in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BoxCollider2D>(true)))
                {
                    if (built.Contains(box) || box.transform.IsChildOf(holder) || box.bounds.size.x < 20f) continue;
                    float sx = box.transform.lossyScale.x, a = Left - 4f, b = Right + 4f;
                    box.offset = new Vector2(((a + b) * .5f - box.transform.position.x) / sx, box.offset.y);
                    box.size = new Vector2((b - a) / Mathf.Abs(sx), box.size.y);
                }

                // ---- gimmicks: clone the whole source island (keeps references between its pieces), take what we need ----
                foreach (var g in gimmicks)
                {
                    if (g.names.All(n => island.Find(n))) continue;
                    var clone = Object.Instantiate(Island(Open("Stage" + g.source)).gameObject);
                    SceneManager.MoveGameObjectToScene(clone, scene);
                    foreach (var name in g.names)
                    {
                        var piece = clone.transform.Find(name);
                        if (!piece) { Debug.LogWarning($"{sceneName}: Stage{g.source} has no '{name}'"); continue; }
                        piece.SetParent(island, true);
                        if (g.squeeze > 0f)
                        {
                            piece.position = new Vector3(piece.position.x * g.squeeze + g.dx, piece.position.y, piece.position.z);
                            var s = piece.localScale; s.x *= g.squeeze; piece.localScale = s;
                        }
                        else piece.position += Vector3.right * g.dx;
                        if (PrefabUtility.IsPartOfPrefabInstance(piece)) PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(piece), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    }
                    Object.DestroyImmediate(clone);
                }

                // Pieces that watched their own stage's player (the jump wall) lost that link in the copy: point them here.
                foreach (var behaviour in island.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    var so = new SerializedObject(behaviour);
                    var watch = so.FindProperty("watch");
                    if (watch == null || watch.propertyType != SerializedPropertyType.ObjectReference || watch.objectReferenceValue) continue;
                    watch.objectReferenceValue = stage.Player;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // ---- start position (entrance door and player move with the spawn point) ----
                if (!float.IsNaN(spawnX) && stage.SpawnPoint && Mathf.Abs(spawnX - stage.SpawnPoint.position.x) > .01f)
                {
                    var delta = Vector3.right * (spawnX - stage.SpawnPoint.position.x);
                    stage.SpawnPoint.position += delta;
                    stage.Player.position += delta;
                    foreach (var t in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)))
                        if (t.name is "Entrance Door" or "Start Marker") t.position += delta;
                }

                // ---- drones ----
                foreach (var old in island.GetComponentsInChildren<DroneSentry>(true)) Object.DestroyImmediate(old.gameObject);
                for (int i = 0; i < drones.Length; i++)
                {
                    var d = drones[i];
                    var go = new GameObject("Drone " + (char)('A' + i));
                    SceneManager.MoveGameObjectToScene(go, scene);
                    go.transform.SetParent(island, false);
                    go.transform.position = new Vector3((d.left + d.right) * .5f, d.y, 0f);
                    var so = new SerializedObject(go.AddComponent<DroneSentry>());
                    so.FindProperty("patrolLeft").floatValue = d.left;
                    so.FindProperty("patrolRight").floatValue = d.right;
                    so.FindProperty("beamLength").floatValue = d.beam;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                // ---- section rules and labels ----
                var rules = stage.GetComponent<StageSections>() ?? stage.gameObject.AddComponent<StageSections>();
                var data = new SerializedObject(rules);
                var list = data.FindProperty("sections");
                list.arraySize = sections.Length;
                for (int i = 0; i < sections.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("endX").floatValue = sections[i].endX;
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("challenge").enumValueIndex = (int)sections[i].challenge;
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                foreach (var text in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMPro.TMP_Text>(true)))
                    if (text.name == "Stage Name") text.text = title;

                Physics2D.SyncTransforms();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) Debug.LogError("Could not save " + sceneName);
                Debug.Log($"{sceneName} built: {built.Count} floors, {gimmicks.Length} gimmick groups, {drones.Length} drones.");
            }
            finally
            {
                foreach (var sc in opened) if (sc.IsValid() && sc.isLoaded) EditorSceneManager.CloseScene(sc, true);
            }
        }
    }
}
#endif
