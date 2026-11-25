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
| `BL-001` | Bug crítico: missão/objetivo reseta ou troca indevidamente ao falar com NPC | Crítico | `FEITO` | 2026-11-25 | `24bbf37` |
| `BL-002` | HUD Overlap superior esquerdo (`QA-01` — HP vs Objetivo) | Médio | `FEITO` | 2026-11-25 | `c9a8fe9` |
| `BL-003` | HUD Overlap inferior direito (`QA-02` — ToolHUD vs Poderes ECO/ESTASE) | Médio | `FEITO` | 2026-11-25 | `bfb6f9a` |
| `BL-004` | Modelos 3D faltantes (peças 4 e 5 + Fragmento de Núcleo + Célula de Energia + Estação de Parry — TripoSG exclusivo) | Médio | `FEITO` | 2026-11-25 | *(lote 1)* |
