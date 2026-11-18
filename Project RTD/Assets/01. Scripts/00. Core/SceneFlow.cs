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
        public const string LevelSelectScene = "LevelSelectScene";
        public const int StageCount = 7;
        private const string UnlockedStageKey = "rtd.highest-unlocked-stage";
        private static string levelSelectReturnScene;

        public static int HighestUnlockedStage => Mathf.Clamp(PlayerPrefs.GetInt(UnlockedStageKey, 1), 1, StageCount);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => levelSelectReturnScene = TitleScene;

        public static void LoadPrologue() => Load(PrologueScene);

        public static void LoadTitle()
        {
            Load(TitleScene);
        }

        public static void StartNewGame()
        {
            LoadStage(1);
        }

        public static void LoadStage(int stageNumber)
        {
            if (stageNumber >= 1 && stageNumber <= StageCount) Load($"Stage{stageNumber:00}");
        }

        public static void LoadStage01()
        {
            Load(Stage01Scene);
        }

        public static void LoadEnding()
        {
            Load(EndingScene);
        }

        public static void CompleteActiveStage()
        {
            string scene = SceneManager.GetActiveScene().name;
            for (int stage = 1; stage <= StageCount; stage++)
            {
                if (scene != $"Stage{stage:00}") continue;
                if (stage == StageCount)
                {
                    LoadEnding();
                    return;
                }

                int next = stage + 1;
                if (stage == HighestUnlockedStage)
                {
                    PlayerPrefs.SetInt(UnlockedStageKey, next);
                    PlayerPrefs.Save();
                }
                LoadStage(next);
                return;
            }
            LoadEnding();
        }

        public static void LoadLevelSelect()
        {
            string current = SceneManager.GetActiveScene().name;
            if (current == LevelSelectScene) return;
            levelSelectReturnScene = current;
            Load(LevelSelectScene);
        }

        public static void LeaveLevelSelect()
        {
            string scene = levelSelectReturnScene ?? TitleScene;
            Load(Application.CanStreamedLevelBeLoaded(scene) ? scene : TitleScene);
        }

        public static void LoadSelectedStage(int number)
        {
            if (number <= HighestUnlockedStage) LoadStage(number);
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
