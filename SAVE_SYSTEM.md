# SAVE SYSTEM — Save Real Versionado (JSON) & Checkpoints

## 1. Arquitetura (Fonte Única de Verdade)
- **Local do arquivo:** `Application.persistentDataPath/concerto_save.json` (escrita atômica via `.tmp` → destino).
- **Regra inviolável:** **ZERO estado de jogo em `PlayerPrefs`** (`PlayerPrefs` fica reservado exclusivamente às opções gráficas/áudio/sensibilidade de `GameSettingsManager`).
- **API Central (`Studio.Game.Save.SaveSystem`):**
  - `SaveSystem.Save(checkpointId, checkpointPos, checkpointYaw)` — captura todo o estado vivo e grava em disco.
  - `SaveSystem.Load()` / `SaveSystem.TryLoad(out SaveGameData)` — lê, valida versão e migra.
  - `SaveSystem.HasSave()` — checa se existe save válido (corrompido retorna `false`).
  - `SaveSystem.Delete()` — apaga o save (usado em **Novo Jogo**).
  - `SaveSystem.RespawnPlayerAtLastCheckpoint()` — devolve o Dez ao último checkpoint em caso de knockdown de combate, preservando todo o estado do mundo.

## 2. Inventário Completo do Schema (`schemaVersion = 1`)

| Grupo | Campos Salvos |
|---|---|
| **Metadados** | `schemaVersion` (`1`), `savedAtUtc` |
| **Cena & Jogador** | `sceneName`, `playerPosition (x,y,z)`, `playerYaw` |
| **Checkpoint / Knockdown** | `currentCheckpointId`, `checkpointPosition (x,y,z)`, `checkpointYaw` |
| **Objetivo & Ato 1** | `currentObjective`, `partsCollectedMask`, `canCollectParts`, `act1Completed` |
| **Cutscenes** | `cs01Played`, `cs02Played`, `cs03Played` |
| **Ato 2 (Anomalias & Reparo)** | `act2AnomaliesActive`, `resolvedAnomalyIds[]`, `act2Completed` |
| **Ferramenta & Poderes** | `energyCells`, `energyBar`, `activeToolModeId`, `unlockedPowerIds[]` |

## 3. Como Adicionar um Campo Novo Sem Quebrar Saves Antigos
1. Adicione o novo campo serializável em `SaveGameData.cs` com um **valor default seguro** (ex: `public int playerMaxHp = 4;`).
2. Se houver mudança estrutural (ex: renomear ou converter formato), incremente `SaveGameData.CurrentSchemaVersion` (`1 -> 2`) e adicione o bloco `if (schemaVersion < 2) { ... }` dentro de `SaveGameData.MigrateIfNeeded()`.
3. Saves v1 continuam carregando normalmente sem perder progresso.

## 4. Fallback Seguro para Arquivo Corrompido ou Versão Incompatível (Gate 3)
Se `concerto_save.json` estiver truncado, com JSON inválido ou `schemaVersion` fora de `1..CurrentSchemaVersion`:
- `TryLoad` move o arquivo quebrado para `concerto_save.json.corrupt.bak`, dispara `OnCorruptSaveFallback`, retorna `false` e o fluxo cai de forma segura em **Novo Jogo** — nunca crasha e nunca trava em tela preta.

## 5. Onde Ficam os Checkpoints (`Caldeiral.unity`)
- **Por evento-chave (`SaveCheckpointHUD`):** pós-Cutscene 1 (`cp_pos_cs01_oficina`), entrega de peças (`cp_ato1_pecas_entregues`), pós-Cutscene 2 (`cp_pos_cs02_viagem`), pós-Cutscene 3 (`cp_pos_cs03_retorno`), a cada anomalia reparada (`cp_ato2_reparo_k_N`) e fim do Ato 2 (`cp_fim_ato2`).
- **Por entrada de área (`CheckpointTrigger`):** `CP_cp_oficina_bancada` `(-28.5, 0.8, -18.5)`, `CP_cp_oficina_maquina` `(-26.2, 0.8, -16.8)` e `CP_cp_oficina_passarela` `(-18.5, 0.8, -18.5)`.
- **Feedback visual:** pílula discreta **`● SALVO`** por `1.0s` no canto inferior esquerdo (`SaveIndicator_Canvas`), sem tela de loading.
