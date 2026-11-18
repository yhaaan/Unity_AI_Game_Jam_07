using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Core
{
    public static class SceneFlow
    {
        public const string TitleScene = "TitleScene";
        public const string Stage01Scene = "Stage01";
        public const string EndingScene = "EndingScene";

        public static void LoadTitle()
        {
            Load(TitleScene);
        }

        public static void LoadStage(int stageNumber)
        {
            Load($"Stage{stageNumber:00}");
        }

        public static void LoadStage01()
        {
            Load(Stage01Scene);
        }

        public static void LoadEnding()
        {
            Load(EndingScene);
        }

        public static void RestartActiveScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                SceneManager.LoadScene(activeScene.name, LoadSceneMode.Single);
            }
        }

        private static void Load(string sceneName)
        {
            if (Application.CanStreamedLevelBeLoaded(sceneName))
            {
                SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
        }
    }
}
