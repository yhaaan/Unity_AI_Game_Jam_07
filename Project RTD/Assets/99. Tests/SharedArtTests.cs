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
    public void StageGimmickTexturesAndFrameGridsAreReady()
    {
        var bank = AssetDatabase.LoadAssetAtPath<SharedMenuArt>("Assets/05. Arts/SharedMenuArt.asset");
        var textures = new[] { bank.crate, bank.caveRoof, bank.reroll, bank.plateSheet, bank.doorSheet, bank.pressSheet };
        var paths = new[] { "stage/box.png", "stage/cave 1.png", "UI/DNA_icon.png", "stage/red_button_press_sheet.png", "Background/door_animation_sheet.png", "stage/press_retract_sheet.png" };
        var missing = new System.Collections.Generic.List<string>();
        for (int i = 0; i < textures.Length; ++i)
        {
            var path = "Assets/90. ArtShare/ART/" + paths[i];
            if (AssetDatabase.GetAssetPath(textures[i]) == path) continue;
            missing.Add(path);
        }
        Assert.That(missing, Is.Empty, string.Join("\n", missing));
        Assert.That(bank.plateGrid, Is.EqualTo(new Vector2Int(4, 1)));
        Assert.That(bank.doorGrid, Is.EqualTo(new Vector2Int(4, 2)));
        Assert.That(bank.pressGrid, Is.EqualTo(new Vector2Int(4, 2)));
    }

    [Test]
    public void EveryStageHudCanOpenAndCloseSettings()
    {
        float originalScale = Time.timeScale;
        for (int level = 1; level <= 7; level++)
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene($"Assets/00. Scenes/Stage{level:00}.unity");
            try
            {
                var hud = scene.GetRootGameObjects().Single(o => o.name == "Stage HUD");
                var menu = hud.GetComponent<StagePauseMenu>();
                Assert.That(menu, Is.Not.Null, scene.name);
                // Preview scenes don't dispatch behaviour messages like play-mode scenes do.
                void Invoke(string method, params object[] args) => typeof(StagePauseMenu).GetMethod(method,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(menu, args);
                if (!hud.transform.Find("Pause menu")) Invoke("Awake");
                Assert.That(hud.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                Time.timeScale = .75f;
                Invoke("OpenMenu", true);
                Assert.That(hud.transform.Find("Pause menu/Settings/Image/Music volume").gameObject.activeInHierarchy, Is.True, scene.name);
                Assert.That(Time.timeScale, Is.Zero);
                Invoke("Close");
                Assert.That(hud.transform.Find("Pause menu").gameObject.activeSelf, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(.75f));
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                Time.timeScale = originalScale;
            }
        }
    }

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
