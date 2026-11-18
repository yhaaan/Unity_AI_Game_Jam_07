using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using ProjectRTD.Core;
using UnityEditor;
using UnityEngine;

public class SharedArtTests
{
    [Test]
    public void EverySharedPngHasAMenuBindingOrIdenticalParallaxSprite()
    {
        const string shared = "Assets/90. ArtShare/ART";
        var bank = AssetDatabase.LoadAssetAtPath<SharedMenuArt>("Assets/05. Arts/SharedMenuArt.asset");
        Assert.That(PlayerSettings.GetPreloadedAssets(), Does.Contain(bank));
        var menu = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(bank)).ToHashSet();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/02. Prefabs/ParallaxBackground.prefab");
        var layers = prefab.GetComponentsInChildren<SpriteRenderer>(true)
            .Select(r => AssetDatabase.GetAssetPath(r.sprite)).Distinct().ToDictionary(p => Path.GetFileName(p));
        using var hash = SHA256.Create();
        foreach (var file in Directory.GetFiles(shared, "*.png", SearchOption.AllDirectories))
        {
            if (menu.Contains(file.Replace('\\','/'))) continue;
            Assert.That(layers.TryGetValue(Path.GetFileName(file), out string layer), Is.True, file);
            using var original = File.OpenRead(file);
            using var runtime = File.OpenRead(layer);
            CollectionAssert.AreEqual(hash.ComputeHash(original), hash.ComputeHash(runtime), file);
        }
    }
}
