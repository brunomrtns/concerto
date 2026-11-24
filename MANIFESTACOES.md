# MANIFESTAÇÕES — ESTILHAÇO, FLUTUANTE E LOOP DE COMBATE (#94)

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0
**Pré-requisitos verificados:** Movimento (`DezLocomotionController`), Ferramenta (`MultiToolController` + `IDamageable`) e Poderes (`DezPowersController` + `StasisFieldSystem`, `#93@8949c9a`) presentes no repo.

---

## 1. Resumo da Implementação

- **2 Inimigos Exclusivamente via TripoSG + PBR 4K:**
  - **Estilhaço** (`Enemy_Estilhaco.prefab` — `EstilhacoEnemy.cs`): rusher melee rápido e fraco (morre em 2 golpes leves ou 1 golpe carregado), com telegraph visual + sonoro claro (`0.48s`) antes do avanço.
  - **Flutuante** (`Enemy_Flutuante.prefab` — `FlutuanteEnemy.cs`): paira a `1.60m`, mantém distância preferida (`7.5m`) e dispara 1 projétil lento (`ManifestationProjectile`, `7.2 m/s`) que o **Eco Temporal** desvia com i-frames e a **Estase** desacelera para `50%`.
- **FSM Honesta e Extensível (`ManifestationEnemyBase.cs`):**
  - Estados: `IdlePatrol → Chase → Telegraph → Attack → Stagger → Dead`.
  - Implementa `IDamageable` (reusa hitstop `65–95ms` e shake `< 280ms` da ferramenta) e `ITimeScalable` (Estase `#93` desacelera inimigos e projéteis localmente).
  - **Morte:** dissolve em partículas ciano de glitch + som reverso (`sfx_enemy_dissolve_reverse_TEMP.wav`) + chance configurável de dropar `EnergyCellPickup`.
  - **Bloqueio de regressão:** pausa IA e dano automaticamente em cutscenes (`CS01–CS04`), diálogos (`DialogueController`), pause e ritual de reparo (`RepairRitualController`).
- **HP do Player & Knockdown (`PlayerHealthController.cs` — TEMP até passada de dificuldade):**
  - 4 segmentos de HP no HUD superior esquerdo, vignette vermelha de dano (`decay 260ms < 300ms`), i-frames pós-dano (`0.75s`) + respeito integral aos i-frames do Eco Temporal.
  - **Knockdown ao zerar HP (0/4):** banner curto (`1.6s`) e respawn automático no último checkpoint persistido via `SaveSystem` (`cp_oficina_inicio`, `cp_setor_externo_entrada`, `cp_oficina_pos_cs03` e `SetCheckpoint()`), preservando anomalias e progresso e restaurando HP `4/4`. Durante knockdown, Eco Temporal e ataques da ferramenta ficam bloqueados.
