# 02 — Personagens

> Fichas prontas para o pipeline de assets (conceito 2D → TRELLIS → auto_rig) e para o casting de vozes TTS `[NOVO]` · tags: ver README

## 1. Desiderio "Dez" `[DEFINIDO: nome em consideração, agora fechado DECISÃO + apelido NOVO]`

**Ficha.** Espécie: **varn**, de Varn-4 (lua de um gigante gasoso) `[NOVO]`. Profissão: mecânico de manutenção `[DEFINIDO]`. Idade: ~32 anos varn (~meia-idade, vive ~90). Altura: 1,25 m. Traço: humilde, carismático, fala rápido demais, arruma o que não foi pedido `[DEFINIDO]`. Defeito: culpa transformada em autossuficiência — nunca pede ajuda, sempre conserta antes de chorar.

**Corpo (para o rig — decisão de pipeline `[NOVO]`):** bípede humanoide-padrão para o `auto_rig.py` e retarget Mixamo-compatible; **4 braços**, sendo **2 principais** (mapeados nos slots Humanoid Upper/Lower Arm) e **2 braços-auxiliares menores** saindo do cinto-ferramentas, rigados como bones acessórios **fora do mapeamento Humanoid** (animados por curvas simples). Assim o fluxo Fab3D → auto_rig → retarget (Idle/Walk/Run/Attack/Talk) funciona sem adaptação, e o visual "mecânico de quatro mãos" é mantido. Pele âmbar oxidada com sardas luminosas que pulsam no ritmo do fuso local (bug vira feature: as sardas são o "medidor" narrativo da Hora Eterna).

**Traje:** macacão corta-fogo cor de cobre velho com remendos coloridos (cada remendo = um fuso visitado — acumula no jogo, memória visual da jornada), cinto-ferramentas com 6 bolsos, botas magnéticas com sola de borracha berrante-verde, e o **Bis** no pulso esquerdo.

**Bis (o dispositivo)** `[DEFINIDO: dispositivo de viagem no tempo] + [NOVO: nome/design]`: catraca original do Grande Relógio adaptada por Juno como presente. Design: círculo de latão com janela mostrando um ponteiro que gira ao contrário quando a Re-Sincronia está disponível; faz som "tíque-tíque" nos menus (assinatura sonora). Humor recorrente: Dez conversa com o Bis; o Bis "responde" por meio de ticks (Porca traduz, sempre errado).

**No combate:** Chave Sonora (melee), Faísca (ranged), Martelada Aérea (carga aérea) `[DEFINIDO: carga aérea + ataque de energia à distância] + [NOVO: nomes]`.

**Prompt-base de conceito (para o ComfyUI/art-bible):** "small alien mechanic, bipedal, 4 arms (2 main + 2 small back arms), warm oxidized-brass skin, glowing freckles, patched fireproof jumpsuit, oversized tool belt, wrist-mounted brass turnstile device, stylized detailed, friendly expressive face" — ver `art-bible.yaml`.

## 2. Elenco principal

### Juno (irmã de Dez / A Primeira Nota) `[NOVO, nome e papel; "irmã/memória perdida" deriva do drama DEFINIDO]`
Engenheira-chefe do Grande Relógio, a melhor de Ora. Rigorosa, sarcasmo seco, chamava Dez de "mão-de-obra" com orgulho. Construiu o Bis e a catraca de seu pulso. Apagada da linha do tempo pelo erro de Dez; existe agora como eco-motriz do Relógio (A Primeira Nota). Aparência nas memórias/echos: formas ligeiramente "atrasadas" (trail de movimento de meio segundo atrás dela — assinatura visual do estado dela). Aparição: cutscenes 01, 03, 04 + eco sussurros durante o jogo.

### Porca (companheira-drone) `[NOVO]`
Drone flutuante de manutenção, esfera de latão com garra magnética e olho-de-lente. Humor principal do jogo: pedante, literal, apaixonada por regras do manual "Manual de Boas Práticas de Fraturas Temporais, 3ª ed.". Traduz os ticks do Bis — sempre errado. Combate: marca alvos, puxa itens, segura plataformas (co-op de movimento). Pipeline: personagem flutuante **sem rig bípede** — animado por curvas/bones simples (não passa no auto_rig; precisa ser gerado no TRELLIS como peça única + animação procedural). Voz: TTS leve (Kokoro-82M) com pitch alto + leve formant shift.

### Vó Segunda (guia) `[NOVO]`
Variana anciã que vive "entre os segundos" — uma brecha estável da Fratura onde o tempo escorrega. Nome vem da cultura pós-Fratura: pós-fratura, pessoas ganharam nomes de unidades de tempo (Vó Segunda, Tio Horário, Prima Cadência). Função: tutorial narrativo, quests secundárias de memória, comércio de Melhorias. Aparência: pequena, óculos de lente dupla, casaco de bolsos com relógios quebrados (cada um parado num momento que ela se recusa a esquecer — micro-histórias).

### Tio Horário (mercador) `[NOVO]`
Comerciante itinerante entre fusos via "atalhos" que só ele conhece. Vende Melhorias (upgrades) e cosméticos de remendo para o macacão. Humor: cobrança em "segundos" (moeda do jogo: **Segundos**, coletados no mundo `[NOVO: economia simples]`).

## 3. Antagonistas

