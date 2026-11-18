#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using ProjectRTD.Gameplay;
using UnityEditor;
using UnityEngine;

public sealed class PlayerPartBuildTests
{
    [Test]
    public void FinalCsvAndClipboardMatchCatalog()
    {
        string csv = System.IO.File.ReadAllText("Assets/99. Documents/PartsTemplate.txt");
        string tsv = csv.Replace("\"canFly, canSwim\"", "canFly| canSwim").Replace(',', '\t').Replace('|', ',');
        var catalog = AssetDatabase.LoadAssetAtPath<PartCatalog>("Assets/06. Data/PartCatalog.asset");
        foreach (var text in new[] { csv, tsv })
        {
            var rows = AnimalPartCsv.Parse(text);
            Assert.That(rows.Count, Is.EqualTo(18));
            foreach (var row in rows)
            {
                var part = catalog.ForSlot(row.slot).Single(p => p.PartId == row.partId);
                Assert.That(part.Animal, Is.EqualTo(row.animal), row.partId);
                Assert.That(part.DisplayName, Is.EqualTo(row.displayName), row.partId);
                Assert.That(new[] { part.WeightDelta, part.HealthDelta, part.HeightDelta },
                    Is.EqualTo(new[] { row.weightDelta, row.healthDelta, row.heightDelta }), row.partId);
                Assert.That(part.EffectIds, Is.EquivalentTo(row.effectIds), row.partId);
            }
        }
    }

