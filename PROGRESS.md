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
- [ ] **Vozes (`voices/`):** gravar/definir as 7 amostras `.wav` (começando por `narrador`, `juno`, `dez` para CS01) e validar com `validate_voice_samples.py`.
- [ ] **CS01 (`cs01_abertura`):** adaptar `dialogues.yaml` + `cutscene_01_abertura.yaml` → VO via `tts_bridge.py` → `CutsceneBuilder.cs` com `audio_clips_bound == 6`.
- [ ] **Caldeiral (Vertical Slice jogável):** kit de oficina (desync/sync) + texturas tileáveis + Re-Sincronia + combate Contratempo.
