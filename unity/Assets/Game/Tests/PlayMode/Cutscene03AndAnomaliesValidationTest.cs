using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// Valida TODOS os Gates da Cutscene 3 ("O Retorno") + 5 Anomalias Ambientais (Ato 2):
    ///   - Gate 1: Compila limpo, 0 erros, skip devolve estado limpo + anomalias ativas
    ///   - Gate 2: Playthrough completo CS03 -> controle devolvido + objetivo "Investigue a oficina" + 5 anomalias ativas
    ///   - Gate 3: ZERO anomalias ativas ANTES da Cutscene 3 (Ato 1 intacto!)
    ///   - Gate 4: Captura 1080p MP4 (CAPTURE_CS03=1): CS03 inteira (32s) + tour pelas 5 anomalias ativas + 5 screenshots
    /// </summary>
    public class Cutscene03AndAnomaliesValidationTest
    {
        private bool _capture;
        private string _capDir;
        private string _framesDir;
        private int _frameIdx;
        private RenderTexture _rt;
        private Texture2D _tex;
        private Text _tourBannerText;
        private GameObject _tourBannerCanvas;

        [UnityTest]
        public IEnumerator ValidateCutscene03AndFiveAct2Anomalies()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_CS03") == "1";

            // Garante estado de Ato 1 (antes da CS03): CS01 ja vista, CS03 e anomalias NAO ativas!
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.DeleteKey("CONCERTO_CS03_PLAYED");
            PlayerPrefs.DeleteKey("CONCERTO_ACT2_ANOMALIES_ACTIVE");
            PlayerPrefs.Save();

            Time.captureFramerate = _capture ? 30 : 60;

#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Game/Scenes/Caldeiral.unity",
                new LoadSceneParameters(LoadSceneMode.Single)
            );
#else
            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
