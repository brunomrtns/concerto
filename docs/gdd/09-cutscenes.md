# 09 — Cutscenes (Timeline + Cinemachine declarativos)

> Cutscenes prontas para o CutsceneBuilder.cs `[DEFINIDO pilar 7: YAML declarativo → Timeline + Cinemachine, com capturas de keyframes em evidence/]` · conteúdo `[NOVO]` · tags: ver README

## 1. Princípio: cutscene é roteiro versionado `[NOVO]`

Nenhuma cutscene é montada à mão no editor: a IA (ou você) escreve `cutscene_*.yaml`; o `CutsceneBuilder.cs` (Editor, batchmode) lê o YAML e monta o **Unity Timeline** com Cinemachine, puxa os VOs do `dialogues.yaml` (via tts_bridge), posiciona atores/poses da biblioteca JSON, e grava **capturas dos keyframes** em `evidence/captures/` para revisão antes do merge `[DEFINIDO pilar 7]`. O YAML é a fonte da verdade; o Timeline é artefato gerado (re-gerável a qualquer momento).

## 2. Schema do `cutscene_*.yaml` `[NOVO]`

```yaml
meta:
  id: cs01_abertura
  duration_s: 165
  aspect: adaptive            # 16:9 desktop / 19.5:9 mobile crop-safe
  skippable_after_s: 8
  subtitle_lang: [pt-BR, en]
music: { track: music/concerto_tema_quebrado, layers: [A], tempo_bpm: 96 }
actors:
  dez:   { prefab: Characters/Dez, pose_ref: poses/dez/repair_kneel }
  juno:  { prefab: Characters/Juno, pose_ref: poses/juno/explain }
  censor_elite: { prefab: Characters/CensorHeavy }
sets:
  grande_relogio_noite: { scene: Sets/GrandeRelogio/Noite, lighting: preset_dramatico }
shots:
  - id: s01
    start_s: 0.0
    dur_s: 8.0
    camera: { type: dolly, from: [0,2,8], to: [0,2,5], look_at: dez, lens_mm: 35, dof: true }
    action: { actor: dez, anim: repair_idle, fx: sparks_small }
    vo: null
    subtitle: null
  - id: s02
    start_s: 8.0
    dur_s: 10.0
    camera: { type: closeup, target: juno.mao, lens_mm: 50 }
    action: { actor: juno, anim: handoff_device }
    vo: cs01_juno_01
    fx: { type: fade, when: end, dur_s: 0.6 }
subtitles:                    # legenda global do trecho ( fallbacks e SFX descritos)
  - { at_s: 160.0, text: { "pt-BR": "CONCERTO", "en": "CONCERTO" }, style: title }
```

Campos que o CutsceneBuilder consome direto: `shots[].camera` (tipos suportados na v1: `static`, `dolly`, `orbit`, `closeup`, `crane` — CinemachineVirtualCamera gerada por shot com blend 0,4 s), `shots[].action` (animação do pose library + FX), `vo` (id do dialogues.yaml → AudioClip + timing de boca fake), `music` (stems com fade), `subtitles` (SRT gerado junto). Capturas: 1 frame por shot em `evidence/captures/cs01/shot_s01.png` para revisão do Strict Reviewer.

## 3. Lista de cutscenes do jogo 1 `[NOVO; cs01 definido na conversa]`

| ID | Nome | Duração | Momento | Tecnologia |
|---|---|---|---|---|
| `cs01_abertura` | O Concerto que Quebrou o Mundo | 2'45" | Prólogo `[DEFINIDO]` | Timeline in-engine |
| `cs02_a_oferta` | A Oferta da Maestrina | 1'40" | Twist do Ato 2 | Timeline in-engine |
| `cs03_a_verdade` | A Primeira Nota | 2'10" | Revelação do Ato 3 | Timeline in-engine |
| `cs04_fim_e_depois` | Solta a Nota / Devolve a Nota | 2'30" | Finais (2 variantes no mesmo YAML via `branches:`) | Timeline in-engine |
| `cs05_pos_creditos` | Essa Batida | 0'45" | Teaser da série | Timeline in-engine |
| `vinheta_<fuso>` | Vinhetas de chegada | 0'12" | Entrada em cada fuso | câmera + VO curto |

Budget total de cutscene do jogo 1: ~11 min de Timeline + ~6 min de VO — dentro do custo do pipeline (pilar 7) sem "cinemática AAA".

## 4. cs01_abertura — decupagem (resumo; YAML completo no pacote) `[NOVO]`

1. **s01 (8 s):** Grande Relógio à noite, câmera dolly-in sobre o Pêndulo-Mor; coro mecânico ao fundo.
2. **s02–s05 (40 s):** oficina interna — Dez e Juno no reparo; os 4 braços de Dez trabalhando em close; Juno entrega a mola contraindicada e brinca ("Peça nova, mundo novo" — flag-off do que vem). VO: `cs01_juno_01..02`.
3. **s06 (12 s):** o ajuste noturno: Dez sozinho, manômetro no vermelho; a mola cede — **frame de 0,3 s de silêncio total** (assinatura Contratempo antecipada).
4. **s07–s09 (35 s):** a Fratura em 3 cortes: luz rachando o céu / os 6 fragmentos voando / Juno desfazendo-se em trail de meio segundo ("perdida no meio tempo"). VO narrador: `cs01_narr_01`.
5. **s10 (18 s):** Dez acordando na sucata flutuante; primeira imagem do Bis no pulso girando ao contrário; primeiro "tíque" — corte.
6. **s11 (12 s):** título CONCERTO com a marchinha incompleta (2 compassos e a trilha "emperra" — o jogo começa no tempo quebrado).

Regra de direção para todas as cutscenes: **máx. 12 shots por cutscene**, cada shot ≤ 12 s (facilita geração/captura e reuso de poses), drama sempre com um elemento quente de oficina no quadro (06 §4).

## 5. Vinhetas de fuso `[NOVO]`

Formato fixo de 12 s: drone aéreo do fuso Desincronizado → primeira Re-Sincronia "mostrada" (onda) → drone do fuso Restaurado → título do fuso na tela (tipografia display). VO: 2 linhas do Narrador + 1 bark de Dez. Mesmo template para os 6 fusos — custo de produção baixo, payoff de "episódio" alto.

## 6. Fluxo de produção de cutscene (gate a gate) `[NOVO, pilar 7]`

1. Roteiro textual → `cutscene_*.yaml` (IA escreve; você aprova o diff).
2. `tts_bridge.py` gera/atualiza os VOs do YAML (`dialogues.yaml` é atualizado no mesmo commit).
3. `CutsceneBuilder.cs` (batchmode) monta Timeline + capturas → `evidence/captures/csXX/`.
4. Revisão: capturas + 1 playthrough gravado; Strict Reviewer aprova/veta.
5. Build do APK com o Timeline gerado; se o YAML muda, Timeline regenera (nunca editar Timeline à mão).
