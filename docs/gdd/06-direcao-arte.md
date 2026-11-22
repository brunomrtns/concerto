# 06 — Direção de Arte

> "Estilizado detalhado" `[DECISÃO]` · identidade forte por bioma `[DEFINIDO]` · contrato único de prompt para ComfyUI/Fab3D `[NOVO, alinhado ao pilar 8 do estúdio]` · tags: ver README

## 1. Direção geral: "Relógio de bruxo" (clockwork-whimsy) `[NOVO]`

A assinatura visual de CONCERTO é o encontro entre **mecânica de relógio** (latão, molas, catracas, esmalte de fogo) e **calor de oficina de família** (madeira gastada, cobre polido pelo uso, remendos coloridos, etiquetas escritas à mão). Tudo é legível em silhueta: cada forma tem um "mecanismo" visível — portas têm molas à mostra, prédios têm pêndulos estruturais, os Censores têm o rosto-relógio. Referências de espírito (não de cópia): a engenharia carinhosa de Laputa, o calor das oficinas de Robots (2005), o controle de forma de Okami-legibilidade, a luz de Interstellar nas cenas dramáticas `[DEFINIDO: inspiração]`.

**Estilizado detalhado significa** `[DECISÃO]`: formas simplificadas e limpas (silhueta em 3 segundos) com **detalhe mecânico concentrado** onde importa (juntas, mostradores, remendos). O detalhe é "informativo": todo detalhe explica função (um parafuso, um medidor, um cano). Detalhe sem função é proibido no art-bible — regra que também reduz custo de geração e de tris.

**Linguagem de forma por facção:**

| Facção | Forma | Material | Cor |
|---|---|---|---|
| Oficina/Livre (Dez, fusos restaurados) | arredondada, orgânico-mecânica, bolhas e rebites | latão, cobre, madeira, vidro soprado | âmbar/cobre quente |
| Sincronato (regime) | reta, angular, repetida em grid | aço azul, esmalte frio, vidro fosco | azul-aço, branco-osso |
| Desincronia (corrupção) | "derretida no tempo": bordas que arrastam trail, pátina | metal enferrujado, vidro rachado | ferrugem + magenta-estático |

Esta tríade dá leitura instantânea de aliado/inimigo/estado sem HUD — e mapeia direto nas 3 cores funcionais de combate (04 §2).

## 2. Paletas por fuso (hex de referência; versão completa no art-bible.yaml) `[NOVO]`

| Fuso | Estado Desincronizado | Estado Restaurado | Cor de energia/accents |
|---|---|---|---|
| Caldeiral | #6e5a4a fumaça, #3d342c carvão, #8c5a2b cobre | #e8a13c sódio, #b3542c marchinha, #2e2419 noite quente | âmbar #ffb454 |
| Vitralha | #4a6a6a cromo frio, #2c4044 sombra de vidro | #7ad0a8 folha, #d8e8a0 luz, #5a7a5a musgo | verde-sinal #35c98e |
| Marésia | #3a4a5e bruma, #5e7a8a espuma congelada | #2c6a7a mar, #e8d8a0 velas, #8a5a3a madeira | ciano-pérola #58d0d8 |
| Ecodia | #2c3444 madrugada, #444e5e pátina | #e85a3a neon de loja, #d8c8a0 art déco, #3a4460 céu | laranja-neon #ff8a5a |
| Retrógrado | #d8b878 areia invertida, #8a6a4a sombra errada | #c89858 duna, #4a6878 ruína, #e8e0c8 horizonte | âmbar-pálido #e8c88a |
| Corona | #1a2438 quebrado / #24384e sincronizado | #d8e8f8 vivo | luz branca-azulada #c8e8ff |

Regra do art-bible: **uma cor de energia por fuso** (accents de luz, partículas e Faísca local) — o jogador aprende "onde estou" pela cor da energia, mesmo em tela pequena.

## 3. Personagens: regras visuais `[NOVO, detalhado em 02]`

- **Dez:** silhueta "triângulo de cima" (cinto largo, cabeça grande expressiva); as 4 mãos nunca idênticas (uma sempre segurando ferramenta); sardas-luz = único elemento animado de emissão do corpo. Cores: macacão cobre #b35a2c com remendos das cores dos fusos.
- **Sincronato:** máscara-relógio branca com mostrador funcional (o ponteiro dos olhos aponta o alvo — telegraph honesto), corpo aço azul #3a5a7a. Nada de capacetes "de Cristo": o rosto-relógio é a identidade.
- **Ferrugens:** simetria quebrada (uma junta sempre "errada"), pátina com trilha-magenta de estática — inimigo parece doente, não maligno (tema: o tempo doente).

## 4. Luz e atmosfera `[NOVO]`

- **Regra do relógio:** luz "passa" — no estado Desincronizado, toda luz é estacionária (sombras congeladas, sem flicker); no Restaurado, luz respira (dust no ar, flicker de vela sutil). O jogador sente o estado antes de olhar o HUD.
- Sombra: 1 direcional por fuso (budget mobile) + emissivos; baked light em kit estático.
- Cenas dramáticas (cutscenes 02/03/04): contraluz + volumétrico fino (Interstellar-ish), mas sempre com um elemento "quente de oficina" no quadro (uma lâmpada de bancada) — o jogo nunca perde o calor.

## 5. HUD, UI e tipografia `[NOVO]`

- **Tipografia:** display com terminais "de mostrador" (escolher/criar fonte display com dígitos de painel analógico); corpo de texto: sans redonda legível em mobile. PT-BR com aspas curvas.
- **HUD diegético onde possível:** Pressão é o manômetro no pulso do Dez; Fusíveis são fusíveis de vidro na tela só quando mudam (fade automático).
- **Tela de Fusos:** o "mapa-joia" do jogo — grafo de engrenagens; cada fuso é um mostrador apontando seu estado; linhas de equilíbrio pulsam quando um conserto afeta outro. Essa tela é o cartão de visita da identidade visual do jogo (e o ícone do ícone do app).
- Ícones: família única de linha grossa 2 px com canto de "rebite" (imposto pelo art-bible para ComfyUI).

## 6. art-bible.yaml: o contrato com as pontes `[NOVO, pilar 8 do estúdio]`

O arquivo `art-bible.yaml` incluído no pacote é a proposta inicial do contrato que o **Art Director** (agente a criar no pilar 8) vai guardar e o **Strict Reviewer** vai auditar. Ele contém: paletas, regras de forma por facção, **prompt-base único** para ComfyUI (FLUX.1-schnell), negative prompt, padrão de imagem de referência para o Fab3D/TRELLIS (fundo neutro, luz de 3 pontos estilizada, escala de personagem com régua), regras de tileables e de ícones. Estratégia: **um só prompt-base com "modificadores por fuso/personagem"** — nunca prompts soltos por asset; assim o jogo inteiro nasce da mesma mão.

Checklist do Reviewer (resumo; completo no YAML): silhueta em 3 s; detalhe só com função; cor de energia única por fuso; 2 estados por peça de kit; escala com régua em toda ref de personagem; sem texto gerado por IA nas imagens (texto entra no Unity).
