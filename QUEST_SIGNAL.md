# Missão "Sinal" — Restaurar a Antena e Descobrir o Epicentro

## Visão Geral
- **Cena:** `unity/Assets/Game/Scenes/SetorExterno.unity`
- **Orquestrador:** `unity/Assets/Game/Scripts/Quests/SignalQuestController.cs`
- **Configuração Data-Driven:** `unity/Assets/Game/Quests/Data/quest_signal_config.json`
- **Diálogos (100% em arquivos JSON, zero hardcode):**
  - Intro: `unity/Assets/Game/Dialogue/Data/dlg_vera_signal_intro.json`
  - Resultado (registro corrompido `14h02m11s` → epicentro): `unity/Assets/Game/Dialogue/Data/dlg_vera_signal_result.json`
  - Textos PT-BR: `unity/Assets/Game/Dialogue/Data/strings_pt_br.json` (`sig_intro_01..04`, `sig_result_01..05`, `obj_signal_*`)
- **Interagíveis Reutilizados / Integrados:**
  - `NPC_Engenheira_Vera` (`NpcDialogueInteractable`)
  - 2 alimentadores do gerador (`sector_anom_01_gerador` e `sector_anom_03_carga` via `Anomaly` + `Repairable` + `RepairRitualController`)
  - `POI_02_Antena_TripoSG` (`AntennaCaptureInteractable` + feixe ciano + `signal_antenna_capture_TEMP.wav`)
  - `EastGate_To_Epicenter` (`EastGateInteractable`)

---

## Cadeia de Estados, Checkpoints e Flags

| Step | Estado (`SignalQuestStep`) | Ação do Jogador | Objetivo no HUD | Checkpoint (`SaveSystem`) |
|---|---|---|---|---|
| `0` | `NotStarted` | Falar com Engenheira Vera (`dlg_vera_signal_intro.json`) | `"Fale com Vera"` | *(pós-CS04)* |
| `1` | `RestoreFeeders` | Reparar 2 alimentadores (`sector_anom_01_gerador`, `sector_anom_03_carga`) | `"Restaure o alimentador do gerador (k/2)"` | `cp_signal_step1_feeders` |
| `2` | `CaptureAntenna` | Interagir `[E]` na `POI_02_Antena_TripoSG` (captura 2.8s + registro `14:02:11`) | `"Capture a leitura da antena"` | `cp_signal_step2_antenna_ready` |
| `3` | `ReportToVera` | Conversar com Vera (`dlg_vera_signal_result.json` — registro corrompido → epicentro) | `"Mostre a leitura para Vera"` | `cp_signal_step3_reading_captured` |
| `4` | `Completed` | Portão Leste destrava automaticamente e abre passagem | `"Chegue ao epicentro"` | `cp_signal_completed_east_gate` |

### Flags & Persistência (`SaveSystem`)
- **Flag do Portão Leste:** `EastGateInteractable.IsEastGateUnlocked` (`PlayerPrefs` key `CONCERTO_EAST_GATE_UNLOCKED`, atalho debug `F9`) + `SaveGameData.eastGateUnlocked`.
- **Estado da Missão no Save JSON:** `SaveGameData.signalQuestStep` (`0..4`).
- **Interromper e Retomar:** Ao salvar no meio (ex.: Step `2 = CaptureAntenna`) e clicar **Continuar** no `MainMenu`, `SaveSystem.ApplyLoadedStateToCurrentScene()` restaura as anomalias já resolvidas e chama `SignalQuestController.Instance.RestoreFromSave(data.signalQuestStep, data.eastGateUnlocked)`, retomando exatamente no passo salvo (diálogo da Vera, antena habilitada e Portão Leste sincronizados).

---

## Como Criar a Próxima Missão Reusando Este Padrão
1. Crie um JSON de config em `Assets/Game/Quests/Data/quest_<nome>_config.json` listando os `anomalyIds` (ou objetivos), `introDialogueId`, `resultDialogueId`, textos de objetivo e IDs de checkpoint.
2. Crie os arquivos de diálogo em `Assets/Game/Dialogue/Data/dlg_<npc>_<nome>_*.json` apontando apenas para chaves em `strings_pt_br.json` (zero hardcode em C#).
3. Crie um controlador seguindo o contrato de `SignalQuestController`:
   - Assine `DialogueController.OnConversationFinished` e `RepairRitualController.OnRepairProgressChanged`;
   - Chame `SaveSystem.Save(checkpointId)` a cada transição de passo;
   - Exponha `RestoreFromSave(int step, ...)` e conecte o campo correspondente em `SaveGameData` / `SaveSystem`.
