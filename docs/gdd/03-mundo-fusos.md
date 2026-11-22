# 03 — Mundo: Ora, os Fusos e as Relíquias

> Biomas/dimensões modulares com identidade forte `[DEFINIDO]` · estados temporais duplos `[DEFINIDO: biomas mudam com alterações temporais]` · tags: ver README

## 1. O planeta Ora `[NOVO]`

Ora é um planeta pequeno e industrial, construído em torno de uma única maravilha: o **Grande Relógio**, uma máquina do tamanho de uma cordilheira que não marcava o tempo — ele o **sustentava**. Sem o Relógio, os dias de Ora não "continuam"; eles param. Antes da Fratura, Ora era barulhenta e alegre: caldeiras cantando, turnos trocando, samba-marchas nas ferrovias (a música do jogo tem base marchinha + mecânica — o "concerto" do título também é musical). Depois: silêncio de estática, cada região presa no seu segundo.

**Geografia simbólica** `[NOVO]`: os seis Fusos do jogo 1 formam um círculo ao redor da **Corona** (o Relógio, no centro-vertical do planeta — um "pólo temporal" que flutua acima do ponto de fratura). O hub **A Orquestra** fica na borda entre Caldeiral e o círculo: uma antiga sala de concertos do Relógio, hoje ponto de encontro dos fugitivos da Hora Eterna.

**Modularidade real** `[NOVO, serve DEFINIDO: design modular otimizado para geração por IA]`: cada fuso é construído de **kits modulares** com regras de encaixe (ver 05 e 07) — plataformas-viga, catracas-porta, torres-caldeira, dutos, trilhos. Os kits são os "vocabulários" que a Fábrica 3D (TRELLIS) produz em série e o level design combina. O mesmo kit recebe 2 variantes de textura/estado (Desincronizado/Restaurado) — metade do visual "dois estados" vem de graça do pipeline.

## 2. Os seis Fusos do jogo 1 `[NOVO, cada um implementa DEFINIDO: biomas com identidade forte]`

### 2.1 Caldeiral — "a oficina que nunca fecha"
- **Estado Desincronizado:** presos no "Grande Turno": milhares de mecânicos trabalhando em loop, cinza-cobre, fumaça estacionária, sirene de 4 segundos que todos atendem. Atmosfera: bela e triste (Metrópolis-meets-Wall-E).
- **Estado Restaurado:** o turno acaba — mercado noturno, marchinha nas caixas de música, luzes de sódio quentes, a mesma geometria agora colorida e habitada.
- **Cor:** cobre #8c5a2b / cinza-fumaça / sódio âmbar (restaurado) — ver art-bible.yaml.
- **Papel no jogo:** tutorial + Ato 1; boss **O Contramestre**; Relíquia: **Mola**.
- **Momento assinatura:** a primeira Re-Sincronia: a sirene para no meio do som, e a fumaça congelada cai como neve para revelar o mercado.

### 2.2 Vitralha — floresta de vidro
- **Desincronizado:** floresta congelada "no meio da primavera para sempre": árvores-catedral de vidro soprado, luz refratada em mosaico, pássaros-vidro parados no ar (plataformas vivas). Frio: azuis-verdes, cromado.
- **Restaurado:** o rio volta (consequência: alaga Marésia), as árvores "descem" ao chão e florescem, luz âmbar-quente; plataformas agora se movem com o vento (desafio novo).
- **Papel:** Relíquia: **Ponteiro Maior** (Sopro Estendido); boss **A Estufa Viva** (estufa-robô que "cultiva" lutas).
- **Gameplay assinatura:** plataformas de luz refratada — o jogador atravessa "feixes" de luz que só existem no estado Restaurado.

### 2.3 Marésia — o mar parado
- **Desincronizado:** oceano congelado em ondas de 10 m — barcos flutuando inclinados, peças de madeira/latão; caminhar sobre ondas congeladas é o platforming vertical (anda-se nas cristas).
- **Restaurado:** o mar degela (custo: arrasta pedaço da Ecodia) — navegação de barco-drift entre fusos, ondas como elevadores.
- **Papel:** Relíquia: **Pêndulo** (Garra com balanço — swing entre mastros); boss **A Baleia de Âncora** (leviã-hidro mecânico, não é combate de matar: é "desferrujar" em fases — variety de boss design).
- **Atmosfera:** melancolia bonita; a trilha é canto de baleia + violeiro na proa.

