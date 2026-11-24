using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    public sealed class DezPowersValidationAndCaptureTest
    {
        private static Type GameType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo nao encontrado em Assembly-CSharp: {fullName}");
            return t;
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
        public IEnumerator ValidateEcoTemporalEstasePlaygroundAndCapture()
        {
            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.SetInt("CONCERTO_CS03_PLAYED", 1);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var cs01T   = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var locoT   = GameType("Studio.Game.Animation.DezLocomotionController");
            var powersT = GameType("Studio.Game.Powers.DezPowersController");
            var stasisT = GameType("Studio.Game.Powers.StasisFieldSystem");
            var objT    = GameType("Studio.Game.Powers.StasisAffectedObject");

            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var dezGo = GameObject.Find("Player_Dez_Placeholder");
            Assert.IsNotNull(dezGo, "Player_Dez_Placeholder obrigatorio.");
            var loco = dezGo.GetComponent(locoT);
            var powers = dezGo.GetComponent(powersT);
            Assert.IsNotNull(powers, "DezPowersController deve estar ligado ao Dez.");

            // Garante desbloqueio pos-CS03
            powersT.GetMethod("UnlockPowers").Invoke(powers, new object[] { true });
            Assert.IsTrue((bool)powersT.GetProperty("IsUnlocked").GetValue(powers), "Poderes devem estar desbloqueados pos-CS03.");

            // Cria 2 objetos de playground afetados pela Estase (pendulo + rotor ciano-dourado) na frente do Dez
            Vector3 dezPos = dezGo.transform.position;
            var rotor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rotor.name = "Playground_StasisRotor";
            rotor.transform.position = dezPos + new Vector3(2.8f, 1.3f, 0.8f);
            rotor.transform.localScale = new Vector3(0.45f, 1.4f, 0.45f);
            var rotorComp = rotor.AddComponent(objT);

            var pendulum = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pendulum.name = "Playground_StasisPendulum";
            pendulum.transform.position = dezPos + new Vector3(2.8f, 1.6f, -1.0f);
            pendulum.transform.localScale = Vector3.one * 0.65f;
            pendulum.AddComponent(objT);

            // Teste 1: Dano fora do Eco Temporal e recebido normalmente
            bool dmgOutside = (bool)powersT.GetMethod("TryReceiveTestDamage").Invoke(powers, new object[] { 20f });
            Assert.IsTrue(dmgOutside, "Dano fora de i-frames deve ser recebido.");

            // Configura captura do playground para MP4 1080p
            string outDir = Path.Combine(Application.dataPath, "..", "Logs", "powers_capture");
            Directory.CreateDirectory(outDir);
            var eventsLog = new List<string>();
            void LogEv(float t, string ev, string clip)
            {
                eventsLog.Add(string.Format(CultureInfo.InvariantCulture,
                    "{{\"t\":{0:F3},\"event\":\"{1}\",\"clip\":\"{2}\"}}", t, ev, clip));
            }

            const int width = 1280, height = 720;
            const float fps = 15f;
            const float dtStep = 1f / fps;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            var pUseScripted = locoT.GetProperty("UseScriptedInput");
            var pMoveDir     = locoT.GetProperty("ScriptedMoveDir");
            var pRun         = locoT.GetProperty("ScriptedRun");
            pUseScripted.SetValue(loco, true);

            int frame = 0;
            float simT = 0f;

            void Cap()
            {
                CaptureFrameJpg(Camera.main, rt, tex, Path.Combine(outDir, $"frame_{frame:D4}.jpg"));
                frame++;
            }

            // Fase A (0..2.0s): Movimento normal + objetos do mundo girando a 1.0x
            pMoveDir.SetValue(loco, Vector3.right);
            pRun.SetValue(loco, false);
            for (int i = 0; i < 30; i++)
            {
                Cap();
                yield return null;
                simT += dtStep;
            }

            // Fase B (2.0..3.5s): Dispara ECO TEMPORAL -> verifica i-frames reais + afterimages cianos!
            bool ecoOk = (bool)powersT.GetMethod("TriggerEcoTemporal").Invoke(powers, null);
            Assert.IsTrue(ecoOk, "Eco Temporal deve ativar quando pronto.");
            LogEv(simT, "eco_temporal", "SFX/Powers/sfx_eco_temporal_reverse_TEMP.wav");

            bool isInvuln = (bool)powersT.GetProperty("IsInvulnerable").GetValue(powers);
            Assert.IsTrue(isInvuln, "Eco Temporal deve conceder i-frames imediatos (~0.30s).");
            bool dmgDuringEco = (bool)powersT.GetMethod("TryReceiveTestDamage").Invoke(powers, new object[] { 25f });
            Assert.IsFalse(dmgDuringEco, "Dano de teste deve ser BLOQUEADO durante i-frames do Eco Temporal!");

            for (int i = 0; i < 24; i++)
            {
                Cap();
                yield return null;
                simT += dtStep;
            }

            int afterimages = (int)powersT.GetProperty("AfterimageSpawnCount").GetValue(powers);
            Assert.GreaterOrEqual(afterimages, 2, "Eco Temporal deve gerar afterimages cianos.");

            // Fase C (3.5..7.5s): Dispara ESTASE -> mundo desacelera 50% (0.50f), Dez corre a 100% (1.00f)!
            pMoveDir.SetValue(loco, Vector3.left);
            pRun.SetValue(loco, true);
            bool stasisOk = (bool)powersT.GetMethod("TriggerEstase").Invoke(powers, null);
            Assert.IsTrue(stasisOk, "Estase deve ativar quando pronta.");
            LogEv(simT, "estase_pulse", "SFX/Powers/sfx_estase_pulse_TEMP.wav");

            yield return null;
            float worldScaleDuring = (float)stasisT.GetProperty("WorldTimeScale").GetValue(null);
            float rotorScaleDuring = (float)objT.GetProperty("LocalTimeScale").GetValue(rotorComp);
            float dezScaleDuring   = (float)powersT.GetProperty("DezTimeScale").GetValue(powers);

            Assert.AreEqual(0.50f, worldScaleDuring, 0.02f, "Estase deve reduzir WorldTimeScale para ~50% (0.50).");
            Assert.AreEqual(0.50f, rotorScaleDuring, 0.02f, "Objeto no campo de Estase deve rodar a 50%.");
            Assert.AreEqual(1.00f, dezScaleDuring,   0.001f, "Estase NUNCA pode desacelerar o Dez!");

            Vector3 posStartStasis = dezGo.transform.position;
            float realWait = 0f;
            while ((bool)stasisT.GetProperty("IsAnyStasisActive").GetValue(null) && realWait < 5.0f)
            {
                Cap();
                yield return null;
                realWait += Time.unscaledDeltaTime;
                simT += dtStep;
            }
            float dezDistDuringStasis = Vector3.Distance(posStartStasis, dezGo.transform.position);
            Assert.Greater(dezDistDuringStasis, 1.5f, "Dez deve se mover livremente em velocidade normal durante Estase.");

            // Gate 1 critico: WorldTimeScale volta EXATO a 1.000f apos Estase!
            float worldScaleAfter = (float)stasisT.GetProperty("WorldTimeScale").GetValue(null);
            float rotorScaleAfter = (float)objT.GetProperty("LocalTimeScale").GetValue(rotorComp);
            Assert.AreEqual(1.0f, worldScaleAfter, 0.0001f, "WorldTimeScale DEVE voltar EXATO para 1.000 apos Estase!");
            Assert.AreEqual(1.0f, rotorScaleAfter, 0.0001f, "Objeto afetado pela Estase DEVE voltar EXATO para 1.000!");

            // Fase D (pos-estase): 1.5s de movimento normal com mundo restaurado
            pMoveDir.SetValue(loco, Vector3.right);
            for (int i = 0; i < 20; i++)
            {
                Cap();
                yield return null;
                simT += dtStep;
            }

            // Gate 3: Regressao zero — Cutscene pausa poderes!
            if (cs01 != null)
            {
                cs01T.GetMethod("PlayCutscene").Invoke(cs01, null);
                yield return null;
                bool canDuringCs = (bool)powersT.GetMethod("CanUsePowersNow").Invoke(powers, null);
                Assert.IsFalse(canDuringCs, "Poderes devem ser bloqueados durante cutscene.");
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
                yield return null;
            }

            pMoveDir.SetValue(loco, Vector3.zero);
            pUseScripted.SetValue(loco, false);
            UnityEngine.Object.Destroy(rotor);
            UnityEngine.Object.Destroy(pendulum);
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"duration_sec\": {0:F2},", frame * dtStep));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  \"fps\": {0:F2},", fps));
            sb.AppendLine("  \"events\": [");
            for (int i = 0; i < eventsLog.Count; i++)
                sb.AppendLine("    " + eventsLog[i] + (i < eventsLog.Count - 1 ? "," : ""));
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(outDir, "powers_events.json"), sb.ToString(), new UTF8Encoding(false));

            Debug.Log($"[PowersTest #93] PASSOU: afterimages={afterimages}, iFramesBlocked=1, worldDuring={worldScaleDuring:F2}, worldAfter={worldScaleAfter:F3}, dezDist={dezDistDuringStasis:F2}m, frames={frame}");
        }
    }
}
