using Studio.Game.Combat;
using Studio.Game.Powers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.Game.Save
{
    /// <summary>
    /// BUG-P0-01 Fix (#106): Garante estado 100% limpo em toda transicao de cena.
    /// Impede Time.timeScale preso (de parry freeze / finisher slowmo / hitstop), AudioListener.pause preso,
    /// Estase vazando entre cenas ou tokens de IA presos apos SceneManager.LoadScene.
    /// </summary>
    public static class SceneTransitionSafetyGuard
    {
        private static bool _hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (_hooked) return;
            _hooked = true;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Mathf.Abs(Time.timeScale - 1f) > 0.001f)
            {
                Debug.Log($"[SceneTransitionSafetyGuard] Time.timeScale ({Time.timeScale:F2}) restaurado EXATO para 1.000 ao carregar '{scene.name}'.");
                Time.timeScale = 1f;
            }
            AudioListener.pause = false;
            StasisFieldSystem.ClearAll();
            CombatGroupCoordinator.ResetAll();
        }
    }
}
