using System.Collections;
using Studio.Game.Cutscenes;
using Studio.Game.Parts;
using Studio.Game.Repair;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.Game.Save
{
    /// <summary>
    /// Orquestrador de Auto-Save em eventos-chave (pos-cutscene, pos-beat de ato, pos-reparo)
    /// + icone sutil de 1s no canto inferior esquerdo (sem tela de loading parada!).
    /// </summary>
    public sealed class SaveCheckpointHUD : MonoBehaviour
    {
        public CanvasGroup saveIndicatorGroup;
        public Text saveIndicatorText;
        public float indicatorDurationSec = 1.0f;

        private Coroutine _flashRoutine;
        private CutsceneManager _cs01;
        private Cutscene02TravelController _cs02;
        private Cutscene03ReturnController _cs03;

        private void Awake()
        {
            if (saveIndicatorGroup != null) saveIndicatorGroup.alpha = 0f;
            SaveSystem.OnGameSaved += HandleGameSaved;
            SaveSystem.OnBackupRecovered += HandleBackupRecovered;
        }

        private void Start()
        {
            _cs01 = FindAnyObjectByType<CutsceneManager>();
            _cs02 = FindAnyObjectByType<Cutscene02TravelController>();
            _cs03 = FindAnyObjectByType<Cutscene03ReturnController>();

            if (_cs01 != null) _cs01.OnCutsceneEnded += OnCS01Ended;
            if (_cs02 != null) _cs02.OnCutscene02Ended += OnCS02Ended;
            if (_cs03 != null) _cs03.OnCutscene03Ended += OnCS03Ended;
            if (PartsLoopManager.Instance != null) PartsLoopManager.Instance.OnAct1Completed += OnAct1Done;
            if (RepairRitualController.Instance != null)
            {
                RepairRitualController.Instance.OnRepairProgressChanged += OnAnomalyRepaired;
                RepairRitualController.Instance.OnAct2FinalBeatCompleted += OnAct2Done;
            }

            // Se o jogador clicou "Continuar" no MainMenu para uma cena sem CutsceneManager (ex: SetorExterno),
            // restaura automaticamente o estado salvo (posicao, yaw, objetivo, porta, celulas)!
            if (SaveSystem.IsPendingContinueLoad && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Caldeiral")
            {
                SaveSystem.TryApplyPendingLoadToWorkshop();
            }

            string pendingNotice = SaveSystem.ConsumePendingRecoveryNotice();
            if (!string.IsNullOrEmpty(pendingNotice))
            {
                ShowBannerMessage(pendingNotice, 3.8f);
            }
        }

        private void OnDestroy()
        {
            SaveSystem.OnGameSaved -= HandleGameSaved;
            SaveSystem.OnBackupRecovered -= HandleBackupRecovered;
            if (_cs01 != null) _cs01.OnCutsceneEnded -= OnCS01Ended;
            if (_cs02 != null) _cs02.OnCutscene02Ended -= OnCS02Ended;
            if (_cs03 != null) _cs03.OnCutscene03Ended -= OnCS03Ended;
            if (PartsLoopManager.Instance != null) PartsLoopManager.Instance.OnAct1Completed -= OnAct1Done;
            if (RepairRitualController.Instance != null)
            {
                RepairRitualController.Instance.OnRepairProgressChanged -= OnAnomalyRepaired;
                RepairRitualController.Instance.OnAct2FinalBeatCompleted -= OnAct2Done;
            }
        }

        private void OnCS01Ended() => SaveSystem.Save("cp_pos_cs01_oficina");
        private void OnAct1Done()  => SaveSystem.Save("cp_ato1_pecas_entregues");
        private void OnCS02Ended() => SaveSystem.Save("cp_pos_cs02_viagem");
        private void OnCS03Ended() => SaveSystem.Save("cp_pos_cs03_retorno");
        private void OnAnomalyRepaired(int repaired, int total) => SaveSystem.Save($"cp_ato2_reparo_{repaired}_{total}");
        private void OnAct2Done()  => SaveSystem.Save("cp_fim_ato2");

        private void HandleGameSaved(SaveGameData data)
        {
            ShowBannerMessage("● SALVO", Mathf.Max(0.4f, indicatorDurationSec));
        }

        private void HandleBackupRecovered(string slotName, string reason)
        {
            ShowBannerMessage($"! BACKUP RECUPERADO ({slotName})", 3.8f);
        }

        private void ShowBannerMessage(string message, float holdSec)
        {
            if (saveIndicatorGroup == null || !isActiveAndEnabled) return;
            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(ShowSubtleIndicator(message, holdSec));
        }

        private IEnumerator ShowSubtleIndicator(string message, float holdSec)
        {
            if (saveIndicatorText != null) saveIndicatorText.text = message;
            saveIndicatorGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(holdSec);
            float t = 0f;
            while (t < 0.25f)
            {
                t += Time.unscaledDeltaTime;
                saveIndicatorGroup.alpha = 1f - (t / 0.25f);
                yield return null;
            }
            saveIndicatorGroup.alpha = 0f;
            _flashRoutine = null;
        }
    }
}
