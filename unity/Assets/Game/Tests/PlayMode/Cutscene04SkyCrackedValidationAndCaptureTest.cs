using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// Valida TODOS os Gates da Cutscene 4 ("O Ceu Rachou" — ponte Oficina -> Setor Externo):
    ///   - Gate 1: Compila limpo, 0 erros, skip no Beat 1 devolve controle limpo em SetorExterno + porta destravada + save.
    ///   - Gate 2: MP4 1080p completo: Beat final do Ato 2 -> Cutscene 4 (4 beats) -> transicao SEAMLESS -> controle no setor -> Engenheira Vera alcancavel.
    ///   - Gate 3: Auto-save dispara no fim em SetorExterno e "Continuar" (via MainMenu -> SaveSystem) restaura DENTRO do setor (prova no video!).
    /// </summary>
    public sealed class Cutscene04SkyCrackedValidationAndCaptureTest
    {
        private bool _capture;
        private string _capDir;
        private string _framesDir;
        private RenderTexture _rt;
        private Texture2D _tex;
        private int _frameIdx;
        private GameObject _bannerCanvas;
        private Text _bannerText;

        [UnityTest]
        public IEnumerator ValidateAct2FinalBeatToCutscene04SeamlessSectorAndContinueSave()
        {
            _capture = Environment.GetEnvironmentVariable("CAPTURE_CS04") == "1";
            Time.captureFramerate = 30;

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string tempSave = Path.Combine(Application.temporaryCachePath, "cs04_gate3_save.json");
            var saveType = Type.GetType("Studio.Game.Save.SaveSystem, Assembly-CSharp");
            Assert.IsNotNull(saveType, "SaveSystem nao encontrado!");
            saveType.GetMethod("SetCustomPathForTests").Invoke(null, new object[] { tempSave });
            saveType.GetMethod("Delete").Invoke(null, null);

            PlayerPrefs.DeleteKey("CONCERTO_SECTOR_EXTERNO_UNLOCKED");
            PlayerPrefs.DeleteKey("CONCERTO_CS04_PLAYED");
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            if (_capture)
            {
                _capDir = Path.Combine(repoRoot, "captures", "cs04_sky_cracked");
                _framesDir = Path.Combine(_capDir, "frames_raw");
                Directory.CreateDirectory(_capDir);
                if (Directory.Exists(_framesDir)) Directory.Delete(_framesDir, true);
                Directory.CreateDirectory(_framesDir);
                _rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
                _tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                _frameIdx = 0;
            }

            // 1. Carrega Oficina (Caldeiral) e mostra o Beat final do Ato 2 (bugiganga quebrada + luz piscando) -> aciona Cutscene 4!
            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return null;

            var cs04Type = Type.GetType("Studio.Game.Cutscenes.Cutscene04SkyCrackedController, Assembly-CSharp");
            Assert.IsNotNull(cs04Type, "Cutscene04SkyCrackedController nao encontrado!");
            var cs04 = UnityEngine.Object.FindAnyObjectByType(cs04Type);
            Assert.IsNotNull(cs04, "CS04_O_Ceu_Rachou_Rig ausente em Caldeiral.unity!");

            // Mostra 2.5s do pos-beat final do Ato 2 na oficina antes do alarme disparar Cutscene 4
            var repCtrlType = Type.GetType("Studio.Game.Repair.RepairRitualController, Assembly-CSharp");
            var repCtrl = UnityEngine.Object.FindAnyObjectByType(repCtrlType);
            var ceilingGo = repCtrl != null ? repCtrlType.GetField("ceilingLightsRoot").GetValue(repCtrl) as GameObject : null;
            if (_capture) CreateBanner("ATO 2 → BEAT FINAL: BUGIGANGA QUEBROU • LUZES PISCAM");
            yield return StepFrames(_capture ? 32 : 2);
            if (ceilingGo != null) ceilingGo.SetActive(false);
            yield return StepFrames(_capture ? 8 : 1);
            if (ceilingGo != null) ceilingGo.SetActive(true);
            yield return StepFrames(_capture ? 35 : 2);

            // 2. Dispara Cutscene 4 ("O Ceu Rachou") — 36s, 4 Beats, transicao SEAMLESS para SetorExterno!
            cs04Type.GetMethod("PlayCutscene").Invoke(cs04, null);
            var isPlayingProp = cs04Type.GetProperty("IsPlaying");
            var elapsedProp   = cs04Type.GetProperty("ElapsedSeconds");
            Assert.IsTrue((bool)isPlayingProp.GetValue(cs04), "Cutscene 4 deveria iniciar!");

            bool s1 = false, s2 = false, s3 = false, s4 = false;
            int guard = 0;
            int maxFrames = _capture ? 1400 : 1250;
            while ((bool)isPlayingProp.GetValue(cs04) && guard < maxFrames)
            {
                float el = (float)elapsedProp.GetValue(cs04);
                string shot = null;

                if (_capture)
                {
                    if (el < 10.0f) UpdateBanner("BEAT 1 (0:00–0:10) — ALARME + LUZ DE BACKUP • JUNO ENTRA APRESSADA");
                    else if (el < 18.0f) UpdateBanner("BEAT 2 (0:10–0:18) — PORTA PESADA DESTRAVA (TRANSIÇÃO SEAMLESS)");
                    else if (el < 26.0f) UpdateBanner("BEAT 3 (0:18–0:26) — SETOR EXTERNO: O CÉU RACHOU (SILÊNCIO • SÓ VENTO)");
                    else if (el < 32.0f) UpdateBanner("BEAT 3 (0:26–0:32) — ENGENHEIRA VERA AO LONGE OLHANDO PRA CIMA");
                    else UpdateBanner("BEAT 4 (0:32–0:36) — OBJETIVO 'FALE COM VERA' + CONTROLE + AUTO-SAVE");
                }

                if (!s1 && el >= 5.2f)  { s1 = true; shot = "01_cs04_beat1_oficina_alarme_juno.png"; }
                if (!s2 && el >= 14.8f) { s2 = true; shot = "02_cs04_beat2_porta_destravando.png"; }
                if (!s3 && el >= 21.5f) { s3 = true; shot = "03_cs04_beat3_ceu_rachado.png"; }
                if (!s4 && el >= 28.5f) { s4 = true; shot = "04_cs04_beat3_vera_olhando_ceu.png"; }

                yield return StepFrames(1, shot, 0);
                guard++;
            }

            // Aguarda 3 frames para finalizar rotina de entrega de controle + auto-save
            yield return StepFrames(_capture ? 45 : 6, "05_cs04_beat4_controle_setor_autosave.png", _capture ? 18 : 1);

            // Valida estado apos Cutscene 4: ja em SetorExterno, porta destravada, controle ativo, Vera alcancavel!
            Assert.AreEqual("SetorExterno", SceneManager.GetActiveScene().name, "Transicao de cena deveria entregar o jogador em SetorExterno!");

            var doorType = Type.GetType("Studio.Game.Sector.SectorDoorInteractable, Assembly-CSharp");
            Assert.IsTrue((bool)doorType.GetProperty("IsSectorUnlocked").GetValue(null), "Porta do Setor Externo deveria estar destravada!");
            Assert.AreEqual(1, PlayerPrefs.GetInt("CONCERTO_CS04_PLAYED", 0), "Flag CONCERTO_CS04_PLAYED deveria estar ativa!");

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            Assert.IsNotNull(dezGo, "Player_Dez_Placeholder ausente em SetorExterno!");
            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var loco = dezGo.GetComponent(locoType) as Behaviour;
            Assert.IsTrue(loco != null && loco.enabled, "Controle do Dez nao foi entregue limpo no Setor Externo!");

            var objType = Type.GetType("Studio.Game.Dialogue.ObjectiveSystem, Assembly-CSharp");
            string curObj = (string)objType.GetProperty("CurrentObjective").GetValue(null);
            Assert.AreEqual("Fale com Vera", curObj, $"Objetivo pos-CS04 incorreto! Atual: '{curObj}'");

            // Confirma que Engenheira Vera e alcancavel e interagivel pelo Dez
            var veraGo = GameObject.Find("NPC_Engenheira_Vera");
            Assert.IsNotNull(veraGo, "NPC_Engenheira_Vera ausente!");
            var npcDlgType = Type.GetType("Studio.Game.Dialogue.NpcDialogueInteractable, Assembly-CSharp");
            var veraNpc = veraGo.GetComponent(npcDlgType);
            Assert.IsTrue((bool)npcDlgType.GetMethod("CanInteract").Invoke(veraNpc, new object[] { dezGo }), "Engenheira Vera deveria estar alcancavel/interagivel!");

            // GATE 3: Valida que o auto-save disparou no fim da CS04 com sceneName = SetorExterno -> Continuar restaura DENTRO do setor!
            Assert.IsTrue((bool)saveType.GetMethod("HasSave").Invoke(null, null), "Gate 3 FALHOU: Auto-save nao foi gravado no fim da CS04!");
            var savedData = saveType.GetMethod("Load").Invoke(null, null);
            var dataType = savedData.GetType();
            Assert.AreEqual("SetorExterno", (string)dataType.GetField("sceneName").GetValue(savedData), "Save deveria apontar para SetorExterno!");
            Assert.IsTrue((bool)dataType.GetField("cs04Played").GetValue(savedData), "Save deveria marcar cs04Played = true!");
            Assert.IsTrue((bool)dataType.GetField("sectorDoorUnlocked").GetValue(savedData), "Save deveria marcar sectorDoorUnlocked = true!");

            // Prova no video: volta ao MainMenu e aciona CONTINUAR -> restaura dentro de SetorExterno!
            if (_capture) UpdateBanner("GATE 3 — VOLTANDO AO MENU PRINCIPAL → CLICANDO 'CONTINUAR'");
            var mmOp = SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            while (mmOp != null && !mmOp.isDone) yield return null;
            yield return StepFrames(_capture ? 32 : 2);

            object[] args = new object[] { null };
            bool canCont = (bool)saveType.GetMethod("MarkContinueRequested").Invoke(null, args);
            string targetScene = args[0] as string;
            Assert.IsTrue(canCont, "Continuar deveria carregar o save da CS04!");
            Assert.AreEqual("SetorExterno", targetScene, "Continuar deveria carregar SetorExterno!");

            var contOp = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
            while (contOp != null && !contOp.isDone) yield return null;
            yield return StepFrames(_capture ? 45 : 4, "06_cs04_continuar_restaurado_no_setor.png", _capture ? 22 : 1);

            Assert.AreEqual("SetorExterno", SceneManager.GetActiveScene().name, "Continuar nao restaurou em SetorExterno!");
            Assert.AreEqual("Fale com Vera", (string)objType.GetProperty("CurrentObjective").GetValue(null), "Objetivo nao restaurou apos Continuar no Setor Externo!");

            // Limpeza
            saveType.GetMethod("Delete").Invoke(null, null);
            saveType.GetMethod("SetCustomPathForTests").Invoke(null, new object[] { null });
            PlayerPrefs.DeleteKey("CONCERTO_SECTOR_EXTERNO_UNLOCKED");
            PlayerPrefs.DeleteKey("CONCERTO_CS04_PLAYED");
            PlayerPrefs.Save();
            Time.captureFramerate = 0;

            if (_capture)
            {
                if (_bannerCanvas != null) UnityEngine.Object.Destroy(_bannerCanvas);
                UnityEngine.Object.Destroy(_rt);
                UnityEngine.Object.Destroy(_tex);
            }
        }

        [UnityTest]
        public IEnumerator ValidateCutscene04SkipHandsOverCleanStateInSector()
        {
            string tempSave = Path.Combine(Application.temporaryCachePath, "cs04_skip_save.json");
            var saveType = Type.GetType("Studio.Game.Save.SaveSystem, Assembly-CSharp");
            saveType.GetMethod("SetCustomPathForTests").Invoke(null, new object[] { tempSave });
            saveType.GetMethod("Delete").Invoke(null, null);

            PlayerPrefs.DeleteKey("CONCERTO_SECTOR_EXTERNO_UNLOCKED");
            PlayerPrefs.DeleteKey("CONCERTO_CS04_PLAYED");
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (op != null && !op.isDone) yield return null;
            yield return null;

            var cs04Type = Type.GetType("Studio.Game.Cutscenes.Cutscene04SkyCrackedController, Assembly-CSharp");
            var cs04 = UnityEngine.Object.FindAnyObjectByType(cs04Type);
            Assert.IsNotNull(cs04);

            cs04Type.GetMethod("PlayCutscene").Invoke(cs04, null);
            yield return null;
            yield return null;

            // Pula ainda no Beat 1 (na oficina) -> deve destravar porta, carregar SetorExterno, devolver controle limpo e salvar!
            cs04Type.GetMethod("SkipCutscene").Invoke(cs04, null);
            int guard = 0;
            while (SceneManager.GetActiveScene().name != "SetorExterno" && guard < 180) { yield return null; guard++; }
            yield return null;
            yield return null;

            Assert.AreEqual("SetorExterno", SceneManager.GetActiveScene().name, "Skip da CS04 deveria levar ao SetorExterno!");
            Assert.AreEqual(1f, Time.timeScale, 0.01f, "Time.timeScale deveria estar em 1 apos skip!");

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var loco = dezGo.GetComponent(locoType) as Behaviour;
            Assert.IsTrue(loco != null && loco.enabled, "Skip da CS04 nao devolveu controle do Dez!");

            var objType = Type.GetType("Studio.Game.Dialogue.ObjectiveSystem, Assembly-CSharp");
            Assert.AreEqual("Fale com Vera", (string)objType.GetProperty("CurrentObjective").GetValue(null));
            Assert.IsTrue((bool)saveType.GetMethod("HasSave").Invoke(null, null), "Skip da CS04 tambem deveria gravar o auto-save!");

            saveType.GetMethod("Delete").Invoke(null, null);
            saveType.GetMethod("SetCustomPathForTests").Invoke(null, new object[] { null });
            PlayerPrefs.DeleteKey("CONCERTO_SECTOR_EXTERNO_UNLOCKED");
            PlayerPrefs.DeleteKey("CONCERTO_CS04_PLAYED");
            PlayerPrefs.Save();
        }

        private IEnumerator StepFrames(int count, string shotName = null, int shotAt = -1)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
                if (!_capture) continue;
                var cam = Camera.main;
                if (cam == null) continue;

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
                cam.targetTexture = null;

                for (int c = 0; c < canvases.Length; c++)
                    canvases[c].renderMode = oldModes[c];

                RenderTexture.active = _rt;
                _tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                _tex.Apply();
                RenderTexture.active = null;

                File.WriteAllBytes(Path.Combine(_framesDir, $"frame_{_frameIdx:D5}.jpg"), _tex.EncodeToJPG(88));
                if (!string.IsNullOrEmpty(shotName) && i == shotAt)
                {
                    File.WriteAllBytes(Path.Combine(_capDir, shotName), _tex.EncodeToPNG());
                    Debug.Log($"[CS04Capture] Saved screenshot: {shotName}");
                }
                _frameIdx++;
            }
        }

        private void CreateBanner(string text)
        {
            if (_bannerCanvas == null)
            {
                _bannerCanvas = new GameObject("CS04_Capture_Banner");
                UnityEngine.Object.DontDestroyOnLoad(_bannerCanvas);
                var c = _bannerCanvas.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 999;
                var sc = _bannerCanvas.AddComponent<CanvasScaler>();
                sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1920, 1080);

                var bg = new GameObject("Bg");
                bg.transform.SetParent(_bannerCanvas.transform, false);
                var brt = bg.AddComponent<RectTransform>();
                brt.anchorMin = new Vector2(0.18f, 0.925f);
                brt.anchorMax = new Vector2(0.82f, 0.985f);
                brt.offsetMin = brt.offsetMax = Vector2.zero;
                bg.AddComponent<Image>().color = new Color(0.04f, 0.07f, 0.12f, 0.85f);

                var tGo = new GameObject("Label");
                tGo.transform.SetParent(bg.transform, false);
                var trt = tGo.AddComponent<RectTransform>();
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = trt.offsetMax = Vector2.zero;
                _bannerText = tGo.AddComponent<Text>();
                _bannerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _bannerText.fontSize = 23;
                _bannerText.fontStyle = FontStyle.Bold;
                _bannerText.alignment = TextAnchor.MiddleCenter;
                _bannerText.color = new Color(0.45f, 0.95f, 1.0f, 1f);
            }
            _bannerText.text = text;
        }

        private void UpdateBanner(string text)
        {
            if (_bannerCanvas == null) CreateBanner(text);
            else _bannerText.text = text;
        }
    }
}
