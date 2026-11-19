using System;

namespace ProjectRTD.Core
{
    // New-game intro hook. Gameplay registers a Runner (the old-film fusion show); Core only calls Run.
    public static class GameStartIntro
    {
        /// <summary>plays the intro, then calls the action it is given (once)</summary>
        public static Action<Action> Runner;
        public static bool Running { get; set; }

        public static void Run(Action then)
        {
            if (Running) return;
            if (Runner == null) { then(); return; }
            Runner(then);
        }
    }
}
