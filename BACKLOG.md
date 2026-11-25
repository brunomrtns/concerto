# BACKLOG VIVO — CONCERTO (MODO AUTOPILOTO)

**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0
**Regras Permanentes ativas:**
1. Fila histórica `#93 → #94 → #102 → #103 → #109 → #106` respeitada (todas já concluídas na `main`).
2. `Tests/SmokeTest/` **7/7 VERDE** antes de TODO commit.
3. Modelos 3D novos: **EXCLUSIVAMENTE TripoSG** (com lista no `.MD`).
4. Textura de modelo TripoSG pode ser reprocessada (Topaz / ESRGAN) sem alterar malha, anotada no `.MD`.
5. Escopo fechado por item, conformidade `DESIGN_BIBLE.md`, evidência em `/captures/`, commit + push por item, máx. 5 itens por sessão.

---

## Itens Abertos / Em Andamento (Prioridade: Crítico → Médio → Baixo)

### `BL-001` — Bug crítico: missão/objetivo reseta ou troca indevidamente ao falar com NPC
- **Severidade:** `CRÍTICO`
- **Status:** `EM_ANDAMENTO`
- **Descrição:** `DialogueController.EndConversation()` aplica `ObjectiveSystem.Set(_loc.Format(postObjKey, partsTotal))` incondicionalmente toda vez que uma conversa termina. Se o jogador já coletou peças (`k/N`), já concluiu o Ato 1, ou está no Ato 2/Setor Externo e fala novamente com a Juno (ou qualquer NPC com `postObjectiveKey`), o objetivo atual no HUD é sobrescrito/resetado para um objetivo antigo (ex.: `"Encontre as peças da máquina (0/3)"` ou `"Ato 1 concluído"`).
- **Critério de pronto:**
  1. `DialogueController` rastreia conversas já concluídas (`_completedDialogueIds`) e só aplica `postObjectiveKey` na primeira conclusão E quando a progressão ainda não superou aquele marco (nunca sobrescreve progresso de coleta `k/N`, Ato 2 ou etapas posteriores).
  2. Teste PlayMode automatizado prova que re-conversar com Juno/NPCs durante coleta (`2/5`), após Ato 1 e durante Ato 2 mantém o objetivo intacto.
  3. `0 warnings CS`, `Tests/SmokeTest/` 7/7 verde, evidência em `captures/backlog/bl001_objetivo_intacto.png`, commit + push.

### `BL-002` — HUD Overlap superior esquerdo (`QA-01`)
- **Severidade:** `MÉDIO`
- **Status:** `ABERTO`
- **Descrição:** Reportado em `QA_AUTOMATIZADO.md` (`QA-01`): em `1280x720`, os 4 segmentos de HP (`PlayerHealthController`) sobrepõem diretamente o texto `"OBJETIVO / Fale com Juno"` no canto superior esquerdo.
- **Critério de pronto:** Separar verticalmente a barra de HP e o banner de Objetivo sem nenhum pixel de sobreposição em `1280x720`, evidência visual em `captures/backlog/bl002_hud_top_left_depois.png`, `0 warnings CS`, `Tests/SmokeTest/` 7/7 verde, commit + push.

### `BL-003` — HUD Overlap inferior direito (`QA-02`)
- **Severidade:** `MÉDIO`
- **Status:** `ABERTO`
- **Descrição:** Reportado em `QA_AUTOMATIZADO.md` (`QA-02`): em `1280x720`, o painel `[TAB] MODO: CONCERTAR / CÉLULAS` (`MultiToolController`) sobrepõe parcialmente o indicador `ECO [Q] / PRONTO` (`DezPowersController`) no canto inferior direito.
- **Critério de pronto:** Reposicionar o HUD de Poderes (`PowersHUD_Canvas`) acima ou à esquerda limpa do painel da ferramenta sem nenhum pixel de sobreposição em `1280x720`, evidência visual em `captures/backlog/bl003_hud_bottom_right_depois.png`, `0 warnings CS`, `Tests/SmokeTest/` 7/7 verde, commit + push.

