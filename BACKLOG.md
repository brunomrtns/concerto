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

### `BL-007` — Substituir arquitetura greybox do Setor Externo por kit TripoSG + PBR (`AUDITORIA_VISUAL`)
- **Severidade:** `ALTO` (Custo estimado: `~4h`)
- **Status:** `ABERTO`
- **Descrição:** As paredes, torres e pátio estrutural de `SetorExterno.unity` ainda usam blocos primitivos greybox; receberam materiais PBR paliativos na auditoria visual, mas exigem kit arquitetônico externo dedicado (TripoSG).
- **Critério de pronto:** Kit arquitetônico externo TripoSG aplicado em `SetorExterno.unity` com texturas PBR coerentes, `0` erros de colisão/Z-Fighting, screenshot antes/depois + `Tests/SmokeTest/` 100% verde.

### `BL-008` — Skybox cósmico da Fenda Temporal + Portal arquitetônico TripoSG no Epicentro (`AUDITORIA_VISUAL`)
- **Severidade:** `ALTO` (Custo estimado: `~3h`)
- **Status:** `ABERTO`
- **Descrição:** `Epicentro.unity` ainda usa o skybox padrão cinza-bege do Unity e um arco-guia de entrada composto por cubos primitivos na arena do Guardião.
- **Critério de pronto:** Skybox cósmico da Fenda Temporal configurado + portal da arena substituído por modelo TripoSG dedicado, mantendo 60fps e `Tests/SmokeTest/` 100% verde.

### `BL-009` — Substituir primitivos de EnergyCell e Alvo de Treino em Caldeiral Z1 por malhas TripoSG (`AUDITORIA_VISUAL`)
- **Severidade:** `MÉDIO` (Custo estimado: `~2h`)
- **Status:** `ABERTO`
- **Descrição:** Em `Caldeiral.unity` (Z1 Oficina), `EnergyCell_cell_*` e `Tool_TrainingTarget_Dummy` ainda exibem primitivos lisos (cápsula ciano e cilindro laranja) em vez das malhas TripoSG texturizadas (`prop_celula_energia` / alvo mecânico).
- **Critério de pronto:** Pickups de célula e alvo de treino usando malhas TripoSG com materiais PBR, apoiados no piso (`gap = 0`) + `Tests/SmokeTest/` 100% verde.

### `BL-010` — Regenerar malha `c5_prop_sirene_mestra` (TripoSG) em Caldeiral Z4 (`AUDITORIA_VISUAL`)
- **Severidade:** `MÉDIO` (Custo estimado: `~2h`)
- **Status:** `ABERTO`
- **Descrição:** Em `Caldeiral.unity` (Z4 Câmara Central), `ENT_Sirene_Mestra_Placeholder` (`c5_prop_sirene_mestra`) apresenta facetas esticadas nas colunas laterais em close-up.
- **Critério de pronto:** Nova malha TripoSG com topologia limpa substituindo `c5_prop_sirene_mestra` mantendo escala/ancoragem + `Tests/SmokeTest/` 100% verde.

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
| `BL-005` | Menu de Pause × Playcaller (`QA-03` — ponte `PlaycallerPauseBridge`, `SimulateKeyPress` unscaled, `Smoke_19` 4 ciclos, `captures/qa/bl005_antes.mp4`) | Médio | `FEITO` | 2026-11-25 | `9c382c1` |
| `BL-006` | Layout HUD responsivo em ultrawide (`QA-05` — `HudResponsiveScaler` 1920x1080 `Expand`, `Smoke_20`, `captures/ui/bl006_ultrawide.png`) | Baixo | `FEITO` | 2026-11-25 | *(commit BL-006)* |
