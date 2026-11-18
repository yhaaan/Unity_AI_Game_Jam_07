using System.Linq;
using NUnit.Framework;
using ProjectRTD.Gameplay;
using UnityEditor.SceneManagement;
using UnityEngine;

// The layout is baked into the scenes (Tools/RTD/Bake Stage Layout), so check what is saved there.
public class StageCourseLayoutTests
{
    [TestCase(1, false)] [TestCase(2, true)] [TestCase(3, true)] [TestCase(4, false)]
    [TestCase(5, true)] [TestCase(6, false)] [TestCase(7, false)]
    public void StageSceneHoldsTheWidenedCourse(int level, bool split)
    {
        var scene = EditorSceneManager.OpenPreviewScene($"Assets/00. Scenes/Stage{level:00}.unity");
        try
        {
            var stage = scene.GetRootGameObjects().Select(o => o.GetComponentInChildren<StageRuntime>(true)).Single(s => s);
            var floors = stage.GetComponentsInChildren<BoxCollider2D>().Where(c => c.name == "Island Surface").OrderBy(c => c.bounds.center.x).ToArray();
            Assert.That(floors.Length, Is.EqualTo(split ? 2 : 1));
            var limits = stage.transform.Find("Course Boundaries");
            Assert.That(limits, Is.Not.Null, "run Tools/RTD/Bake Stage Layout");
            var left = limits.Find("Left Boundary").GetComponent<BoxCollider2D>().bounds.max.x;
            var right = limits.Find("Right Boundary").GetComponent<BoxCollider2D>().bounds.min.x;
            Assert.That(right - left, Is.EqualTo(StageCourseLayout.Width).Within(.01f));
            Assert.That(floors.First().bounds.min.x, Is.EqualTo(left).Within(.01f));
            Assert.That(floors.Last().bounds.max.x, Is.EqualTo(right).Within(.01f));
            Assert.That(limits.GetComponentsInChildren<LabFloorArt>().Count(a => !a.pit), Is.EqualTo(floors.Length));
            // the whole entrance door (0.6 left of the spawn, ~1.75 half width) is inside the room
            Assert.That(stage.SpawnPoint.position.x - .6f - 1.75f, Is.GreaterThan(left));

            // doors are saved in the scene and stay inside the room
            var entrance = stage.SpawnPoint.Find("Entrance Door");
            Assert.That(entrance, Is.Not.Null);
            var goal = stage.GetComponentInChildren<StageGoal>(true);
            var exit = goal.transform.parent.Find("Exit Door");
            Assert.That(exit, Is.Not.Null);
            Assert.That(exit.position.x + 1.75f, Is.LessThan(right));
            Assert.That(scene.GetRootGameObjects().Any(o => o.name == "Assembly Canvas"), Is.False);

            var bounds = scene.GetRootGameObjects().Select(o => o.GetComponentInChildren<StageOverviewCamera>(true)).Single(c => c);
            Assert.That(bounds.leftWall, Is.Not.Null);
            Assert.That(bounds.rightWall, Is.Not.Null);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
