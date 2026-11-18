using NUnit.Framework;
using ProjectRTD.Gameplay;
using UnityEditor;
using UnityEngine;

public class EnemyTurnTests
{
    [Test]
    public void Stage04UsesAnimatedScientistPrefab()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene("Assets/00. Scenes/Stage04.unity");
        try
        {
            var active = new System.Collections.Generic.List<PlatformEnemy>();
            foreach (var root in scene.GetRootGameObjects()) active.AddRange(root.GetComponentsInChildren<PlatformEnemy>());
            Assert.That(active.Count, Is.EqualTo(1));
            var enemy = active[0];
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(enemy.gameObject), Is.EqualTo("Assets/02. Prefabs/Scientist.prefab"));
            var art = new SerializedObject(enemy).FindProperty("animatedArt").objectReferenceValue as Transform;
            Assert.That(art && art.GetComponent("ScientistAnimator"), Is.True);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase("Drone")]
    [TestCase("Scientist")]
    public void CardTurnMirrorsOnlyEnemyArt(string kind)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/02. Prefabs/{kind}.prefab");
        var enemy = Object.Instantiate(prefab);
        try
        {
            var visual = enemy.GetComponentInChildren<SpriteRenderer>().transform;
            Assert.That(visual.parent, Is.EqualTo(enemy.transform));
            var box = enemy.GetComponent<BoxCollider2D>();
            var size = box.bounds.size;
            float width = visual.localScale.x;
            var turn = new CardTurn(visual);
            turn.Tick(1f, .04f);
            Assert.That(Mathf.Abs(visual.localScale.x), Is.LessThan(width * .01f));
            Assert.That(box.bounds.size, Is.EqualTo(size));
            turn.Tick(1f, .04f);
            Assert.That(visual.localScale.x, Is.EqualTo(-width).Within(.0001f));
            turn.Tick(-1f, .08f);
            Assert.That(visual.localScale.x, Is.EqualTo(width).Within(.0001f));
        }
        finally { Object.DestroyImmediate(enemy); }
    }
}
