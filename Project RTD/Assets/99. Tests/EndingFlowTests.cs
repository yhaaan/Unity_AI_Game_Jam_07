using System.Collections;
using NUnit.Framework;
using ProjectRTD.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EndingFlowTests
{
    [UnityTest]
    public IEnumerator FinishingCreditsLeadsThroughPrologueToTitle()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync(SceneFlow.EndingScene);
        yield return null;
        yield return null;
        var roll = GameObject.Find("/Screen Flow/Ending Credits");
        Assert.That(roll, Is.Not.Null);
        Assert.That(GameObject.Find("/Ending Canvas"), Is.Null);
        var stage = roll.transform.Find("Stage");
        for (int i = 1; i <= 4; i++) Assert.That(stage.Find("Card " + i), Is.Not.Null);
        // Exercise the real final-card completion callback without waiting for all four holds.
        roll.SendMessage("Show", 3);
        roll.SendMessage("Next");
        float timeout = Time.realtimeSinceStartup + 10f;
        while (SceneManager.GetActiveScene().name == SceneFlow.EndingScene && Time.realtimeSinceStartup < timeout)
        {
            Assert.That(GameObject.Find("/Ending Canvas"), Is.Null);
            yield return null;
        }
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneFlow.PrologueScene));
        yield return null;
        var flow = Object.FindFirstObjectByType<ProjectRTD.Gameplay.PrologueFlow>();
        Assert.That(flow, Is.Not.Null);
        flow.SendMessage("Leave");
        yield return null;
        yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneFlow.TitleScene));
        yield return new ExitPlayMode();
    }
}
