# FUSO VITRALHA — FLORESTA DE VIDRO COMPLETA (GDD 03 §2.2 + 05 §4)

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0 · [docs/gdd/03-mundo-fusos.md](docs/gdd/03-mundo-fusos.md) §2.2 · [docs/gdd/05-level-design.md](docs/gdd/05-level-design.md) §4
**Data:** 2026-11-26
**Cena:** [Vitralha.unity](unity/Assets/Game/Scenes/Vitralha.unity) (`400 × 400 m`, grid `2 m`)

---

## 1. Declaração de Conformidade (`DESIGN_BIBLE.md` + `GDD 03 §2.2` + `GDD 05 §4`)

- **Kit Modular TripoSG (Regra 2 & GDD 05 §2):** CONFORME — 8 peças geradas **exclusivamente via TripoSG** em `Assets/Game/Art/Generated/vitralha/`, todas dentro do orçamento de triângulos (`chão ≤ 600`, `paredão ≤ 1.500`, `hero piece ≤ 12.000`), com geometria idêntica e 2 variantes de material (`Mat_Vitralha_Desincronizado` azul-verde frio / `Mat_Vitralha_Restaurado` âmbar-primavera quente).
- **7 Beats Exatos (GDD 05 §4):** CONFORME — Entrada pela Estação de Luz → Bosque dos Pássaros-Vidro → Mirante do Rio Parado → Cachoeira de Vidro → Boss A Estufa Viva → Relíquia Ponteiro Maior → Consequência (rio volta e alaga Marésia).
- **Boss Design (GDD 05 §7):** CONFORME — Arena muda de estado no meio da luta (Fase 2), Contratempo (parry) quebra a guarda refratada da Fase 2, Posto de Reparo dentro da arena com reinício `≤ 8s`.
- **Relíquia Ponteiro Maior → Sopro Estendido (GDD 04 §1):** CONFORME — Dash `14 m` em `0,30 s` atravessando feixes de luz longos (`HasRelicPonteiroMaior` + `IsExtendedSoproActive`).
- **Persistência & QA:** CONFORME — Checkpoints e save por beat em JSON versionado (`SaveGameData`), `0 warnings CS`, `0 errors CS`, suíte `Tests/SmokeTest/` **28/28 VERDE**.

---

## 2. Kit Modular Vitralha — Modelos 3D TripoSG (8 Peças, 2 Estados)

*Todos gerados localmente via `triposg_runner.py` (`tools/vitralha/run_vitralha_kit_triposg.sh`) e decimados abaixo do orçamento GDD 05 §2:*

| # | Slug / FBX (`Assets/Game/Art/Generated/vitralha/`) | Papel no Kit | Orçamento GDD 05 §2 | Variantes de Estado |
|---|---|---|---|---|
| 1 | `vit_floor_tile.fbx` | Chão / plataforma modular (grid 2m) | `≤ 600 tris` (`≤ 540`) | Desincronizado / Restaurado |
| 2 | `vit_wall_prism.fbx` | Paredão prisma-catedral (wall-run) | `≤ 1.500 tris` (`≤ 1.350`) | Desincronizado / Restaurado |
| 3 | `vit_light_bridge.fbx` | Feixe/ponte de luz refratada (Re-Sincronia) | `≤ 900 tris` (`≤ 780`) | Bloqueia (Desync) / Atravessável (Restaurado) |
| 4 | `vit_glass_tree.fbx` | Árvore-catedral de vidro soprado | `≤ 1.500 tris` (`≤ 1.380`) | Desincronizado / Restaurado |
| 5 | `vit_resync_node.fbx` | Nó de Re-Sincronia (catraca-prisma) | `≤ 800 tris` (`≤ 720`) | Desincronizado / Restaurado |
| 6 | `vit_waterfall_arch.fbx` | Arco da Cachoeira de Vidro | `≤ 1.500 tris` (`≤ 1.380`) | Desincronizado / Restaurado |
| 7 | `vit_glass_bird_platform.fbx` | Pássaro-vidro congelado (plataforma viva) | `≤ 600 tris` (`≤ 540`) | Desincronizado / Restaurado |
| 8 | `vit_hero_estufa_viva.fbx` | Hero piece — Boss **A Estufa Viva** | `≤ 12.000 tris` (`≤ 9.500`) | Fase 1 / Fase 2 (Parry) / Fase 3 |

---

## 3. Os 7 Beats, Checkpoints e Consequência

| Beat | Checkpoint ID | Descrição |
|---|---|---|
| 1 | `vit_b1_estacao_luz` | Entrada pela Estação de Luz — feixe refratado atravessável só no Restaurado via `ResyncZoneController`. |
| 2 | `vit_b2_bosque_passaros` | Bosque dos Pássaros-Vidro — árvores-catedral e plataformas vivas de pássaros-vidro (`Rebote`). |
| 3 | `vit_b3_mirante_rio` | Mirante do Rio Parado — vista narrativa elevada sobre a floresta congelada. |
| 4 | `vit_b4_cachoeira_vidro` | Cachoeira de Vidro — escalada por arcos cristalinos (`Rebote + Sopro`). |
| 5 | `vit_b5_boss_estufa_viva` | Boss **A Estufa Viva** — arena muda de estado na Fase 2, Contratempo quebra guarda refratada, Posto de Reparo (`reinício ≤ 8s`). |
| 6 | `vit_b6_reliquia_ponteiro` | Relíquia **Ponteiro Maior** — libera **Sopro Estendido** (`14 m` em `0,30 s`, atravessando feixes longos). |
| 7 | `vit_b7_consequencia_rio` | Consequência — cutscene in-engine curta mostrando o rio voltando + registra `evt_cost_vitralha_flood_maresia` ("alaga Marésia") no grafo e Save JSON. |

---

## 4. Evidências (GATES 8 e 9)

- **Smoke Test:** `Smoke_28_VitralhaGoldenRouteBossRelicAndPersistence` em [ConcertoSmokeTests.cs](unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs) — **PASSED** (`28/28` suíte completa verde).
- **MP4 Rota de Ouro:** [captures/vitralha/vitralha_rota_ouro.mp4](captures/vitralha/vitralha_rota_ouro.mp4).
