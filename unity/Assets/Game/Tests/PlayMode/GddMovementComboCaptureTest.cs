using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// GATE 7 (Movimentacao GDD): Captura o combo longo completo na Zona de Treino da oficina:
    ///   Corrimao (wall-run) -> Sopro (dash cancel mantendo momentum) -> Rebote (pulo duplo +3.5m) -> Martelada Aerea (0.6s -> mergulho + choque 3m).
    /// </summary>
    public sealed class GddMovementComboCaptureTest
    {
        [UnityTest]
        public IEnumerator MovementGdd_CaptureLongComboFrames()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "captures", "movimentacao", "_tmp_frames"));
            if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);

            PlayerPrefs.SetInt("CONCERTO_CS01_PLAYED", 1);
            PlayerPrefs.Save();

            var op = SceneManager.LoadSceneAsync("Caldeiral", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            yield return null;

            var locoT = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var zoneT = Type.GetType("Studio.Game.Animation.GddMovementTrainingZone, Assembly-CSharp");
            var loco = UnityEngine.Object.FindAnyObjectByType(locoT) as Component;
            var zone = zoneT.GetProperty("Instance").GetValue(null) as Component;
            var cc = loco.GetComponent<CharacterController>();
            Assert.IsNotNull(loco);
            Assert.IsNotNull(zone);

            locoT.GetProperty("UseScriptedInput").SetValue(loco, true);
            locoT.GetProperty("DevUnlockAllGddAbilities").SetValue(loco, true);
            locoT.GetProperty("ScriptedRun").SetValue(loco, true);

            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var camGo = new GameObject("[GddCombo_Cam]");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 58f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.targetTexture = rt;

            int frameIdx = 0;
            void Snap()
            {
                Vector3 target = loco.transform.position + Vector3.up * 1.1f;
                cam.transform.position = target + new Vector3(-2.5f, 2.8f, -7.8f);
                cam.transform.LookAt(target);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, $"frame_{frameIdx:D4}.png"), tex.EncodeToPNG());
                frameIdx++;
            }

            try
            {
                // Posiciona Dez no inicio do muro de wall-run da zona de treino
                Vector3 zp = zone.transform.position;
                cc.enabled = false;
                loco.transform.position = zp + new Vector3(-4.2f, 1.8f, 2.15f);
                cc.enabled = true;
                locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.right);

                // Fase 1: Corrimao (wall-run 0.65s)
                float t = 0f;
                while (t < 0.65f) { Snap(); yield return null; t += Time.deltaTime; }

                // Fase 2: Sopro (dash cancel mantendo momentum)
                locoT.GetProperty("ScriptedDash").SetValue(loco, true);
                t = 0f;
                while (t < 0.28f) { Snap(); yield return null; t += Time.deltaTime; }

                // Fase 3: Rebote (pulo duplo +3.5m no ar)
                locoT.GetProperty("ScriptedJump").SetValue(loco, true);
                t = 0f;
                while (t < 0.45f) { Snap(); yield return null; t += Time.deltaTime; }

                // Fase 4: Martelada Aerea (segura ataque 0.6s -> mergulho + choque 3m)
                locoT.GetProperty("ScriptedAttackHeld").SetValue(loco, true);
                t = 0f;
                while (t < 1.35f) { Snap(); yield return null; t += Time.deltaTime; }
                locoT.GetProperty("ScriptedAttackHeld").SetValue(loco, false);

                for (int i = 0; i < 8; i++) { Snap(); yield return null; }
                Assert.GreaterOrEqual(frameIdx, 40);
            }
            finally
            {
                locoT.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);
                locoT.GetProperty("ScriptedRun").SetValue(loco, false);
                locoT.GetProperty("ScriptedAttackHeld").SetValue(loco, false);
                locoT.GetProperty("UseScriptedInput").SetValue(loco, false);
                locoT.GetProperty("DevUnlockAllGddAbilities").SetValue(loco, false);
                UnityEngine.Object.Destroy(camGo);
                rt.Release();
                UnityEngine.Object.Destroy(rt);
                UnityEngine.Object.Destroy(tex);
            }
        }
    }
}
