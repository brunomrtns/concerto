using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    public class DezAnimShowcaseTest
    {
        [UnityTest]
        public IEnumerator CaptureDezPlayModeShowcaseIfEnabled()
        {
            if (Environment.GetEnvironmentVariable("CAPTURE_DEZ_SHOWCASE") != "1")
                yield break;

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
            var anim = dezRoot.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(anim, "Animator nao encontrado em Player_Dez_Placeholder.");

            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.applyRootMotion = false;
            foreach (var smr in dezRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;

            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            Assert.IsNotNull(cam, "Main Camera nao encontrada.");

            string repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".."));
            string framesDir = Path.Combine(repoRoot, "evidence", "captures", "dez_ual_anim_frames");
            if (Directory.Exists(framesDir)) Directory.Delete(framesDir, true);
            Directory.CreateDirectory(framesDir);

            int width = 960, height = 540;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);

            int frameIdx = 0;
            float dt = 0.05f; // 20 fps simulado suave
            Vector3 pos = dezRoot.transform.position;
            float yaw = dezRoot.transform.eulerAngles.y;

            void CaptureFrame(string label)
            {
                dezRoot.transform.position = pos;
                dezRoot.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                // Camera 3a pessoa lateral-frontal acompanhando o Dez
                Vector3 camPos = pos + new Vector3(2.2f, 1.35f, 2.0f);
                cam.transform.position = camPos;
                cam.transform.LookAt(pos + new Vector3(0f, 0.72f, 0f));

                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(framesDir, $"frame_{frameIdx:D3}_{label}.png"), tex.EncodeToPNG());
                frameIdx++;
            }

            // 1. IDLE (respiracao e movimento natural vivo)
            anim.CrossFade("Idle", 0.05f);
            for (int i = 0; i < 20; i++)
            {
                anim.Update(dt);
                yield return null;
                CaptureFrame("Idle");
            }

            // 2. WALK (andando pelo chao da Oficina)
            anim.CrossFade("Walk", 0.12f);
            for (int i = 0; i < 24; i++)
            {
                Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                pos += fwd * (1.8f * dt);
                anim.Update(dt);
                yield return null;
                CaptureFrame("Walk");
            }

            // 3. TURN (virando suavemente 160 graus)
            for (int i = 0; i < 14; i++)
            {
                yaw += 160f / 14f;
                anim.Update(dt);
                yield return null;
                CaptureFrame("Turn");
            }

            // 4. RUN (correndo de volta pela Oficina)
            anim.CrossFade("Run", 0.12f);
            for (int i = 0; i < 24; i++)
            {
                Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                pos += fwd * (3.6f * dt);
                anim.Update(dt);
                yield return null;
                CaptureFrame("Run");
            }

            // 5. JUMP COM FISICA E GRAVIDADE REAL (subindo do chao e caindo com g = -18 m/s^2)
            anim.CrossFade("Jump", 0.08f);
            float vy = 6.2f;
            float gravity = -18.0f;
            for (int i = 0; i < 24; i++)
            {
                Vector3 fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                pos += fwd * (1.5f * dt);
                pos.y += vy * dt;
                vy += gravity * dt;
                if (pos.y < 0f) { pos.y = 0f; vy = 0f; if (i > 12) anim.CrossFade("Idle", 0.1f); }
                anim.Update(dt);
                yield return null;
                CaptureFrame("Jump");
            }

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(tex);
            Assert.Greater(frameIdx, 90);
        }
    }
}
