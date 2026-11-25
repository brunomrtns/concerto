# SAVE_SYSTEM — Save à Prova de Corrupção (CONCERTO)

Arquitetura de persistência resiliente a queda de energia, crash, escrita parcial, adulteração e evolução de schema em CONCERTO.

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Formato e local:** JSON versionado (`schemaVersion = 3`) gravado em `Application.persistentDataPath/concerto_save.json`.
- **`PlayerPrefs`:** Reservado exclusivamente para preferências do jogador (`GameSettingsManager`: volume, fullscreen, qualidade, sensibilidade de câmera, reduzir shake). Quaisquer chaves legadas de gameplay (`CONCERTO_POWERS_UNLOCKED`, `CONCERTO_CS03_PLAYED`, `CONCERTO_CS04_PLAYED`, `CONCERTO_SECTOR_EXTERNO_UNLOCKED`, `CONCERTO_EAST_GATE_UNLOCKED`, `CONCERTO_ACT2_COMPLETED`, `CONCERTO_ACT3_COMPLETED`) são automaticamente migradas para o JSON e removidas do `PlayerPrefs`.
- **Fallback seguro:** Save corrompido nunca crasha o jogo e nunca sobrescreve um backup íntegro.
- **Modelos 3D TripoSG criados nesta tarefa:** `0 modelos criados`.

---

## 2. Formato do JSON (`SaveGameData` — `schemaVersion = 3`)

