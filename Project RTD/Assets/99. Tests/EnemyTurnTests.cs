using NUnit.Framework;
using ProjectRTD.Gameplay;
using UnityEditor;
using UnityEngine;

public class EnemyTurnTests
{
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
