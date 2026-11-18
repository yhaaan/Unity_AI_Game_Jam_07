using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class ParallaxBackgroundTests
{
    [Test]
    public void BackgroundUsesEveryLabImageWithoutMissingSprites()
    {
        const string folder = "Assets/03. Sprites/Background(New)";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02. Prefabs/ParallaxBackground.prefab");
        var renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var renderer in renderers)
            Assert.IsNotNull(renderer.sprite, renderer.name);

        var used = renderers.Select(r => AssetDatabase.GetAssetPath(r.sprite)).Distinct();
        var supplied = Directory.GetFiles(folder, "*.png").Select(p => p.Replace('\\', '/'));
        CollectionAssert.AreEquivalent(supplied, used);
    }
}
