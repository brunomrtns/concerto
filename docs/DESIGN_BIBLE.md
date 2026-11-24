# DESIGN_BIBLE — CONCERTO

Versão 1.0 · Manual de design do projeto · Fonte de verdade de regras e métricas

> **COMO USAR ESTE ARQUIVO:** todo código, cena, cutscene e task daqui pra frente
> OBEDECE a este manual. Toda task nova **declara conformidade** nos itens que
> toca (template na seção 12). Violação P1 = conserta antes de entregar.
> Violação P2 = backlog documentado. Este arquivo é a LEI do projeto; mudar uma
> regra daqui só com justificativa no relatório da task.
>
> **Este manual é auto-suficiente: não depende de internet nem de download.**
> Todas as leis e métricas estão completas aqui dentro. As fontes da seção 13
> servem só para aprofundamento opcional — se precisar de mais detalhe de um
> método, pesquise na web pelo NOME da obra; nunca invente regra que não está
> escrita aqui.

---

## 1. A experiência essencial (o norte de tudo)

CONCERTO é sobre **cuidar do que quebrou**. A experiência essencial do jogo:
consertar algo com as próprias mãos e sentir o alívio de ver funcionar de novo
— sabendo, no fundo, que existe uma coisa que nunca poderá ser consertada.

Toda feature, cena, mecânica e linha de diálogo passa neste teste:

- **Soma ou dilui a sensação de "consertar"?** Se só adiciona barulho, corta.

---

## 2. Os 3C's — Character, Camera, Controls

### Character (Dez)

- Silhueta legível em qualquer distância e em qualquer dos 3 cenários
  (rim light garantido na oficina, no setor e no epicentro).
- Peso coerente: aceleração, parada e giro sem pop de animação.
- Estados de movimento legíveis num olhar: parado / andando / correndo /
  dash / dano.
- **ZERO enganchamento**: o Dez não trava em quina, emenda de collider ou
  vão estreito (ver 8.1). O player não empurra props; props não seguram o player.

### Camera

- Terceira pessoa com oclusão resolvida: a câmera NUNCA atravessa geometria;
  o player dá fade quando ela encosta.
- Sem náusea: damping suave, sem sway em repouso, FOV estável (kick de dash
  ≤4° e ≤200ms).
- Shake: amplitude pequena, decay <300ms, com opção "reduzir shake" nas opções.

### Controls

- Input→resposta <100ms em TUDO (ideal ~67ms a 60fps; 1 frame ≈ 16.7ms).
- Coyote time ~100ms; input buffer ~150ms (pulo, dash, ataque).
- 100% dos fluxos funcionam em gamepad E teclado — fluxo teclado-only é bug.
- Toda ação tem resposta em ≤1 frame (som + visual) — tabela de juice (seção 9).

**Violou qualquer 3C = violação P1.**

---

## 3. Métricas de game feel (números, não opinião)

| Métrica | Valor alvo | Como medir |
|---|---|---|
| Frame budget | 16,7ms (60fps travado) | Profiler por cena |
| Hitch máximo | ≤2 frames | Profiler em combate / VFX / transição |
| Input→resposta | <100ms (ideal ~67ms) | Gravação a 60fps, contar frames |
| Coyote time | ~100ms | Config do controller |
| Input buffer | ~150ms | Config do controller |
| Hitstop (acerto importante) | 50–100ms | Config de combate |
| Camera shake (decay) | <300ms | Config de câmera |
| Telegraph de inimigo | ≥300ms antes do dano | Review de cada ataque |
| Morte → respawn | ≤3s | Cronômetro |
| Tela preta em cutscene | ≤3s | Cronômetro |

- Alvo é 60fps TRAVADO, não "na média": média de 60 com spike de 40 é reprovado.
- MEDIR ANTES DE OTIMIZAR: toda mudança de performance entra no relatório com
  número antes/depois. Nada de otimizar "no feeling".

