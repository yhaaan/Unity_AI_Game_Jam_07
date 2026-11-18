using System.Collections;
using NUnit.Framework;
using ProjectRTD.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class EndingFlowTests
{
    [UnityTest]
    public IEnumerator FinishingCreditsRestoresTheReturnScreen()
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
        yield return null;
        yield return null;
        Assert.That(GameObject.Find("/Ending Canvas"), Is.Not.Null);
        Assert.That(GameObject.Find("/Screen Flow/Ending Credits"), Is.Null);
        yield return new ExitPlayMode();
    }
}
