# CONCERTO — Documento de Design do Jogo (GDD)

> Jogo 1 da série **ORA** · Estúdio: Fábrica de Games IA (brunomrtns) · Versão 0.3 · 2026-11-22

Este pacote organiza tudo o que foi definido na conversa de concept (agente Gemini), as decisões fechadas com você e os desdobramentos que eu preenchi para deixar o documento acionável pelo estúdio (gamedev-setup-ia + 3dprinter-setup-ia).

## Como ler as tags

| Tag | Significado |
|---|---|
| `[DEFINIDO]` | Veio da conversa original (Gemini) ou das suas mensagens. **Ideia sagrada — não alterada.** |
| `[DECISÃO]` | Fechado nas perguntas rápidas que te fiz (formato, profundidade, plataforma, tom, visual, combate, nome). |
| `[NOVO]` | Preenchi eu, com aprovação total que você me deu. **Pode vetar/trocar sem quebrar nada.** |

## Estrutura do pacote

| Arquivo | Conteúdo |
|---|---|
| `00-visao-geral.md` | Pitch, pilares de design, gênero, plataforma, título e série |
| `01-narrativa.md` | Temas, premissa, história por atos (0–3), finais, ganchos da série |
| `02-personagens.md` | Fichas de personagem prontas para geração de assets + cast de vozes |
| `03-mundo-fusos.md` | Planeta Ora, o Sincronato, os 6 Fusos, as Relíquias, sistema de Equilíbrio |
| `04-mecanicas.md` | Movimentação, combate, Re-Sincronia, progressão, controles (touch + gamepad) |
| `05-level-design.md` | Estrutura hub+Fusos, beat map do Caldeiral e da Vitralha, checkpoints, sessões mobile |
| `06-direcao-arte.md` | Direção visual "estilizado detalhado", linguagem de forma, paletas, HUD |
| `07-pipeline-assets.md` | Fluxo texto→2D→3D→rig→Unity com TRELLIS, ComfyUI, Blender, Fab3D |
| `08-audio-vozes.md` | Música/SFX, stack de TTS local, casting, schema `dialogues.yaml`, normalização |
| `09-cutscenes.md` | Sistema declarativo `cutscene_*.yaml` para o CutsceneBuilder.cs, lista de cutscenes |
| `10-spec-tecnica.md` | Unity 6.3 URP, alvos híbridos, budgets de performance, organização Studio Core vs. jogo |
| `11-roadmap.md` | Marcos M0–M6 mapeados nos 9 pilares do estúdio, gates de validação, riscos |
| `art-bible.yaml` | Contrato de direção de arte para ComfyUI/Fab3D (proposta pronta para o repo) |
| `dialogues.yaml` | Roteiro de falas pronto para o `tts_bridge.py` (cutscene de abertura + exemplos) |
| `cutscene_01_abertura.yaml` | Cutscene de abertura declarativa, pronta para o CutsceneBuilder.cs |

## Pitch em uma linha

Um mecânico alienígena fugitivo, com um dispositivo de catraca no pulso, precisa re-sincronizar os estilhaços temporais de um planeta que ele mesmo quebrou — sabendo que cada conserto cria um novo problema em outro lugar. `[DEFINIDO] + [NOVO]`

## Decisões fechadas `[DECISÃO]`

- **Formato**: pacote `.md` no padrão `docs/` do estúdio + PDF mestre + ZIP com tudo.
- **Profundidade**: completo (~25–35 páginas) — história por atos, level design, pipeline de assets, cutscenes prontas para TTS.
- **Plataforma**: híbrida — Android como alvo de build do estúdio, PC como alvo de desenvolvimento.
- **Protagonista**: Desiderio, com apelido (**"Dez"**).
- **Tom**: drama + humor leve.
- **Visual**: estilizado detalhado.
- **Combate**: energético limpo (sem sangue, focus em leitura clara de ação).

## Glossário rápido `[NOVO]`

- **Ora** — planeta da série; nome que também é piada recorrente ("Ora, ora.").
- **A Fratura** — evento que estilhaçou o tempo de Ora em Fusos.
- **Fuso** — cada bioma/dimensão; um pedaço do planeta preso num momento próprio.
- **Desincronia** — estado "doente" de um Fuso (ferrugem temporal, loops, estática).
- **Re-Sincronia** — a habilidade do dispositivo **Bis**: alternar uma zona entre dois estados.
- **Sincronato** — o regime de coletivismo temporal extremo; slogan: "A Hora Eterna".
- **Censores** — enforcadores do regime, de rosto-relógio.
- **Relíquias do Compasso** — 6 peças do Grande Relógio espalhadas pelos Fusos.
- **Contratempo** — o parry do jogo (também significa "reverso" musical: dobrar o tempo contra o ataque).
- **Ferrugem Temporal** — corrupção que cria os inimigos comuns (máquinas enferrujadas pelo tempo quebrado).