- **Robustez Pós-Review (#93/#94):**
  - **VFX & Materiais URP (FIX 1):** `GlitchDissolve_VFX` usa material estático `URP/Particles/Unlit` ciano (zero magenta), `EnergyCellPickup` usa `URP/Lit` emissivo ciano e projétil do Flutuante usa `URP/Unlit` cacheado.
  - **PlayerRegistry (FIX 4):** referência ao Dez via `PlayerRegistry` (registrado em `DezLocomotionController` e `PlayerHealthController`), eliminando `GameObject.Find("Player_Dez_Placeholder")` nos sistemas de combate e save.
  - **Combate Justo & Performance (FIX 7/8/9):** `FlutuanteEnemy.HasLineOfSightToPlayer()` impede disparos através de paredes (`LayerEnvironment|LayerDefault`); knockback de inimigos faz raycast contra parede antes de deslocar; flash de dano usa `MaterialPropertyBlock` (zero alocação de clone `.material`).
- **Spawn Config-Driven (`CombatEncounterZone.cs`):**
  - Zona `CombatZone_Act2_Manifestacoes` posicionada na oficina (`Caldeiral.unity`), ativa após o beat final do Ato 2 (`RepairRitualController.Act2Completed`), wave única curta e tensa (`2 Estilhaços + 1 Flutuante`).

---

## 2. Tabela de Balance Inicial (`CombatBalanceConfig.asset`)

| Parâmetro | Estilhaço | Flutuante | Player (Dez) | O que ajustar 1º se fácil/difícil |
|---|---:|---:|---:|---|
| HP Máximo | `32` (2 golpes leves) | `45` (1 carregado / 3 leves) | `4 segmentos` | `estilhacoMaxHp` / `flutuanteMaxHp` |
| Velocidade | `5.4 m/s` (lunge `10.5 m/s`) | `3.2 m/s` (projétil `7.2 m/s`) | `DezMovementConfig` | `flutuanteProjectileSpeed` |
| Telegraph | `0.48s` | `0.65s` | — | Aumentar se jogador não ler a tempo |
| Cooldown Ataque | `1.45s` | `2.40s` | — | Reduzir para subir pressão |
| Dano | `1 segmento` | `1 segmento` | — | Manter `1` (tensão curta) |
| Stagger | `0.35s` | `0.40s` | Invuln pós-hit `0.75s` | Ajustar se stunlock ficar fácil |
| Drop de Célula | `45%` | `65%` | — | Subir se faltar energia para Bolt |

### Como Adicionar um Novo Inimigo
1. Gere a malha via **TripoSG** e crie o prefab em `Assets/Game/Prefabs/Enemies/` com `CapsuleCollider`, `ActorSolidSeparation (bodyKind=Enemy)` e camada `LayerEnemy (23)`.
2. Crie uma subclasse de `ManifestationEnemyBase` implementando `MaxHp`, `CellDropChance`, `GetStaggerDuration()` e `TickFsm(float dt)`.
3. Adicione o novo tipo ao enum `ManifestationType` e ao `CombatEncounterZone`.

---

## 3. Modelos TripoSG Gerados & Áudio Novo

| Asset | Pipeline / Registro | Polígonos / Altura | QA |
|---|---|---|---|
| `enemy_estilhaco.fbx` + PBR 4K (`Mat_Fab3D_enemy_estilhaco.mat`) | **TripoSG exclusivo** (`run_manifestacoes_triposg_factory.sh`, seed `940`, `triposg_runner.py` + `pipeline/run_asset.py`) | `6500 polys`, `1.45m` | `qa_pass=True` |
| `enemy_flutuante.fbx` + PBR 4K (`Mat_Fab3D_enemy_flutuante.mat`) | **TripoSG exclusivo** (`run_manifestacoes_triposg_factory.sh`, seed `940`, `triposg_runner.py` + `pipeline/run_asset.py`) | `6500 polys`, `1.55m` | `qa_pass=True` |
| `sfx_enemy_telegraph_TEMP.wav`, `sfx_enemy_dissolve_reverse_TEMP.wav`, `sfx_player_hurt_TEMP.wav` | Síntese procedural original (CC0 1.0) | 44.1 kHz mono WAV | OK |

---

## 4. Como Testar & Evidências (Gates)

- **Compilação:** `0 warnings CS`, `0 errors CS`.
- **Testes PlayMode (Pós-Review #93/#94):**
  - `ManifestacoesValidationAndCaptureTest.ValidateManifestacoesCombatLoopKnockdownRespawnAndCapture` — **PASSED (`10.14s`)** (abates reais via `CombatToolModule.ExecuteHeavyStrike()` / `OverlapSphere → IDamageable.ApplyDamage`, zero reflection em `ApplyDamage`).
  - `DezPowersValidationAndCaptureTest.ValidateEcoTemporalEstasePlaygroundAndCapture` — **PASSED (`7.42s`)**.
  - `ReviewFix9394ValidationAndCaptureTest.ValidateAllNinePostReviewFixesAndCaptureFix1BeforeAfter` — **PASSED (`6.00s`)** (valida todos os 9 fixes P1/P2 + captura ANTES/DEPOIS).
- **MP4s de Evidência:**
  - `captures/manifestacoes/manifestacoes_combate_real_1080p.mp4` (`1920x1080 H.264 + AAC 44.1kHz stereo`).
  - `captures/review_fix_93_94/fix1_dissolve_antes_depois_1080p.mp4` (`1920x1080 H.264`, comparação ANTES magenta vs DEPOIS ciano URP no mesmo ângulo).

---

## CONFORMIDADE — DESIGN_BIBLE.md v1.0
- **Seção 2 (Character & Controls):** CONFORME — `LayerEnemy (23)` + `ActorSolidSeparation (30%/70%)` impede sobreposição e travamento de canto; Eco Temporal concede evasiva limpa.
- **Seção 3 (Métricas):** CONFORME — Hitstop `65–95ms`, flash branco `120ms`, vignette vermelha `260ms (< 300ms)`, telegraph claro (`0.48s–0.65s`).
- **Seção 7 (Legibilidade):** CONFORME — silhuetas distintas (Estilhaço baixo/predatório vs Flutuante suspenso com halo) + brilho e áudio distintos de telegraph antes de cada ataque.
- **Seção 9 (Juice):** CONFORME — flash branco, knockback, hitstop, partículas de dissolve ciano, som reverso na morte, drop brilhante de célula e vignette vermelha de dano.
