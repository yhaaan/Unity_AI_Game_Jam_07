using NUnit.Framework;
using ProjectRTD.Gameplay;
using UnityEngine;

public sealed class FoleyTests
{
    private static ProjectRTD.Core.GameSoundBank Bank => UnityEditor.AssetDatabase.LoadAssetAtPath<ProjectRTD.Core.GameSoundBank>("Assets/05. Arts/GameSoundBank.asset");

    [Test]
    public void TravelRepeatsByDistanceRatherThanTime()
    {
        int Contacts(float dx, bool water)
        {
            var gait = new TravelCadence();
            int count = 0;
            for (int i = 0; i <= 100; i++)
                if (gait.Tick(i * dx, true, water, .5f)) count++;
            return count;
        }
        Assert.That(Contacts(.12f, false), Is.EqualTo(8));
        Assert.That(Contacts(-.12f, false), Is.EqualTo(8));
        Assert.That(Contacts(.06f, false), Is.EqualTo(4));
        Assert.That(Contacts(.12f, true), Is.EqualTo(6));
    }

    [Test]
    public void BlockedInactiveAndTeleportDoNotProduceContacts()
    {
        var gait = new TravelCadence();
        for (int i = 0; i < 120; i++) Assert.That(gait.Tick(2f, true, false, .5f), Is.False);
        for (int i = 0; i < 120; i++) Assert.That(gait.Tick(2f + i * .12f, false, false, .5f), Is.False);
        Assert.That(gait.Tick(-30f, true, false, .5f), Is.False);
        Assert.That(gait.Tick(-30.12f, true, false, .5f), Is.False);
    }

    [Test]
    public void JumpPlaybackContainsOnlyAnImmediateShortContact()
    {
        var original = Bank.GetClip(ProjectRTD.Core.Sfx.Jump);
        var cut = ProjectRTD.Core.JumpTakeoff.FirstContact(original);
        Assert.That(cut, Is.Not.Null);
        try
        {
            var pcm = new float[cut.samples];
            Assert.That(cut.GetData(pcm, 0), Is.True);
            Assert.That(cut.length, Is.InRange(.01f, .121f));
            int first = System.Array.FindIndex(pcm, v => Mathf.Abs(v) > .01f);
            Assert.That(first, Is.InRange(0, cut.frequency / 100));
            Assert.That(Mathf.Abs(pcm[pcm.Length - 1]), Is.LessThan(.0001f));
            TestContext.Out.WriteLine($"Jump: {original.length * 1000:F0} ms -> {cut.length * 1000:F0} ms; first signal at {1000f * first / cut.frequency:F1} ms.");
        }
        finally { Object.DestroyImmediate(cut); }
    }

    [Test]
    public void EveryNewRecordingLoadsAndContainsSignal()
    {
        Assert.That(UnityEditor.PlayerSettings.GetPreloadedAssets(), Does.Contain(Bank));
        foreach (ProjectRTD.Core.Sfx cue in System.Enum.GetValues(typeof(ProjectRTD.Core.Sfx)))
        {
            var name = cue.ToString();
            var clip = Bank.GetClip(cue);
            Assert.That(clip, Is.Not.Null, name);
            Assert.That(UnityEditor.AssetDatabase.GetAssetPath(clip), Does.StartWith("Assets/05. Arts/"));
            var pcm = new float[clip.samples * clip.channels];
            Assert.That(clip.GetData(pcm, 0), Is.True, name);
            Assert.That(System.Array.Exists(pcm, v => Mathf.Abs(v) > .01f), Is.True, name);
        }
    }
}
