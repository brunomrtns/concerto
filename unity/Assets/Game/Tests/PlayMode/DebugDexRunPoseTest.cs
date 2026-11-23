using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    public class DebugDexRunPoseTest
    {
        [UnityTest]
        public IEnumerator CaptureRunPoseIfEnabled()
        {
            if (Environment.GetEnvironmentVariable("DEBUG_DEX_RUN") != "1")
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
            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            var loco = dezRoot.GetComponent(locoType);
            locoType.GetProperty("UseScriptedInput").SetValue(loco, true);
            locoType.GetProperty("ScriptedMoveDir").SetValue(loco, new Vector3(1f, 0f, 0f));
            locoType.GetProperty("ScriptedRun").SetValue(loco, true);

            var cc = dezRoot.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            dezRoot.transform.position = new Vector3(-31.5f, 0.01f, -19.2f);
            dezRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            if (cc != null) cc.enabled = true;

            var anim = dezRoot.GetComponentInChildren<Animator>(true);
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // Roda 120 frames (2.0s completos!) de corrida reta em +X e mede media dos ultimos 60 frames
            var bones = new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Neck, HumanBodyBones.Head };
            var sumLean = new float[bones.Length];
            var sumYaw  = new float[bones.Length];
            int samples = 0;

            for (int i = 0; i < 120; i++)
            {
                yield return null;
                if (i >= 60)
                {
                    samples++;
                    for (int bi = 0; bi < bones.Length; bi++)
                    {
                        var t = anim.GetBoneTransform(bones[bi]);
                        if (t == null) continue;
                        Vector3 upInRoot = Quaternion.Inverse(dezRoot.transform.rotation) * t.up;
                        Vector3 fwdInRoot = Quaternion.Inverse(dezRoot.transform.rotation) * t.forward;
                        sumLean[bi] += Mathf.Atan2(upInRoot.x, upInRoot.y) * Mathf.Rad2Deg;
                        sumYaw[bi]  += Mathf.Atan2(fwdInRoot.x, fwdInRoot.z) * Mathf.Rad2Deg;
                    }
                }
            }

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"ROOT_ROT={dezRoot.transform.eulerAngles} | AVG_OVER_60_FRAMES (t=1.0s..2.0s)");
            for (int bi = 0; bi < bones.Length; bi++)
            {
                sb.AppendLine($"{bones[bi],-12} | avgLeanSideDeg={(sumLean[bi]/samples):F2} | avgYawDeg={(sumYaw[bi]/samples):F2}");
            }
            File.WriteAllText(Path.Combine(repoRoot, "evidence", "captures", "debug_dex_run_bones.txt"), sb.ToString());

            // Foto por tras 3a pessoa exata!
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            Vector3 p = dezRoot.transform.position;
            cam.transform.position = p - dezRoot.transform.forward * 2.2f + Vector3.up * 1.15f;
            cam.transform.LookAt(p + Vector3.up * 0.72f);

            int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(repoRoot, "evidence", "captures", "debug_dex_run_back.png"), tex.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);
            Time.captureFramerate = 0;
        }
    }
}
