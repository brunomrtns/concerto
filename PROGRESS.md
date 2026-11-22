# PROGRESS.md — CONCERTO (Série ORA, Jogo 1)

> Repositório limpo criado sobre o **Studio V2 Core** (`gamedev-setup-ia@edc2845a`),
> 100% local (RTX 3060 12 GB, somente `C:\`), com Git LFS ativo desde o commit 1.
> Contratos oficiais: `docs/gdd/`, `docs/art/art-bible.yaml`, `docs/art/plano-producao-personagens.md`,
> `docs/narrative/dialogues.yaml`, `docs/narrative/cutscenes/cutscene_01_abertura.yaml`.

## Marco M0 — Bootstrap & Contratos (22/11)
- [x] Repositório limpo `C:\Users\Bruno\workspace\game-concerto` criado com Studio Core + Git LFS + Unity 6.3 URP skeleton + Quaternius UAL (`UAL1_Standard.fbx`, `UAL2_Standard.fbx`).
- [x] Pacote `concerto-gdd.zip` (v0.3) e `plano-producao-personagens.md` importados nos caminhos canônicos.
- [x] **S-artbib-01:** `docs/art/art-bible.yaml` completado com os 6 personagens/inimigos faltantes (`vo_segunda`, `tio_horario`, `censor`, `ferrugem_trabalhadora`, `ferrugem_espelho`, `ferrugem_coletora`) e validado com `concept_bridge.py --resolve-only`.

## Marco M1 — Produção de Personagens (Vertical Slice Caldeiral)
- [ ] **S-char-01 (Porca):** conceito (4 candidatas → aprovação do dono ou Rota Híbrida) → 3D ≤ 2.5k tris → hover/lente procedural → prefab + captura.
- [ ] **S-char-02 (Dez base):** conceito (aprovação do dono ou Rota Híbrida) → 3D ≤ 8k tris → UniRig + Humanoid + UAL → captura.
- [ ] **S-char-03 (Dez — 2 braços auxiliares + IK):** 2 bones acessórios fora do Humanoid + curvas/IK de interação (`repair_kneel`).
- [ ] **S-char-04 (NPCs — Vó Segunda + Tio Horário):** Humanoid UAL + 1 conversa + 2 barks com `DialoguePresenter` + VO.
- [ ] **S-char-05 (Ferrugens):** 3 famílias ≤ 3.5k + FSM Patrulha→Perseguição→Ataque.

## Marco M2 — Vozes, Cutscene CS01 & 1º Fuso (Caldeiral)
- [x] **Vozes (`voices/`):** 7 amostras `.wav` validadas (`evidence/audio/voice_samples_concerto.json`, PASS 7/7) + VO XTTS-v2 + BGM/SFX em `unity/Assets/Game/Audio/` (**CAMINHO CONGELADO**).
- [ ] **CS01 (`cs01_abertura`):** adaptar `dialogues.yaml` + `cutscene_01_abertura.yaml` → VO via `tts_bridge.py` → `CutsceneBuilder.cs` com `audio_clips_bound == 6`.
- [ ] **Caldeiral (Vertical Slice jogável):** kit de oficina (desync/sync) + texturas tileáveis + Re-Sincronia + combate Contratempo.

---

## Sincronia Studio Core & Gates de Transição do Caldeiral (C1 → C5 — Adendo B)
- [x] **Fase 2 — Sincronia Studio Core (`gates-v1.0` / `bf214100`, commit `5361900`):** `validate_visual.py`, `validate_gltf.py`, `run_visual_gates.py`, `Fab3dImporter.cs`, fixture `fixture_biped_deitado` (`exit 1`), `studio-scene-director`, `studio-strict-reviewer`, `LICENSES-ARTE.md`, BAN LIST verbatim no `AGENTS.md`, relatório diagnóstico da Porca (`evidence/validation/validate_visual_porca_report.json`) e smoke test `prop_caixote` no `game-concerto` (`evidence/captures/fab3d_prop_caixote_20261122_163054.png`).
- [x] **Adendo A + B + E:** `CAMINHOS CONGELADOS`, gate de aprovação em `PROGRESS.md` e protocolo de sync registrados em `AGENTS.md`.
- [ ] **Pré-requisito C2 — Adendo C (`validate_scene.py` Android URP budget + fixture de cena estourada):** pendente (obrigatório antes de C2).
- [ ] **Pré-requisito C2 — Adendo D (Baseline PlayMode M1–M2 em `evidence/tests/`):** pendente (obrigatório antes de C2).
- **Status dos Estágios de Cenário (C1 → C5):**
  - **C1 APROVADO por Bruno em 2026-11-22 — Caldeiral = ambiente introdutório; futuros ambientes usarão o mesmo método** (`docs/level-design/caldeiral_c1.md` + `evidence/concepts/caldeiral_c1_topdown.png`).
  - **C2 (Greybox Fechado):** BLOQUEADO *(exige Rastreabilidade Mecânica + Adendo C + Adendo D verdes).*
  - **C3 (Kit CC0 + PBR World-Scale):** BLOQUEADO *(exige C2 aprovado).*
  - **C4 (Composição & Câmeras CS01):** BLOQUEADO *(exige C3 aprovado).*
  - **C5 (Props & Heróis Fábrica 3D):** BLOQUEADO *(exige C4 aprovado).*
