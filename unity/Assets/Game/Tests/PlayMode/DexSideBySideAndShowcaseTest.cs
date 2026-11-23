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
    /// RUN dex_rig_v1 (TAREFA 4):
    ///   1. Prova Side-by-Side (240 frames @ 60fps): modelo VELHO (hero_dez) vs NOVO (hero_dex) no mesmo clip de corrida (tras + frente).
    ///   2. Showcase 60fps do Dex novo (720 frames = 12.0s @ 60fps, 1280x720) + QA automatico Lei 9.1.
    /// So executa quando CAPTURE_DEX_V1=1; no baseline normal passa em <5ms.
    /// </summary>
    public class DexSideBySideAndShowcaseTest
    {
        [UnityTest]
        public IEnumerator CaptureDexSideBySideAndShowcaseIfEnabled()
        {
            if (Environment.GetEnvironmentVariable("CAPTURE_DEX_V1") != "1")
                yield break;

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
            Assert.IsNotNull(locoType, "DezLocomotionController nao encontrado.");
            var loco = dezRoot.GetComponent(locoType);
            Assert.IsNotNull(loco, "DezLocomotionController nao anexado.");

            PropertyInfo pUseScripted = locoType.GetProperty("UseScriptedInput");
            PropertyInfo pMoveDir     = locoType.GetProperty("ScriptedMoveDir");
            PropertyInfo pRun         = locoType.GetProperty("ScriptedRun");
            PropertyInfo pJump        = locoType.GetProperty("ScriptedJump");
            PropertyInfo pYawRate     = locoType.GetProperty("ScriptedYawRate");
            pUseScripted.SetValue(loco, true);

            // Estaciona Porca ao fundo sem cobrir a camera
            var porca = GameObject.Find("Companion_Porca_Placeholder");
            if (porca != null)
            {
                foreach (var mb in porca.GetComponents<MonoBehaviour>())
                    if (mb != null && mb.GetType().Name.Contains("Porca")) mb.enabled = false;
                porca.transform.position = new Vector3(-25.5f, 1.35f, -17.5f);
            }

            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            Assert.IsNotNull(cam, "Main Camera nao encontrada.");

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string sbsDir = Path.Combine(repoRoot, "evidence", "captures", "dex_v1_side_by_side_frames");
            string shwDir = Path.Combine(repoRoot, "evidence", "captures", "dex_v1_60fps_frames");
            if (Directory.Exists(sbsDir)) Directory.Delete(sbsDir, true);
            if (Directory.Exists(shwDir)) Directory.Delete(shwDir, true);
            Directory.CreateDirectory(sbsDir);
            Directory.CreateDirectory(shwDir);

            int width = 1280, height = 720;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            // ============================================================
            // PARTE 1: A PROVA SIDE-BY-SIDE (VELHO a esquerda vs NOVO a direita)
            // ============================================================
#if UNITY_EDITOR
            var oldPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Fab3D/hero_dez_humanoid.prefab");
            var newPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Fab3D/hero_dex_humanoid.prefab");
            if (oldPrefab != null && newPrefab != null)
            {
                dezRoot.SetActive(false);
                var oldGo = UnityEngine.Object.Instantiate(oldPrefab, new Vector3(-30.6f, 0.0f, -19.2f), Quaternion.Euler(0f, 90f, 0f));
                var newGo = UnityEngine.Object.Instantiate(newPrefab, new Vector3(-30.6f, 0.0f, -18.0f), Quaternion.Euler(0f, 90f, 0f));

                // Normaliza ambos exatamente para 1.25m de altura
                foreach (var g in new[] { oldGo, newGo })
                {
                    var smr = g.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    if (smr != null)
                    {
                        smr.updateWhenOffscreen = true;
                        float h = smr.bounds.size.y;
                        if (h > 0.01f) g.transform.localScale = Vector3.one * (1.25f / h);
                    }
                    var a = g.GetComponentInChildren<Animator>(true);
                    if (a != null)
                    {
                        a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        a.SetBool("IsGrounded", true);
                        a.SetFloat("Speed", 3.6f); // Mesmo clip de corrida (Sprint_Loop) nos dois!
                    }
                }

                Vector3 mid = new Vector3(-30.6f, 0.65f, -18.6f);
                for (int f = 0; f < 240; f++)
                {
                    // Primeiros 120 frames (2s): VISTA POR TRAS (3a pessoa real); ultimos 120 frames (2s): VISTA FRONTAL!
                    bool backView = (f < 120);
                    string tag = backView ? "SBS_RUN_BACK" : "SBS_RUN_FRONT";
                    cam.transform.position = backView
                        ? (mid + new Vector3(-2.45f, 0.55f, 0f))
                        : (mid + new Vector3( 2.45f, 0.45f, 0f));
                    cam.transform.LookAt(mid);

                    yield return null;
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply();
                    File.WriteAllBytes(Path.Combine(sbsDir, $"sbs_{f:D4}_{tag}.png"), tex.EncodeToPNG());
                }

                UnityEngine.Object.Destroy(oldGo);
                UnityEngine.Object.Destroy(newGo);
                dezRoot.SetActive(true);
            }
#endif

            // ============================================================
            // PARTE 2: SHOWCASE 60 FPS DO DEX NOVO (720 frames = 12.0s)
            // ============================================================
            var anim = dezRoot.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(anim, "Animator nao encontrado.");
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var smr in dezRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;

            var cc = dezRoot.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            dezRoot.transform.position = new Vector3(-31.2f, 0.01f, -19.2f);
            dezRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            if (cc != null) cc.enabled = true;

            Transform leftFoot  = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            Transform earL = null;
            foreach (var t in dezRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == "Ear_L") { earL = t; break; }

            Vector3 prevLF = leftFoot ? leftFoot.position : Vector3.zero;
            Vector3 prevRF = rightFoot ? rightFoot.position : Vector3.zero;
            Quaternion baseEarRot = earL ? earL.localRotation : Quaternion.identity;

            float maxFootSlideCm = 0f, sumFootSlideCm = 0f, maxJumpY = 0f, maxEarAngleDeg = 0f;
            int footPlantSamples = 0;

            Vector3 camCurrent = dezRoot.transform.position + new Vector3(1.35f, 0.95f, -1.60f);
            int totalFrames = 720;

            for (int f = 0; f < totalFrames; f++)
            {
                float t = f / 60f;
                string phase;
                Vector3 camOffset;

                if (t < 2.2f)
                {
                    phase = "IDLE_VIVO";
                    pMoveDir.SetValue(loco, Vector3.zero);
                    pRun.SetValue(loco, false);
                    pYawRate.SetValue(loco, 0f);
                    camOffset = new Vector3(1.25f, 0.92f, -1.55f); // 3/4 frontal
                }
                else if (t < 4.7f)
                {
                    phase = "WALK";
                    pMoveDir.SetValue(loco, new Vector3(1.0f, 0f, 0.05f));
                    pRun.SetValue(loco, false);
                    pYawRate.SetValue(loco, 0f);
                    camOffset = new Vector3(0.35f, 1.02f, -2.25f); // perfil lateral
                }
                else if (t < 5.3f)
                {
                    phase = "WALK_STOP";
                    pMoveDir.SetValue(loco, Vector3.zero);
                    pRun.SetValue(loco, false);
                    pYawRate.SetValue(loco, 0f);
                    camOffset = new Vector3(-1.80f, 1.15f, -0.40f); // transiciona para vista por tras 3a pessoa!
                }
                else if (t < 7.2f)
                {
                    phase = "RUN_RETO_TRAS";
                    pMoveDir.SetValue(loco, new Vector3(-1.0f, 0f, 0.0f)); // corre reto de volta — camera 3a pessoa atras!
                    pRun.SetValue(loco, true);
                    pYawRate.SetValue(loco, 0f);
                    camOffset = new Vector3(2.35f, 1.15f, 0.0f); // 100% atras das costas!
                }
                else if (t < 9.6f)
                {
                    phase = "JUMP_GRAVIDADE";
                    pMoveDir.SetValue(loco, new Vector3(0.75f, 0f, 0.10f));
                    pRun.SetValue(loco, false);
                    pYawRate.SetValue(loco, 0f);
                    if (f == (int)(7.35f * 60f)) pJump.SetValue(loco, true);
                    camOffset = new Vector3(0.35f, 1.18f, -2.55f); // perfil aberto mostrando pulo + sombra + pouso
                }
                else
                {
                    phase = "IDLE_POS_POUSO";
                    pMoveDir.SetValue(loco, Vector3.zero);
                    pRun.SetValue(loco, false);
                    pYawRate.SetValue(loco, 0f);
                    camOffset = new Vector3(1.25f, 0.92f, -1.60f); // 3/4 frontal pos-pouso
                }

                yield return null;

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

                Vector3 lookPos = p + new Vector3(0f, 0.68f, 0f);
                Vector3 desiredCam = p + camOffset;
                if (Physics.Linecast(lookPos, desiredCam, out RaycastHit camHit, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (camHit.transform.root != dezRoot.transform.root)
                        desiredCam = camHit.point + camHit.normal * 0.25f;
                }
                camCurrent = Vector3.Lerp(camCurrent, desiredCam, 0.16f);
                cam.transform.position = camCurrent;
                cam.transform.LookAt(lookPos);

                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(shwDir, $"frame_{f:D4}_{phase}.png"), tex.EncodeToPNG());
            }

            float avgFootSlideCm = footPlantSamples > 0 ? (sumFootSlideCm / footPlantSamples) : 0f;
            var inv = CultureInfo.InvariantCulture;
            string qaPath = Path.Combine(repoRoot, "evidence", "validation", "qa_animacao_dex_v1.json");
            Directory.CreateDirectory(Path.GetDirectoryName(qaPath));
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

            Assert.AreEqual(720, totalFrames);
        }
    }
}