---

## 4. Cutscenes — regras duras

1. Duração ≤90s (as canônicas do jogo ficam na faixa de 40–60s — manter).
2. Skippable após 3s (hold); pular em QUALQUER beat não deixa estado sujo.
3. ≤3s de tela preta/escura no início: a rampa de luz/exposição começa no frame
   do corte (rig acende no SignalEmitter do primeiro beat).
4. **ZERO ator colidindo com cenário**: movimento scriptado ignora colisão de
   props (layer própria / collision matrix). Nenhum personagem fica "andando
   travado" ou empurra nada na tela.
5. Câmera nunca clipa geometria; corte entre vcams é seco e limpo.
6. Legendas legíveis: tamanho mínimo, contraste garantido, dentro da zona segura.
7. Toda cutscene nova entrega com o checklist da CUTSCENE_QA preenchido.

---

## 5. Onboarding — teach → test → twist

Nenhuma mecânica combina com outra ANTES de passar pelas três etapas:
**ensinar em espaço seguro → testar sozinha → torcer sob pressão**.

| Mecânica | Teach (espaço seguro) | Test | Twist |
|---|---|---|---|
| Consertar | Oficina, anomalia simples | Loop das peças (ato 1) | Reparo sob pressão (ato 2); O Último Reparo (final) |
| Ferramenta (modos) | Primeira troca de modo guiada | Anomalia + primeiro inimigo | Boss (mistura combate/conserto) |
| Dash | Área aberta sem perigo | Vãos do setor | Travessia do Epicentro (vazio) |
| Eco | Demonstração com alvo parado | Desviar de onda de choque | Plataforma/boss (evasiva principal) |
| Estase | Demonstração em objeto | Plataforma rápida (Epicentro) | Spray do boss; janela do reparo final |
| Absorver (Parry) | Estação de treino segura na oficina (`ParryTeachStation`, dano 0) | Refletir projétil do Flutuante / atordoar Estilhaço | Quebrar guarda frontal do Égide sob pressão de grupo |

- Regra: se uma mecânica ainda não teve **teach**, ela não pode ser exigida em
  teste — nem em arena, nem em puzzle.
- Toda mecânica nova entra nesta tabela antes de entrar no jogo.

---

## 6. Kishōtenketsu — estrutura de atos e de arenas

Quatro partes (estrutura narrativa clássica japonesa, usada no level design da
Nintendo): **Ki** (introdução) → **Shō** (desenvolvimento) → **Ten** (virada) →
**Ketsu** (conclusão).

Mapeamento canônico dos atos do CONCERTO:

- **Ki — Ato 1**: a oficina, o mundo, a mecânica (peças).
- **Shō — Ato 2**: anomalias e reparo; o mundo estragando.
- **Ten — Ato 3 → Ato 4**: o céu racha, o epicentro, o boss, O REGISTRO
  (a virada: quem quebrou o mundo).
- **Ketsu — O Final**: o último concerto.

Aplica-se TAMBÉM a cada fase e arena (não só ao arco macro):

- Exemplo canônico — arena do boss: entrada calma (Ki) → primeiro contato
  (Shō) → fase 2 muda as regras (Ten) → morte com dissolve e silêncio (Ketsu).
- Toda arena/fase nova documenta o próprio Ki/Shō/Ten/Ketsu no doc da task.

---

## 7. Legibilidade

- Todo ataque de inimigo: telegraph VISUAL ≥300ms antes do dano (brilho, pose,
  som). Ataque sem telegraph = violação P1.
- Todo interagível: highlight + prompt de input legíveis num olhar, sem busca.
- Inimigo NUNCA se confunde com cenário: rim light / contraste por cenário.
- Perigo (vazio, armadilha, queda) lê ANTES de matar. Morte invisível = bug.
- **1 objetivo ativo no HUD.** Sem lista de quests na tela.

---

