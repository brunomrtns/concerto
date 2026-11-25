# BUGS — Concerto (Game Concerto)

Registro de bugs críticos de gameplay investigados, reproduzidos, corrigidos e protegidos por testes de regressão em `Tests/SmokeTest/`.

---

## BL-001 — Missão reinicia/troca ao falar com o NPC (CRÍTICO — CORRIGIDO)

- **Data:** 2026-11-25
- **Severidade:** Crítico (quebra de progressão de gameplay)
- **Modelos 3D criados nesta tarefa:** `0 modelos 3D criados`
- **Conformidade `DESIGN_BIBLE.md`:**
  - Save/estado em **JSON versionado** (`schemaVersion = 1`) em `Application.persistentDataPath` — **zero `PlayerPrefs`** para estado de missão ou diálogos concluídos.
  - Zero regressões na suíte `Tests/SmokeTest/` (**8/8 verdes**, `0 warnings CS`, `0 errors CS`).
  - Escopo fechado exclusivamente no bug de reset/troca de missão ao falar com NPC.

### 1. Sintoma
Ao iniciar a missão principal do Ato 1 (ex.: coletar peças em `01_Caldeiral_Ato1`, progresso `"Peças (2/5)"`) e falar novamente com um NPC cujo diálogo possui `postObjectiveKey` (como Juno — `dlg_juno_oficina_intro.json`, que define `"postObjectiveKey": "obj_find_parts"`), o objetivo ativo da missão era sobrescrito de volta para `"Encontre as peças da máquina (0/5)"` (reset/troca de objetivo). Além disso, ao salvar e recarregar o jogo, o histórico de diálogos concluídos era perdido, fazendo o bug reaparecer após qualquer reload.

### 2. Investigação de Causa Raiz (GATE 2)

Todas as 5 causas típicas foram verificadas individualmente antes da correção:

| # | Causa típica investigada | Resultado | Evidência no código |
|---|---|---|---|
| 1 | Trigger de diálogo re-disparando (`OnTriggerEnter`/`Stay` repetido) | **NÃO** | `NpcInteractable` exige interação explícita (`KeyCode.E`) com debounce; não dispara por `OnTriggerStay`. |
| 2 | Evento de missão inscrito 2x | **NÃO** | `DialogueController.OnConversationFinished` é inscrito 1x em `PartsLoopManager.Start()` e desinscrito em `OnDestroy()`. |
| 3 | Recarregamento de cena resetando o estado | **NÃO** | Falar com NPC não recarrega cena; ocorre in-place em `01_Caldeiral_Ato1`. |
| 4 | Estado da missão guardado em objeto destruído/desativado ao abrir o diálogo | **NÃO** | `ObjectiveSystem` (estático) e `PartsLoopManager` permanecem ativos durante o diálogo. |
| 5 | `ScriptableObject` mutado em runtime | **NÃO** | Diálogos são carregados de JSON imutável (`DialogueDatabase`); não há SO mutável controlando objetivo. |

**Causa Raiz Real (2 fatores combinados):**
1. **`DialogueController.EndConversation()`** aplicava `ObjectiveSystem.Set(_loc.Format(postObjKey, partsTotal))` **incondicionalmente** ao final de toda conversa com `postObjectiveKey`, mesmo quando o diálogo já havia sido concluído antes ou quando o jogador já possuía um objetivo mais avançado (como `"Peças (2/5)"` ou missões do Ato 2).
2. **`SaveGameData` / `SaveSystem`** não persistiam o conjunto de diálogos já concluídos (`completedDialogueIds`) no save JSON versionado, de modo que qualquer save + load limpava `_completedDialogueIds` em memória e re-habilitava a sobrescrita do objetivo.

### 3. Fix Aplicado (GATE 3)

- **[DialogueController.cs](unity/Assets/Game/Scripts/Dialogue/DialogueController.cs):**
  - Rastreamento de diálogos concluídos em `_completedDialogueIds` (`HashSet<string>`).
  - Guarda `ShouldApplyPostObjective(string dialogueId)` impedindo reaplicação de `postObjectiveKey` quando o diálogo já foi concluído ou quando já existe progresso de objetivo ativo (`PartsLoopManager` / objetivo já em andamento).
  - Métodos `CopyCompletedDialogueIdsTo(List<string>)` e `RestoreCompletedDialogues(List<string>)` para integração limpa com o sistema de save JSON.
- **[SaveGameData.cs](unity/Assets/Game/Scripts/Save/SaveGameData.cs):**
  - Adicionado campo serializável `public List<string> completedDialogueIds = new List<string>();` no JSON versionado (`SaveGameData`, `Application.persistentDataPath`, sem `PlayerPrefs`).
- **[SaveSystem.cs](unity/Assets/Game/Scripts/Save/SaveSystem.cs):**
  - `Save()` persiste `completedDialogueIds` no JSON.
  - `ApplyLoadedStateToCurrentScene()` restaura `completedDialogueIds` no `DialogueController` ao carregar o save.
- **[PartsLoopManager.cs](unity/Assets/Game/Scripts/Gameplay/PartsLoopManager.cs):**
  - Proteção contra reset do objetivo ativo quando peças já foram coletadas.

### 4. Teste de Regressão (GATE 4)

- **Arquivo:** [ConcertoSmokeTests.cs](unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs)
- **Teste:** `Smoke_08_MissionDoesNotResetAfterTalkingToNpc`
- **Fluxo verificado:**
  1. Carrega `01_Caldeiral_Ato1`, inicia missão e progride objetivo para `"Peças (2/5)"`.
  2. Executa `SaveSystem.Save()` (JSON em `Application.persistentDataPath`) e recarrega estado via `SaveSystem.Load()` + `ApplyLoadedStateToCurrentScene()`.
  3. Re-inicia e conclui diálogo com Juno (`dlg_juno_oficina_intro`).
  4. Valida que `ObjectiveSystem.CurrentObjective` permanece intacto (`"Peças (2/5)"`), sem reset nem troca, e com `0` erros de console.

### 5. Evidências em Vídeo (GATES 1 e 5)

- **ANTES (reprodução do bug — GATE 1):** `captures/bugs/missao_reset_antes.mp4` (1920x1080 H.264, 7.5s) — mostra o objetivo `"Peças (2/5)"` sendo sobrescrito de volta para `"Encontre as peças da máquina (0/5)"` ao concluir o diálogo com Juno.
- **DEPOIS (prova da correção — GATE 5):** `captures/bugs/missao_reset_depois.mp4` (1920x1080 H.264, 7.5s) — mesmo fluxo com save/reload + re-conversa com Juno mantendo `"Peças (2/5)"` intacto.

### 6. Resultado da Suíte Smoke (GATE 7)

- `Tests/SmokeTest/` (`ConcertoSmokeTests`): **8/8 PASSED** (`0 failed`, `0 skipped`, `0 warnings CS`, `0 errors CS`).
- `BacklogBL001ObjectiveNoResetTest`: **1/1 PASSED** (total da rodada: **9/9 PASSED**).