### O Sincronato (regime) `[DEFINIDO: coletivismo extremo] + [NOVO: nome/mecânica]`
Estado único pós-Fratura. Ideologia "Horólatra": o tempo individual é a fonte de toda desigualdade (uns envelhecem, outros não; uns perdem cedo, outros tarde). Solução: **A Hora Eterna** — todos vivem o mesmo segundo sincronizado, todos os turnos, memórias e lutos agendados. Estética: azul-aço, linhas retas, faixas com mostradores, cartazes "SEU TEMPO É O NOSSO TEMPO". Militares: **Censores** (enforcadores de rosto-relógio), **Arquivistas** (cobradores do "imposto de memória" — coletam lembranças "não autorizadas").

### Maestrina Íria (líder / boss final) `[NOVO]`
Fundadora do Sincronato, ex-regente de orquestra de Ora. Carismática, cansada, convincente: perdeu o próprio filho num acidente "fora de hora" — a Fratura deu a ela a certeza de que o tempo imprevisível é o inimigo. Não odeia Dez: **quer contratá-lo** (a melhor mecânica que Ora já viu). A oferta do Ato 2 (devolver Juno) é a arma dela. Luta final: rítmica — ela "rege" os ataques como uma orquestra; o jogador vence tocando **contra** (contratempo). Visual: 2,1 m, manto-capa com cauda de pauta musical, batuta de mola, rosto jovem demais (ela "gastou" anos dos outros para ficar no mesmo tempo).

### Inimigos comuns — Ferrugens `[NOVO, materializa combate "energético limpo" DEFINIDO]`
Máquinas corrompidas pela Desincronia (ferrugem temporal): relógios-robôs, marmitas-mecânicas, catracas selvagens. Design em 3 famílias, todas geráveis no TRELLIS a partir de kits (≤ 6k tris):
- **Ferrugem-Trabalhadora** (melee, lenta, comum) — marreta de pistão.
- **Ferrugem-Espelho** (ranged, reflete Faísca sem o Aro) — dutos de pressão.
- **Ferrugem-Coletora** (support, escuda outras; prioridade tática) — sino de coleta.
**Censores** (elite): rosto-relógio, 2 formas (patrulha ≤ 6k tris; pesada ≤ 8k tris). Derrotáveis só com Contratempo no primeiro encontro (ensina o parry).

## 4. Cast de vozes (TTS) `[NOVO, alinhado ao pilar 5 do estúdio]`

| Personagem | Motor recomendado | Direção de voz | Notas técnicas |
|---|---|---|---|
| Dez | XTTS-v2 (ou F5-TTS) | Barítono-tenor quente, leve rouquidão, ritmo acelerado | 6–10 samples de referência; speed 1.05 |
| Íria | XTTS-v2 | Mezzo baixa, serena, pausas longas | speed 0.92; processar leve sala grande |
| Porca | Kokoro-82M | Aguda, rápida, clipada "de alto-falante pequeno" | pitch +6 st, leve ring modulation |
| Vó Segunda | F5-TTS | Idosa, quente, arrastada | speed 0.88 |
| Tio Horário | F5-TTS | Grosso, vendedor, risada no meio | — |
| Juno (ecos) | XTTS-v2 | Suave, próximo demais do ouvido | reverb + lowpass 3 kHz + delay 220 ms (echo temporal) |
| Narrador | F5-TTS | Neutro-contador de histórias | usado só na abertura e vinhetas |

Regras: 1) todo VO em PT-BR e EN a partir do mesmo `dialogues.yaml` `[DEFINIDO: PT-BR/EN]`; 2) vozes clonadas apenas de amostras criadas para o projeto (sem vozes de terceiros); 3) processamento pós-TTS (echo de Juno, pitch de Porca) feito no `tts_bridge.py` via chain ffmpeg, declarado no YAML — ver `08-audio-vozes.md`.

## 5. Fichas para geração de assets (resumo por personagem)

| Personagem | Tris (alvo) | Rig | Animações base | Texturas |
|---|---|---|---|---|
| Dez (hero) | ≤ 8.000 | Humanoid + 2 bones aux | idle, walk, run, dash, jump, dbljump, wallrun, attack_melee, attack_ranged, aerial_slam, parry, talk, hurt | 1024 (atlas) |
| Porca | ≤ 2.500 | Custom (float rig) | hover loop, grab, point, spin | 512 |
| Vó Segunda | ≤ 4.000 | Humanoid | idle, walk, talk, gift | 512 |
| Tio Horário | ≤ 4.000 | Humanoid | idle, walk, talk, trade | 512 |
| Íria | ≤ 12.000 (boss) | Humanoid | idle, walk, conduct (5 padrões), attack_conduct, hurt | 1024 + emissive |
| Censor (2 formas) | ≤ 6.000 / ≤ 8.000 | Humanoid | idle, walk, run, attack, parry_stun | 512 |
| Ferrugens (3) | ≤ 3.500 | Generic/4-leg custom | idle, walk, attack, die | 512 |
| Juno (echo) | ≤ 6.000 | Humanoid | idle, walk, talk, fade | 512 + emissive |

Orçamentos detalhados e fluxo de produção em `07-pipeline-assets.md`. Animações base compartilhadas via retarget para minimizar o custo do Pose Studio; poses personalizadas por personagem entram na biblioteca JSON de poses do 3dprinter-setup-ia.
