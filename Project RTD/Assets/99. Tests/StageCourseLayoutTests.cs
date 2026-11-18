using System.Linq;
using NUnit.Framework;
using ProjectRTD.Gameplay;
using UnityEditor.SceneManagement;
using UnityEngine;

public class StageCourseLayoutTests
{
    [TestCase(1, false)] [TestCase(2, true)] [TestCase(3, true)] [TestCase(4, false)]
    [TestCase(5, true)] [TestCase(6, false)] [TestCase(7, false)]
    public void WidenCourseWithoutChangingPuzzleGaps(int level, bool split)
    {
        var scene = EditorSceneManager.OpenPreviewScene($"Assets/00. Scenes/Stage{level:00}.unity");
        try
        {
            var stage = scene.GetRootGameObjects().Select(o => o.GetComponent<StageRuntime>()).Single(s => s);
            var floors = stage.GetComponentsInChildren<BoxCollider2D>().Where(c => c.name == "Island Surface").OrderBy(c => c.bounds.center.x).ToArray();
            Assert.That(floors.Length, Is.EqualTo(split ? 2 : 1));
            float gap = floors.Last().bounds.min.x - floors.First().bounds.max.x;
            var goal = stage.GetComponentInChildren<StageGoal>();
            float travel = goal.transform.position.x - stage.SpawnPoint.position.x;
            StageCourseLayout.Apply(stage);
            var left = stage.transform.Find("Course Boundaries/Left Boundary").GetComponent<BoxCollider2D>().bounds.max.x;
            var right = stage.transform.Find("Course Boundaries/Right Boundary").GetComponent<BoxCollider2D>().bounds.min.x;
            Assert.That(right - left, Is.EqualTo(24.8f).Within(.01f));
            Assert.That(floors.First().bounds.min.x, Is.EqualTo(left).Within(.01f));
            Assert.That(floors.Last().bounds.max.x, Is.EqualTo(right).Within(.01f));
            Assert.That(goal.transform.position.x - stage.SpawnPoint.position.x, Is.GreaterThan(travel * 1.8f));
            if (split) Assert.That(floors.Last().bounds.min.x - floors.First().bounds.max.x, Is.EqualTo(gap).Within(.01f));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
