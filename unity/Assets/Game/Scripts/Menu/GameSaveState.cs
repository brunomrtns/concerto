using Studio.Game.Dialogue;
using Studio.Game.Parts;
using Studio.Game.Save;

namespace Studio.Game.Menu
{
    /// <summary>
    /// Fachada de integracao do Menu Principal / Pause com o SaveSystem real (JSON versionado em persistentDataPath).
    /// ZERO estado de jogo salvo em PlayerPrefs (PlayerPrefs fica exclusivamente para GameSettingsManager)!
    /// </summary>
    public static class GameSaveState
    {
        public const string DefaultGameScene = SaveSystem.DefaultGameScene;

        public static bool HasSave => SaveSystem.HasSave();
        public static string SavedScene
        {
            get
            {
                var d = SaveSystem.Load();
                return d != null && !string.IsNullOrEmpty(d.sceneName) ? d.sceneName : DefaultGameScene;
            }
        }

        public static bool HasActiveProgress()
        {
            if (SaveSystem.HasSave()) return true;
            if (PartsLoopManager.Instance != null &&
                (PartsLoopManager.Instance.CollectedCount > 0 || PartsLoopManager.Instance.CanCollectParts || PartsLoopManager.Instance.Act1Completed))
                return true;
            return !string.IsNullOrEmpty(ObjectiveSystem.CurrentObjective);
        }

        public static void PrepareNewGame()
        {
            SaveSystem.MarkNewGameRequested();
        }

        public static void PrepareContinue()
        {
            SaveSystem.MarkContinueRequested(out _);
        }

        public static void SaveCurrentWorkshopState()
        {
            SaveSystem.Save();
        }

        public static bool TryRestoreOnWorkshopBoot()
        {
            return SaveSystem.TryApplyPendingLoadToWorkshop();
        }
    }
}
