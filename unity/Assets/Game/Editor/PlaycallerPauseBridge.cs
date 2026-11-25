#if UNITY_EDITOR
using System;
using System.Reflection;
using Studio.Game.Menu;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.Game.EditorTools
{
    /// <summary>
    /// BL-005: Ponte Editor-only entre Playcaller (PlayCallerInput / PlaycallerGameBridge)
    /// e PauseMenuController.
    ///   - Consome teclas enfileiradas por playcaller_key_press via EditorApplication.update
    ///     (nao depende de foco da Game View nem de Time.timeScale > 0).
    ///   - Expoe QueryHandler no PlaycallerGameBridge para inspecao instantanea de Pause/TimeScale.
    /// </summary>
    [InitializeOnLoad]
    public static class PlaycallerPauseBridge
    {
        private static readonly KeyCode[] ForwardedKeys =
        {
            KeyCode.Escape,
            KeyCode.UpArrow,
            KeyCode.DownArrow,
            KeyCode.Return,
            KeyCode.Space
        };

        private static MethodInfo _consumeKeyMethod;
        private static bool _initialized;

        static PlaycallerPauseBridge()
        {
            EnsureReflectionBindings();
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void EnsureReflectionBindings()
        {
            if (_initialized) return;
            _initialized = true;

            Type inputType = Type.GetType("Playcaller.Editor.PlayCallerInput, Playcaller.Editor");
            if (inputType != null)
            {
                _consumeKeyMethod = inputType.GetMethod("ConsumeKey", BindingFlags.Public | BindingFlags.Static);
            }

            Type bridgeType = Type.GetType("Playcaller.Editor.PlaycallerGameBridge, Playcaller.Editor");
            if (bridgeType != null)
            {
                FieldInfo qField = bridgeType.GetField("QueryHandler", BindingFlags.Public | BindingFlags.Static);
                if (qField != null)
                {
                    Func<string, string> handler = HandleGameQuery;
                    qField.SetValue(null, handler);
                }
            }
        }

        private static void OnEditorUpdate()
        {
            if (!Application.isPlaying) return;
            if (_consumeKeyMethod == null) return;

            var pause = PauseMenuController.Instance;
            if (pause == null) return;

            for (int i = 0; i < ForwardedKeys.Length; i++)
            {
                KeyCode key = ForwardedKeys[i];
                // So consome teclas de navegacao quando ja esta pausado, evitando roubar Space/Return do gameplay
                if (key != KeyCode.Escape && !pause.IsPaused) continue;

                object consumedObj = _consumeKeyMethod.Invoke(null, new object[] { key });
                if (consumedObj is bool consumed && consumed)
                {
                    pause.SimulateKeyPress(key);
                }
            }
        }

        private static string HandleGameQuery(string queryType)
        {
            var pause = PauseMenuController.Instance;
            string sceneName = SceneManager.GetActiveScene().name;
            bool isPaused = pause != null && pause.IsPaused;
            int cycles = pause != null ? pause.PauseCycleCount : 0;
            string focus = pause != null ? pause.FocusedPauseLabel : "";
            float ts = Time.timeScale;
            return $"{{\"scene\":\"{sceneName}\",\"hasPauseController\":{(pause != null ? "true" : "false")},\"isPaused\":{(isPaused ? "true" : "false")},\"timeScale\":{ts.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)},\"pauseCycles\":{cycles},\"focusedLabel\":\"{focus}\"}}";
        }
    }
}
#endif
