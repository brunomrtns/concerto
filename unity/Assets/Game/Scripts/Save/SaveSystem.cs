using System;
using System.IO;
using Studio.Game.Animation;
using Studio.Game.Anomalies;
using Studio.Game.Combat;
using Studio.Game.Cutscenes;
using Studio.Game.Dialogue;
using Studio.Game.Onboarding;
using Studio.Game.Parts;
using Studio.Game.Powers;
using Studio.Game.Repair;
using Studio.Game.Tool;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Studio.Game.Save
{
    /// <summary>
    /// SaveSystem — Unica fonte de verdade para persistencia de estado de jogo em CONCERTO.
    ///   - Arquivo JSON versionado em Application.persistentDataPath/concerto_save.json (NUNCA em PlayerPrefs!)
    ///   - Escrita atomica (.tmp -> destino) para evitar corrupcao em crash/queda de energia
    ///   - Fallback 100% seguro se arquivo corrompido ou versao invalida (nunca crasha, nunca trava em tela preta)
    ///   - Gerencia Checkpoints e Respawn de Knockdown preservando o estado completo do mundo
    /// </summary>
    public static class SaveSystem
    {
        public const string SaveFileName = "concerto_save.json";
        public const string DefaultGameScene = "Caldeiral";

        private static string _customPathOverride;
        private static SaveGameData _lastLoadedOrSaved;
        private static bool _pendingContinueLoad;
        private static bool _pendingNewGameOnboarding;

        public static string SaveFilePath => !string.IsNullOrEmpty(_customPathOverride)
            ? _customPathOverride
            : Path.Combine(Application.persistentDataPath, SaveFileName);

        public static bool IsPendingContinueLoad => _pendingContinueLoad;
        public static bool IsPendingNewGameOnboarding => _pendingNewGameOnboarding;
        public static SaveGameData CachedState => _lastLoadedOrSaved;

        public static bool ConsumePendingNewGameOnboarding()
        {
            if (!_pendingNewGameOnboarding) return false;
            _pendingNewGameOnboarding = false;
            return true;
        }

        public static event Action<SaveGameData> OnGameSaved;
        public static event Action<SaveGameData> OnGameLoaded;
        public static event Action<string> OnCorruptSaveFallback;

        public static void SetCustomPathForTests(string pathOrNull) => _customPathOverride = pathOrNull;

        public static bool HasSave()
        {
            return TryLoad(out _);
        }

        public static SaveGameData Load()
        {
            return TryLoad(out var data) ? data : null;
        }

        /// <summary>
        /// Le e valida o JSON versionado. Se corrompido ou versao incompativel:
        /// faz backup .corrupt.bak, limpa o arquivo quebrado e retorna false (fallback seguro para Novo Jogo!).
        /// </summary>
        public static bool TryLoad(out SaveGameData data)
        {
            data = null;
            string path = SaveFilePath;
            if (!File.Exists(path)) return false;

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    throw new FormatException("Arquivo de save vazio.");

                var parsed = JsonUtility.FromJson<SaveGameData>(json);
                int loadedVersion = parsed != null ? parsed.schemaVersion : -1;
                if (parsed == null || !parsed.MigrateIfNeeded())
                    throw new FormatException($"Schema invalido ou incompativel (version={loadedVersion}).");

                // FIX 3: se migrou de v1 -> v2 trazendo flags legados do PlayerPrefs, persiste v2 e limpa PlayerPrefs
                bool hadLegacyPrefs = PlayerPrefs.HasKey("CONCERTO_POWERS_UNLOCKED") || PlayerPrefs.HasKey("CONCERTO_CS03_PLAYED");
                if (loadedVersion < SaveGameData.CurrentSchemaVersion || hadLegacyPrefs)
                {
                    if (PlayerPrefs.GetInt("CONCERTO_POWERS_UNLOCKED", 0) == 1) parsed.powersUnlocked = true;
                    if (PlayerPrefs.GetInt("CONCERTO_CS03_PLAYED", 0) == 1) parsed.cs03Played = true;
                    if (hadLegacyPrefs)
                    {
                        PlayerPrefs.DeleteKey("CONCERTO_POWERS_UNLOCKED");
                        PlayerPrefs.DeleteKey("CONCERTO_CS03_PLAYED");
                        PlayerPrefs.Save();
                    }
                    WriteAtomic(path, parsed);
                }

                data = parsed;
                _lastLoadedOrSaved = parsed;
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Save corrompido ou incompativel detectado ({ex.Message}). Aplicando fallback seguro!");
                QuarantineCorruptFile(path);
                _lastLoadedOrSaved = null;
                OnCorruptSaveFallback?.Invoke(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Coleta TODO o estado vivo da cena atual e grava atomicamente em concerto_save.json.
        /// </summary>
        public static SaveGameData Save(string checkpointId = null, Vector3? checkpointPos = null, float? checkpointYaw = null)
        {
            var data = _lastLoadedOrSaved != null ? Clone(_lastLoadedOrSaved) : new SaveGameData();
            data.schemaVersion = SaveGameData.CurrentSchemaVersion;
            data.savedAtUtc = DateTime.UtcNow.ToString("o");

            string activeScene = SceneManager.GetActiveScene().name;
            if (!string.IsNullOrEmpty(activeScene) && activeScene != "MainMenu")
                data.sceneName = activeScene;
            else if (string.IsNullOrEmpty(data.sceneName))
                data.sceneName = DefaultGameScene;

            // 1. Posicao & rotacao do Dez (via PlayerRegistry — FIX 4)
            var dez = PlayerRegistry.Resolve();
            if (dez != null)
            {
                data.playerPosition = new SaveVec3(dez.transform.position);
                data.playerYaw = dez.transform.eulerAngles.y;
            }

            // 2. Checkpoint ativo (para respawn de knockdown — FIX 2)
            if (!string.IsNullOrEmpty(checkpointId))
            {
                data.currentCheckpointId = checkpointId;
                data.checkpointPosition = new SaveVec3(checkpointPos ?? (dez != null ? dez.transform.position : data.checkpointPosition.ToVector3()));
                data.checkpointYaw = checkpointYaw ?? (dez != null ? dez.transform.eulerAngles.y : data.checkpointYaw);
            }
            else if (PlayerHealthController.Instance != null)
            {
                data.checkpointPosition = new SaveVec3(PlayerHealthController.Instance.respawnPosition);
            }

            // 3. Objetivo atual & dialogos concluidos (BL-001 — zero reset apos reload!)
            if (!string.IsNullOrEmpty(ObjectiveSystem.CurrentObjective))
                data.currentObjective = ObjectiveSystem.CurrentObjective;
            if (DialogueController.Instance != null)
            {
                data.completedDialogueIds.Clear();
                DialogueController.Instance.CopyCompletedDialogueIdsTo(data.completedDialogueIds);
            }

            // 3b. Progresso do Onboarding (JSON versionado — NUNCA PlayerPrefs)
            var onboarding = OnboardingSequenceController.Instance;
            if (onboarding != null)
            {
                data.onboardingCompleted = onboarding.IsCompleted;
                data.onboardingSkipped = onboarding.WasSkipped;
                data.onboardingStepIndex = (int)onboarding.CurrentStep;
                if (data.completedOnboardingSteps == null)
                    data.completedOnboardingSteps = new System.Collections.Generic.List<string>();
                data.completedOnboardingSteps.Clear();
                onboarding.CopyCompletedStepsTo(data.completedOnboardingSteps);
            }

            // 4. Loop de pecas do Ato 1
            var parts = PartsLoopManager.Instance;
            if (parts != null)
            {
                data.partsCollectedMask = parts.GetCollectedMask();
                data.canCollectParts    = parts.CanCollectParts;
                data.act1Completed      = parts.Act1Completed;
            }

            // 5. Cutscenes assistidas, porta do Setor Externo & Missao "Sinal"
            data.cs01Played = true;
            if (data.act1Completed) data.cs02Played = true;
            if (PlayerPrefs.GetInt("CONCERTO_CS04_PLAYED", 0) == 1) data.cs04Played = true;
            if (PlayerPrefs.GetInt("CONCERTO_SECTOR_EXTERNO_UNLOCKED", 0) == 1 || activeScene == "SetorExterno")
                data.sectorDoorUnlocked = true;
            if (Studio.Game.Quests.SignalQuestController.Instance != null)
                data.signalQuestStep = Studio.Game.Quests.SignalQuestController.Instance.CurrentStepInt;
            if (PlayerPrefs.GetInt("CONCERTO_EAST_GATE_UNLOCKED", 0) == 1)
                data.eastGateUnlocked = true;

            // 6. Anomalias & Reparo do Ato 2
            var anomMgr = WorkshopAnomalyManager.Instance;
            if (anomMgr != null)
            {
                data.act2AnomaliesActive = anomMgr.AreAnomaliesActive;
                if (anomMgr.AreAnomaliesActive) data.cs03Played = true;
                data.resolvedAnomalyIds.Clear();
                var all = anomMgr.GetAllAnomalies();
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && !all[i].IsAnomalous)
                        data.resolvedAnomalyIds.Add(all[i].anomalyId);
                }
            }
            if (RepairRitualController.Instance != null)
                data.act2Completed = RepairRitualController.Instance.Act2Completed;

            // 7. Ferramenta Multi-Uso & Poderes Temporais (FIX 3: progressao no JSON versionado!)
            var tool = MultiToolController.Instance;
            if (tool != null)
            {
                data.energyCells      = tool.EnergyCells;
                data.energyBar        = tool.EnergyBar;
                data.activeToolModeId = tool.ActiveModeId;
            }
            var powers = DezPowersController.Instance;
            if (powers != null && powers.IsUnlocked)
            {
                data.powersUnlocked = true;
                data.cs03Played = true;
                if (!data.unlockedPowerIds.Contains("eco_temporal")) data.unlockedPowerIds.Add("eco_temporal");
                if (!data.unlockedPowerIds.Contains("estase")) data.unlockedPowerIds.Add("estase");
            }

            WriteAtomic(SaveFilePath, data);
            _lastLoadedOrSaved = data;
            OnGameSaved?.Invoke(data);
            Debug.Log($"[SaveSystem] Jogo salvo em {SaveFilePath} (cp={data.currentCheckpointId}, obj='{data.currentObjective}', cells={data.energyCells}, anomActive={data.act2AnomaliesActive}, resolved={data.resolvedAnomalyIds.Count})");
            return data;
        }

        public static void Delete()
        {
            _lastLoadedOrSaved = null;
            _pendingContinueLoad = false;
            try
            {
                if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
                string tmp = SaveFilePath + ".tmp";
                if (File.Exists(tmp)) File.Delete(tmp);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Erro ao remover save: {ex.Message}");
            }
        }

        public static void MarkNewGameRequested()
        {
            Delete();
            _pendingNewGameOnboarding = true;
            PlayerPrefs.DeleteKey(CutsceneManager.PlayedPrefKey);
            PlayerPrefs.Save();
        }

        public static bool MarkContinueRequested(out string sceneToLoad)
        {
            sceneToLoad = DefaultGameScene;
            if (!TryLoad(out var data))
            {
                // Fallback seguro (Gate 3): save corrompido -> inicia jogo novo sem crashar!
                MarkNewGameRequested();
                return false;
            }

            _pendingContinueLoad = true;
            _pendingNewGameOnboarding = false;
            sceneToLoad = string.IsNullOrEmpty(data.sceneName) ? DefaultGameScene : data.sceneName;
            if (data.cs01Played)
            {
                PlayerPrefs.SetInt(CutsceneManager.PlayedPrefKey, 1);
                PlayerPrefs.Save();
            }
            return true;
        }

        /// <summary>
        /// Restaura TODO o estado salvo na cena da oficina (posicao, objetivo, pecas, anomalias resolvidas, celulas, modo, checkpoint).
        /// </summary>
        public static bool TryApplyPendingLoadToWorkshop()
        {
            if (!_pendingContinueLoad) return false;
            _pendingContinueLoad = false;

            if (!TryLoad(out var data))
                return false;

            return ApplyLoadedStateToCurrentScene(data);
        }

        public static bool ApplyLoadedStateToCurrentScene(SaveGameData data)
        {
            if (data == null) return false;

            // 1. Posicao & rotacao do Dez + Checkpoint de Respawn (FIX 2 & FIX 4)
            var dez = PlayerRegistry.Resolve();
            if (dez != null)
            {
                var cc = dez.GetComponent<CharacterController>();
                bool hadCc = cc != null && cc.enabled;
                if (hadCc) cc.enabled = false;
                dez.transform.position = data.playerPosition.ToVector3();
                dez.transform.rotation = Quaternion.Euler(0f, data.playerYaw, 0f);
                if (hadCc) cc.enabled = true;
            }
            if (PlayerHealthController.Instance != null)
                PlayerHealthController.Instance.SetCheckpointFromSaveOnly(data.checkpointPosition.ToVector3());

            // 2. Pecas do Ato 1, Dialogos Concluidos (BL-001) & Onboarding
            if (DialogueController.Instance != null && data.completedDialogueIds != null)
                DialogueController.Instance.RestoreCompletedDialogues(data.completedDialogueIds);
            if (OnboardingSequenceController.Instance != null)
                OnboardingSequenceController.Instance.RestoreFromSave(
                    data.onboardingCompleted,
                    data.onboardingSkipped,
                    data.onboardingStepIndex,
                    data.completedOnboardingSteps);
            if (PartsLoopManager.Instance != null)
                PartsLoopManager.Instance.RestoreFromSave(data.partsCollectedMask, data.canCollectParts, data.act1Completed);

            // 3. Anomalias do Ato 2 (ativa todas e resolve exatamente as ja resolvidas!)
            var anomMgr = WorkshopAnomalyManager.Instance;
            if (anomMgr != null)
            {
                if (data.act2AnomaliesActive)
                {
                    anomMgr.ActivateAllAnomalies(persistFlag: false);
                    var all = anomMgr.GetAllAnomalies();
                    for (int i = 0; i < all.Count; i++)
                    {
                        if (all[i] != null && data.resolvedAnomalyIds.Contains(all[i].anomalyId))
                            all[i].Resolve();
                    }
                }
                else
                {
                    anomMgr.ResetAllToNormal();
                }
            }
            if (RepairRitualController.Instance != null)
                RepairRitualController.Instance.RefreshTargets();

            // 4. Ferramenta Multi-Uso & Poderes Temporais (FIX 3)
            if (MultiToolController.Instance != null)
                MultiToolController.Instance.RestoreStateFromSave(data.energyCells, data.energyBar, data.activeToolModeId);
            if (DezPowersController.Instance != null)
                DezPowersController.Instance.RestoreFromSave(data.powersUnlocked || data.cs03Played || (data.unlockedPowerIds != null && data.unlockedPowerIds.Count > 0));

            // 5. Objetivo atual + Flags de Porta/CS04/Missao Sinal
            if (data.sectorDoorUnlocked)
                PlayerPrefs.SetInt("CONCERTO_SECTOR_EXTERNO_UNLOCKED", 1);
            if (data.cs04Played)
                PlayerPrefs.SetInt("CONCERTO_CS04_PLAYED", 1);
            if (data.eastGateUnlocked)
                PlayerPrefs.SetInt("CONCERTO_EAST_GATE_UNLOCKED", 1);
            if (data.sectorDoorUnlocked || data.cs04Played || data.eastGateUnlocked)
                PlayerPrefs.Save();

            if (Studio.Game.Quests.SignalQuestController.Instance != null)
                Studio.Game.Quests.SignalQuestController.Instance.RestoreFromSave(data.signalQuestStep, data.eastGateUnlocked);

            if (!string.IsNullOrEmpty(data.currentObjective))
                ObjectiveSystem.Set(data.currentObjective);

            _lastLoadedOrSaved = data;
            OnGameLoaded?.Invoke(data);
            Debug.Log($"[SaveSystem] Estado restaurado com sucesso (cp={data.currentCheckpointId}, pos={data.playerPosition.ToVector3()}, cells={data.energyCells}, obj='{data.currentObjective}')");
            return true;
        }

        /// <summary>
        /// Respawn de Knockdown de combate: devolve o Dez ao ULTIMO checkpoint preservando o mundo inteiro!
        /// </summary>
        public static bool RespawnPlayerAtLastCheckpoint()
        {
            var data = _lastLoadedOrSaved ?? Load();
            var dez = PlayerRegistry.Resolve();
            if (dez == null) return false;

            Vector3 targetPos = data != null ? data.checkpointPosition.ToVector3() : new Vector3(-28.5f, 0.05f, -18.5f);
            float targetYaw   = data != null ? data.checkpointYaw : 45f;

            var cc = dez.GetComponent<CharacterController>();
            bool hadCc = cc != null && cc.enabled;
            if (hadCc) cc.enabled = false;
            dez.transform.position = targetPos;
            dez.transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);
            if (hadCc) cc.enabled = true;

            Debug.Log($"[SaveSystem] Respawn no ultimo checkpoint '{(data != null ? data.currentCheckpointId : "default")}' em {targetPos} (estado do mundo preservado!).");
            return true;
        }

        private static void WriteAtomic(string path, SaveGameData data)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            string tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, json);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmpPath, path);
        }

        private static void QuarantineCorruptFile(string path)
        {
            try
            {
                string bak = path + ".corrupt.bak";
                if (File.Exists(bak)) File.Delete(bak);
                if (File.Exists(path)) File.Move(path, bak);
            }
            catch { /* ignora erro de IO no backup */ }
        }

        private static SaveGameData Clone(SaveGameData src)
        {
            return JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(src));
        }
    }
}
