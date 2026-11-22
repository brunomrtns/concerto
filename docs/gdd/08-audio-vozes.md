# 08 — Áudio, Vozes (TTS) e Música

> Vozes locais por TTS, PT-BR/EN `[DEFINIDO: TTS + tts_bridge.py + dialogues.yaml]` · casting e specs `[NOVO]` · tags: ver README

## 1. Direção de música `[NOVO]`

A trilha é **uma orquestra de oficina**: marchinha brasileira + samba-marcha de ferro (correias, martelos calibrados, sinos de bicicleta, caixas de música de latão) sobre acordes de oboé/violoncelo para o drama. Referência de espírito: bandas de metais de feira + coro mecânico (cada fuso "toca" com sua percussão própria). A ideia central (pilar 2: tempo é ritmo): **a música é o relógio do mundo** — nos fusos Desincronizados, a trilha está "emperrada" (loop curto, nota presa, metrônomo atrasando); nos Restaurados, ela abre em compasso completo.

Camadas adaptativas (3 camadas por fuso): **A** ambiente/percussão de oficina · **B** melodia de fuso · **C** camada de tensão (combate/boss). Composição por conjunto de stems; SFX de marteladas fazem o "shaker". Tema principal: "Concerto" — a marchinha que a Orquestra toca incompleta no início do jogo e ganha instrumentos conforme fusos são restaurados (o hub como medidor musical).

Fontes locais (pilar 6 do estúdio): geração por Stable Audio Open / AudioGen para texturas e SFX de oficina + banco CC0 local como fallback; trilha melódica composta por stem (loop) para garantir identidade. Tudo normalizado em **-16 LUFS** (padrão mobile) com true peak -1 dBTP — normalização automática no pipeline de áudio (pilar 6).

## 2. SFX `[NOVO]`

| Família | Exemplos | Fonte |
|---|---|---|
| Movimentação | Sopro (sopro de vapor + pitch bend), Rebote (mola), Corrimão (trilho), Garra (catraca) | AudioGen + banco CC0 |
| Combate | Chave Sonora (acorde metálico harmônico por golpe), Faísca (arco de energia), Contratempo (clic de trava + silêncio de 0,1 s — assinatura!) | AudioGen |
| Bis/Re-Sincronia | tíque-tíque de catraca + "reverso" (áudio rodando ao contrário por 0,8 s) | sintetizado + reverso |
| Mundo | sirene do Caldeiral, mosaico de vidro da Vitralha, canto da Baleia | AudioGen/gravação CC0 |
| UI | catraca nos menus (a mesma do Bis — identidade) | sintetizado |

Regra de assinatura: **o Contratempo entra com 0,1 s de silêncio absoluto** (o mundo "para de tempo" — som como mecanica, não decoração).

## 3. Stack de vozes TTS `[DEFINIDO pilar 5] + [NOVO: casting]`

| Personagem | Motor | Emoções suportadas no YAML | Velocidade |
|---|---|---|---|
| Dez | XTTS-v2 (fallback F5-TTS) | neutro, feliz, ironico, urgente, triste | 1.05 |
| Maestrina Íria | XTTS-v2 | sereno, ameaçador-suave, triste | 0.92 |
| Porca | Kokoro-82M | neutro, alarmado, orgulhoso | 1.15 |
| Vó Segunda | F5-TTS | neutro, brincalhão, sério | 0.88 |
| Tio Horário | F5-TTS | neutro, vendedor | 1.0 |
| Juno (eco) | XTTS-v2 | amoroso, triste, firme | 0.95 |
| Narrador | F5-TTS | neutro | 1.0 |

Decisões técnicas: (a) modelos instalados em **D:\** (708 GB livres, sem risco de C:); (b) execução **sequencial** por job para respeitar os 12 GB de VRAM; (c) PT-BR primeiro, EN como segunda passada do mesmo YAML (`lang: en`), permitindo lançamento bilíngue sem re-trabalho `[DEFINIDO: PT-BR/EN]`; (d) vozes clonadas **só de amostras criadas para o projeto**; (e) todo VO passa por normalização e de-essing no `tts_bridge.py`; (f) arquivo final `.ogg` (Unity-friendly) + `.wav` de fonte + `.json` de timings (para legendas sincronizadas e lip-sync fake no Personagen: escala de emissão das sardas/frente do mostrador).

## 4. `dialogues.yaml` — o contrato com o tts_bridge `[NOVO, alinhado ao seu pilar 5]`

Schema (versão completa de exemplo no arquivo `dialogues.yaml` do pacote):

```yaml
meta:
  lang_default: pt-BR
  output_dir: Assets/Game/Audio/VO/
  formats: [ogg, wav]
  normalize: { lufs: -16, true_peak: -1 }
voices:
  dez:    { engine: xtts-v2, ref: voices/dez/*.wav, speed: 1.05 }
  porca:  { engine: kokoro-82m, pitch_shift: 6, formant: 1.2 }
  juno:   { engine: xtts-v2, fx: [echo_220ms, lowpass_3k] }
lines:
  - id: cal_tuto_parry_01
    scene: caldeiral/tutorial
    character: dez
    voice: dez
    emotion: ironico
    max_duration_ms: 4000
    subtitles: { "pt-BR": "Parry no contratempo, Porca. Não no meio...", "en": "Parry on the off-beat, Nut. Not in the middle..." }
    text: "Parry no contratempo, Porca. Não no meio — no contratempo."
```

Regras de conteúdo: (a) `id` único, prefixo do fuso/cena (estável, nunca renomear — scripts referenciam); (b) `text` = fala; `subtitles` = legendas (podem abreviar); (c) `max_duration_ms` obriga o bridge a re-gerar acelerando (ou falha o job — protege pacing); (d) barks de gameplay (ex.: `bark_dez_dash_01`) ficam no mesmo arquivo, seção `lines` com `scene: barks` — um formato só.

## 5. Integração no jogo `[NOVO]`

- **Box de diálogo:** retrato 512 + nome + legenda; texto avança por palavra com o VO (timings do .json); botão pula linha (mobile-friendly).
- **Barks:** pool de falas curtas disparadas por triggers (dash, parry, fuso visitado) — vida constante do jogo com custo zero de gravação.
- **Censores não falam** (só tique-taque) — a linguagem do regime é não-fala: reforça tema.
- **Idioma:** voz e legenda seguem idioma do jogo; trocar idioma regenera apenas legendas (áudio das duas línguas já no build — APK + download por fuso).

## 6. Orçamento de produção de VO `[NOVO]`

| Item | Quantidade | Observação |
|---|---|---|
| Falas de cutscene | ~120 linhas | cs01–cs04 + vinhetas |
| Diálogo de quest/mundo | ~300 linhas | NPC + ecos de Juno |
| Barks | ~150 linhas | pools de 5–10 por contexto |
| Total | ~570 linhas | gerar em lotes por cena; gate de revisão por lote |

Com TTS local, o custo marginal por linha é ~0 — a limitação real é revisão de naturalidade (lote revisado = lote aprovado pelo Reviewer).
