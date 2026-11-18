using System.Collections;
using NUnit.Framework;
using ProjectRTD.Core;
using ProjectRTD.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class StageRunRulesTests
{
    static PlayerPartLoadout Parts => StageRuntime.Instance.Player.GetComponent<PlayerPartLoadout>();
    static IEnumerator Ready()
    {
        float until = Time.realtimeSinceStartup + 30f;
        while ((!StageRuntime.Instance || !Parts || !Parts.didStart || StageRuntime.Instance.InDoorway) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(Parts.Current, Is.Not.Null);
        Assert.That(StageRuntime.Instance.InDoorway, Is.False);
    }

    [UnityTest] public IEnumerator StarterAndRerollSurviveStageLoads()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene);
        yield return SceneManager.LoadSceneAsync(SceneFlow.Stage01Scene);
        yield return null;
        yield return Ready();
        Assert.That(new[] { Parts.Current.Head.Animal, Parts.Current.Body.Animal, Parts.Current.Legs.Animal }, Is.EqualTo(new[] { PartAnimal.Axolotl, PartAnimal.Bear, PartAnimal.Chicken }));
        Assert.That(Parts.TryResummon(), Is.True);
        var selected = Parts.Current;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        yield return null; yield return null;
        yield return Ready();
        Assert.That(Parts.Current, Is.SameAs(selected));
        yield return SceneManager.LoadSceneAsync("Stage02");
        yield return null;
        yield return Ready();
        Assert.That(Parts.Current, Is.SameAs(selected));
        yield return new ExitPlayMode();
    }

    [UnityTest] public IEnumerator ScientistReturnsToPatrolStart()
    {
        yield return new EnterPlayMode();
        // Follow the normal menu-to-stage path after the editor enters play mode.
        yield return SceneManager.LoadSceneAsync(SceneFlow.TitleScene);
        yield return SceneManager.LoadSceneAsync("Stage04");
        yield return null;
        var stage = StageRuntime.Instance;
        var enemy = stage.GetComponentInChildren<PlatformEnemy>().GetComponent<Rigidbody2D>();
        Vector2 initial = enemy.position;
        yield return Ready();
        // Displace the patrol independently of frame rate, then exercise the stage's real failure sequence.
        enemy.position = initial + Vector2.right * 2f;
        bool reset = false;
        stage.ResetMechanics += () => reset = true;
        stage.ReturnToStart();
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!reset && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(reset, Is.True);
        Assert.That(Vector2.Distance(enemy.position, initial), Is.LessThan(.02f));
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        yield return null; yield return null;
        var restarted = StageRuntime.Instance.GetComponentInChildren<PlatformEnemy>().GetComponent<Rigidbody2D>();
        Assert.That(Vector2.Distance(restarted.position, initial), Is.LessThan(.02f));
        yield return new ExitPlayMode();
    }

    [UnityTest] public IEnumerator WaterBlocksOnlyNonSwimmers()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("Stage05");
        yield return null;
        yield return Ready();
        var gate = Object.FindFirstObjectByType<StageWaterHazard2D>().GetComponent<BoxCollider2D>();
        Assert.That(gate.isTrigger, Is.False);
        Assert.That(gate.enabled, Is.True);
        for (int i = 0; i < 100 && gate.enabled; i++) Parts.TryResummon();
        Assert.That(Parts.Current.HasEffect(PartEffect.Float) && !gate.enabled, Is.True);
        for (int i = 0; i < 100 && !gate.enabled; i++) Parts.TryResummon();
        Assert.That(!Parts.Current.HasEffect(PartEffect.Float) && gate.enabled, Is.True);
        yield return new ExitPlayMode();
    }
}
