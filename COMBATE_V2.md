# COMBATE V2 — AMPLIAÇÃO DO KIT, IA COORDENADA E ÉGIDE (#109)

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0 (Seções 5 e 9 atualizadas com as novas linhas!)
**Pré-requisitos verificados:** Ferramenta multi-uso, Eco Temporal + Estase (`#93@8949c9a`), Estilhaço + Flutuante (`#94@1ce2dd8`), Epicentro (`#102@7f1c08c`) e Guardião da Fenda (`#103@7aac136`).

---

## 1. Referências Pesquisadas (Passo 0)
- **Tokens de Ataque em Grupos:** arquitetura de *Attack Token Manager* consagrada em *Batman: Arkham* (Rocksteady), *DOOM Eternal* (id Software) e *God of War* (Santa Monica Studio) — limita atacantes simultâneos (`max = 2`) enquanto os demais circulam/flanqueiam para manter legibilidade sem pile-up.
- **Janelas de Parry & Cancelamento:** *Sekiro* / *Hollow Knight* / *Hades* — janela precisa `150–200ms` (`180ms` aqui) com recompensa tátil imediata (freeze `150ms` + reflexão/stagger) e cancelamento de recovery por dash evasivo (`250ms`).
- **Lock-On Suave:** *Ratchet & Clank* / *Hades* — seleção automática do alvo mais próximo à frente com troca manual (`[V]`) sem prender a câmera dura.

---

## 2. Ampliações do Kit + IA + Feel
1. **Combo de 3 Golpes + Input Buffer (`MultiToolConfig.cs` / `CombatToolModule.cs`):** `15 → 22 → 36` de dano (1–2 rápidos, 3º punição com hitstop até `98ms` e knockback real) + `attackInputBufferSec = 0.16s`.
2. **Cancelamento com Eco Temporal:** janela de `0.25s` após qualquer golpe onde disparar Eco corta imediatamente o recovery (`TryCancelRecoveryWithEco`).
3. **ABSORVER / Parry (`[F]` ou Botão Meio — `ParryAndLockOnController.cs`):** janela `180ms`, cooldown `3.0s`, custo `0`. Reflete projéteis de volta (`1.65x` velocidade, `55` dano) e atordoa atacantes corpo-a-corpo por `2.4s`, quebrando a guarda frontal do **Égide**. Freeze `150ms` + clang + som reverso.
4. **Lock-On Suave (`[V]`):** seleciona o inimigo vivo mais próximo à frente e alterna alvos sem câmera presa dura.
5. **Inimigo Novo — "Égide" (`EgideEnemy.cs` + `Enemy_Egide.prefab` TripoSG):** blindagem frontal (`HP = 65`); golpes frontais ricocheteiam (`dano 0` + clang metálico); quebra por **ABSORVER** (`1.35x` dano em stagger) ou **flanqueio pelas costas** (`1.25x` dano).
6. **IA Coordenada (`CombatGroupCoordinator.cs`):** máximo **2 tokens de ataque simultâneos**, steering anti-aglomeração (`2.0–2.4m`), flanqueio angular (`±28°`) no Estilhaço, reposicionamento lateral pós-tiro no Flutuante e micro-slowmo de finalização (`0.16s ≤ 200ms`) no último inimigo do encounter.
7. **TEACH em Espaço Seguro (`ParryTeachStation.cs` em `Caldeiral.unity`):** emite pulso lento de treino com `dano = 0` para ensinar `[F] ABSORVER` antes de qualquer encontro exigir parry.

---

## 3. Tabela de Balance & TTK Medido

| Inimigo | HP | Dano | Velocidade | Telegraph | TTK Estimado | O que ajustar 1º |
|---|---:|---:|---:|---:|---:|---|
| **Estilhaço** | `32` | `1 seg` | `5.4 m/s` | `0.48s` | ~`1.2s` (2 golpes leves) | `estilhacoMoveSpeed` |
| **Flutuante** | `45` | `1 seg` | `3.2 m/s` | `0.65s` | ~`2.0s` (1 carregado ou 1 reflexão) | `flutuanteProjectileSpeed` |
| **Égide (Novo)** | `65` | `1 seg` | `3.4 m/s` | `0.62s` | ~`3.5s` (1 parry + carregado, ou 2 flanqueios) | `egideMaxHp` / `parryWindowSec` |
| **Guardião da Fenda** | `320` | `1 seg` | `3.8 m/s` | `0.72–0.80s` | ~`2–3 min` (2 fases) | `exposedCoreDamageMultiplier` |

---

## 4. Modelo TripoSG Gerado & Evidências (Gates)

| Asset | Pipeline / Registro | Polígonos / Altura |
|---|---|---|
| `enemy_egide.fbx` + PBR 4K (`Mat_Fab3D_enemy_egide.mat`) | **TripoSG exclusivo** (`run_egide_triposg_factory.sh`, seed `1090`, `triposg_runner.py` + `pipeline/run_asset.py`) | `6500 polys`, `1.85m` |

- **Compilação:** `0 warnings CS`, `0 errors CS`.
- **Testes PlayMode (2/2 PASSED):**
  - `CombatV2ValidationAndCaptureTest` (`5.95s`): `teachParry=True, combo3=True, ecoCancel=1, tokens<=2, egideRicochet+Flank+ParryBreak=True`.
  - **Regressão Boss:** `RiftGuardianBossValidationAndCaptureTest` (`11.46s`): **PASSED** com o kit novo!
- **MP4 de Evidência:** `captures/combat_v2/combate_v2_showcase_1080p.mp4` (`1920x1080 H.264 + AAC 44.1kHz stereo`).

---

## CONFORMIDADE — DESIGN_BIBLE.md v1.0
- **Seção 2 (Controls):** CONFORME — combo de 3 golpes, input buffer `160ms`, cancelamento por Eco (`250ms`), Absorver (`180ms`) e lock-on suave sem câmera dura.
- **Seção 3 (Métricas):** CONFORME — todos os telegraphs `≥ 480ms (> 300ms)`, hitstop `65–98ms (≤ 100ms)`, micro-slowmo final `160ms (≤ 200ms)`.
- **Seção 5 (Teach → Test → Twist):** CONFORME — `ParryTeachStation` (dano 0) ensina Absorver em espaço seguro antes do Égide; tabela da Seção 5 atualizada em `docs/DESIGN_BIBLE.md`.
- **Seção 7 (Legibilidade):** CONFORME — máximo 2 atacantes simultâneos (tokens), steering anti-aglomeração e ricochete visual/sonoro inconfundível no escudo do Égide.
- **Seção 9 (Juice) & Seção 11 (MDA):** CONFORME — 5 novas linhas adicionadas à tabela de juice da `DESIGN_BIBLE.md`; dinâmica de tensão curta e tática (ler → absorver/evadir → punir) preservada sem virar spam infinito.
