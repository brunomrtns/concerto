# 01 — Narrativa

> Drama com humor leve `[DECISÃO]` · inspiração Interstellar (gravidade emocional em cenário mecânico) `[DEFINIDO]` · tags: ver README

## 1. Tema e tese

A tese do jogo 1: **tempo compartilhado demais é prisão; tempo solto demais é caos — viver é manter o próprio compasso.** O Sincronato vende a solução definitiva para a dor humana (no caso, alienígena): ninguém mais sofre "no horário errado", ninguém envelhece sozinho, ninguém perde alguém fora de turno. O custo é que ninguém tem um "seu" tempo — memórias, duelos, encontros e até o luto viram cronograma. Dez, que perdeu alguém fora de hora por causa de um parafuso mal calibrado, é a pessoa errada-perfeita para julgar essa proposta. `[NOVO, materializa DEFINIDO: antagonista coletivista + erro técnico do passado]`

O humor leve funciona como válvula e como assinatura de voz: Dez fala com as ferramentas, apelida o inimigo, e o drone Porca é literalmente uma porca de rosca com opinião. O ritmo narrativo alterna: 2 cenas de drama pesado → 1 respiro cômico (regra de ouro de pacing `[NOVO]`). A inspiração Interstellar aparece na relação irmão-irmã como "gravidade" da história: algo invisível, que deforma tudo ao redor `[DEFINIDO: inspiração]`.

## 2. Premissa e o erro técnico `[NOVO, fecha a lacuna aberta na conversa]`

Dez era mecânico de manutenção do **Grande Relógio** — a máquina-ótiva que, antes da Fratura, apenas marcava o tempo de Ora. Seu turno: lubrificar o Escapamento, o coração mecânico do Relógio. Na noite do "Grande Ajuste" (a cerimônia em que o regime recém-fundado sincronizaria todos os fusos), Dez fez um reparo de rotina com uma substituição de mola contraindicada — a única peça que sua irmã **Juno**, engenheira-chefe do Relógio, tinha proibido de trocar sem revisão. A mola errada, sob a carga de sincronizar um planeta inteiro, rompeu: o **Escapamento estilhaçou** e lançou seis fragmentos do Compasso pelos fusos de Ora. O planeta quebrou em **Fusos** — cada um preso num momento próprio. Juno, que estava na plataforma do Escapamento, foi apagada da linha do tempo: **nunca existiu** para todo mundo, exceto para Dez, protegido pela carga temporal do Bis que ele segurava (o protótipo que ela mesma construiu para ele).

O regime nasceu **depois** da Fratura e sobre a Fratura: a Maestrina Íria tomou o Relógio quebrado e o transformou em instrumento de controle — com o Escapamento ausente, o Relógio só consegue "pulsar" um único segundo repetido para todos: **A Hora Eterna**. A versão oficial ensinada nas escolas é outra: a Fratura foi obra de "sabotadores do caos", e a Hora Eterna é a vacina. A verdade sobre a mola (e sobre o que aconteceu com Juno) é a revelação do Ato 3 `[NOVO]`.

Por que a Maestrina caça Dez e não apenas o ignora: o Bis é feito de uma catraca original do Grande Relógio — a única chave viva que pode alternar fusos entre estados sem a autorização do regime. Cada fuso que Dez restaura enfraquece o pulso da Hora Eterna (o Relógio precisa "puxar" tempo dos fusos restaurados para manter a sincronia). Em jogo: **cada Relíquia coletada reduz a influência do regime no mapa** — narrativa e progressão são o mesmo sistema. `[NOVO]`

## 3. Estrutura em atos `[NOVO, implementa DEFINIDO: cutscene de abertura + arco de consertos]`

### Ato 0 — "O Concerto que Quebrou o Mundo" (cutscene de abertura) `[DEFINIDO que existe]`

Cutscene 2'30"–3'00" gerada pelo CutsceneBuilder a partir de `cutscene_01_abertura.yaml` (arquivo incluído no pacote). Estrutura: noturno industrial → reparo íntimo entre irmãos → a mola cede → fratura de luz → título CONCERTO. Termina em Dez acordando semanas depois numa sucata flutuante, com o Bis no pulso e o mundo em silêncio de estática. Sem diálogo expositivo demais: a história da mola é contada por imagem (close nas mãos de quatro braços trabalhando) — a Voz do Narrador entra apenas em duas linhas. Ver `dialogues.yaml` (seção `cs01`).

### Ato 1 — Fugir e Aprender (Fuso: Caldeiral)

Objetivo do jogador: sobreviver, entender os controles, reativar o Bis.

- Tutorial orgânico na Oficina-Navio de Dez: movimento básico, Câmera, interação.
- Primeiro contato com **Censores** — combate energético limpo, derrotável com Contratempo.
- Reativar o Bis com a **Relíquia: Mola** (primeira peça, obtida no boss **O Contramestre**) — destrava a **Re-Sincronia** local e o pulo duplo evoluído (Salto-Mola).
- Conhecer **Vó Segunda** (guia que vive "entre os segundos") e **Porca** (drone, humor).
- Final do ato: a fuga do Caldeiral ao som da primeira vinheta de fuso restaurado. `[NOVO]`

### Ato 2 — Os Fusos (hub: A Orquestra, central temporal abandonada)

Objetivo: coletar 4 Relíquias em fusos à escolha do jogador (ordem livre — cada ordem gera rede de equilíbrio diferente).

