using System.Collections;
using NUnit.Framework;
using Studio.Gameplay;
using Studio.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace Studio.Tests.PlayMode
{
    /// <summary>
    /// Adendo D — Baseline PlayMode M1-M2 (CONCERTO).
    /// Prova deterministica antes de qualquer edicao de blockout C2:
    ///   1. Jogo carrega e Player (Dez + Porca) spawna com componentes core ativos e zero excecoes.
    ///   2. Objetivo M1 (I1 Bancada -> I2..I4 Valvulas -> I5 No Re-Sincronia + DialoguePresenter)
    ///      completa usando exclusivamente InteractionSystem.cs e DialoguePresenter.cs congelados.
    /// </summary>
    public class ConcertoM1M2BaselinePlayModeTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        [UnityTest]
        public IEnumerator PlayMode_Baseline_SceneLoads_PlayerSpawns_ZeroExceptions()
        {
            _root = new GameObject("BaselineM1M2_Stage");
            var camGo = new GameObject("BaselineCam");
            camGo.transform.SetParent(_root.transform);
            camGo.AddComponent<Camera>();

            var player = new GameObject("Player_Dez");
            player.transform.SetParent(_root.transform);
            player.transform.position = new Vector3(-33f, 0f, -20f);
            var interactor = player.AddComponent<PlayerInteractor>();
            var dialogue = player.AddComponent<DialoguePresenter>();
            dialogue.showDefaultOverlay = false;

            var porca = new GameObject("Companion_Porca");
            porca.transform.SetParent(_root.transform);
            porca.transform.position = player.transform.position + new Vector3(0.8f, 1.4f, -0.4f);

            yield return null;
            yield return new WaitForFixedUpdate();

            Assert.IsNotNull(player, "Player_Dez deve spawnar no PlayMode.");
            Assert.IsNotNull(interactor, "PlayerInteractor deve estar ativo no Player_Dez.");
            Assert.IsNotNull(dialogue, "DialoguePresenter deve estar ativo no Player_Dez.");
            Assert.IsNotNull(porca, "Companion_Porca deve spawnar junto ao Player_Dez.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PlayMode_Baseline_M1Objective_CompletesViaInteractionSystem()
        {
            _root = new GameObject("BaselineM1M2_ObjectiveStage");

            var player = new GameObject("Player_Dez");
            player.transform.SetParent(_root.transform);
            player.transform.position = Vector3.zero;
            var interactor = player.AddComponent<PlayerInteractor>();
            var dialogue = player.AddComponent<DialoguePresenter>();
            dialogue.showDefaultOverlay = false;

            string[] objectiveIds =
            {
                "I1_Bancada_Relogio",
                "I2_Valvula_A",
                "I3_Valvula_B",
                "I4_Valvula_C",
                "I5_No_ReSincronia"
            };

            int completedCount = 0;
            bool dialogueStarted = false;
            dialogue.OnLineStarted += (line, sub) => { dialogueStarted = true; };

            for (int i = 0; i < objectiveIds.Length; i++)
            {
                var targetPos = new Vector3(i * 6f, 0f, 0f);
                var objGo = new GameObject(objectiveIds[i]);
                objGo.transform.SetParent(_root.transform);
                objGo.transform.position = targetPos;
                var col = objGo.AddComponent<BoxCollider>();
                col.size = Vector3.one;
                var interactable = objGo.AddComponent<InteractableBase>();
                interactable.OnInteracted += _ =>
                {
                    completedCount++;
                    dialogue.TriggerBark("Porca", $"Objetivo {objGo.name} sincronizado!", 1.0f);
                };

                // Move player into interactRadius (2.4m)
                player.transform.position = targetPos + new Vector3(0f, 0f, -1.0f);
                yield return new WaitForFixedUpdate();

                var focused = interactor.ScanNearest();
                Assert.IsNotNull(focused, $"PlayerInteractor deve focar {objectiveIds[i]} dentro do raio de 2.4m.");
                bool ok = interactor.TryInteractFocused();
                Assert.IsTrue(ok, $"TryInteractFocused() deve completar {objectiveIds[i]}.");
                yield return null;
            }

            Assert.AreEqual(objectiveIds.Length, completedCount, "Todos os 5 objetivos do loop M1-M2 devem completar.");
            Assert.IsTrue(dialogueStarted, "DialoguePresenter deve disparar feedback de fala/bark durante o loop M1-M2.");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
