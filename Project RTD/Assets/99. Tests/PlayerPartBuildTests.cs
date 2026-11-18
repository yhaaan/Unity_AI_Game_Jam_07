#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using ProjectRTD.Gameplay;
using UnityEditor;
using UnityEngine;

public sealed class PlayerPartBuildTests
{
    [Test]
    public void EveryImportedCombinationHasValidLevels()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PartCatalog>("Assets/06. Data/PartCatalog.asset");
        Assert.That(catalog, Is.Not.Null);
        var heads = catalog.ForSlot(PartSlot.Head).ToArray();
        var bodies = catalog.ForSlot(PartSlot.Body).ToArray();
        var legs = catalog.ForSlot(PartSlot.Legs).ToArray();
        Assert.That(new[] { heads.Length, bodies.Length, legs.Length }, Is.All.EqualTo(5));
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
    public void HeavyLionLosesOneSpeedAfterClamping()
    {
        AnimalPartDefinition Part(string slot) => AssetDatabase.LoadAssetAtPath<AnimalPartDefinition>("Assets/06. Data/Part_lion_" + slot + ".asset");
        var lion = new PlayerPartBuild(Part("head"), Part("body"), Part("legs"));
        Assert.That(new[] { lion.Speed, lion.Jump, lion.Weight, lion.Health }, Is.EqualTo(new[] { 3, 3, 4, 5 }));
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
