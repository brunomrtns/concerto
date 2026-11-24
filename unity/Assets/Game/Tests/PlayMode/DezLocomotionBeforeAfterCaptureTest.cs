using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// Captura 30s padronizados da locomocao do Dez (ANTES ou DEPOIS via CONCERTO_LOCO_CAPTURE_PHASE)
    /// na mesma rota e angulo em Caldeiral: andar -> correr -> parar brusco -> pular/cair/aterrissar -> interagir.
    /// Tambem mede latencia real input -> acao (ms) para o relatorio do GATE 7/9.
    /// </summary>
    public sealed class DezLocomotionBeforeAfterCaptureTest
    {
        private static Type GameType(string fullName)
        {
            var t = Type.GetType(fullName + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo nao encontrado: {fullName}");
            return t;
        }

        private static void CaptureFrame(Camera cam, RenderTexture rt, Texture2D tex, string path)
        {
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            tex.Apply(false, false);
            cam.targetTexture = prevTarget;
            RenderTexture.active = prevActive;
            File.WriteAllBytes(path, tex.EncodeToJPG(88));
        }

        [UnityTest]
        public IEnumerator Capture30sLocomotionShowcaseAndMeasureInputLatency()
        {
            string phase = Environment.GetEnvironmentVariable("CONCERTO_LOCO_CAPTURE_PHASE");
            if (string.IsNullOrEmpty(phase)) phase = "before";

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            yield return SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var cs01T = GameType("Studio.Game.Cutscenes.CutsceneManager");
            var cs01 = UnityEngine.Object.FindAnyObjectByType(cs01T);
            if (cs01 != null && (bool)cs01T.GetProperty("IsPlaying").GetValue(cs01))
                cs01T.GetMethod("SkipCutscene").Invoke(cs01, null);
            yield return null;

            var locoT = GameType("Studio.Game.Animation.DezLocomotionController");
            var loco = UnityEngine.Object.FindAnyObjectByType(locoT) as Component;
            Assert.IsNotNull(loco, "DezLocomotionController obrigatorio.");

            var cc = loco.GetComponent<CharacterController>();
            Vector3 startSpot = new Vector3(-25.5f, 0.08f, -15.5f);
            if (cc != null) cc.enabled = false;
            loco.transform.position = startSpot;
            loco.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();

            // Camera de acompanhamento 3/4 lateral limpa (exato mesmo angulo no ANTES e DEPOIS!)
            var camGo = new GameObject("LocomotionCompareCam");
            var capCam = camGo.AddComponent<Camera>();
            capCam.fieldOfView = 42f;
            capCam.nearClipPlane = 0.1f;
            capCam.farClipPlane = 120f;

            string outDir = Path.Combine(Application.dataPath, "..", "Logs", "locomocao_capture", phase);
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            const int width = 960, height = 1080; // metade exata de 1920x1080 lado-a-lado!
            const int fps = 15;
            const int totalFrames = 450; // 30.0 segundos exatos
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            var pUseScripted = locoT.GetProperty("UseScriptedInput");
            var pMoveDir     = locoT.GetProperty("ScriptedMoveDir");
            var pRun         = locoT.GetProperty("ScriptedRun");
            var pJump        = locoT.GetProperty("ScriptedJump");
            var pJumpHeld    = locoT.GetProperty("ScriptedJumpHeld");

            // Procura opcionalmente o componente de IK (existe no DEPOIS, nao no ANTES)
            var ikT = Type.GetType("Studio.Game.Animation.DezAnimationRiggingController, Assembly-CSharp");
            Component ikComp = ikT != null ? loco.GetComponentInChildren(ikT) : null;
            var mSetInteractIk = ikT != null ? ikT.GetMethod("SetInteractTarget") : null;
            var mClearInteractIk = ikT != null ? ikT.GetMethod("ClearInteractTarget") : null;

            pUseScripted.SetValue(loco, true);
            pJumpHeld.SetValue(loco, true);

            // Mede latencia real input -> acao (ms) no inicio da caminhada
            double inputStartTime = Time.realtimeSinceStartupAsDouble;
            double inputResponseMs = -1.0;
            Vector3 posAtInput = loco.transform.position;

            // Alvo de interacao real na oficina (bancada / ponto a frente do Dez)
            Vector3 interactTargetWorld = new Vector3(-24.2f, 1.05f, -18.2f);
            var benchGo = GameObject.Find("Bench_Workshop") ?? GameObject.Find("c5_prop_bancada_relogio");
            if (benchGo != null)
                interactTargetWorld = benchGo.transform.position + Vector3.up * 0.95f;

            for (int f = 0; f < totalFrames; f++)
            {
                float t = f / (float)fps;

                if (t < 5.0f)
                {
                    // 0..5s: ANDAR (Walk)
                    pMoveDir.SetValue(loco, Vector3.right);
                    pRun.SetValue(loco, false);
                    pJump.SetValue(loco, false);
                }
                else if (t < 11.0f)
                {
                    // 5..11s: CORRER (Run) ida e volta na passarela da oficina
                    Vector3 dir = t < 8.0f ? Vector3.right : Vector3.left;
                    pMoveDir.SetValue(loco, dir);
                    pRun.SetValue(loco, true);
                    pJump.SetValue(loco, false);
                }
                else if (t < 14.0f)
                {
                    // 11..14s: PARAR BRUSCO -> IDLE
                    pMoveDir.SetValue(loco, Vector3.zero);
                    pRun.SetValue(loco, false);
                    pJump.SetValue(loco, false);
                }
                else if (t < 20.0f)
                {
                    // 14..20s: PULAR + CAIR + ATERRISSAR (2 pulos: parado e em movimento)
                    bool doJump1 = (f == 215 || f == 216);
                    bool doJump2 = (f == 258 || f == 259);
                    pMoveDir.SetValue(loco, t >= 16.5f ? Vector3.right : Vector3.zero);
                    pRun.SetValue(loco, t >= 16.5f && t < 18.8f);
                    pJump.SetValue(loco, doJump1 || doJump2);
                }
                else if (t < 24.0f)
                {
                    // 20..24s: APROXIMAR DE OBJETO DA FASE 1 (ANDANDO)
                    Vector3 toObj = interactTargetWorld - loco.transform.position;
                    toObj.y = 0f;
                    pMoveDir.SetValue(loco, toObj.magnitude > 0.95f ? toObj.normalized : Vector3.zero);
                    pRun.SetValue(loco, false);
                    pJump.SetValue(loco, false);
                }
                else
                {
                    // 24..30s: INTERAGIR COM OBJETO (Hand IK ativo no DEPOIS!)
                    pMoveDir.SetValue(loco, Vector3.zero);
                    pRun.SetValue(loco, false);
                    pJump.SetValue(loco, false);
                    if (ikComp != null && mSetInteractIk != null)
                    {
                        if (t < 28.5f)
                            mSetInteractIk.Invoke(ikComp, new object[] { loco.transform.position + loco.transform.forward * 0.62f + Vector3.up * 1.05f });
                        else if (mClearInteractIk != null)
                            mClearInteractIk.Invoke(ikComp, null);
                    }
                }

                yield return null;

                if (inputResponseMs < 0.0 && Vector3.Distance(loco.transform.position, posAtInput) > 0.005f)
                {
                    inputResponseMs = (Time.realtimeSinceStartupAsDouble - inputStartTime) * 1000.0;
                }

                // Atualiza camera 3/4 lateral acompanhando o Dez (com anti-oclusao contra paredes/pilares!)
                Vector3 dezPos = loco.transform.position;
                Vector3 lookAtPt = dezPos + new Vector3(0f, 0.92f, 0f);
                Vector3 desiredCam = dezPos + new Vector3(-0.35f, 1.38f, 2.75f);
                if (Physics.Linecast(lookAtPt, desiredCam, out RaycastHit camHit, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (camHit.transform.root != loco.transform.root)
                        desiredCam = camHit.point + (lookAtPt - desiredCam).normalized * 0.25f;
                }
                camGo.transform.position = desiredCam;
                camGo.transform.LookAt(lookAtPt);

                CaptureFrame(capCam, rt, tex, Path.Combine(outDir, $"frame_{f:D4}.jpg"));
            }

            if (ikComp != null && mClearInteractIk != null)
                mClearInteractIk.Invoke(ikComp, null);

            pMoveDir.SetValue(loco, Vector3.zero);
            pUseScripted.SetValue(loco, false);

            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(camGo);

            string metricsJson = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"phase\":\"{0}\",\"frames\":{1},\"fps\":{2},\"duration_sec\":{3:F1},\"input_to_action_ms\":{4:F2} }}",
                phase, totalFrames, fps, totalFrames / (float)fps, inputResponseMs);
            File.WriteAllText(Path.Combine(outDir, "metrics.json"), metricsJson, new UTF8Encoding(false));
            Debug.Log($"[LocomotionCapture:{phase}] CONCLUIDO: {metricsJson}");
        }
    }
}
