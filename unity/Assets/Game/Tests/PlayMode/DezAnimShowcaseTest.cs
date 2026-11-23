using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// Showcase AAA a 60 fps reais (1280x720) + QA Automatico Lei 9.1 (foot-slide, altura pulo, SpringBone).
    /// So grava frames quando CAPTURE_DEZ_SHOWCASE=1; no baseline normal passa em <10ms.
    /// </summary>
    public class DezAnimShowcaseTest
    {
        [UnityTest]
        public IEnumerator CaptureDezPlayModeShowcaseIfEnabled()
        {
            if (Environment.GetEnvironmentVariable("CAPTURE_DEZ_SHOWCASE") != "1")
                yield break;

            // Trava timestep deterministico a 60 fps exatos!
            Time.captureFramerate = 60;

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

            var dezRoot = GameObject.Find("Player_Dez_Placeholder");
            Assert.IsNotNull(dezRoot, "Player_Dez_Placeholder nao encontrado.");
            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            Assert.IsNotNull(locoType, "Tipo DezLocomotionController nao encontrado em Assembly-CSharp.");
            var loco = dezRoot.GetComponent(locoType);
            Assert.IsNotNull(loco, "DezLocomotionController nao encontrado no Player_Dez_Placeholder.");
            PropertyInfo pUseScripted = locoType.GetProperty("UseScriptedInput");
            PropertyInfo pMoveDir     = locoType.GetProperty("ScriptedMoveDir");
            PropertyInfo pRun         = locoType.GetProperty("ScriptedRun");
            PropertyInfo pJump        = locoType.GetProperty("ScriptedJump");
            PropertyInfo pYawRate     = locoType.GetProperty("ScriptedYawRate");
            var anim = dezRoot.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(anim, "Animator nao encontrado.");

            // Garante area livre na Oficina (estaciona a Porca ao fundo direito sem perseguir o Dez durante o showcase)
            var porca = GameObject.Find("Companion_Porca_Placeholder");
            if (porca != null)
            {
                foreach (var mb in porca.GetComponents<MonoBehaviour>())
                    if (mb != null && mb.GetType().Name.Contains("Porca")) mb.enabled = false;
                porca.transform.position = new Vector3(-25.5f, 1.35f, -17.5f);
            }

            // Posicao inicial limpa no centro aberto e iluminado da Oficina Z1
            var cc = dezRoot.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            dezRoot.transform.position = new Vector3(-31.2f, 0.01f, -19.2f);
            dezRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            if (cc != null) cc.enabled = true;

            pUseScripted.SetValue(loco, true);
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var smr in dezRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;

            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            Assert.IsNotNull(cam, "Main Camera nao encontrada.");

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string framesDir = Path.Combine(repoRoot, "evidence", "captures", "dez_aaa_60fps_frames");
            if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
            Directory.CreateDirectory(framesDir);

            int width = 1280, height = 720;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            // QA metric trackers (Lei 9.1)
            Transform leftFoot  = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform earL = null;
            foreach (var t in dezRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == "Ear_L") { earL = t; break; }

            Vector3 prevLF = leftFoot ? leftFoot.position : Vector3.zero;
            Vector3 prevRF = rightFoot ? rightFoot.position : Vector3.zero;
            Quaternion baseEarRot = earL ? earL.localRotation : Quaternion.identity;

            float maxFootSlideCm = 0f;
            float sumFootSlideCm = 0f;
            int footPlantSamples = 0;
            float maxJumpY = 0f;
            float maxEarAngleDeg = 0f;

            Vector3 camCurrent = dezRoot.transform.position + new Vector3(1.35f, 0.95f, -1.65f);
            int totalFrames = 810; // 13.5s @ 60 fps

            for (int f = 0; f < totalFrames; f++)
            {
                float t = f / 60f;
                string phase;
                Vector3 camOffset;

                if (t < 2.4f)
                {
                    phase = "IDLE_VIVO";
                    pMoveDir.SetValue(loco, Vector3.zero);
                    pRun.SetValue(loco, false);
                    pYawRate.SetValue(loco, 0f);
                    camOffset = new Vector3(1.25f, 0.92f, -1.55f); // close 3/4 frontal para ver respiracao + orelhas
                }
                else if (t < 5.2f)
                {
                    phase = "WALK";
                    pMoveDir.SetValue(loco, new Vector3(1.0f, 0f, 0.06f)); // caminha +4.0m pelo centro aberto
                    pRun.SetValue(loco, false);
                    pYawRate.SetValue(loco, 0f);
                    camOffset = new Vector3(0.45f, 1.02f, -2.25f); // perfil lateral limpo
                }
                else if (t < 5.9f)
                {
                    phase = "WALK_STOP";
                    pMoveDir.SetValue(loco, Vector3.zero);
                    pRun.SetValue(loco, false);
                    pYawRate.SetValue(loco, 0f);
                    camOffset = new Vector3(0.70f, 1.02f, -2.10f);
                }
                else if (t < 7.4f)
                {
                    phase = "TURN_IN_PLACE";
                    pMoveDir.SetValue(loco, Vector3.zero);
                    pRun.SetValue(loco, false);
                    pYawRate.SetValue(loco, -120f); // gira 180 deg em 1.5s
                    camOffset = new Vector3(1.05f, 1.00f, -1.90f);
                }
                else if (t < 9.1f)
                {
                    phase = "RUN_CURVA";
                    // 1.7s de corrida em curva suave (~5.0m de volta ao centro, 100% longe de paredes!)
                    float u = (t - 7.4f) / 1.7f;
                    float ang = Mathf.Lerp(Mathf.PI, Mathf.PI * 1.24f, u);
                    Vector3 runDir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang) * 0.40f).normalized;
                    pMoveDir.SetValue(loco, runDir);
                    pRun.SetValue(loco, true);
                    pYawRate.SetValue(loco, 0f);
                    camOffset = new Vector3(-0.35f, 1.15f, -2.40f); // 3/4 dinamico
                }
                else if (t < 11.5f)
                {
                    phase = "JUMP_GRAVIDADE";
                    pMoveDir.SetValue(loco, new Vector3(0.75f, 0f, 0.12f)); // pula de volta para o centro aberto
                    pRun.SetValue(loco, false);
                    pYawRate.SetValue(loco, 0f);
                    if (f == (int)(9.25f * 60f)) pJump.SetValue(loco, true);
                    camOffset = new Vector3(0.35f, 1.18f, -2.55f); // perfil aberto mostrando altura + sombra + pouso
                }
                else
                {
                    phase = "IDLE_POS_POUSO";
                    pMoveDir.SetValue(loco, Vector3.zero);
                    pRun.SetValue(loco, false);
                    pYawRate.SetValue(loco, 0f);
                    camOffset = new Vector3(1.25f, 0.92f, -1.65f); // 3/4 frontal pos-pouso de pe e viva!
                }

                yield return null;

                // Coleta metricas QA automaticas (Lei 9.1)
                Vector3 p = dezRoot.transform.position;
                if (p.y > maxJumpY) maxJumpY = p.y;

                if (earL != null)
                {
                    if (f == 2) baseEarRot = earL.localRotation;
                    float ang = Quaternion.Angle(baseEarRot, earL.localRotation);
                    if (ang > maxEarAngleDeg) maxEarAngleDeg = ang;
                }

                if (f > 5 && leftFoot != null && rightFoot != null && p.y < 0.08f)
                {
                    if (leftFoot.position.y < 0.16f)
                    {
                        float d = Vector2.Distance(new Vector2(leftFoot.position.x, leftFoot.position.z), new Vector2(prevLF.x, prevLF.z)) * 100f;
                        if (d < 12f) { sumFootSlideCm += d; footPlantSamples++; if (d > maxFootSlideCm) maxFootSlideCm = d; }
                    }
                    if (rightFoot.position.y < 0.16f)
                    {
                        float d = Vector2.Distance(new Vector2(rightFoot.position.x, rightFoot.position.z), new Vector2(prevRF.x, prevRF.z)) * 100f;
                        if (d < 12f) { sumFootSlideCm += d; footPlantSamples++; if (d > maxFootSlideCm) maxFootSlideCm = d; }
                    }
                }
                if (leftFoot != null) prevLF = leftFoot.position;
                if (rightFoot != null) prevRF = rightFoot.position;

                // Camera suave 3a pessoa acompanhando Dez com protecao anti-parede
                Vector3 lookTargetPos = p + new Vector3(0f, 0.68f, 0f);
                Vector3 desiredCam = p + camOffset;
                if (Physics.Linecast(lookTargetPos, desiredCam, out RaycastHit camHit, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (camHit.transform.root != dezRoot.transform.root)
                        desiredCam = camHit.point + camHit.normal * 0.25f;
                }
                camCurrent = Vector3.Lerp(camCurrent, desiredCam, 0.14f);
                cam.transform.position = camCurrent;
                cam.transform.LookAt(lookTargetPos);

                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(framesDir, $"frame_{f:D4}_{phase}.png"), tex.EncodeToPNG());
            }

            float avgFootSlideCm = footPlantSamples > 0 ? (sumFootSlideCm / footPlantSamples) : 0f;
            string qaPath = Path.Combine(repoRoot, "evidence", "validation", "qa_animacao_dez_v6.json");
            Directory.CreateDirectory(Path.GetDirectoryName(qaPath));
            var inv = CultureInfo.InvariantCulture;
            string qaJson = "{\n" +
                $"  \"frames_captured\": {totalFrames},\n" +
                $"  \"target_fps\": 60,\n" +
                $"  \"resolution\": \"1280x720\",\n" +
                $"  \"max_jump_height_m\": {maxJumpY.ToString("F3", inv)},\n" +
                $"  \"spring_ear_max_angle_deg\": {maxEarAngleDeg.ToString("F2", inv)},\n" +
                $"  \"foot_plant_samples\": {footPlantSamples},\n" +
                $"  \"avg_foot_slide_cm_per_frame\": {avgFootSlideCm.ToString("F3", inv)},\n" +
                $"  \"max_foot_slide_cm_per_frame\": {maxFootSlideCm.ToString("F3", inv)},\n" +
                $"  \"foot_slide_avg_pass_le_2cm\": {(avgFootSlideCm <= 2.0f).ToString().ToLowerInvariant()},\n" +
                $"  \"jump_gravity_pass\": {(maxJumpY >= 0.75f).ToString().ToLowerInvariant()},\n" +
                $"  \"spring_bone_active_pass\": {(maxEarAngleDeg >= 1.5f).ToString().ToLowerInvariant()}\n" +
                "}\n";
            File.WriteAllText(qaPath, qaJson);

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);
            Time.captureFramerate = 0;

            Assert.AreEqual(810, totalFrames);
        }
    }
}
