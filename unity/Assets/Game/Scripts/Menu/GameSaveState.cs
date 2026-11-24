using Studio.Game.Cutscenes;
using Studio.Game.Dialogue;
using Studio.Game.Parts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.Game.Menu
{
    /// <summary>
    /// Estado minimo de progresso via PlayerPrefs (save serio e task futura):
    /// Cena + Objetivo atual + Pecas coletadas (bitmask) + Flags do Ato 1.
    /// </summary>
    public static class GameSaveState
    {
        public const string KeySaveExists     = "CONCERTO_SAVE_EXISTS";
        public const string KeySaveScene      = "CONCERTO_SAVE_SCENE";
        public const string KeySaveObjective  = "CONCERTO_SAVE_OBJECTIVE";
        public const string KeySavePartsMask  = "CONCERTO_SAVE_PARTS_MASK";
        public const string KeySaveCanCollect = "CONCERTO_SAVE_CAN_COLLECT";
        public const string KeySaveAct1Done   = "CONCERTO_SAVE_ACT1_DONE";
        public const string KeyLoadMode       = "CONCERTO_LOAD_MODE";

        public const string ModeNewGame  = "new_game";
        public const string ModeContinue = "continue";
        public const string DefaultGameScene = "Caldeiral";

        public static bool HasSave => PlayerPrefs.GetInt(KeySaveExists, 0) == 1;
        public static string SavedScene => PlayerPrefs.GetString(KeySaveScene, DefaultGameScene);
        public static string SavedObjective => PlayerPrefs.GetString(KeySaveObjective, string.Empty);
        public static int SavedPartsMask => PlayerPrefs.GetInt(KeySavePartsMask, 0);

        public static bool HasActiveProgress()
        {
            if (HasSave) return true;
            if (PartsLoopManager.Instance != null &&
                (PartsLoopManager.Instance.CollectedCount > 0 || PartsLoopManager.Instance.CanCollectParts || PartsLoopManager.Instance.Act1Completed))
                return true;
            return !string.IsNullOrEmpty(ObjectiveSystem.CurrentObjective);
        }

        /// <summary>
        /// Novo Jogo: reseta flags de save + CONCERTO_CS01_PLAYED para a Cutscene 1 rodar do zero!
        /// </summary>
        public static void PrepareNewGame()
        {
            PlayerPrefs.DeleteKey(KeySaveExists);
            PlayerPrefs.DeleteKey(KeySaveScene);
            PlayerPrefs.DeleteKey(KeySaveObjective);
            PlayerPrefs.DeleteKey(KeySavePartsMask);
            PlayerPrefs.DeleteKey(KeySaveCanCollect);
            PlayerPrefs.DeleteKey(KeySaveAct1Done);
            PlayerPrefs.DeleteKey(CutsceneManager.PlayedPrefKey);
            PlayerPrefs.SetString(KeyLoadMode, ModeNewGame);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Continuar: marca modo continue + garante que Cutscene 1 nao rode novamente ao entrar na oficina.
        /// </summary>
        public static void PrepareContinue()
        {
            PlayerPrefs.SetInt(CutsceneManager.PlayedPrefKey, 1);
            PlayerPrefs.SetString(KeyLoadMode, ModeContinue);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Salva o estado minimo atual da oficina (cena + objetivo + pecas).
        /// </summary>
        public static void SaveCurrentWorkshopState()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName) || sceneName == "MainMenu")
                sceneName = DefaultGameScene;

            PlayerPrefs.SetInt(KeySaveExists, 1);
            PlayerPrefs.SetString(KeySaveScene, sceneName);
            PlayerPrefs.SetInt(CutsceneManager.PlayedPrefKey, 1);

            if (!string.IsNullOrEmpty(ObjectiveSystem.CurrentObjective))
                PlayerPrefs.SetString(KeySaveObjective, ObjectiveSystem.CurrentObjective);

            var parts = PartsLoopManager.Instance;
            if (parts != null)
            {
                PlayerPrefs.SetInt(KeySavePartsMask, parts.GetCollectedMask());
                PlayerPrefs.SetInt(KeySaveCanCollect, parts.CanCollectParts ? 1 : 0);
                PlayerPrefs.SetInt(KeySaveAct1Done, parts.Act1Completed ? 1 : 0);
            }

            PlayerPrefs.Save();
        }

        /// <summary>
        /// Chamado ao abrir a cena da oficina: se entrou via Continuar, restaura pecas + objetivo salvo.
        /// </summary>
        public static bool TryRestoreOnWorkshopBoot()
        {
            string mode = PlayerPrefs.GetString(KeyLoadMode, string.Empty);
            if (mode != ModeContinue || !HasSave)
                return false;

            PlayerPrefs.DeleteKey(KeyLoadMode);
            PlayerPrefs.Save();

            int mask       = PlayerPrefs.GetInt(KeySavePartsMask, 0);
            bool canCol    = PlayerPrefs.GetInt(KeySaveCanCollect, 0) == 1;
            bool act1Done  = PlayerPrefs.GetInt(KeySaveAct1Done, 0) == 1;
            string objText = PlayerPrefs.GetString(KeySaveObjective, string.Empty);

            if (PartsLoopManager.Instance != null)
                PartsLoopManager.Instance.RestoreFromSave(mask, canCol, act1Done);

            if (!string.IsNullOrEmpty(objText))
                ObjectiveSystem.Set(objText);

            Debug.Log($"[GameSaveState] Estado restaurado (Continuar): mask={mask}, canCollect={canCol}, act1Done={act1Done}, obj='{objText}'");
            return true;
        }
    }
}
