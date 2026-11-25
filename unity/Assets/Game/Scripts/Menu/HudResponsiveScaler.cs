using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Studio.Game.Menu
{
    /// <summary>
    /// BL-006 (QA-05): Normalizador responsivo de CanvasScaler para 16:9 (1920x1080) e Ultrawide 21:9 (2560x1080).
    /// Garante que TODOS os HUDs (serializados em cena e gerados em runtime) compartilhem exatamente
    /// a mesma referencia (1920x1080) e ScreenMatchMode.Expand (escala 1.0 tanto em 1920x1080 quanto em 2560x1080),
    /// evitando inflacao vertical, cortes e sobreposicoes entre cantos em monitores ultrawide.
    /// </summary>
    public sealed class HudResponsiveScaler : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        public static HudResponsiveScaler Instance { get; private set; }

        private int _lastWidth;
        private int _lastHeight;
        private int _pendingNormalizationFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("HudResponsiveScaler");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<HudResponsiveScaler>();
            Instance.NormalizeAllActiveScalers();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _lastWidth = Screen.width;
            _lastHeight = Screen.height;
            _pendingNormalizationFrames = 3;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _pendingNormalizationFrames = 3;
            NormalizeAllActiveScalers();
        }

        private void LateUpdate()
        {
            bool resChanged = Screen.width != _lastWidth || Screen.height != _lastHeight;
            if (resChanged)
            {
                _lastWidth = Screen.width;
                _lastHeight = Screen.height;
                _pendingNormalizationFrames = 2;
            }

            if (_pendingNormalizationFrames > 0)
            {
                _pendingNormalizationFrames--;
                NormalizeAllActiveScalers();
            }
        }

        public static void ApplyStandardScaling(CanvasScaler scaler)
        {
            if (scaler == null) return;
            var canvas = scaler.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace) return;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 1f;
        }

        public int NormalizeAllActiveScalers()
        {
            var scalers = FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < scalers.Length; i++)
            {
                var sc = scalers[i];
                if (sc == null) continue;
                var canvas = sc.GetComponent<Canvas>();
                if (canvas != null && canvas.renderMode == RenderMode.WorldSpace) continue;

                ApplyStandardScaling(sc);
                count++;
            }
            return count;
        }
    }
}
