# RELEASE — CONCERTO Demo Build (Windows x64)

- **Versão automática:** `2026.11.25+5128109` (gravada em `PlayerSettings.bundleVersion` e exibida na tela inicial via `BuildVersionWatermark` + `Resources/BuildVersionInfo.json`)
- **Artefato de Build (fora do Git via `.gitignore`):** `builds/Concerto_Demo_Win64/Concerto.exe`
- **Tamanho da Build:** `316.47 MB` (`331,844,397` bytes)
- **Tempo de Build:** `61.7 s` (`BuildResult.Succeeded`, `0 errors`)
- **Hardware de Validação:** NVIDIA GeForce RTX 3060 (12 GB VRAM), Direct3D 12, `1920x1080`
- **Modelos 3D criados nesta tarefa:** `0 modelos criados`

---

## 1. Métricas Medidas na BUILD Standalone (GATE 4 — Fora do Editor)

| Métrica (Ato 1 — `Caldeiral`, `Concerto.exe`) | Alvo (`DESIGN_BIBLE.md`) | Medido na Build | Status |
|---|---|---|---|
| **FPS Médio** | `≥ 60 FPS` | **`278.5 FPS`** (`8,355` frames em `30.0s`) | **PASSOU (4.64x alvo)** |
| **P99 Frame Time (1% Low)** | `≤ 16.67 ms` (`≥ 60 FPS`) | **`6.02 ms` (`166.0 FPS`)** | **PASSOU** |
| **FPS Máximo** | — | `465.8 FPS` | — |
| **Erros de Console (`Player.log`)** | `0` | **`0`** | **PASSOU** |
| **Checklist Pré-Build (4 cenas, 34 prefabs)** | `0` MissingScripts, `0` ErrorShaders | **`0` MissingScripts, `0` ErrorShaders** | **PASSOU** |

---

## 2. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Performance (60fps):** CONFORME — Build standalone segura **`278.5 FPS` médios** e **`166.0 FPS` em 1% low (`6.02 ms`)** no Ato 1 (`1920x1080`), muito acima da meta de `60 FPS`.
- **Qualidade & Estabilidade:** CONFORME — `0` erros de console na build, `0 warnings CS`, `0` shaders rosa, `0` scripts ausentes (incluindo extração dos `MonoScript`s inline `CheckpointTrigger`, `EnergyCellPickup` e `InteractableBase` para arquivos próprios, eliminando corrupção de `level1` na build standalone).
- **Save & Acessibilidade:** CONFORME — Save JSON versionado (`v3`) com SHA-256, escrita atômica e 3 slots rotativos em `Application.persistentDataPath`; opções/acessibilidade em `PlayerPrefs`.
- **Pipeline 3D:** CONFORME — `0 modelos 3D criados` nesta tarefa; todos os modelos novos da demo são exclusivos do TripoSG.

---

## 3. Changelog desde a Última Build

- **Core & Fila de Desbloqueio:** `#93` (Eco Temporal & Estase), `#94` (Manifestações), `#102` (Setor Externo), `#103` (Epicentro), `#109` (Combate v2), `#106` (Boss Guardião da Fenda).
- **Correções Críticas & Polimento:**
  - `BL-001` (`3189e28`): Correção definitiva do reset/troca de missão ao falar com NPC + `BUGS.md`.
  - `BL-002` / `BL-003` (`c9a8fe9`, `bfb6f9a`): Correção de sobreposições de HUD (HP/Objetivo e Ferramenta/Poderes).
  - Polimento de locomoção/combate do Dez (`b633c5d`).
- **Sistemas de Produção:**
  - **Onboarding Primeiros 15 Minutos** (`b121fc5`): 5 passos diegéticos (`Mover → Pular → Interagir → Eco Temporal → Estase`), skippável após `3s`.
  - **Save à Prova de Corrupção** (`a0cde43`): Escrita atômica + 3 slots rotativos + SHA-256 + migração `v1/v2 → v3`.
  - **UI, HUD & Acessibilidade** (`c7ee5c8`): Pause/Opções 100% por controle, 5 buses de áudio, brilho, 3 escalas de fonte, 3 perfis daltônicos, remapeamento de teclas e legendas com falante.
  - **Game Feel Pass** (`5128109`): Hitstop `65–95ms`, shake `<300ms`, flash/pausa no dano, squash & stretch + poeira, coleta em 3 camadas.
  - **Release Builder:** `ConcertoReleaseBuilder.cs` (menu do Editor + CLI), watermark de versão automática na tela inicial e extração de `CheckpointTrigger.cs`, `EnergyCellPickup.cs` e `InteractableBase.cs` para integridade da build standalone.

---

## 4. O Que Está Cortado / Fora do Escopo desta Demo

- **Ato 4 / Final completo (`O Último Concerto`):** reservado para a versão completa após o ciclo de feedback da demo.
- **Telemetria Sentry pública:** aguardando publicação externa da build (desativada em builds locais).
- **Lote 2+ de props 3D adicionais:** apenas o Lote 1 (5 modelos TripoSG abaixo) está incluído nesta demo.

---

## 5. Modelos 3D TripoSG Presentes na Demo

Referência completa em [ASSETS_GERADOS.md](ASSETS_GERADOS.md) (`0 modelos criados` nesta tarefa):

1. **`part_condensador_fluxo`** — Peça 4 (Condensador de Fluxo) em `Caldeiral.unity`
2. **`part_estabilizador_orbital`** — Peça 5 (Estabilizador Orbital) em `Caldeiral.unity`
3. **`prop_fragmento_nucleo`** — Fragmento de Núcleo em `Epicentro.unity`
4. **`prop_celula_energia`** — Célula de Energia em `Caldeiral`, `SetorExterno`, `Epicentro`
5. **`prop_estacao_parry`** — Estação de Treino de Parry em `Caldeiral.unity`

---

## 6. Como Gerar a Build

- **Pelo Unity Editor:** menu `Concerto → Build → Build Windows Release Demo` (ou `Run Pre-Build Checklist`).
- **Por linha de comando:**
  ```powershell
  Unity.exe -batchmode -projectPath C:\Users\Bruno\workspace\game-concerto\unity -executeMethod Studio.Game.Editor.ConcertoReleaseBuilder.BuildFromCommandLine
  ```
- **Evidência em vídeo (GATE 3):** `captures/release/build_15min.mp4` (`1920x1080`, H.264).
