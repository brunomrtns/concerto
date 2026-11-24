using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    public sealed class DezPolishVisualVerificationTest
    {
        private static Type GameType(string n)
        {
            var t = Type.GetType(n + ", Assembly-CSharp");
            Assert.IsNotNull(t, $"Tipo ausente: {n}");
            return t;
        }

        private static void Snap(Camera cam, RenderTexture rt, Texture2D tex, string path)
        {
            var pt = cam.targetTexture;
            var pa = RenderTexture.active;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            tex.Apply(false, false);
            cam.targetTexture = pt;
            RenderTexture.active = pa;
            File.WriteAllBytes(path, tex.EncodeToJPG(92));
        }

        [UnityTest]
        public IEnumerator VerifyArmsCombatHandMountMantleAndRunningRoll()
        {
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
            var procT = GameType("Studio.Game.Animation.DezProceduralAnimator");
            var toolT = GameType("Studio.Game.Tool.MultiToolController");

            var loco = UnityEngine.Object.FindAnyObjectByType(locoT) as Component;
            var proc = loco.GetComponentInChildren(procT) as Component;
            var tool = UnityEngine.Object.FindAnyObjectByType(toolT) as Component;
            Assert.IsNotNull(loco);
            Assert.IsNotNull(proc);
            Assert.IsNotNull(tool);

            // Mantem Dez na posicao de spawn natural da oficina (piso 100% livre!)
            var cc = loco.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            loco.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            if (cc != null) cc.enabled = true;
            Physics.SyncTransforms();

            // Verifica que toolVisualTransform foi parentado diretamente na mao direita (RightHand)!
            var toolVis = toolT.GetField("toolVisualTransform").GetValue(tool) as Transform;
            var anim = loco.GetComponentInChildren<Animator>();
            if (toolVis != null && anim != null && anim.isHuman)
            {
                Transform rHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                Assert.AreSame(rHand, toolVis.parent, "Ferramenta deve estar parentada diretamente em RightHand (nunca voando no ar!).");
            }

            string outDir = Path.Combine(Application.dataPath, "..", "Logs", "dez_polish_shots");
            Directory.CreateDirectory(outDir);
            var camGo = new GameObject("PolishVerifyCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);

            var pUseScripted = locoT.GetProperty("UseScriptedInput");
            var pMoveDir = locoT.GetProperty("ScriptedMoveDir");
            var pRun = locoT.GetProperty("ScriptedRun");
            pUseScripted.SetValue(loco, true);

            // 1. Walk front 3/4 (verifica bracos afastados do torso)
            pMoveDir.SetValue(loco, Vector3.forward);
            pRun.SetValue(loco, false);
            for (int i = 0; i < 20; i++) yield return null;
            Vector3 p = loco.transform.position;
            camGo.transform.position = p + new Vector3(0.95f, 1.15f, 2.25f);
            camGo.transform.LookAt(p + Vector3.up * 0.82f);
            Snap(cam, rt, tex, Path.Combine(outDir, "01_walk_arms_front.jpg"));

            // 2. Modo Combate — arma na mao + swing melee
            pMoveDir.SetValue(loco, Vector3.zero);
            toolT.GetMethod("EquipModuleIndex").Invoke(tool, new object[] { 1, false });
            for (int i = 0; i < 8; i++) yield return null;
            toolT.GetMethod("PlayToolSwingFeedback").Invoke(tool, new object[] { false, 1 });
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue((bool)procT.GetProperty("IsPlayingCombatSwing").GetValue(proc), "Swing melee deve animar o braco do Dez.");
            p = loco.transform.position;
            camGo.transform.position = p + new Vector3(1.1f, 1.15f, 2.0f);
            camGo.transform.LookAt(p + Vector3.up * 0.85f);
            Snap(cam, rt, tex, Path.Combine(outDir, "02_combat_tool_swing.jpg"));

            // 3. Tiro Bolt — coice real na mao/braco
            for (int i = 0; i < 16; i++) yield return null;
            toolT.GetMethod("PlayBoltFireFeedback").Invoke(tool, new object[] { p + Vector3.forward });
            yield return null;
            yield return null;
            Assert.IsTrue((bool)procT.GetProperty("IsPlayingBoltRecoil").GetValue(proc), "Tiro Bolt deve disparar coice real na mao/braco.");
            Snap(cam, rt, tex, Path.Combine(outDir, "03_bolt_recoil.jpg"));

            // 4. Escalada de borda (Mantle) — bracos erguidos + joelho subindo
            toolT.GetMethod("EquipModuleIndex").Invoke(tool, new object[] { 0, false });
            procT.GetMethod("SetMantleState").Invoke(proc, new object[] { true, 0.45f });
            yield return null;
            camGo.transform.position = p + new Vector3(1.85f, 1.15f, 0.85f);
            camGo.transform.LookAt(p + Vector3.up * 0.90f);
            Snap(cam, rt, tex, Path.Combine(outDir, "04_mantle_climb.jpg"));
            procT.GetMethod("SetMantleState").Invoke(proc, new object[] { false, 0f });

            // 5. Pouso correndo -> RunningRoll
            procT.GetMethod("TriggerRunningRoll").Invoke(proc, null);
            yield return null;
            Assert.IsTrue((bool)procT.GetProperty("IsPlayingRunningRoll").GetValue(proc), "Pouso correndo deve ativar absorcao/rolamento.");

            pUseScripted.SetValue(loco, false);
            UnityEngine.Object.Destroy(tex);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(camGo);
        }
    }
}
