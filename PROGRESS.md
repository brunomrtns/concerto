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
- [ ] **S-char-01 (Porca):** conceitos 2D A–D (`sdxl-turbo`, Kit 4.0) gerados em `director_review/2026-11-22_c5_conceitos_porca_dez/` (**AGUARDANDO APROVAÇÃO DO BRUNO** ou A/B FLUX no `mega_v4`; zero 3D ainda) → 3D ≤ 2.5k tris → hover/lente procedural → prefab + captura.
- [ ] **S-char-02 (Dez base):** conceitos 2D A–D (`sdxl-turbo`, Kit 4.0) gerados em `director_review/2026-11-22_c5_conceitos_porca_dez/` (**AGUARDANDO APROVAÇÃO DO BRUNO**; nota honesta: `sdxl-turbo` falhou em manter macacão + 4 braços — forte candidato a re-conceito com FLUX.1-dev FP8 no `mega_v4` ou Rota Híbrida; zero 3D ainda) → 3D ≤ 8k tris → UniRig + Humanoid + UAL → captura.
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
- [x] **Pré-requisito C2 — Adendo C (`validate_scene.py` Android URP budget + fixture de cena estourada):** validado (`Calibration.unity` exit 0, `tests/fixtures/scene_gate/fixture_scene_overbudget.unity` exit 1 acusando os 5 limites, commit `d644359` sincronizado de `gamedev-setup-ia@41610c98`).
- [x] **Pré-requisito C2 — Adendo D (Baseline PlayMode M1–M2 em `evidence/tests/`):** validado (**2/2 PASSED**, 0 exceções, `evidence/tests/baseline_playmode_m1_m2.xml` + `.json`).
- [x] **Rastreabilidade Mecânica (§1.1 de `caldeiral_c1.md`, commit `43f42d2`):** 3 células VERDES (`InteractionSystem.cs`, `DialoguePresenter.cs`, `CutscenePlayer.cs`), 5 células VERMELHAS ajustadas na planta (rampas transitáveis `0m↔+3.5m↔+7.0m`, ponte sobre vão `6.5m`, passarela ao longo dos paredões, inimigos não-bloqueantes em C2).
- **Status dos Estágios de Cenário (C1 → C5):**
  - **C1 APROVADO por Bruno em 2026-11-22 — Caldeiral = ambiente introdutório; futuros ambientes usarão o mesmo método** (`docs/level-design/caldeiral_c1.md` + `evidence/concepts/caldeiral_c1_topdown.png`).
  - **C2 APROVADO por Bruno em 2026-11-22 — veredito: 'parece tá legal, são só cubos e formas geométricas, nada de textura nem modelo final — aprovado NA CONDIÇÃO de blockout; a meta visual (bonito) é das etapas C4/C5'**
    - **C2-Z1 (`Oficina da Sucata`):** APROVADO (`2026-11-22`; nota: placeholder Dez com braços levemente afastados — cosmético, descartado no C5; Dez HERO não herda pose nem proporções do placeholder; `director_review/2026-11-22_c2_z1_oficina_sucata/`).
    - **C2-Z2 (`Passarela da Bigorna`):** APROVADO (`2026-11-22`, `director_review/2026-11-22_c2_z2_passarela_bigorna/`, commit `926fc87`).
    - **C2-Z3 (`Pátio das Sirenes + Galeria de Fumaça`):** APROVADO (`2026-11-22`, `director_review/2026-11-22_c2_z3_patio_sirenes/`, commit `421f810`).
    - **C2-Z4 (`Câmara do Caldeiral Central`):** APROVADO (`2026-11-22`, `director_review/2026-11-22_c2_z4_camara_caldeiral/`, commit `9f89b3f`).
    - **C2-Z5 (`Galeria de Retorno Sync + Mirante sobre Z1`):** APROVADO (`2026-11-22`, `director_review/2026-11-22_c2_z5_galeria_retorno/`).
    - **C2-Walkthrough (`Loop Completo Z1→Z5`):** APROVADO (`2026-11-22`, `director_review/2026-11-22_c2_walkthrough_loop/`).
  - **C3 APROVADO por Bruno em 2026-11-22 — refinamento arquitetônico sem textura confirmado como esperado** (`director_review/2026-11-22_c3_caldeiral_consolidado/`, commit `4f8bc46`).
  - **C4 (Direção de Arte, Vestimenta Visual & Câmeras):** LIBERADO EM 2026-11-22
    - **Kit 4.0 APROVADO por Bruno em 2026-11-22 — linguagem visual oficial: latão/cobre/ferro escuro, rebitado, clockwork-industrial; motor de forma oficial = TripoSG via pipeline 3dprinter-setup-ia (run_asset.py → Hunyuan3D-Paint → pack_glb)** (`director_review/2026-11-22_c4_0_kit_direcao_arte/`, commit `73ce70f`).
    - **Fase 4.1 (Vestimenta Z1→Z5 + Iluminação Real + Walkthrough):** APROVADA por Bruno em 2026-11-22 (`director_review/2026-11-22_c4_1_caldeiral_vestido/`, commit `d3f28bd`; C4 completo encerrado).
  - **C5 (Props & Heróis Fábrica 3D):** EM ANDAMENTO (2026-11-22 / 2026-11-23)
    - **RUN `mega_v5` — TAREFA 1 (Parada limpa):** `run_c5_props_batch2_3d.py` interrompido com segurança no prop 1/4 (`c5_prop_banca_tio_horario`, estágio `02_paint_hunyuan`), motivo `"parada do dono — mega_v5"`. Outputs parciais arquivados (sem apagar) em `~/printer3d-work/trash/interrupted_v5/` (`evidence/trash/interrupted_v5/manifest.json`); `nvidia-smi` limpo (`741 MiB / 12288 MiB`, `0` procs GPU).
    - **RUN `mega_v5` — TAREFA 2/3 (Gate Anti-Grade + Vista Única):** template oficial em `docs/art/conceito-vista-unica.md`; gate CPU `gate_concept_image.py` integrado em `concept_flux.py` e `concept_bridge.py`. Auditoria retroativa (61 PNGs): todos os contact sheets `8/8 FAIL` (ref. humana apenas); Porca FLUX A–D, Dez FLUX A–D, Batch-2 FLUX A/B e Batch-1 FLUX A/B **100% `gate_pass=true` (vista única)** — não regenerados (TAREFA 3.4).
    - **Derivados antigos marcados `INVÁLIDO PARA O JOGO (substituir na TAREFA 4)`:** `c5_prop_bancada_relogio`, `c5_prop_no_resincronia`, `c5_prop_valvula_ritmica` (versões v3 SDXL-Turbo reprovaram em `corte_borda` 66–75%; substituídos na TAREFA 4 pelas versões FLUX.1-dev FP8 `gate_pass=true`). `c5_prop_sirene_mestra` (v3) PASSOU no gate (`mantido intocado`, TAREFA 4.3).
    - **C5 Props Batch-1 (`sirene_mestra`, `no_resincronia`, `valvula_ritmica` ×3, `bancada_relogio`):** **AGUARDANDO APROVAÇÃO DO BRUNO** (`director_review/2026-11-22_c5_props_batch1/` → atualização FLUX em `director_review/2026-11-23_c5_regen_vista_unica/`).

---

## Decisões do Diretor (Registradas em 2026-11-22)
- **Mirante Z5:** Panorâmico no Vertical Slice; atalho físico Z5→Z1 = candidato para expansão pós-M2 (decidir quando houver conteúdo após M2).
- **Nível `+7.0 m` em Z4:** Permanece friso arquitetônico; vira alvo de Rebote SE e QUANDO o Bruno aprovar essa mecânica no futuro. Não criar segunda rampa.
- **Passarela `4 m` em Z3:** PERMANENTE como rota segura/acessibilidade; wall-run futuro, se aprovado um dia, será atalho opcional, nunca substituto.
- **Renomear `3dprinter-setup-ia` → `3dcreator-setup-ia`:** Na fila; executar num intervalo natural, não no meio de etapa.
- **Regra permanente de Git:** Todo commit vai direto na `main` com `git push` imediato.
- **Motor de Forma 3D da Fábrica:** Motor de forma padrão = TripoSG (dono, 2026-11-22, com base no benchmark Fase C); trellis2 = fallback; trellis(1) e triposr descontinuados para produção de jogo; ban TripoAI-nuvem mantido (ADR-009).
