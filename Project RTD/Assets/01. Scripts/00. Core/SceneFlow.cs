using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectRTD.Core
{
    public static class SceneFlow
    {
        public const string TitleScene = "TitleScene";
        public const string Stage01Scene = "Stage01";
        public const string PrologueScene = "PrologueScene";
        public const string EndingScene = "EndingScene";

        public static void LoadTitle()
        {
            Load(TitleScene);
        }

        // New game from the title: comic prologue first when it is in the build, otherwise straight to Stage01.
        public static void StartNewGame()
        {
            Load(Application.CanStreamedLevelBeLoaded(PrologueScene) ? PrologueScene : Stage01Scene);
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