- **Vitralha** (floresta de vidro) → Relíquia: Ponteiro Maior → Sopro Estendido (dash atravessa vãos longos).
- **Marésia** (mar congelado em ondas) → Relíquia: Pêndulo → Garra com balanço (swing).
- **Ecodia** (cidade presa nos mesmos 6 segundos) → Relíquia: Ponteiro Menor → Micra (câmera lenta de 2 s).
- **Retrógrado** (deserto onde o tempo corre ao contrário) → Relíquia: Aro → Aro-Escudo (reflete Faísca).
- Twist de meio de jogo (**cutscene_02_a_oferta**): a Maestrina Íria fala com Dez através de um Censor e oferece um acordo — devolva as Relíquias e ela devolve Juno ("Eu guardo todos os tempos; guardo também o dela."). A tentação é real: o jogo não diz que é mentira. `[NOVO]`
- Consequências de equilíbrio cruzadas entre fusos ficam mais pesadas aqui (ex.: restaurar Vitralha traz o rio de volta e alaga as cavernas de Marésia onde mora gente). `[NOVO, implementa DEFINIDO: consertar cria novos problemas]`

### Ato 3 — A Corona (Grande Relógio)

- Infiltração no Relógio usando todas as habilidades; plataformação "greatest hits".
- **cutscene_03_a_verdade**: dentro do Escapamento vazio, Dez descobre que o pulso da Hora Eterna é alimentado pelo eco de Juno — "A Primeira Nota". O regime não apagou a irmã de Dez: ela existe como o coração da máquina que oprime o mundo. Só Dez a ouve.
- **Boss final: Maestrina Íria** — luta rítmica (atacar nos tempos da música; Contratempo nos contratempos dela).
- **Decisão final** (cutscene_04_fim_e_depois): libertar a Nota ou entregá-la. Ver seção 4.
- Pós-créditos: teaser da série — um fuso novo "acorda" fora do mapa, com um som que Porca reconhece: "Essa batida... eu conheço essa batida." `[NOVO]`

## 4. Finais `[NOVO]`

A decisão final tem duas pontas e um epílogo compartilhado (mantém a série viável):

1. **"Solta a Nota" (canônico):** o eco de Juno deixa de ser bateria do Relógio. Os fusos passam a respirar com tempos próprios; a Hora Eterna acaba. Juno não volta — seu eco diz a última linha: "Não me concertes às pressas, Dez. Conserta o mundo primeiro." Drama fecha, série abre: o jogo 2 busca o jeito de trazê-la de volta (as Relíquias espalhadas pelos fusos do universo ORA).
2. **"Devolve a Nota" (secundário):** Dez entrega as Relíquias. A Hora Eterna se fortalece; Juno "prometida" continua sendo só promessa; os fusos restaurados re-desincronizam na frente do jogador. Final triste com uma fresta de esperança (Porca guarda uma cópia da batida da Nota). Desbloqueia modo "Hora Eterna" (New Game+ com regras do regime: cronômetro global). `[NOVO]`

## 5. Sistema narrativo: Linha do Equilíbrio `[NOVO, implementa DEFINIDO]`

Cada restauração de fuso define **1 benefício + 1 custo** em outro fuso. Exemplos canonizados (vazados para o design de levels em `05`):

| Conserto | Benefício | Custo |
|---|---|---|
| Vitralha | Rio volta a correr → novo acesso aquático em Marésia | Enchente alaga cavernas-habitação de Marésia (quest de desvio) |
| Caldeiral | Energia estável para a Orquestra (hub ganha NPCs e loja) | Fumaça cessa → céu limpo congela o Retrógrado por 1 "dia" (surpresa para quem foi primeiro) |
| Marésia | Mar degela → rotas de barco entre fusos | Ondas soltas arrastam um pedaço da Ecodia (prédio-quebra-cabeça) |
| Ecodia | Habitantes saem do loop de 6 s (quests de memória) | Sem o loop, os Censores passam a patrulhar em tempo real (dificuldade sobe) |
| Retrógrado | Areia-parada revela ruínas do pré-Fratura (lore + desafios) | O tempo reverso "desfaz" melhorias feitas em Caldeiral (nada crítico, humor) |

A interface "Tela de Fusos" mostra o grafo com ícones de benefício/custo **antes** do jogador confirmar o conserto — decisão informada, sem tela de moralidade. `[NOVO]`

## 6. Diálogo e texto `[NOVO]`

- **Formato de origem única:** todo fala nasce em `dialogues.yaml` (id, personagem, voz, emoção, texto, legendas) — consumido pelo `tts_bridge.py`. Nada de áudio solto.
- **Voz do Dez:** primeira pessoa, frases curtas, ironia carinhosa; nunca sarcasmo cruel. Ex.: "Se eu tivesse um cruzado por cada vez que 'concertar' deu errado... ah, não. Esse é o problema."
- **Regra do humor:** piadas não negam o drama; elas suspendem por 20 segundos e devolvem. Nunca piada no meio de uma morte em cutscene.
- **PT-BR primeiro, EN segunda:** textos escritos em PT-BR; EN gerado por revisão humana + TTS em EN (o stack de TTS suporta ambos `[DEFINIDO]`).
- **Legendas:** todo VO tem legenda embutida (gerada junto com o áudio pelo tts_bridge, com durações). Mobile-first: texto mínimo 14 pt equivalentes.

## 7. Ganchos para a série ORA `[NOVO, serve DEFINIDO: série]`

- Juno como objetivo de longo prazo (não morreu; está "fora do compasso").
- Os "Outros Relógios": sugestão plantada no pós-créditos de que Ora não era o único planeta com Grande Relógio (universo com fusos entre mundos).
- Personagens secundários com arco próprio: Porca descobre que foi construída por Juno (jogo 2); Vó Segunda sabe mais sobre a Fratura do que diz (jogo 3).
- Modo "Hora Eterna" do final secundário pode virar formato de desafio serial da série.
