using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    public class LedgeMantlePlayModeTest
    {
        [UnityTest]
        public IEnumerator DexGrabsAndMantlesHighLedge()
        {
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
            Assert.IsNotNull(dezRoot, "Player_Dez_Placeholder nao encontrado");

            var locoType = Type.GetType("Studio.Game.Animation.DezLocomotionController, Assembly-CSharp");
            Assert.IsNotNull(locoType, "DezLocomotionController nao encontrado");
            var loco = dezRoot.GetComponent(locoType);
            Assert.IsNotNull(loco);

            // Cria uma plataforma/borda de teste de 1.80m de altura na frente do Dex (pulo sozinho ~1.15m nao passa os pes, mas cabeca/maos alcancam!)
            var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ledge.name = "Test_HighLedge_180";
            ledge.transform.position = new Vector3(-28.0f, 0.90f, -19.5f);
            ledge.transform.localScale = new Vector3(2.0f, 1.80f, 2.0f);

            var cc = dezRoot.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            dezRoot.transform.position = new Vector3(-29.45f, 0.02f, -19.5f);
            dezRoot.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            if (cc != null) cc.enabled = true;

            locoType.GetProperty("UseScriptedInput").SetValue(loco, true);
            locoType.GetProperty("ScriptedMoveDir").SetValue(loco, new Vector3(1f, 0f, 0f));
            locoType.GetProperty("ScriptedRun").SetValue(loco, true);
            locoType.GetProperty("ScriptedJump").SetValue(loco, true);

            bool triggeredMantle = false;
            var isMantlingProp = locoType.GetProperty("IsMantling");

            for (int i = 0; i < 90; i++)
            {
                yield return null;
                if ((bool)isMantlingProp.GetValue(loco))
                    triggeredMantle = true;
                if (i == 45)
                    locoType.GetProperty("ScriptedMoveDir").SetValue(loco, Vector3.zero);
            }

            float finalY = dezRoot.transform.position.y;
            UnityEngine.Object.Destroy(ledge);
            Time.captureFramerate = 0;

            Assert.IsTrue(triggeredMantle, "Dex deveria ter disparado Ledge Grab / Mantle ao pular contra a borda de 1.80m!");
            Assert.GreaterOrEqual(finalY, 1.70f, $"Dex deveria estar de pe em cima da borda (1.80m), mas terminou em Y={finalY:F2}m");
        }
    }
}