| Grupo | Campos principais | Tipo | Descrição |
|---|---|---|---|
| **Metadados & Integridade** | `schemaVersion` | `int` (`3`) | Versão do schema para migração segura. |
| | `savedAtUtc` | `string` (ISO-8601) | Timestamp UTC da gravação. |
| | `payloadChecksum` | `string` (64 hex) | **SHA-256** calculado sobre o JSON canônico com `payloadChecksum = ""`. |
| **Cena & Checkpoint** | `sceneName`, `playerPosition`, `playerYaw` | `string` / `SaveVec3` / `float` | Cena ativa e transform do Dez. |
| | `currentCheckpointId`, `checkpointPosition`, `checkpointYaw` | `string` / `SaveVec3` / `float` | Último checkpoint para respawn de knockdown. |
| **Progressão & Missões** | `currentObjective`, `partsCollectedMask`, `canCollectParts`, `act1Completed` | `string` / `int` / `bool` | Estado do Ato 1 e objetivo ativo. |
| | `completedDialogueIds` | `List<string>` | Diálogos concluídos (correção `BL-001`, evita reset de objetivo). |
| | `onboardingCompleted`, `onboardingSkipped`, `onboardingStepIndex`, `completedOnboardingSteps` | `bool` / `int` / `List<string>` | Progresso completo do onboarding. |
| **Ato 2, Ato 3 & Portas** | `cs01Played`..`cs04Played`, `sectorDoorUnlocked`, `signalQuestStep`, `eastGateUnlocked` | `bool` / `int` | Flags narrativas e portões. |
| | `act2AnomaliesActive`, `resolvedAnomalyIds`, `act2Completed`, `act3Completed` | `bool` / `List<string>` | Anomalias resolvidas e conclusão dos atos. |
| **Ferramenta & Poderes** | `energyCells`, `energyBar`, `activeToolModeId`, `powersUnlocked`, `unlockedPowerIds` | `int` / `float` / `string` / `bool` / `List<string>` | Estado da ferramenta multi-uso e poderes temporais (#93). |

---

## 3. Escrita Atômica (GATE 1) + Checksum SHA-256 (GATE 4) + Regra dos 3 Slots (GATE 3)

### Escrita Atômica + Rotação
1. `SaveSystem.WriteAtomic(...)` assina o objeto calculando `payloadChecksum = ComputeChecksum(data)` (**SHA-256** em UTF-8 sobre o JSON canônico).
2. Grava primeiro em `concerto_save.json.tmp` usando `FileStream` com `Flush(flushToDisk: true)` — **nunca escreve diretamente sobre o save bom**.
3. Re-lê e valida o `.tmp` (parse JSON + `VerifyChecksum`) antes de tocar em qualquer slot existente.
4. **Rotação dos 3 slots** (executada **somente** se o slot atual existir e estiver íntegro, para que um arquivo corrompido nunca sobrescreva um backup válido):
   - **Slot 2 (mais antigo):** `concerto_save.bak2.json` ← recebe o antigo `bak1`
   - **Slot 1 (anterior):** `concerto_save.bak1.json` ← recebe o antigo `atual`
   - **Slot 0 (atual):** `concerto_save.json` ← recebe o `.tmp` validado via `File.Move`

### Recuperação Automática e Aviso ao Jogador
Ao carregar (`SaveSystem.TryLoad`):
1. Tenta ler e validar **Slot 0** (`concerto_save.json`) com verificação de `schemaVersion` e **SHA-256**.
2. Se o Slot 0 estiver corrompido (JSON truncado, arquivo vazio ou checksum adulterado):
   - Move o arquivo corrompido para quarentena (`concerto_save.json.corrupt.bak`).
   - Tenta automaticamente **Slot 1** (`bak1`) e, se falhar, **Slot 2** (`bak2`).
   - Ao recuperar um backup válido, restaura-o atomicamente no Slot 0, marca `WasLastLoadRecoveredFromBackup = true`, dispara `SaveSystem.OnBackupRecovered` e exibe o aviso ao jogador via `SaveCheckpointHUD` (`"! BACKUP RECUPERADO (bak1)"`).
3. Somente se todos os 3 slots estiverem indisponíveis/corrompidos, aplica fallback seguro para Novo Jogo sem crash.

---

## 4. Como a Migração de Versões Antigas Funciona (GATE 2)

`SaveGameData.MigrateIfNeeded()` aceita qualquer `schemaVersion` entre `1` e `CurrentSchemaVersion` (`3`):
- **`v1 → v2`:** infere `powersUnlocked = true` se `unlockedPowerIds` não estiver vazio e importa flags legados de `PlayerPrefs` (`CONCERTO_POWERS_UNLOCKED`, `CONCERTO_CS03_PLAYED`).
- **`v2 → v3`:** inicializa listas novas com defaults seguros (`completedDialogueIds`, `completedOnboardingSteps`), importa quaisquer flags restantes de `PlayerPrefs` (`cs04`, portas, `act2`/`act3`), limpa essas chaves do `PlayerPrefs` e, no `TryLoad`, regrava imediatamente o arquivo em `v3` assinado com **SHA-256** sem perder nenhum campo anterior.

---

## 5. Evidências e Testes Automatizados (GATES 5, 6 e 8)

- **MP4 de Prova (GATE 6):** `captures/save/save_recuperacao_backup.mp4` (`1920x1080`, H.264, `8.50s`) — jogo salvando em `Caldeiral` (3 slots + SHA-256) → `concerto_save.json` corrompido na mão → reabertura via `MainMenu → Continuar` → recuperação automática do `bak1` com aviso ao jogador.
- **Testes Smoke (`Tests/SmokeTest/ConcertoSmokeTests.cs`):**
  - `Smoke_06_VersionedJsonSaveWriteAndReloadIntegrity` (gravar → carregar → estado íntegro)
  - `Smoke_10_CorruptSaveRecoversFromBackupSlotWithoutCrash` (adulteração SHA-256 + truncamento JSON → recuperação automática de `bak1` sem crash)
  - `Smoke_11_LegacySaveVersionMigratesWithoutDataLoss` (`schemaVersion = 1` → migração para `v3` + SHA-256 preservando todos os dados)
- **Resultado da Suíte `Tests/SmokeTest/`:** **11/11 PASSED** (`0 failed`, `0 skipped`, `0 warnings CS`, `0 errors CS`) + `SaveCorruptionRecoveryCaptureTest` (**12/12 PASSED**).
