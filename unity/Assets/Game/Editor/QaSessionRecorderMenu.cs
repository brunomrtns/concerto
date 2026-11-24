#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Game.EditorTools
{
    /// <summary>
    /// Utilitario Editor-only de QA Automatizado (Playcaller + Unity Recorder).
    /// Nao altera nenhuma logica de gameplay; apenas expoe itens de menu para o Playcaller MCP
    /// abrir cenas e iniciar/encerrar gravacoes MP4 via Unity Recorder.
    /// </summary>
    public static class QaSessionRecorderMenu
    {
        private const string OutputPathPrefKey = "CONCERTO_QA_RECORDER_OUT_PATH";
        private static RecorderController _controller;

        private static string GetDefaultOutputNoExt()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string defaultPath = Path.Combine(repoRoot, "captures", "qa", "sessao_playcaller");
            string custom = EditorPrefs.GetString(OutputPathPrefKey, string.Empty);
            return string.IsNullOrEmpty(custom) ? defaultPath : custom;
        }

        [MenuItem("CONCERTO/QA/Open Act 1 Caldeiral")]
        public static void OpenAct1Caldeiral()
        {
            if (EditorApplication.isPlaying)
                EditorApplication.isPlaying = false;

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Caldeiral.unity", OpenSceneMode.Single);
            Debug.Log("[QA] Cena Assets/Game/Scenes/Caldeiral.unity aberta (Ato 1).");
        }

        [MenuItem("CONCERTO/QA/Start Recorder MP4")]
        public static void StartRecorderMp4()
        {
            StartRecorderInternal(GetDefaultOutputNoExt(), 125.0f, 20.0f, CoreEncoderSettings.VideoEncodingQuality.Low);
        }

        [MenuItem("CONCERTO/QA/Start Performance Final Recorder MP4")]
        public static void StartPerformanceFinalRecorderMp4()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string outNoExt = Path.Combine(repoRoot, "captures", "profiler", "performance_final");
            StartRecorderInternal(outNoExt, 16.0f, 30.0f, CoreEncoderSettings.VideoEncodingQuality.Medium);
        }

        private static void StartRecorderInternal(string outNoExt, float durationSec, float fps, CoreEncoderSettings.VideoEncodingQuality quality)
        {
            StopRecorderMp4();

            string outDir = Path.GetDirectoryName(outNoExt);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
            controllerSettings.SetRecordModeToTimeInterval(0.0f, durationSec);
            controllerSettings.FrameRate = fps;
            controllerSettings.CapFrameRate = false;

            var movieSettings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
            movieSettings.name = "CONCERTO_QA_MP4";
            movieSettings.Enabled = true;
            movieSettings.OutputFile = outNoExt;
            movieSettings.EncoderSettings = new CoreEncoderSettings
            {
                Codec = CoreEncoderSettings.OutputCodec.MP4,
                EncodingQuality = quality
            };
            movieSettings.ImageInputSettings = new GameViewInputSettings
            {
                OutputWidth = 1280,
                OutputHeight = 720
            };
            movieSettings.AudioInputSettings.PreserveAudio = true;

            controllerSettings.AddRecorderSettings(movieSettings);
            controllerSettings.Save();

            _controller = new RecorderController(controllerSettings);
            _controller.PrepareRecording();
            bool started = _controller.StartRecording();
            Debug.Log($"[QA] Unity Recorder MP4 iniciado={started} ({durationSec:F0}s @ {fps:F0}fps) -> {outNoExt}.mp4");
        }

        [MenuItem("CONCERTO/QA/Stop Recorder MP4")]
        public static void StopRecorderMp4()
        {
            if (_controller == null) return;
            try
            {
                if (_controller.IsRecording())
                    _controller.StopRecording();
            }
            finally
            {
                _controller = null;
                Debug.Log("[QA] Unity Recorder MP4 finalizado.");
            }
        }
    }
}
#endif
