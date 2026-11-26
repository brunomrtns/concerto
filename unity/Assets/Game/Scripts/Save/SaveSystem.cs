using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Studio.Game.Animation;
using Studio.Game.Anomalies;
using Studio.Game.Combat;
using Studio.Game.Cutscenes;
using Studio.Game.Dialogue;
using Studio.Game.Hub;
using Studio.Game.Onboarding;
using Studio.Game.Parts;
using Studio.Game.Powers;
using Studio.Game.Repair;
using Studio.Game.Resync;
using Studio.Game.Tool;
using Studio.Game.Vitralha;
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
        private static bool _wasLastLoadRecoveredFromBackup;
        private static string _lastRecoveredSlotName = string.Empty;
        private static string _pendingRecoveryNotice = string.Empty;

        public static string SaveFilePath => !string.IsNullOrEmpty(_customPathOverride)
            ? _customPathOverride
            : Path.Combine(Application.persistentDataPath, SaveFileName);

        public static string Backup1FilePath => BuildSlotPath(SaveFilePath, ".bak1");
        public static string Backup2FilePath => BuildSlotPath(SaveFilePath, ".bak2");

        public static bool IsPendingContinueLoad => _pendingContinueLoad;
        public static bool IsPendingNewGameOnboarding => _pendingNewGameOnboarding;
        public static bool WasLastLoadRecoveredFromBackup => _wasLastLoadRecoveredFromBackup;
        public static string LastRecoveredSlotName => _lastRecoveredSlotName;
        public static SaveGameData CachedState => _lastLoadedOrSaved;

        public static string ConsumePendingRecoveryNotice()
        {
            if (string.IsNullOrEmpty(_pendingRecoveryNotice)) return string.Empty;
            string msg = _pendingRecoveryNotice;
            _pendingRecoveryNotice = string.Empty;
            return msg;
        }

        public static bool ConsumePendingNewGameOnboarding()
        {
            if (!_pendingNewGameOnboarding) return false;
            _pendingNewGameOnboarding = false;
            return true;
        }

        public static event Action<SaveGameData> OnGameSaved;
        public static event Action<SaveGameData> OnGameLoaded;
        public static event Action<string, string> OnBackupRecovered; // (slotName, reason)
        public static event Action<string> OnCorruptSaveFallback;

        private static string BuildSlotPath(string primaryPath, string suffix)
        {
            string ext = Path.GetExtension(primaryPath);
            if (string.IsNullOrEmpty(ext)) return primaryPath + suffix;
            return primaryPath.Substring(0, primaryPath.Length - ext.Length) + suffix + ext;
        }

        public static string ComputeChecksum(SaveGameData data)
        {
            if (data == null) return string.Empty;
            string prev = data.payloadChecksum;
            data.payloadChecksum = string.Empty;
            string canonical = JsonUtility.ToJson(data, prettyPrint: false);
            data.payloadChecksum = prev;

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static bool VerifyChecksum(SaveGameData data)
        {
            if (data == null || string.IsNullOrEmpty(data.payloadChecksum))
                return false;
            string expected = ComputeChecksum(data);
            return string.Equals(data.payloadChecksum, expected, StringComparison.OrdinalIgnoreCase);
        }

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
        /// Le e valida o JSON versionado com verificacao de checksum SHA-256 (GATE 4) e recuperacao
        /// automatica pelos 3 slots rotativos (atual -> bak1 -> bak2 — GATE 3).
        /// Nunca crasha e nunca sobrescreve um backup bom com um arquivo corrompido.
        /// </summary>
        public static bool TryLoad(out SaveGameData data)
        {
            data = null;
            _wasLastLoadRecoveredFromBackup = false;
            _lastRecoveredSlotName = string.Empty;

            string primaryPath = SaveFilePath;
            string primaryError = string.Empty;
            bool primaryExisted = File.Exists(primaryPath);

            if (primaryExisted)
            {
                if (TryLoadSlot(primaryPath, out var parsed, out primaryError, out bool migrated))
                {
                    if (migrated || HasLegacyGameplayPlayerPrefs())
                    {
                        MergeAndPurgeLegacyGameplayPlayerPrefs(parsed);
                        WriteAtomic(primaryPath, parsed, rotateBackups: false);
                    }
                    data = parsed;
                    _lastLoadedOrSaved = parsed;
                    return true;
                }

                Debug.LogWarning($"[SaveSystem] Slot principal corrompido ({primaryError}). Quarentenando e tentando recuperar backup rotativo...");
                QuarantineCorruptFile(primaryPath);
            }

            // GATE 3: Fallback automatico para os slots de backup (bak1 -> bak2)
            string[] backupPaths = { Backup1FilePath, Backup2FilePath };
            string[] backupNames = { "bak1", "bak2" };
            for (int i = 0; i < backupPaths.Length; i++)
            {
                if (!File.Exists(backupPaths[i])) continue;
                if (TryLoadSlot(backupPaths[i], out var recovered, out _, out _))
                {
                    MergeAndPurgeLegacyGameplayPlayerPrefs(recovered);
                    WriteAtomic(primaryPath, recovered, rotateBackups: false);
                    data = recovered;
                    _lastLoadedOrSaved = recovered;
                    _wasLastLoadRecoveredFromBackup = true;
                    _lastRecoveredSlotName = backupNames[i];
                    _pendingRecoveryNotice = $"Save corrompido detectado — progresso recuperado do backup ({backupNames[i]})!";
                    OnBackupRecovered?.Invoke(backupNames[i], primaryError);
                    Debug.LogWarning($"[SaveSystem] Progresso recuperado com sucesso do backup '{backupNames[i]}' (cp={recovered.currentCheckpointId}, obj='{recovered.currentObjective}').");
                    return true;
                }
            }

            if (primaryExisted)
            {
                _lastLoadedOrSaved = null;
                OnCorruptSaveFallback?.Invoke(primaryError);
            }
            return false;
        }

        private static bool TryLoadSlot(string path, out SaveGameData parsed, out string errorReason, out bool migrated)
        {
            parsed = null;
            errorReason = string.Empty;
            migrated = false;

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    errorReason = "Arquivo de save vazio.";
                    return false;
                }

                var candidate = JsonUtility.FromJson<SaveGameData>(json);
                if (candidate == null)
                {
                    errorReason = "Falha ao desserializar JSON.";
                    return false;
                }

                int loadedVersion = candidate.schemaVersion;
                if (loadedVersion <= 0 || loadedVersion > SaveGameData.CurrentSchemaVersion)
                {
                    errorReason = $"Versao de schema invalida ou incompativel (version={loadedVersion}).";
                    return false;
                }

                // GATE 4: Para schema v3+, exige checksum SHA-256 integro
                if (loadedVersion >= 3)
                {
                    if (!VerifyChecksum(candidate))
                    {
                        errorReason = "Checksum SHA-256 invalido (arquivo corrompido ou adulterado).";
                        return false;
                    }
                }
                else
                {
                    migrated = true;
                }

                if (!candidate.MigrateIfNeeded())
                {
                    errorReason = $"Falha na migracao do schema v{loadedVersion}.";
                    return false;
                }

                parsed = candidate;
                return true;
            }
            catch (Exception ex)
            {
                errorReason = ex.Message;
                return false;
            }
        }

        private static bool HasLegacyGameplayPlayerPrefs()
        {
            return PlayerPrefs.HasKey("CONCERTO_POWERS_UNLOCKED")
                || PlayerPrefs.HasKey("CONCERTO_CS03_PLAYED")
                || PlayerPrefs.HasKey("CONCERTO_CS04_PLAYED")
                || PlayerPrefs.HasKey("CONCERTO_SECTOR_EXTERNO_UNLOCKED")
                || PlayerPrefs.HasKey("CONCERTO_EAST_GATE_UNLOCKED")
                || PlayerPrefs.HasKey("CONCERTO_ACT2_COMPLETED")
                || PlayerPrefs.HasKey("CONCERTO_ACT3_COMPLETED");
        }

        private static void MergeAndPurgeLegacyGameplayPlayerPrefs(SaveGameData data)
        {
            if (data == null || !HasLegacyGameplayPlayerPrefs()) return;
            if (PlayerPrefs.GetInt("CONCERTO_POWERS_UNLOCKED", 0) == 1) data.powersUnlocked = true;
            if (PlayerPrefs.GetInt("CONCERTO_CS03_PLAYED", 0) == 1) data.cs03Played = true;
            if (PlayerPrefs.GetInt("CONCERTO_CS04_PLAYED", 0) == 1) data.cs04Played = true;
            if (PlayerPrefs.GetInt("CONCERTO_SECTOR_EXTERNO_UNLOCKED", 0) == 1) data.sectorDoorUnlocked = true;
            if (PlayerPrefs.GetInt("CONCERTO_EAST_GATE_UNLOCKED", 0) == 1) data.eastGateUnlocked = true;
            if (PlayerPrefs.GetInt("CONCERTO_ACT2_COMPLETED", 0) == 1) data.act2Completed = true;
            if (PlayerPrefs.GetInt("CONCERTO_ACT3_COMPLETED", 0) == 1) data.act3Completed = true;

            PlayerPrefs.DeleteKey("CONCERTO_POWERS_UNLOCKED");
            PlayerPrefs.DeleteKey("CONCERTO_CS03_PLAYED");
            PlayerPrefs.DeleteKey("CONCERTO_CS04_PLAYED");
            PlayerPrefs.DeleteKey("CONCERTO_SECTOR_EXTERNO_UNLOCKED");
            PlayerPrefs.DeleteKey("CONCERTO_EAST_GATE_UNLOCKED");
            PlayerPrefs.DeleteKey("CONCERTO_ACT2_COMPLETED");
            PlayerPrefs.DeleteKey("CONCERTO_ACT3_COMPLETED");
            PlayerPrefs.Save();
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

            // 4. Loop de pecas do Ato 1 & Economia/Upgrades
            var parts = PartsLoopManager.Instance;
            if (parts != null)
            {
                data.partsCollectedMask = parts.GetCollectedMask();
                data.canCollectParts    = parts.CanCollectParts;
                data.act1Completed      = parts.Act1Completed;
            }
            var econ = EconomyUpgradeSystem.Instance;
            if (econ != null)
            {
                data.partsCurrencyEarned = econ.PartsEarned;
                data.partsCurrencySpent  = econ.PartsSpent;
                if (data.purchasedUpgradeIds == null)
                    data.purchasedUpgradeIds = new System.Collections.Generic.List<string>();
                data.purchasedUpgradeIds.Clear();
                econ.CopyPurchasedUpgradesTo(data.purchasedUpgradeIds);
            }

            // 4c. Re-Sincronia — persiste estado de cada zona ativa no JSON versionado (GATE 4)
            if (data.resyncZoneStates == null) data.resyncZoneStates = new System.Collections.Generic.List<string>();
            data.resyncZoneStates.Clear();
            var resyncZones = ResyncZoneController.AllZones;
            for (int i = 0; i < resyncZones.Count; i++)
            {
                var z = resyncZones[i];
                if (z != null && !string.IsNullOrEmpty(z.zoneId))
                    data.resyncZoneStates.Add($"{z.zoneId}:{(int)z.currentState}");
            }

            // 4d. Hub A Orquestra & Linha do Equilibrio (GATE 5)
            var hub = HubOrquestraController.Instance;
            if (hub != null)
            {
                if (data.restoredFusoIds == null) data.restoredFusoIds = new System.Collections.Generic.List<string>();
                if (data.appliedBalanceEvents == null) data.appliedBalanceEvents = new System.Collections.Generic.List<string>();
                hub.CopyStateToSave(data.restoredFusoIds, data.appliedBalanceEvents);
            }

            // 4e. Fuso Vitralha — beats, boss, reliquia Ponteiro Maior e consequencia (GATE 7 & 8)
            var vit = VitralhaFusoController.Instance;
            if (vit != null)
                vit.CopyToSave(data);

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

            WriteAtomic(SaveFilePath, data, rotateBackups: true);
            _lastLoadedOrSaved = data;
            OnGameSaved?.Invoke(data);
            Debug.Log($"[SaveSystem] Jogo salvo atomicamente em {SaveFilePath} (v{data.schemaVersion}, sha256={data.payloadChecksum.Substring(0, 8)}..., cp={data.currentCheckpointId}, obj='{data.currentObjective}')");
            return data;
        }

        public static void Delete()
        {
            _lastLoadedOrSaved = null;
            _pendingContinueLoad = false;
            _wasLastLoadRecoveredFromBackup = false;
            _lastRecoveredSlotName = string.Empty;
            _pendingRecoveryNotice = string.Empty;
            try
            {
                if (File.Exists(SaveFilePath)) File.Delete(SaveFilePath);
                if (File.Exists(Backup1FilePath)) File.Delete(Backup1FilePath);
                if (File.Exists(Backup2FilePath)) File.Delete(Backup2FilePath);
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
            if (EconomyUpgradeSystem.Instance != null)
                EconomyUpgradeSystem.Instance.RestoreFromSave(data.partsCurrencyEarned, data.partsCurrencySpent, data.purchasedUpgradeIds);

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

            // 6. Re-Sincronia — restaura estado salvo de cada zona (GATE 4)
            if (data.resyncZoneStates != null)
            {
                for (int i = 0; i < data.resyncZoneStates.Count; i++)
                {
                    string entry = data.resyncZoneStates[i];
                    if (string.IsNullOrEmpty(entry)) continue;
                    int sep = entry.IndexOf(':');
                    if (sep <= 0 || sep >= entry.Length - 1) continue;
                    string zId = entry.Substring(0, sep);
                    if (int.TryParse(entry.Substring(sep + 1), out int stateInt))
                    {
                        var z = ResyncZoneController.FindZoneById(zId);
                        if (z != null)
                            z.ApplySavedStateImmediate((ResyncState)stateInt);
                    }
                }
            }

            // 7. Hub A Orquestra & Linha do Equilibrio (GATE 5)
            if (HubOrquestraController.Instance != null)
                HubOrquestraController.Instance.RestoreStateFromSave(data.restoredFusoIds, data.appliedBalanceEvents);

            // 8. Fuso Vitralha — restaura beats, reliquia Ponteiro Maior e estado do rio
            if (VitralhaFusoController.Instance != null)
                VitralhaFusoController.Instance.RestoreFromSave(data);

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

        /// <summary>
        /// GATE 1 (Escrita Atomica) + GATE 3 (Rotacao de 3 Slots) + GATE 4 (Checksum SHA-256).
        /// Grava em .tmp com Flush(true), valida o arquivo temporario, rotaciona bak1->bak2 e atual->bak1,
        /// e so entao renomeia .tmp para o destino final.
        /// </summary>
        private static void WriteAtomic(string path, SaveGameData data, bool rotateBackups = true)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            data.schemaVersion = SaveGameData.CurrentSchemaVersion;
            data.payloadChecksum = ComputeChecksum(data);

            string json = JsonUtility.ToJson(data, prettyPrint: true);
            string tmpPath = path + ".tmp";

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.Write(bytes, 0, bytes.Length);
                fs.Flush(flushToDisk: true);
            }

            // Verifica integridade do .tmp antes de tocar no save atual
            if (!TryLoadSlot(tmpPath, out _, out string tmpErr, out _))
                throw new IOException($"Falha na verificacao pos-escrita do arquivo temporario: {tmpErr}");

            // Rotaciona 3 slots somente se o save atual existir e estiver integro
            if (rotateBackups && File.Exists(path) && TryLoadSlot(path, out _, out _, out _))
            {
                string bak1 = BuildSlotPath(path, ".bak1");
                string bak2 = BuildSlotPath(path, ".bak2");
                if (File.Exists(bak1))
                {
                    if (File.Exists(bak2)) File.Delete(bak2);
                    File.Move(bak1, bak2);
                }
                File.Move(path, bak1);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }

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