    [Test]
    public void EveryImportedCombinationHasValidLevels()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PartCatalog>("Assets/06. Data/PartCatalog.asset");
        Assert.That(catalog, Is.Not.Null);
        var heads = catalog.ForSlot(PartSlot.Head).ToArray();
        var bodies = catalog.ForSlot(PartSlot.Body).ToArray();
        var legs = catalog.ForSlot(PartSlot.Legs).ToArray();
        Assert.That(new[] { heads.Length, bodies.Length, legs.Length }, Is.All.EqualTo(6));
        foreach (var h in heads)
        foreach (var b in bodies)
        foreach (var l in legs)
        {
            var build = new PlayerPartBuild(h, b, l);
            Assert.That(new[] { build.Speed, build.Jump, build.Weight, build.Health }, Is.All.InRange(1, 5));
            foreach (var effect in h.EffectIds.Concat(b.EffectIds).Concat(l.EffectIds)) Assert.That(build.HasEffect(effect));
        }
    }

    [Test]
    public void PartArtIsUniqueAndSharesOneCanvas()
    {
        var paths = new System.Collections.Generic.HashSet<Sprite>();
        foreach (var id in AssetDatabase.FindAssets("t:AnimalPartDefinition", new[] { "Assets/06. Data" }))
        {
            var part = AssetDatabase.LoadAssetAtPath<AnimalPartDefinition>(AssetDatabase.GUIDToAssetPath(id));
            Assert.That(part.Sprite, Is.Not.Null, part.PartId);
            Assert.That(paths.Add(part.Sprite), Is.True, part.PartId);
            Assert.That(part.Sprite.rect.size, Is.EqualTo(new Vector2(512, 512)), part.PartId);
            Assert.That(part.Sprite.pivot, Is.EqualTo(new Vector2(256, 256)), part.PartId);
            Assert.That(part.Sprite.pixelsPerUnit, Is.EqualTo(100), part.PartId);
            if (part.Slot == PartSlot.Legs) Assert.That(part.GroundYpx, Is.InRange(331, 512), part.PartId);
        }
        Assert.That(paths.Count, Is.EqualTo(18));
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02. Prefabs/Player.prefab");
        var visual = player.transform.Find("Visual");
        Assert.That(visual.childCount, Is.EqualTo(3));
        int last = int.MinValue;
        foreach (var slot in new[] { "Legs", "Body", "Head" })
        {
            var renderer = visual.Find(slot).GetComponent<SpriteRenderer>();
            Assert.That(renderer.sprite, Is.Not.Null, slot);
            Assert.That(renderer.transform.localPosition, Is.EqualTo(Vector3.zero), slot);
            Assert.That(renderer.sortingOrder, Is.GreaterThan(last), slot);
            last = renderer.sortingOrder;
        }
    }

    [Test]
    public void PlayerAnimationContainsAllPlayableLayers()
    {
        var library = AssetDatabase.LoadAssetAtPath<ChimeraLayerLibrary>("Assets/04. Animations/PlayerLayers.yaml");
        Assert.That(library, Is.Not.Null, "Reimport the PlayerLayers recipe");
        foreach (var set in new[] { library.parts.heads, library.parts.bodies, library.parts.legs })
        {
            Assert.That(set.Select(p => p.id), Is.EquivalentTo(new[] { "duck", "rabbit", "lion", "bear", "axolotl", "chicken" }));
            foreach (var p in set) foreach (var l in p.layers)
            {
                Assert.That(l.sprite, Is.Not.Null, l.path);
                Assert.That(string.IsNullOrEmpty(l.parent) || p.layers.Any(parent => parent.id == l.parent), Is.True, l.path);
            }
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02. Prefabs/Player.prefab");
        var visual = new SerializedObject(prefab.GetComponentInChildren<PlayerPartVisual>());
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(library, out string guid, out long fileId);
        Assert.That(visual.FindProperty("animations").objectReferenceValue, Is.EqualTo(library), $"Expected library {guid}:{fileId}");
    }

    [Test]
    public void BasinUsesWaterShaderAfterSceneColorCapture()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/00. Scenes/Stage01.unity");
        try
        {
            var basin = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true))
                .Single(sprite => sprite.name == "Water Basin");
            Assert.That(basin.sortingLayerName, Is.EqualTo("WaterOverlay"));
            var shader = basin.sharedMaterial.shader;
            Assert.That(shader.name, Is.EqualTo("ProjectRTD/2D/Basin Water"));
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var renderer = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/07. Settings/Renderer2D.asset"));
            Assert.That(renderer.FindProperty("m_UseCameraSortingLayersTexture").boolValue, Is.True);
            Assert.That(renderer.FindProperty("m_CameraSortingLayersTextureBound").intValue, Is.EqualTo(SortingLayer.NameToID("Default")));
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void WaterSurfaceSupportsOnlyFloatBuilds(bool shouldFloat)
    {
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02. Prefabs/Player.prefab"));
        var water = new GameObject("Test water");
        var state = Random.state;
        try
        {
            Random.InitState(42);
            var loadout = player.GetComponent<PlayerPartLoadout>();
            for (int i = 0; i < 100; i++)
            {
                Assert.That(loadout.TryResummon(), Is.True);
                if (loadout.Current.HasEffect(PartEffect.Float) == shouldFloat) break;
            }
            Assert.That(loadout.Current.HasEffect(PartEffect.Float), Is.EqualTo(shouldFloat));
            player.transform.position = new Vector3(1000, 1000, 0);
            Physics2D.SyncTransforms();
            water.layer = LayerMask.NameToLayer("Water");
            var box = water.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(5, 2);
            var bounds = player.GetComponent<Collider2D>().bounds;
            water.transform.position = new Vector3(bounds.center.x, bounds.min.y + 0.12f - 1, 0);
            Physics2D.SyncTransforms();
            var motor = player.GetComponent<PlayerMotor2D>();
            var rb = player.GetComponent<Rigidbody2D>();
            // Drive only the motor in EditMode, without broadcasting lifecycle messages.
            void Tick(string method) => typeof(PlayerMotor2D).GetMethod(method,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(motor, null);
            Tick("Awake");
            rb.linearVelocity = Vector2.down * 8;
            Tick("FixedUpdate");
            Assert.That(motor.IsGrounded, Is.False);
            Assert.That(motor.IsFloating, Is.EqualTo(shouldFloat));
            if (!shouldFloat) { Assert.That(rb.linearVelocity.y, Is.EqualTo(-8f)); return; }

            // Step gravity and translation locally, without simulating other editor objects.
            float min = 1f, max = 0f;
            float dt = Time.fixedDeltaTime;
            for (int i = 0; i * dt < 6f; i++)
            {
                rb.linearVelocity += Physics2D.gravity * rb.gravityScale * dt;
                rb.position += rb.linearVelocity * dt;
                Physics2D.SyncTransforms();
                Tick("FixedUpdate");
                if (i * dt < 2f) continue;
                var capsule = player.GetComponent<Collider2D>().bounds;
                float fraction = (box.bounds.max.y - capsule.min.y) / capsule.size.y;
                min = Mathf.Min(min, fraction);
                max = Mathf.Max(max, fraction);
                Assert.That(motor.IsFloating, Is.True);
            }
            Assert.That((min + max) * 0.5f, Is.EqualTo(0.5f).Within(0.05f));
            Assert.That(max - min, Is.InRange(0.1f, 0.35f));
        }
        finally { Object.DestroyImmediate(water); Object.DestroyImmediate(player); Random.state = state; }
    }

    [Test]
    public void HeavyLionLosesOneSpeedAfterClamping()
    {
        AnimalPartDefinition Part(string slot) => AssetDatabase.LoadAssetAtPath<AnimalPartDefinition>("Assets/06. Data/Part_lion_" + slot + ".asset");
        var lion = new PlayerPartBuild(Part("head"), Part("body"), Part("legs"));
        Assert.That(new[] { lion.Speed, lion.Jump, lion.Weight, lion.Health }, Is.EqualTo(new[] { 2, 3, 5, 5 }));
        Assert.That(lion.Height, Is.EqualTo(4));
        Assert.That(lion.HasEffect(PartEffect.CanPass), Is.True);
        Assert.That(lion.HasEffect(PartEffect.Glide), Is.False);
    }

    [Test]
    public void PlayerPrefabAppliesEachSummonRelativeToBaseMovement()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02. Prefabs/Player.prefab");
        var player = Object.Instantiate(prefab);
        var randomState = Random.state;
        try
        {
            var loadout = player.GetComponent<PlayerPartLoadout>();
            var motor = player.GetComponent<PlayerMotor2D>();
            float speed = motor.MoveSpeed, jump = motor.JumpVelocity;
            var config = new SerializedObject(loadout);
            var speedCurve = config.FindProperty("speedScale").animationCurveValue;
            var jumpCurve = config.FindProperty("jumpScale").animationCurveValue;
            for (int i = 0; i < 10; i++)
            {
                Assert.That(loadout.TryResummon(), Is.True);
                Assert.That(motor.MoveSpeed, Is.EqualTo(speed * speedCurve.Evaluate(loadout.Current.Speed)).Within(0.0001f));
                Assert.That(motor.JumpVelocity, Is.EqualTo(jump * jumpCurve.Evaluate(loadout.Current.Jump)).Within(0.0001f));
            }
            var previous = loadout.Current;
            player.SetActive(false);
            player.SetActive(true);
            Assert.That(loadout.Current, Is.SameAs(previous));
        }
        finally { Object.DestroyImmediate(player); Random.state = randomState; }
    }
}
#endif