### `BL-004` — Modelos 3D faltantes das peças 4 e 5 do Ato 1 (TripoSG exclusivo)
- **Severidade:** `MÉDIO`
- **Status:** `ABERTO`
- **Descrição:** Em `parts_loop_config.json`, `part_04` (*Condensador de Fluxo*) e `part_05` (*Estabilizador Orbital*) ainda reutilizam os FBXs de `part_bobina_fase` e `part_nucleo_ressonancia` porque `Art/Generated/parts/` possui apenas 3 FBXs (`part_nucleo_ressonancia`, `part_bobina_fase`, `part_regulador_crono`).
- **Critério de pronto:** Gerar os 2 modelos faltantes **exclusivamente via TripoSG** (`part_condensador_fluxo.fbx` e `part_estabilizador_orbital.fbx`), vincular em `parts_loop_config.json` e na cena `Caldeiral.unity`, validar PBR + evidência visual + `Tests/SmokeTest/` 7/7 verde.

### `BL-005` — Menu de Pause × Playcaller (`QA-03`)
- **Severidade:** `MÉDIO`
- **Status:** `ABERTO`
- **Descrição:** Reportado em `QA_AUTOMATIZADO.md` (`QA-03`): no 2º ciclo de abrir/fechar Pause via `Escape` durante gravação no Unity Recorder, comandos subsequentes do Playcaller deram timeout.
- **Critério de pronto:** Diagnosticar interação `PauseMenuController` (`Time.timeScale = 0`) × input externo, corrigir sem quebrar o pause normal do jogador, validar 3+ ciclos de Pause via Playcaller + `Tests/SmokeTest/` 7/7 verde.

### `BL-006` — Layout HUD responsivo em ultrawide (`QA-05`)
- **Severidade:** `BAIXO`
- **Status:** `ABERTO`
- **Descrição:** Reportado em `QA_AUTOMATIZADO.md` (`QA-05`): em aspect ratios ultrawide (`21:9` / `2359x718`), validar `CanvasScaler` (`ScaleWithScreenSize`, `matchWidthOrHeight`) dos HUDs para manter margens consistentes sem sobreposição.
- **Critério de pronto:** Todos os HUDs escalando proporcionalmente em `1920x1080` e `2560x1080` sem cortes nem sobreposição + screenshot de prova + `Tests/SmokeTest/` 7/7 verde.

---

## Histórico de Itens Concluídos (`FEITO`)

| ID | Título | Severidade | Status | Data | Commit |
|---|---|---|---|---|---|
| `#93` | Poderes do Dez: Eco Temporal + Estase | Fila | `FEITO` | 2026-11-24 | `8949c9a` |
| `#94` | Manifestações: Estilhaço + Flutuante (TripoSG) + HP/Knockdown | Fila | `FEITO` | 2026-11-24 | `1ce2dd8` |
| `#102` | Fase nova "O Epicentro" (TripoSG + puzzle Estase) | Fila | `FEITO` | 2026-11-24 | `7f1c08c` |
| `#103` | Mini-boss "Guardião da Fenda" (TripoSG + cartão fim de ato) | Fila | `FEITO` | 2026-11-24 | `7aac136` |
| `#109` | Combate v2 (3-hit combo, Eco cancel, Absorver parry, Égide TripoSG, tokens) | Fila | `FEITO` | 2026-11-24 | `f21e428` |
| `#106` | Varredura de bugs de ponta a ponta (4 atos — 3 fixes P0) | Fila | `FEITO` | 2026-11-24 | `c793e03` |
| `REV-93/94` | 9 correções pós-review #93/#94 (VFX URP, Save v2, PlayerRegistry, swing real, LOS, knockback, MPB) | Crítico | `FEITO` | 2026-11-24 | `fca3427` |
| `QA-F1` | QA Automatizado — Fase 1 (Playcaller MCP, SmokeTest 7/7, sessão 2min MP4, `QA_AUTOMATIZADO.md`) | Infra | `FEITO` | 2026-11-24 | `6d46986` |
| `PERF-01` | Pass de Performance (-19% a -46% ms/frame, NonAlloc physics, Camera cache, HUD dirty-check) | Médio | `FEITO` | 2026-11-24 | `bf1f859` |
| `ANIM-01` | Polimento de animação do Dez (braços afastados, arma em RightHand + swing/coice, mantle 3 fases, pouso correndo sem deslize, passos NPC sincronizados) | Médio | `FEITO` | 2026-11-24 | `b633c5d` |
