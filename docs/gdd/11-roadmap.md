# 11 — Roadmap de Produção

> Marcos M0–M6 `[NOVO]` mapeados nos 9 pilares do estúdio `[DEFINIDO: ordem recomendada de construção]` · tags: ver README

## 1. Princípio: o jogo segue a fábrica, nunca o contrário `[NOVO, seu princípio]`

Nenhum milestone do jogo avança sem o pilar do estúdio que o sustenta. A ordem abaixo é a sua ordem recomendada (governança → 2D → vozes → cutscenes → rigging), com o jogo 1 consumindo cada pilar assim que ele fecha. Isso evita o "projeto infinito" que você sinalizou.

## 2. Mapa de marcos

### M0 — Governança e papéis (semana 0–2) → **Pilar 9 + 8** `[DEFINIDO passo 1]`
- Separar Studio Core vs. jogo (limpeza do gamedev-setup-ia, NEON DISTRICT vira repo próprio).
- Criar repo `concerto` do template; este GDD entra em `docs/`.
- `art-bible.yaml` v1 (deste pacote) + skills **Art Director** e **Strict Reviewer** (pilar 8).
- **Gate M0:** repo limpo; contrato de arte versionado; reviewer com poder de veto ativo.

### M1 — Vertical Slice "Caldeiral completo" (semanas 3–8) → **Pilares 3 + 2 + 4**
- ComfyUI em D:\ + `concept_bridge.py` (pilar 3) → conceitos do Caldeiral (kit de 10 peças, Dez, Censor, Contramestre).
- TRELLIS + forja → kits/props/inimigos; decimate de small_prop_item ≤ 600 tris fechado; Modo 2 ponta-a-ponta testado (pilar 2).
- Fab3D → auto_rig → retarget **validado no Unity** com animações Idle/Walk/Run/Attack/Talk (pilar 4).
- Gameplay: movimentação completa (04 §1), combate v1, 9 beats do Caldeiral (05 §3), boss Contramestre, primeira Re-Sincronia.
- **Gate M1:** rota de ouro jogável 70–90 min de conteúdo mock, 60 fps no alvo, captures aprovadas pelo Reviewer. **Aqui o jogo vale publicar como demo.**

### M2 — Vozes e diálogos (semanas 9–10) → **Pilar 5**
- Instalar TTS em D:\ (XTTS-v2 + F5-TTS + Kokoro-82M); `tts_bridge.py` consome `dialogues.yaml` (deste pacote) → VO PT-BR + legendas.
- Caldeiral e hub falando; barks ativos; teste de duração ≤ max_duration_ms no CI.
- **Gate M2:** 100% das linhas do Caldeiral com áudio aprovado; legendas sincronizadas.

### M3 — Cutscene de abertura (semanas 11–12) → **Pilar 7**
- `CutsceneBuilder.cs` v1 (static/dolly/closeup + VO + legenda + fade + capturas por shot).
- `cs01_abertura` gerada ponta-a-ponta a partir do YAML deste pacote; pós-processo de vídeo para teaser (seu plano de mecânicas de vídeo para cutscenes entra aqui — o Timeline in-engine é a fonte; render de vídeo é derivado).
- **Gate M3:** cs01 reproduzível do YAML limpo (re-geração byte-idêntica das capturas), Reviewer aprova 11 shots.

### M4 — Fusos do Ato 2 (semanas 13–20) → **pilares 2/3/4 em série**
- Vitralha → Marésia → Ecodia → Retrógrado (1 fuso por ~2 semanas, reusando o pipeline calibrado).
- Linha do Equilíbrio completa (grafo na Tela de Fusos), CS02 (A Oferta), Melhorias + economia de Segundos.
- **Gate M4:** 4 fusos aprovados; build Android com download de fuso (Addressables) funcionando; decisão de monetização (premium).

### M5 — A Corona e finais (semanas 21–24) → **pilar 6 + 7**
- Trilha adaptativa em 3 camadas fechada (pilar 6); boss rítmico da Íria; CS03 e CS04 (com `branches:`); pós-créditos.
- New Game+ "Hora Eterna"; polimento de game feel (margens de 10% nos números de 04).
- **Gate M5:** rota completa 4–6 h aprovada; crash rate < 0,5% em smoke de 100 sessões.

### M6 — Lançamento (semanas 25–26)
- Soft launch Android (readiness: retenção D1 ≥ 35%, crashes, telemetria leve).
- PC/Itch simultâneo; PT-BR + EN.
- **Gate M6:** metas de 00 §6; pós-launch: hotfix lane + backlog da série ORA.

## 3. Dependências críticas (o que bloqueia o quê) `[NOVO]`

| Depende de | Para entregar | Risco se faltar |
|---|---|---|
| ComfyUI + concept_bridge (pilar 3) | Todo visual (M1+) | Pipeline 3D sem referência fica inconsistente |
| Fab3D→auto_rig→retarget validado (pilar 4) | Personagens animados (M1) | Jogo vira "prop museum" |
| tts_bridge (pilar 5) | Diálogo e cutscenes (M2+) | cs01 sem VO: péssimo primeiro impacto |
| CutsceneBuilder v1 (pilar 7) | Ato 0 e finais (M3/M5) | Cutscene manual = quebra do princípio da fábrica |
| Art Director + Reviewer (pilar 8) | Consistência visual (M0+) | Cada fuso sai com cara de um jogo diferente |

## 4. Backlog pós-lançamento (semente da série) `[NOVO]`

- Fuso 7+ "acordado" no pós-créditos (conteúdo do jogo 2).
- Modo "Concerto Livre": sandbox de kit (jogadores remontam zonas — usa o mesmo pipeline).
- Suporte de gamepad mobile refinado + cloud save.
- Estúdio: ComfyUI de texturas por segunda GPU? (Só se D:\ sobrar folga — nunca bloquear jogo.)

## 5. Estimativa honesta de esforço `[NOVO]`

26 semanas com a fábrica funcionando é **agressivo mas realista** para 1 pessoa + agentes, porque: assets nascem validados (sem loop manual), VO custa ~0, cutscenes são YAML. Os riscos estão concentrados em M1 (primeiro fuso completo — onde os pilares 3/4 estreiam juntos) e M3 (CutsceneBuilder inédito). Recomendação: não prometer data externa antes do Gate M1.