#endif
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            var anomMgrType = Type.GetType("Studio.Game.Anomalies.WorkshopAnomalyManager, Assembly-CSharp");
            Assert.IsNotNull(anomMgrType, "WorkshopAnomalyManager nao encontrado!");
            var anomMgr = UnityEngine.Object.FindAnyObjectByType(anomMgrType);
            Assert.IsNotNull(anomMgr, "Act2_Workshop_Anomalies / WorkshopAnomalyManager ausente na cena!");

            // === GATE 3: ANTES DA CUTSCENE 3, ZERO ANOMALIAS PODEM ESTAR ATIVAS! ===
            Assert.IsFalse((bool)anomMgrType.GetProperty("AreAnomaliesActive").GetValue(anomMgr), "Gate 3 FALHOU: Anomalias nao podem estar ativas antes da Cutscene 3!");
            Assert.AreEqual(0, (int)anomMgrType.GetProperty("ActiveAnomalyCount").GetValue(anomMgr), "Gate 3 FALHOU: ActiveAnomalyCount deveria ser 0 antes da Cutscene 3!");

            var cs3Go = GameObject.Find("CS03_O_Retorno");
            Assert.IsNotNull(cs3Go, "CS03_O_Retorno nao encontrado em Caldeiral.unity!");
            var cs3Type = Type.GetType("Studio.Game.Cutscenes.Cutscene03ReturnController, Assembly-CSharp");
            var cs3 = cs3Go.GetComponent(cs3Type);
            Assert.IsNotNull(cs3);

            var isPlayingProp = cs3Type.GetProperty("IsPlaying");
            var playMethod    = cs3Type.GetMethod("PlayCutscene");
            var skipMethod    = cs3Type.GetMethod("SkipCutscene");
            var director      = cs3Go.GetComponent<PlayableDirector>();

            if (_capture)
            {
                string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
                _capDir    = Path.Combine(repoRoot, "captures", "act2_return");
                _framesDir = Path.Combine(_capDir, "frames_raw");
                Directory.CreateDirectory(_capDir);
                if (Directory.Exists(_framesDir)) Directory.Delete(_framesDir, true);
                Directory.CreateDirectory(_framesDir);
                _rt  = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                _frameIdx = 0;
            }

            // === GATE 2: Inicia Cutscene 3 ("O Retorno", 32s) ===
            playMethod.Invoke(cs3, null);
            yield return null;
            Assert.IsTrue((bool)isPlayingProp.GetValue(cs3), "Cutscene 3 deveria estar tocando!");

            if (_capture)
            {
                var shotTimes = new (float t, string name)[]
                {
                    (4.8f,  "01_cs03_beat1_retorno_oficina.png"),
                    (12.5f, "02_cs03_beat2_dez_foto_paz.png"),
                    (21.5f, "03_cs03_beat3_bugiganga_quebrada.png"),
                };
                int nextShot = 0;
                int guard = 0;

                while ((bool)isPlayingProp.GetValue(cs3) && guard < 1050)
                {
                    yield return null;
                    double curT = director != null ? director.time : (guard / 30.0);
                    string saveShot = (nextShot < shotTimes.Length && curT >= shotTimes[nextShot].t)
                        ? shotTimes[nextShot++].name
                        : null;
                    CaptureOneFrame(saveShot);
                    guard++;
                }
            }
            else
            {
                for (int i = 0; i < 30; i++) yield return null;
                skipMethod.Invoke(cs3, null);
                yield return null;
            }

            // === VERIFICA POS-CUTSCENE 3: CONTROLE DEVOLVIDO + OBJETIVO + 5 ANOMALIAS ATIVAS ===
            Assert.IsFalse((bool)isPlayingProp.GetValue(cs3), "Cutscene 3 deveria ter encerrado!");
            Assert.IsTrue((bool)anomMgrType.GetProperty("AreAnomaliesActive").GetValue(anomMgr), "Gate 2 FALHOU: Anomalias deveriam estar ativas pos-CS03!");
            Assert.AreEqual(5, (int)anomMgrType.GetProperty("ActiveAnomalyCount").GetValue(anomMgr), "Gate 2 FALHOU: Todas as 5 anomalias deveriam estar ativas!");

            var objType = Type.GetType("Studio.Game.Dialogue.ObjectiveSystem, Assembly-CSharp");
            string curObj = (string)objType.GetProperty("CurrentObjective").GetValue(null);
            Assert.IsTrue(curObj != null && curObj.Contains("Investigue"), $"Objetivo pos-CS03 incorreto: '{curObj}'");

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var loco = dezGo.GetComponent(locoType) as Behaviour;
            Assert.IsTrue(loco != null && loco.enabled, "Gate 1 FALHOU: DezLocomotionController nao foi reabilitado pos-CS03!");

            // === TOUR PELAS 5 ANOMALIAS ATIVAS (NO VIDEO 1080p!) ===
            var anomaliesList = anomMgrType.GetMethod("GetAllAnomalies").Invoke(anomMgr, null) as System.Collections.IList;
            Assert.IsNotNull(anomaliesList);
            Assert.AreEqual(5, anomaliesList.Count, "Deveria haver exatamente 5 anomalias configuradas!");

            if (_capture)
            {
                CreateTourBanner();
                var cam = Camera.main;
                var brainType = Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine");
                var brain = cam.GetComponent(brainType) as Behaviour;
                if (brain != null) brain.enabled = false;
                loco.enabled = false;

                for (int a = 0; a < anomaliesList.Count; a++)
                {
                    var anomComp = anomaliesList[a] as Component;
                    var dName = (string)anomComp.GetType().GetField("displayName").GetValue(anomComp);
                    SetTourBanner($"ANOMALIA {a + 1} / 5 — {dName.ToUpper()}");

                    Vector3 targetPos = anomComp.transform.position;
                    Vector3 camOffset = (a == 4) ? new Vector3(-2.0f, 2.2f, -2.0f) : new Vector3(-2.1f, 0.85f, -1.9f);
                    cam.transform.position = targetPos + camOffset;
                    cam.transform.LookAt(targetPos);

                    string shotName = (a == 2) ? "04_anomalia_ferramenta_flutuando.png"
                                    : (a == 4) ? "05_anomalia_sombra_desalinhada.png"
                                    : null;
                    for (int f = 0; f < 90; f++) // 3s por anomalia = 15s de tour!
                    {
                        yield return null;
                        CaptureOneFrame(f == 45 ? shotName : null);
                    }
                }

                if (brain != null) brain.enabled = true;
                loco.enabled = true;
                UnityEngine.Object.Destroy(_tourBannerCanvas);
                UnityEngine.Object.Destroy(_rt);
                UnityEngine.Object.Destroy(_tex);
            }

            // === PROVA DA API Anomaly.Resolve() e Anomaly.Rebreak() (para a task futura de Reparo!) ===
            var firstAnom = anomaliesList[0];
            firstAnom.GetType().GetMethod("Resolve").Invoke(firstAnom, null);
            Assert.AreEqual(4, (int)anomMgrType.GetProperty("ActiveAnomalyCount").GetValue(anomMgr), "Anomaly.Resolve() deveria reduzir anomalias ativas para 4!");
            firstAnom.GetType().GetMethod("Rebreak").Invoke(firstAnom, null);
            Assert.AreEqual(5, (int)anomMgrType.GetProperty("ActiveAnomalyCount").GetValue(anomMgr), "Anomaly.Rebreak() deveria reativar a anomalia (5/5)!");

            // Limpa PlayerPrefs para nao interferir em outros testes da suite!
            PlayerPrefs.DeleteKey("CONCERTO_CS03_PLAYED");
            PlayerPrefs.DeleteKey("CONCERTO_ACT2_ANOMALIES_ACTIVE");
            PlayerPrefs.Save();
            Time.captureFramerate = 0;
        }

        private void CaptureOneFrame(string savePngName)
        {
            var cam = Camera.main;
            if (cam == null) return;
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var oldModes = new RenderMode[canvases.Length];
            for (int c = 0; c < canvases.Length; c++)
            {
                oldModes[c] = canvases[c].renderMode;
                if (canvases[c].renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    canvases[c].renderMode = RenderMode.ScreenSpaceCamera;
                    canvases[c].worldCamera = cam;
                    canvases[c].planeDistance = 0.25f;
                }
            }

            cam.targetTexture = _rt;
            cam.Render();
            RenderTexture.active = _rt;
            _tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
            _tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;

            for (int c = 0; c < canvases.Length; c++)
            {
                if (oldModes[c] == RenderMode.ScreenSpaceOverlay)
                    canvases[c].renderMode = RenderMode.ScreenSpaceOverlay;
            }

            File.WriteAllBytes(Path.Combine(_framesDir, $"frame_{_frameIdx:D5}.jpg"), _tex.EncodeToJPG(90));
            if (!string.IsNullOrEmpty(savePngName))
            {
                File.WriteAllBytes(Path.Combine(_capDir, savePngName), _tex.EncodeToPNG());
                Debug.Log($"[CS03Capture] Saved screenshot: {savePngName}");
            }
            _frameIdx++;
        }

        private void CreateTourBanner()
        {
            _tourBannerCanvas = new GameObject("CS03_TourBanner");
            var c = _tourBannerCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 95;
            var sc = _tourBannerCanvas.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);

            var bar = new GameObject("Bar");
            bar.transform.SetParent(_tourBannerCanvas.transform, false);
            var rt = bar.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -18f);
            rt.sizeDelta = new Vector2(920f, 48f);
            bar.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.86f);

            var tGo = new GameObject("Text");
            tGo.transform.SetParent(bar.transform, false);
            var trt = tGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            _tourBannerText = tGo.AddComponent<Text>();
            _tourBannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _tourBannerText.fontSize = 22;
            _tourBannerText.fontStyle = FontStyle.Bold;
            _tourBannerText.color = new Color(0.30f, 0.88f, 0.96f, 1f);
            _tourBannerText.alignment = TextAnchor.MiddleCenter;
        }

        private void SetTourBanner(string msg)
        {
            if (_tourBannerText != null) _tourBannerText.text = msg;
        }
    }
}
