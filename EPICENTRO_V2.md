# EPICENTRO V2 — Segunda Passada de Level Design (CONCERTO)

Polimento de ritmo, checkpoints, leitura visual, saídas laterais recompensadas e curva de dificuldade exclusivamente na fase **O Epicentro** (`Epicentro.unity`).

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Escopo fechado:** Exclusivamente `Epicentro.unity` (`EpicenterV2LevelDesignDirector.cs`); nenhum outro ato (`MainMenu`, `Caldeiral`, `SetorExterno`) foi alterado.
- **Checkpoints (`≤30s` de retrabalho):** 5 checkpoints ordenados ao longo dos `116m` da fase (`x = 0, 33.5, 50, 71, 102`), com distância máxima de `33.5m` (`~9.6s` de corrida, muito abaixo do teto de `30s`).
- **Performance & Telegraph:** `60fps` preservado (geometria leve URP Lit sem alocações por frame), `telegraph ≥300ms` nos inimigos e no Boss Guardião da Fenda intactos.
- **Modelos 3D TripoSG criados nesta tarefa:** `0 modelos criados`.

---

## 2. Diagnóstico Inicial (GATE 1 — Levantado ANTES das Mudanças)

1. **Gap de checkpoints `>30s`:** Havia apenas 3 checkpoints (`x=0`, `x=50`, `x=102`) — cair no puzzle de Estase (`x=38`) obrigava a refazer toda a S1 (`~40s`); morrer na Arena B (`x=84`) obrigava a refazer toda a Arena A + ponte (`~45s`).
2. **Ritmo sem alívio:** 4 plataformas seguidas (`x=10.5..38`) e 2 arenas de combate seguidas (`x=58` e `x=84`) sem pontos claros de respiro/recompensa.
3. **Leitura visual fraca:** Falta de linhas luminosas conduzindo o olhar ao longo de `+X` e ausência de marco arquitetônico na entrada da Arena Final.
4. **Sem desvio lateral recompensado:** Nenhuma saída opcional com recompensa para jogadores exploradores.
5. **Pico precoce no puzzle de Estase:** `fastStasisPlatformSpeed = 10.5` logo após a plataforma rotativa sem checkpoint criava um muro de frustração no meio da fase.

---

## 3. Antes vs Depois de Cada Mudança (GATES 2–5)

| Aspecto | ANTES (Epicentro V1) | DEPOIS (Epicentro V2) | Gate |
|---|---|---|---|
| **Checkpoints** | 3 checkpoints (`x=0, 50, 102`), gap máx. `52m` (`>40s` retrabalho) | **5 checkpoints** (`x=0, 33.5, 50, 71, 102`), gap máx. `33.5m` (`~9.6s ≤ 30s`) | GATE 3 |
| **Ritmo (Desafio ↔ Alívio)** | Travessia longa + 2 arenas seguidas sem pausa | **Ilha de Alívio S1B (`x=33.5`)** + **Ponte de Alívio S2B (`x=71`)** + **Pórtico S3 (`x=102`)** | GATE 2 |
| **Leitura da Rota Principal** | Sem balizas no eixo `+X`, entrada do boss discreta | **11 balizas luminosas ciano** ao longo da rota principal (sem setas) + **Pórtico Dourado monumental** em `x=102` | GATE 4 |
| **Saída Lateral Recompensada** | Inexistente | **Desvio lateral seguro (`x=71, z=+6.8`)** com pista dourada e **Relíquia Secreta do Epicentro** (`+1 peça` + feedback 3 camadas) | GATE 4 |
| **Curva de Dificuldade** | Muro precoce no puzzle de Estase (`10.5 m/s` sem checkpoint) | Curva crescente suave: fácil (`S1`) → médio (`Estase 9.2 m/s` com CP prévio) → médio-alto (`Arena A → Alívio → Arena B`) → **pico no Boss (`x=116`)** | GATE 5 |

---

## 4. Mapa de Checkpoints (GATE 3)

| # | Checkpoint | Posição `(x, y, z)` | Distância do anterior | Retrabalho máx. estimado |
|---|---|---|---|---|
| 1 | `Checkpoint_S1_Travessia` | `(0.0, 0.1, 0.0)` | — | `0s` |
| 2 | `Checkpoint_S1B_PreEstase` *(Novo V2)* | `(33.5, 0.8, 0.0)` | `33.5 m` | `~9.6s` (`≤30s`) |
| 3 | `Checkpoint_S2_Combate` | `(50.0, 0.9, 0.0)` | `16.5 m` | `~4.7s` (`≤30s`) |
| 4 | `Checkpoint_S2B_PonteAlivio` *(Novo V2)* | `(71.0, 0.95, 0.0)` | `21.0 m` | `~6.0s` (`≤30s`) |
| 5 | `Checkpoint_S3_ArenaFinal` | `(102.0, 0.9, 0.0)` | `31.0 m` | `~8.9s` (`≤30s`) |

---

## 5. Evidências, Testes e Como Reverter (GATES 6–8)

- **MP4 (GATE 6):** `captures/epicentro/epicentro_v2.mp4` (`1920x1080`, H.264, `46.0s`) — corrida completa da fase passando pelos 5 checkpoints, ilha de alívio, puzzle de Estase, desvio lateral secreto recompensado, pórtico dourado e Arena do Boss.
- **Teste Smoke Novo (`Smoke_15_EpicentroV2CheckpointsPacingAndSecretReward`):** em `unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs`.
- **Suíte `Tests/SmokeTest/`:** **15/15 PASSED** (`0 failed`, `0 skipped`, `0 warnings CS`, `0 errors CS`) + `EpicenterV2FullRunCaptureTest` (**16/16 PASSED**).
- **Como reverter:** `git revert <commit-hash-desta-tarefa>`.
