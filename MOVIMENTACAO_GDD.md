# MOVIMENTAÇÃO GDD COMPLETA — REBOTE, CORRIMÃO, MARTELADA AÉREA & CRAVA-MOLA

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0 · [docs/gdd/04-mecanicas.md](docs/gdd/04-mecanicas.md) §1
**Data:** 2026-11-25
**Modelos 3D criados nesta tarefa:** **0 modelos 3D criados** (Regra 2).
**Animação procedural de ossos adicionada:** **0** — todas as habilidades disparam clipes reais no `Animator` + feedback secundário (partículas/SFX/FOV).

---

## 1. Declaração de Conformidade (`DESIGN_BIBLE.md` + `GDD 04 §1`)

- **Responsividade & Game Feel:** CONFORME — Todo input responde no mesmo frame (`Update()`), momentum é conservado (`Corrimão → Sopro → Rebote → Martelada Aérea`), FOV dinâmico **`+6°`** em alta velocidade (`HighSpeedFovBonusDeg`), partículas e SFX em cada transição de estado.
- **Coyote & Buffer:** CONFORME — `coyoteTimeSec = 0.10s`, `jumpBufferSec = 0.15s` preservados sem regressão.
- **Progressão (Flags de Relíquia + Flag Dev):** CONFORME — Todas as 4 habilidades ficam atrás das flags `UnlockRebote`, `UnlockCorrimao`, `UnlockMarteladaAerea`, `UnlockCravaMola` (em desenvolvimento/QA, liberáveis via `DevUnlockAllGddAbilities = true`).
- **Estabilidade & Testes:** CONFORME — `0 warnings CS`, `0 errors CS`, suíte `Tests/SmokeTest/` **26/26 VERDE**.

---

## 2. Números Implementados vs GDD (`docs/gdd/04-mecanicas.md` §1)

| Habilidade | Parâmetro no GDD 04 §1 | Implementado (`DezMovementConfig` / `DezLocomotionController`) | Desbloqueio na Progressão | Teste Smoke |
|---|---|---|---|---|
| **Rebote** (Pulo Duplo) | `+3,5 m`, 1 por pulo, reseta no chão/parede/garra | `reboteJumpHeight = 3.5f`, `HasReboteCharge`, `ResetReboteCharge()` no chão/wall-run/garra | Relíquia 1 — **Mola** (fim do Ato 1 — Caldeiral) | `Smoke_23_GddReboteDoubleJumpAndReset` |
| **Corrimão** (Wall-Run) | `2,5 s` máx., desliza `1,2 m/s` ao expirar, mantém velocidade de entrada, cancela com Sopro mantendo momentum | `wallRunMaxDurationSec = 2.5f`, `wallRunExpiredSlideSpeed = 1.2f`, `WallRunEntrySpeed`, cancelamento em `TryTriggerDash()` + `HighSpeedFovBonusDeg = +6°` | Fuso **Caldeiral / Pátio das Sirenes** (`UnlockCorrimao`) | `Smoke_24_GddCorrimaoWallRunAndSoproCancel` |
| **Martelada Aérea** | Segurar ataque no ar `0,6 s` → mergulho dano `3` + choque `3 m`, i-frames na queda | `airHammerChargeSec = 0.6f`, `airHammerDamage = 3`, `airHammerShockwaveRadius = 3.0f`, `IsAirHammerInvulnerable = true` | Relíquia de Combate / **A Orquestra** (`UnlockMarteladaAerea`) | `Smoke_25_GddMarteladaAereaChargeDiveAndIFrames` |
| **Crava-Mola** | Baixo + ataque no ar → mergulho `15 m/s` + onda de choque `3 m`, dano `2`, quebra cristas frágeis | `cravaMolaDiveSpeed = 15.0f`, `cravaMolaDamage = 2`, `cravaMolaShockwaveRadius = 3.0f`, `FragileCrestTarget.BreakCrest()` | Relíquia 1 — **Mola** (`UnlockCravaMola`) | `Smoke_26_GddCravaMolaDiveAndBreaksFragileCrest` |

---

## 3. Zona de Treino Diegética na Oficina (GATE 6)

Implementada em [GddMovementTrainingZone.cs](unity/Assets/Game/Scripts/Animation/GddMovementTrainingZone.cs) no pátio aberto de `Caldeiral` (`(16.0, 0.0, -8.5)`), ensinando visualmente sem paredes de texto:
1. **`Training_WallRunPanel`:** muro de `10m` com trilho luminoso ciano indicando linha de corrida de Corrimão.
2. **`Training_ReboteLedge`:** plataforma elevada a `3.6m` acessível com pulo + Rebote (`+3,5m`).
3. **`Training_FragileCrest`:** crista laranja-âmbar (`FragileCrestTarget`) que se estilhaça ao impacto do Crava-Mola.
4. **`Training_AirHammerPad`:** plataforma dourada circular de impacto (`3m`) para a Martelada Aérea.

### Evidência em Vídeo (GATE 7)
- **Combo longo (`Corrimão → Sopro → Rebote → Martelada Aérea`):** [captures/movimentacao/combo_movimentacao_gdd.mp4](captures/movimentacao/combo_movimentacao_gdd.mp4)

---

## 4. Lista de Modelos 3D TripoSG
- **0 modelos 3D criados nesta tarefa.**