## 8. Regras técnicas canônicas (já estabelecidas — manter)

### 8.1 Movimentação e colisão

- Physics material zero-friction no player; capsule lisa com skin adequado.
- Emenda de colliders box encostados: suavizar (collider composto ou rampa
  invisível) — "ghost collision" que para o player é bug de nível grave.
- Todo valor de tuning em config. Nada hardcode.

### 8.2 Conteúdo e pipeline

- **TripoSG only** para geração de modelo 3D novo. Sem fallback, sem modelo de
  terceiros.
- Diálogos em arquivos de dados; zero texto de diálogo hardcode.
- Balance de combate e boss em config + tabela no doc da task.
- Save versionado; arquivo corrompido → fallback seguro (nunca apaga save).

### 8.3 Áudio — regra do motif

- A caixinha = tema da esposa e do jogo. **4 variações canônicas**:
  1. Integral (passado / oficina)
  2. Com falha sutil (ato 2)
  3. Distorcida (epicentro)
  4. Inteira e limpa — SOMENTE na cutscene final.
- Música nunca acima de diálogo/legenda. Loudness consistente entre cenas.

### 8.4 Narrativa — regra de ouro

- **NÃO EXPLICAR.** A revelação (Dez quebrou o mundo; a esposa) é semeada por
  objetos e silêncio — a foto, a bugiganga, o timestamp corrompido, o holograma.
- Ninguém no elenco conecta as pontas pelo jogador. O jogador conecta.
- Quem explicar demais destrói o final. Em caso de dúvida: mostra, não conta.

---

## 9. Tabela de juice — ação → resposta obrigatória

Toda ação tem resposta em ≤1 frame. Mínimo: som + visual.

| Ação | Resposta obrigatória |
|---|---|
| Golpe conectado | Hitstop 50–100ms + partícula no ponto + som + shake curto |
| Golpe carregado | Windup legível + som de carga + hitstop ~100ms no impacto |
| Dash | Afterimage (Eco) + whoosh + FOV kick ≤4° / ≤200ms |
| Estase on/off | Pulse visual + dessaturação leve + som reverso; snap na volta |
| Reparo concluído | "Clack" + luz/estado do objeto muda + micro-partícula |
| Dano recebido | Vinheta leve + shake + som grave + i-frames visíveis |
| Interação em alcance | Highlight + prompt de input (≤1 frame) |
| Checkpoint | Som suave + texto curto (sem cutscene) |
| Morte | Fade rápido → respawn ≤3s |
| Combo 3º golpe (Combate v2) | Hitstop até 98ms + knockback real + arco amplo + som pesado |
| Cancelamento com Eco (Combate v2) | Corte imediato do recovery + afterimage ciano + som reverso |
| Absorver / Parry (Combate v2) | Freeze ~150ms + flash ciano + som clang/reverso + reflexão/stagger |
| Ricochete no Égide (Combate v2) | Flash azul-aço + som metálico agudo + dano 0 (leitura clara de escudo) |
| Finalização de encounter (Combate v2) | Dissolve ciano + micro-slowmo ≤180ms no último inimigo |

Ação nova sem linha nesta tabela = feature incompleta.

---

## 10. As Lentes (adaptadas de Jesse Schell, "The Art of Game Design")

Checklist de auditoria — cada Lente vira pergunta concreta sobre o CONCERTO:

1. **Experiência Essencial** — esta feature soma a sensação de consertar ou só
   soma barulho?