### 2.4 Ecodia — a cidade que repete 6 segundos
- **Desincronizado:** distrito urbano inteiro preso num loop de 6 s: um bonde repassa, um anúncio repete, moradores repetem gestos. Jogador é o único "fora do tempo" — pode apoiar-se em objetos que se movem de forma previsível (loop = puzzle-pista).
- **Restaurado:** tempo corre — NPCs "acordam" com memórias próprias (quests de memória: devolver a cada um o segundo que perderam), mas os Censores patrulham em tempo real (dificuldade sobe — custo do conserto).
- **Papel:** Relíquia: **Ponteiro Menor** (Micra, slow-time 2 s); boss **Eco Dez** — um eco de Desiderio que repete seu erro (drama+humor: ele erra as piadas de Dez, dizendo-as em ordem errada).
- **Estética:** art déco relojoeiro, pátina azul-madrugada + neons quentes de loja.

### 2.5 Retrógrado — o deserto do tempo reverso
- **Desincronizado:** areia cai **para cima**; pegadas se desmancham à frente; artefatos se "desgastam ao contrário" (voltam a ser novos). Plataformação de "desmontagem": plataformas existem cada vez **mais** conforme se espera (tempo invertido = janelas de oportunidade invertidas).
- **Restaurado:** tempo normal, ruínas do pré-Fratura reveladas (lore da série: os Outros Relógios), mas melhorias do Caldeiral "se desfazem" (custo cômico leve).
- **Papel:** Relíquia: **Aro** (Aro-Escudo, reflete Faísca); boss **O Arquivista-Mor** (guardião do "arquivo de amanhãs" — luta contra um inimigo que "já sabe" seus padrões; quebra-se ele imitando padrões que ele ainda não viu).
- **Estética:** âmbar-areia, vidros de relógio de bolso, dunas em forma de espiral de mola.

### 2.6 A Corona — o Grande Relógio `[DEFINIDO: bioma final/central NOVO]`
- Único fuso com **3 estados**: Quebrado (padrão), Sincronizado (sob Íria — linhas duras, azul-aço, coro mecânico), Vivo (final — todos os instrumentos tocando juntos).
- Plataformação nas engrenagens vivas do Relógio (as "muralhas que giram"); a luta com Íria acontece no Pêndulo-Mor, com a orquestra mecânica como banda.
- Relíquia final: **Escapamento** — não é coletada: é o coração do lugar (a última Relíquia é o próprio fuso `[NOVO]`).

## 3. As Relíquias do Compasso `[DEFINIDO: coleta de relíquias] + [NOVO: lista/efeitos]`

| Relíquia | Fuso | Habilidade destravada | Efeito no mundo |
|---|---|---|---|
| Mola | Caldeiral | Salto-Mola (pulo duplo +impulso; crava-mola ao cair) | Reativa o Bis → Re-Sincronia |
| Ponteiro Maior | Vitralha | Sopro Estendido (dash atravessa feixes longos) | Rio de Vitralha volta (→ alaga Marésia) |
| Pêndulo | Marésia | Garra-Balanço (swing com momentum) | Mar degela (→ rotas de barco; arrasta Ecodia) |
| Ponteiro Menor | Ecodia | Micra (2 s de câmera lenta; cooldown 8 s) | Cidade acorda (→ Censores em tempo real) |
| Aro | Retrógrado | Aro-Escudo (parry reflete Faísca em área) | Ruínas pré-Fratura (→ desfaz melhorias do Caldeiral) |
| Escapamento | A Corona | Re-Sincronia Total (todas as zonas, sem nó) | Fim da Hora Eterna (final) |

Cada Relíquia é também **peça-chave de um puzzle do seu fuso** (a Mola é o mecanismo da sirene; o Pêndulo balança no mastro da Baleia) — coleta nunca é só "pegar item brilhando".

## 4. Economia e coleta secundária `[NOVO]`

- **Segundos** (moeda): "fios de tempo" dourados no mundo; usados em Melhorias com Tio Horário. Nome reforça tema (colecionar tempo literal).
- **Memórias-Eco** (colecionável narrativo): 21 fragmentos (3 por fuso + 3 na Corona) que contam a pré-Fratura em micro-cenas — a arqueologia da história principal. Motivação do 100%.
- **Remendos** (cosmético): variantes de remendo do macacão de Dez, algumas compradas, algumas por conquista — coleção visual que conta a jornada.
- Sem inventário de itens: Relíquias + Melhorias + cosméticos. Uma tela só (`04-mecanicas.md`).
