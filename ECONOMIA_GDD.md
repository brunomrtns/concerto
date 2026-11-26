# ECONOMIA CANON — SEGUNDOS, TIO HORÁRIO, MEMÓRIAS-ECO E REMENDOS (`GDD 03 §4` + `04 §3`)

## Declaração de Conformidade
- **`docs/DESIGN_BIBLE.md` & GDD `03 §4` / `04 §3`:** 100% conforme — moeda **Segundos** (*fios de tempo* dourados), 12 melhorias com efeito real no gameplay, 21 Memórias-Eco com gate de epílogo estendido, 3 Remendos puramente cosméticos, nenhum conteúdo exige compra específica e migração automática de Save `v3 → v4` sem perda de progresso.
- **Regra TripoSG-Only:** Modelos da loja do Tio Horário e do Fio de Tempo gerados exclusivamente via TripoSG + decimação Blender em `Assets/Game/Art/Generated/economia/`.

---

## Modelos 3D TripoSG

| # | Peça | Arquivo FBX TripoSG | Uso | Orçamento Tris |
|---|------|---------------------|-----|----------------|
| 1 | **Posto / Loja do Tio Horário** | `tio_horario_shop_booth.fbx` (`seed 501`) | Bancada/quiosque relojoeiro da loja de melhorias e remendos | ≤ 4.500 tris |
| 2 | **Fio de Tempo (Segundo)** | `fio_de_tempo_segundo.fbx` (`seed 502`) | Coletável dourado de Segundos espalhado nos fusos | ≤ 900 tris |

---

## Loja do Tio Horário — 12 Melhorias + Curva de Segundos (GATE 2 & GATE 5)

| # | ID | Melhoria | Custo (Segundos) | Efeito Real no Gameplay |
|---|----|----------|------------------|-------------------------|
| 1 | `upg_fusivel_1` | Fusível +1 (I) | 60 | `+1` Fusível máximo |
| 2 | `upg_fusivel_2` | Fusível +1 (II) | 100 | `+1` Fusível máximo |
| 3 | `upg_pressao_1` | Pressão +25 (I) | 50 | `EffectiveMaxPressure +25` (`PressureResourceSystem`) |
| 4 | `upg_pressao_2` | Pressão +25 (II) | 80 | `EffectiveMaxPressure +25` |
| 5 | `upg_pressao_3` | Pressão +25 (III) | 120 | `EffectiveMaxPressure +25` |
| 6 | `upg_faisca_dupla` | Faísca Dupla | 140 | `ProjectilesPerShot = 2` (`FaiscaRangedController`) |
| 7 | `upg_sopro_ar` | Sopro no Ar | 90 | Libera dash aéreo |
| 8 | `upg_contratempo_largo` | Contratempo Largo (+0,06s) | 110 | `parryWindowSec: 0,18s → 0,24s` (`ParryAndLockOnController`) |
| 9 | `upg_rebote_mola` | Rebote Extra em Mola | 95 | Carga extra de Rebote em molas |
| 10 | `upg_garra_alcance` | Garra +4m | 105 | `grappleRangeM: 12m → 16m` (`DezMovementConfig`) |
| 11 | `upg_pressao_melee` | Cadência Melee (+10p) | 85 | `EffectiveMeleeGain: 30 → 40` (`PressureResourceSystem`) |
| 12 | `upg_afinacao_rapida` | Afinação Rápida | 95 | Finalizador CONCERTO devolve `+15 Pressão` |

### Conta Exata da Curva Econômica
- **Total das 12 melhorias:** `1.130 Segundos`
- **2 Remendos comprados (cosméticos):** `70 + 90 = 160 Segundos` (`remendo_latao_dourado`, `remendo_azul_ecodia`)
- **3º Remendo (`remendo_mestre_relojoeiro`):** `0 Segundos` (desbloqueado por conquista de coleta: `≥ 10` Memórias-Eco — só visual)
- **Custo total para comprar 100% da loja:** `1.290 Segundos`
- **Total de Segundos distribuídos no jogo (Caldeiral + 4 Fusos + Corona + Bosses + Micro-Quests):** `1.440 Segundos`
- **Margem de sobra:** `+150 Segundos` (`+11,6%`) — nenhum conteúdo exige compra específica e o jogador completa tudo com margem limpa.

---

## 21 Memórias-Eco & Migração de Save `v3 → v4` (GATE 3 & Regra 3)
- **21 fragmentos:** 3 por fuso (`Caldeiral`, `Vitralha`, `Marésia`, `Ecodia`, `Retrógrado`, `Orquestra` = 18) + 3 reservados à `Corona` = **21 Memórias-Eco**. Coletar `21/21` ativa `extendedEpilogueUnlocked = true`.
- **Migração de Save (`SaveGameData.CurrentSchemaVersion = 4`):** Saves antigos (`schemaVersion < 4`) convertem automaticamente `partsCurrencyEarned` e `partsCurrencySpent` para `segundosEarned` e `segundosSpent` na taxa **1 peça = 10 Segundos**, sem perda de progresso.

---

## Evidências & Validação
- **Vídeo da Economia & Loja:** `/captures/economia/economia_tio_horario.mp4`
- **SmokeTest automatizado:** `Smoke_33_EconomiaSegundosTioHorarioUpgradesMemoriasEcoRemendosAndSaveMigration` (`33/33 PASSED`, `0 errors CS`, `0 warnings CS`).