2. **O Brinquedo** — mover o Dez é gostoso ANTES de qualquer objetivo?
3. **Curiosidade** — o mundo levanta perguntas que o jogador QUER responder?
4. **Fluxo** — o desafio acompanha a habilidade? (métrica: mortes por checkpoint)
5. **Objetivos** — o jogador sabe o que fazer AGORA, sem abrir menu?
6. **Desafio** — cada desafio cresce do anterior (teach → test → twist)?
7. **Julgamento** — a consequência das ações é previsível (telegraphs, feedback)?
8. **Feedback** — toda ação responde imediato e legível (tabela de juice)?
9. **Recompensa** — recompensa proporcional ao esforço (checkpoint, revelação)?
10. **História** — a mecânica conta a história (consertar = tema)?
11. **O Mundo** — os 3 cenários são coerentes E distintos (quente / frio / glitch)?
12. **Ressonação** — a caixinha, a foto e a bugiganga ressoam com a perda?
13. **Curva de Interesse** — cada ato tem pico próprio; não existe vale morto
    entre atos?
14. **Progresso Visível** — o jogador VÊ que progrediu (flags, cartões, checkpoints)?
15. **O Jogador** — o que o Dez quer (rever a esposa)? As escolhas do jogo refletem
    isso?

---

## 11. MDA — cada mecânica declara sua estética

- **Mechanics** (mecânicas): conserto, ferramenta multi-uso, Eco, Estase, dash.
- **Dynamics** (dinâmicas): ritmo reparo→tensão→alívio; exploração cuidadosa;
  leitura de telegraphs; gestão das janelas de punição.
- **Aesthetics** (estéticas): melancolia agridoce; cuidado; descoberta.

Regra: mecânica nova declara qual estética alvo ela serve. Mecânica sem
estética = cortar ou repensar.

---

## 12. Conformidade — template obrigatório em toda task futura

Toda task nova adiciona ao final do relatório:

```
CONFORMIDADE — DESIGN_BIBLE.md v1.0
- Itens tocados: [IDs das seções]
- Status: conforme | corrigido nesta task | backlog P2
- Evidência: MP4 / medição / doc
```

### Checklist de auditoria (rodar por cena e por sistema)

- [ ] Input→resposta <100ms nas ações principais
- [ ] Coyote ~100ms / buffer ~150ms ativos
- [ ] Telegraph ≥300ms em todo ataque
- [ ] Juice: tabela completa na cena
- [ ] Cutscene: ≤90s, skip limpo, ≤3s de tela preta, zero colisão de ator
- [ ] 1 objetivo no HUD
- [ ] 60fps sem hitch >2 frames
- [ ] Gamepad + teclado em 100% dos fluxos
- [ ] Valores de tuning em config
- [ ] TripoSG only (se houve asset novo)

### Classificação de violação

- **P1** — viola métrica numérica ou regra dura deste manual: conserta antes de
  entregar, com prova antes/depois.
- **P2** — violação de conforto/estética sem número associado: vai pro backlog
  com localização e sugestão. NÃO conserta sem autorização.

---

## 13. Fontes (métodos reais que fundamentam este manual)

- Jesse Schell — *The Art of Game Design: A Book of Lenses* (as Lentes):
  schellgames.com/art-of-game-design
- Steve Swink — *Game Feel* (Morgan Kaufmann, 2009): base das métricas de
  input→resposta e responsividade.
- Hunicke, LeBlanc & Zubek — *MDA: A Formal Approach to Game Design and Game
  Research* (2004): mecânica → dinâmica → estética.
- Kishōtenketsu — estrutura narrativa de 4 partes (Ki/Shō/Ten/Ketsu) aplicada em
  análises públicas do level design da Nintendo: en.wikipedia.org/wiki/Kishōtenketsu
- Mihaly Csikszentmihalyi — *Flow* (1990): base da lente de fluxo e da curva de
  desafio × habilidade.
- Método teach/test/twist — level design do Portal/Valve, documentado em análises
  públicas e no material de playtest da Valve (incl. o vídeo "Valve's Secret
  Weapon", do canal Game Maker's Toolkit).

> Nenhum conteúdo foi copiado dos livros: são conceitos públicos, aplicados ao
> CONCERTO. As obras citadas são referência para quem quiser aprofundar — e
> nenhuma delas precisa ser baixada para este manual funcionar.
