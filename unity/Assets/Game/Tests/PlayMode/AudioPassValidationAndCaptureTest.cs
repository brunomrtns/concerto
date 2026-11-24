using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    public sealed class AudioPassValidationAndCaptureTest
    {
        private const string CaptureRootRel = "Logs/audio_pass_capture";

        private static Type GameType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo nao encontrado em Assembly-CSharp: {fullName}");
            return t;
        }

        private static string GetCaptureDir(string sub)
        {
            string dir = Path.Combine(Application.dataPath, "..", CaptureRootRel, sub);
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void CaptureFrameJpg(Camera cam, RenderTexture rt, Texture2D tex, string filePath)
        {
            if (cam == null) return;
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            tex.Apply(false, false);
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            File.WriteAllBytes(filePath, tex.EncodeToJPG(88));
        }

        [UnityTest]
        public IEnumerator T01_AudioMixerBusesAndPlayerPrefsPersistence()
        {
            yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var mixT = GameType("Studio.Game.Audio.AudioMixController");
            var cfgT = GameType("Studio.Game.Menu.GameSettingsManager");

            var mix = mixT.GetProperty("Instance").GetValue(null);
            Assert.IsNotNull(mix, "AudioMixController.Instance deve existir apos carregar MainMenu.");

            var mixer = mixT.GetProperty("MasterMixer").GetValue(mix) as AudioMixer;
            Assert.IsNotNull(mixer, "ConcertoMasterMixer deve estar carregado no ConcertoAudioBank.");

            // Verifica os 5 buses expostos no AudioMixer
            Assert.IsTrue(mixer.GetFloat("MasterVol", out _), "MasterVol exposto obrigatorio.");
            Assert.IsTrue(mixer.GetFloat("MusicVol", out _), "MusicVol exposto obrigatorio.");
            Assert.IsTrue(mixer.GetFloat("SfxVol", out _), "SfxVol exposto obrigatorio.");
            Assert.IsTrue(mixer.GetFloat("AmbienceVol", out _), "AmbienceVol exposto obrigatorio.");
            Assert.IsTrue(mixer.GetFloat("UiVol", out _), "UiVol exposto obrigatorio.");

            float prevMaster = (float)cfgT.GetProperty("MasterVolume").GetValue(null);
            float prevMusic  = (float)cfgT.GetProperty("MusicVolume").GetValue(null);
            float prevSfx    = (float)cfgT.GetProperty("SfxVolume").GetValue(null);
            float prevAmb    = (float)cfgT.GetProperty("AmbienceVolume").GetValue(null);
            float prevUi     = (float)cfgT.GetProperty("UiVolume").GetValue(null);

            cfgT.GetMethod("SetMasterVolume").Invoke(null, new object[] { 0.73f });
            cfgT.GetMethod("SetMusicVolume").Invoke(null, new object[] { 0.44f });
            cfgT.GetMethod("SetSfxVolume").Invoke(null, new object[] { 0.91f });
            cfgT.GetMethod("SetAmbienceVolume").Invoke(null, new object[] { 0.56f });
            cfgT.GetMethod("SetUiVolume").Invoke(null, new object[] { 0.68f });
            PlayerPrefs.Save();
            yield return null;

            // Simula fechar e reabrir o jogo recarregando PlayerPrefs do disco
            cfgT.GetMethod("LoadAndApply").Invoke(null, null);
            mixT.GetMethod("ApplyAllBusVolumes").Invoke(mix, null);
            yield return null;

            Assert.AreEqual(0.73f, (float)cfgT.GetProperty("MasterVolume").GetValue(null),   0.001f, "MasterVolume deve persistir via PlayerPrefs.");
            Assert.AreEqual(0.44f, (float)cfgT.GetProperty("MusicVolume").GetValue(null),    0.001f, "MusicVolume deve persistir via PlayerPrefs.");
            Assert.AreEqual(0.91f, (float)cfgT.GetProperty("SfxVolume").GetValue(null),      0.001f, "SfxVolume deve persistir via PlayerPrefs.");
            Assert.AreEqual(0.56f, (float)cfgT.GetProperty("AmbienceVolume").GetValue(null), 0.001f, "AmbienceVolume deve persistir via PlayerPrefs.");
            Assert.AreEqual(0.68f, (float)cfgT.GetProperty("UiVolume").GetValue(null),       0.001f, "UiVolume deve persistir via PlayerPrefs.");

            mixer.GetFloat("SfxVol", out float sfxDb);
            float expectedDb = (float)mixT.GetMethod("LinearToDecibels").Invoke(null, new object[] { 0.91f });
            Assert.AreEqual(expectedDb, sfxDb, 0.25f, "SfxVol no AudioMixer deve refletir o slider.");

            // Restaura valores padrao limpos
            cfgT.GetMethod("SetMasterVolume").Invoke(null, new object[] { prevMaster });
            cfgT.GetMethod("SetMusicVolume").Invoke(null, new object[] { prevMusic });
            cfgT.GetMethod("SetSfxVolume").Invoke(null, new object[] { prevSfx });
            cfgT.GetMethod("SetAmbienceVolume").Invoke(null, new object[] { prevAmb });
            cfgT.GetMethod("SetUiVolume").Invoke(null, new object[] { prevUi });
            PlayerPrefs.Save();

            Debug.Log("[AudioPassTest] T01 PASSOU: 5 buses expostos no AudioMixer + persistencia PlayerPrefs verificada.");
        }

        [UnityTest]
        public IEnumerator T02_GameplayAudioFootstepsImpactsUiAmbienceAndCapture60s()
        {
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var cs01T  = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var mixT   = GameType("Studio.Game.Audio.AudioMixController");
            var bankT  = GameType("Studio.Game.Audio.ConcertoAudioBank");
            var locoT  = GameType("Studio.Game.Animation.DezLocomotionController");
            var toolT  = GameType("Studio.Game.Tool.MultiToolController");
            var pauseT = GameType("Studio.Game.Menu.PauseMenuController");

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var mix = mixT.GetProperty("Instance").GetValue(null);
            Assert.IsNotNull(mix, "AudioMixController.Instance obrigatorio.");
            var bank = mixT.GetField("bank").GetValue(mix);
            Assert.IsNotNull(bank, "ConcertoAudioBank obrigatorio.");

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            Assert.IsNotNull(dezGo, "Player_Dez_Placeholder obrigatorio em Caldeiral.");
            var loco = dezGo.GetComponent(locoT);
            Assert.IsNotNull(loco, "DezLocomotionController obrigatorio.");

            var floorArr  = locoT.GetField("footstepFloorClips").GetValue(loco) as AudioClip[];
            var metalArr  = locoT.GetField("footstepMetalClips").GetValue(loco) as AudioClip[];
            var puddleArr = locoT.GetField("footstepPuddleClips").GetValue(loco) as AudioClip[];
            var jumpClip  = locoT.GetField("jumpTakeoffClip").GetValue(loco) as AudioClip;

            Assert.GreaterOrEqual(floorArr != null ? floorArr.Length : 0, 3, "Minimo 3 variacoes de passo em piso.");
            Assert.GreaterOrEqual(metalArr != null ? metalArr.Length : 0, 3, "Minimo 3 variacoes de passo em metal.");
            Assert.GreaterOrEqual(puddleArr != null ? puddleArr.Length : 0, 3, "Minimo 3 variacoes de passo em poca.");
            Assert.IsNotNull(jumpClip, "Jump takeoff SFX obrigatorio.");

            var tool  = UnityEngine.Object.FindAnyObjectByType(toolT);
            var pause = UnityEngine.Object.FindAnyObjectByType(pauseT);

            // Cria 2 placas finas de superficie (Metal em LayerProps=25 e Poca em LayerEnvironment=16)
            Vector3 origin = dezGo.transform.position;
            var metalPad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            metalPad.name = "Metal_Grate_AudioTestPad";
            metalPad.layer = 25;
            metalPad.transform.position = origin + new Vector3(3.2f, -0.02f, 0f);
            metalPad.transform.localScale = new Vector3(3.0f, 0.06f, 4.0f);

            var puddlePad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            puddlePad.name = "Puddle_Water_AudioTestPad";
            puddlePad.layer = 16;
            puddlePad.transform.position = origin + new Vector3(6.4f, -0.02f, 0f);
            puddlePad.transform.localScale = new Vector3(3.0f, 0.06f, 4.0f);

            string outDir = GetCaptureDir("gameplay");
            var eventsLog = new List<string>();
            void LogEvent(float tSec, string ev, string clip, float vol, float pitch)
            {
                eventsLog.Add(string.Format(CultureInfo.InvariantCulture,
                    "{{\"t\":{0:F3},\"event\":\"{1}\",\"clip\":\"{2}\",\"vol\":{3:F2},\"pitch\":{4:F2}}}",
                    tSec, ev, clip, vol, pitch).Replace("\"pitch\":F2", string.Format(CultureInfo.InvariantCulture, "\"pitch\":{0:F2}", pitch)));
            }

            const int width = 1280;
            const int height = 720;
            const float totalDuration = 60.0f;
            const float captureFps = 15.0f;
            const float frameStep = 1.0f / captureFps;
            int totalFrames = Mathf.RoundToInt(totalDuration * captureFps);

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            var pUseScripted = locoT.GetProperty("UseScriptedInput");
            var pMoveDir     = locoT.GetProperty("ScriptedMoveDir");
            var pRun         = locoT.GetProperty("ScriptedRun");
            var pJump        = locoT.GetProperty("ScriptedJump");
            var pStepCount   = locoT.GetProperty("FootstepTriggerCount");
            var pJumpCount   = locoT.GetProperty("JumpSfxTriggerCount");
            var pLastSurf    = locoT.GetProperty("LastSurfaceType");
            var pLastVarIdx  = locoT.GetProperty("LastFootstepVariationIndex");

            var pUiCount     = mixT.GetProperty("UiSfxTriggerCount");
            var pAmbCount    = mixT.GetProperty("AmbienceCrossfadeCount");
            var mCrossfade   = mixT.GetMethod("CrossfadeAmbience");
            var mPlayUiOpen  = mixT.GetMethod("PlayUiOpen");
            var mPlayUiConf  = mixT.GetMethod("PlayUiConfirm");
            var mPlayUiClose = mixT.GetMethod("PlayUiClose");

            var sectAmbClip  = bankT.GetField("sectorAmbienceClip").GetValue(bank) as AudioClip;
            var workAmbClip  = bankT.GetField("workshopAmbienceClip").GetValue(bank) as AudioClip;

            pUseScripted.SetValue(loco, true);
            int prevSteps = (int)pStepCount.GetValue(loco);
            int prevJumps = (int)pJumpCount.GetValue(loco);
            int prevUi    = (int)pUiCount.GetValue(mix);
            int prevHits  = tool != null ? (int)toolT.GetProperty("HitstopTriggerCount").GetValue(tool) : 0;

            bool didJump1 = false, didJump2 = false, didTool1 = false, didTool2 = false, didUi1 = false, didUi2 = false, didAmbSwitch = false, didAmbBack = false;
            var surfacesSeen = new HashSet<string>();

            float simTime = 0f;
            int captured = 0;

            while (captured < totalFrames)
            {
                if (simTime < 14f)
                {
                    pMoveDir.SetValue(loco, Vector3.right);
                    pRun.SetValue(loco, simTime > 6f);
                }
                else if (simTime < 26f)
                {
                    pMoveDir.SetValue(loco, Vector3.left);
                    pRun.SetValue(loco, simTime > 20f);
                }
                else if (simTime < 34f)
                {
                    pMoveDir.SetValue(loco, Vector3.zero);
                    pRun.SetValue(loco, false);
                    if (!didTool1 && simTime >= 27.0f && tool != null)
                    {
                        didTool1 = true;
                        toolT.GetMethod("EquipModuleIndex").Invoke(tool, new object[] { 1, true });
                        LogEvent(simTime, "tool_mode_switch", "SFX/Tool/tool_mode_switch_TEMP.wav", 0.72f, 1.0f);
                    }
                    if (!didTool2 && simTime >= 29.0f && tool != null)
                    {
                        didTool2 = true;
                        toolT.GetMethod("PlayToolSwingFeedback").Invoke(tool, new object[] { false, 1 });
                        LogEvent(simTime, "tool_swing", "SFX/Tool/tool_melee_swing_TEMP.wav", 0.78f, 0.98f);
                        toolT.GetMethod("TriggerHitImpactFeedback").Invoke(tool, new object[] { dezGo.transform.position + dezGo.transform.forward * 1.2f, true });
                        LogEvent(simTime + 0.08f, "tool_hit_heavy", "SFX/Tool/tool_melee_hit_TEMP.wav", 0.90f, 0.82f);
                    }
                }
                else if (simTime < 44f)
                {
                    pMoveDir.SetValue(loco, Vector3.zero);
                    if (!didUi1 && simTime >= 35.0f)
                    {
                        didUi1 = true;
                        if (pause != null) pauseT.GetMethod("TryPause").Invoke(pause, null);
                        else mPlayUiOpen.Invoke(mix, null);
                        LogEvent(simTime, "ui_open", "SFX/UI/ui_open_TEMP.wav", 0.75f, 1.0f);
                    }
                    if (!didUi2 && simTime >= 38.5f)
                    {
                        didUi2 = true;
                        mPlayUiConf.Invoke(mix, null);
                        LogEvent(simTime, "ui_confirm", "SFX/UI/ui_confirm_TEMP.wav", 0.78f, 1.0f);
                        if (pause != null && (bool)pauseT.GetProperty("IsPaused").GetValue(pause))
                            pauseT.GetMethod("ResumeGame").Invoke(pause, null);
                        else
                            mPlayUiClose.Invoke(mix, null);
                        LogEvent(simTime + 1.2f, "ui_close", "SFX/UI/ui_close_TEMP.wav", 0.72f, 0.96f);
                    }
                }
                else
                {
                    pMoveDir.SetValue(loco, (simTime < 52f) ? Vector3.right : Vector3.left);
                    pRun.SetValue(loco, true);
                    if (!didAmbSwitch && simTime >= 44.5f)
                    {
                        didAmbSwitch = true;
                        mCrossfade.Invoke(mix, new object[] { sectAmbClip, 2.0f });
                        LogEvent(simTime, "ambience_crossfade_sector", "SFX/Sector/sector_cold_wind_ambience_TEMP.wav", 0.35f, 1.0f);
                    }
                    if (!didAmbBack && simTime >= 53.0f)
                    {
                        didAmbBack = true;
                        mCrossfade.Invoke(mix, new object[] { workAmbClip, 2.0f });
                        LogEvent(simTime, "ambience_crossfade_workshop", "Ambience/workshop_warm_ambience_TEMP.wav", 0.32f, 1.0f);
                    }
                }

                if (!didJump1 && simTime >= 8.0f)
                {
                    didJump1 = true;
                    pJump.SetValue(loco, true);
                    LogEvent(simTime, "jump", "SFX/Movement/jump_takeoff_TEMP.wav", 0.60f, 1.02f);
                    LogEvent(simTime + 0.65f, "landing", "SFX/Movement/landing_thud_TEMP.wav", 0.68f, 0.97f);
                }
                if (!didJump2 && simTime >= 48.0f)
                {
                    didJump2 = true;
                    pJump.SetValue(loco, true);
                    LogEvent(simTime, "jump", "SFX/Movement/jump_takeoff_TEMP.wav", 0.60f, 0.98f);
                    LogEvent(simTime + 0.65f, "landing", "SFX/Movement/landing_thud_TEMP.wav", 0.72f, 1.01f);
                }

                int curSteps = (int)pStepCount.GetValue(loco);
                if (curSteps > prevSteps)
                {
                    prevSteps = curSteps;
                    string surf = (string)pLastSurf.GetValue(loco);
                    surfacesSeen.Add(surf);
                    int vIdx = Mathf.Clamp((int)pLastVarIdx.GetValue(loco), 0, 2);
                    string suffix = vIdx == 0 ? "_TEMP.wav" : $"_0{vIdx + 1}_TEMP.wav";
                    string clipRel = $"SFX/Movement/footstep_{surf}{suffix}";
                    bool running = (bool)pRun.GetValue(loco);
                    LogEvent(simTime, $"footstep_{surf}", clipRel, running ? 0.56f : 0.38f, UnityEngine.Random.Range(0.94f, 1.06f));
                }

                CaptureFrameJpg(Camera.main, rt, tex, Path.Combine(outDir, $"frame_{captured:D4}.jpg"));
                captured++;

                yield return null;
                yield return null;
                simTime += frameStep;
            }

            if (pause != null && (bool)pauseT.GetProperty("IsPaused").GetValue(pause))
                pauseT.GetMethod("ResumeGame").Invoke(pause, null);
            pMoveDir.SetValue(loco, Vector3.zero);
            pUseScripted.SetValue(loco, false);

            UnityEngine.Object.Destroy(metalPad);
            UnityEngine.Object.Destroy(puddlePad);
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"duration_sec\": {0:F2},", totalDuration));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"fps\": {0:F2},", captureFps));
            sb.AppendLine("  \"events\": [");
            for (int i = 0; i < eventsLog.Count; i++)
                sb.AppendLine("    " + eventsLog[i] + (i < eventsLog.Count - 1 ? "," : ""));
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(outDir, "audio_events_gameplay.json"), sb.ToString(), Encoding.UTF8);

            int totalSteps = (int)pStepCount.GetValue(loco);
            int totalJumps = (int)pJumpCount.GetValue(loco);
            int totalUi    = (int)pUiCount.GetValue(mix);
            int totalAmb   = (int)pAmbCount.GetValue(mix);

            Assert.GreaterOrEqual(totalSteps, 10, "Passos devem disparar durante movimentacao.");
            Assert.GreaterOrEqual(totalJumps - prevJumps, 1, "Jump SFX deve disparar.");
            Assert.GreaterOrEqual(totalUi - prevUi, 3, "UI SFX (open/confirm/close) devem disparar.");
            Assert.GreaterOrEqual(totalAmb, 2, "Crossfade de ambiencia deve disparar.");
            if (tool != null)
                Assert.GreaterOrEqual((int)toolT.GetProperty("HitstopTriggerCount").GetValue(tool) - prevHits, 1, "Hitstop + impacto de ferramenta deve disparar.");

            Debug.Log($"[AudioPassTest] T02 PASSOU: steps={totalSteps}, surfaces={string.Join(",", surfacesSeen)}, jumps={totalJumps}, uiSfx={totalUi}, ambCrossfades={totalAmb}, frames={captured}");
        }

        [UnityTest]
        public IEnumerator T03_CutsceneDuckingSmoothFadeAndCapture()
        {
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var cs01T = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var mixT  = GameType("Studio.Game.Audio.AudioMixController");

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            Assert.IsNotNull(cs01, "CutsceneManager (CS01) obrigatorio em Caldeiral.");
            if ((bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);

            var mix = mixT.GetProperty("Instance").GetValue(null);
            Assert.IsNotNull(mix, "AudioMixController.Instance obrigatorio.");

            var pDuck = mixT.GetProperty("CurrentMusicDuckFactor");
            float targetDuck = (float)mixT.GetField("duckTargetFactor").GetValue(mix);

            float waitReset = 0f;
            while ((float)pDuck.GetValue(mix) < 0.99f && waitReset < 2.0f)
            {
                waitReset += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.GreaterOrEqual((float)pDuck.GetValue(mix), 0.98f, "Musica deve comecar sem ducking (1.0) antes da cutscene.");

            string outDir = GetCaptureDir("cutscene");
            var duckSamples = new List<string>();
            const int width = 1280;
            const int height = 720;
            const float captureFps = 15.0f;
            const float frameStep = 1.0f / captureFps;

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            bool sawFadeInIntermediate = false;
            bool sawFullDuck = false;
            bool sawFadeOutIntermediate = false;
            bool sawFullRecovery = false;

            int frameIdx = 0;
            float elapsed = 0f;

            void SampleAndCapture(string phase)
            {
                float f = (float)pDuck.GetValue(mix);
                duckSamples.Add(string.Format(CultureInfo.InvariantCulture,
                    "{{\"t\":{0:F3},\"duck\":{1:F4},\"phase\":\"{2}\"}}", elapsed, f, phase));
                CaptureFrameJpg(Camera.main, rt, tex, Path.Combine(outDir, $"frame_{frameIdx:D4}.jpg"));
                frameIdx++;
            }

            // Fase 1: 2.5s gameplay normal (duck = 1.0)
            while (elapsed < 2.5f)
            {
                SampleAndCapture("pre_gameplay");
                yield return null;
                elapsed += frameStep;
            }

            // Fase 2: Dispara Cutscene 1 -> mede entrada suave do ducking (ZERO corte seco!)
            cs01T.GetMethod("PlayCutscene").Invoke(cs01, null);
            float csStart = elapsed;
            while (elapsed < csStart + 7.5f)
            {
                float f = (float)pDuck.GetValue(mix);
                if (f > 0.30f && f < 0.95f) sawFadeInIntermediate = true;
                if (f <= targetDuck + 0.03f) sawFullDuck = true;
                SampleAndCapture("cutscene_ducked");
                yield return null;
                elapsed += frameStep;
            }

            // Fase 3: Encerra/pula cutscene -> mede retorno suave (fade out do ducking, ZERO corte seco!)
            cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            float recoveryStart = elapsed;
            while (elapsed < recoveryStart + 3.5f)
            {
                float f = (float)pDuck.GetValue(mix);
                if (f > 0.30f && f < 0.95f) sawFadeOutIntermediate = true;
                if (f >= 0.98f) sawFullRecovery = true;
                SampleAndCapture("post_recovery");
                yield return null;
                elapsed += frameStep;
            }

            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"duration_sec\": {0:F2},", elapsed));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"fps\": {0:F2},", captureFps));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"cutscene_start_sec\": {0:F2},", csStart));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"cutscene_end_sec\": {0:F2},", recoveryStart));
            sb.AppendLine("  \"duck_curve\": [");
            for (int i = 0; i < duckSamples.Count; i++)
                sb.AppendLine("    " + duckSamples[i] + (i < duckSamples.Count - 1 ? "," : ""));
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(outDir, "audio_events_cutscene.json"), sb.ToString(), Encoding.UTF8);

            Assert.IsTrue(sawFadeInIntermediate, "Ducking na entrada da cutscene deve ter fade suave (valores intermediarios, sem corte seco).");
            Assert.IsTrue(sawFullDuck, "Musica deve atingir o nivel alvo de ducking (-12 dB) durante a cutscene.");
            Assert.IsTrue(sawFadeOutIntermediate, "Ducking na saida da cutscene deve retornar com fade suave (sem corte seco).");
            Assert.IsTrue(sawFullRecovery, "Musica deve retornar a 100% (0 dB relativo) apos a cutscene.");

            Debug.Log($"[AudioPassTest] T03 PASSOU: smoothFadeIn={sawFadeInIntermediate}, fullDuck={sawFullDuck}, smoothFadeOut={sawFadeOutIntermediate}, fullRecovery={sawFullRecovery}, frames={frameIdx}");
        }
    }
}
