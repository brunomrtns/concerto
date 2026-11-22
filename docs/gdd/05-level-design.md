# 05 — Level Design

> Estrutura hub+Fusos `[NOVO]` · beat maps `[NOVO]` · sessão mobile `[DECISÃO: híbrido]` · kits modulares `[DEFINIDO: ambientes modulares p/ geração por IA]` · tags: ver README

## 1. Estrutura de fluxo do jogo 1

```
Prologo (Oficina-Navio) ──> Caldeiral (Ato 1, tutorial+boss)
        │
        └──> A Orquestra (HUB: fogueira+loja+Tela de Fusos)
                  │
        ┌─────────┼──────────┬───────────┐
   Vitralha   Marésia     Ecodia    Retrógrado   (ordem livre, 4 Relíquias)
        └─────────┼──────────┴───────────┘
                  └──> A Corona (Ato 3: escala final + boss Íria + decisão)
```

A ordem livre dos 4 fusos do Ato 2 é a "escolha" principal do jogo: a Linha do Equilíbrio (01-narrativa §5) garante que cada ordem produza um jogo com problemas diferentes. O hub cresce conforme fusos são restaurados (NPCs chegam, marchinha toca mais completa — o hub é um "medidor de progresso" auditivo).

**Números de escopo** `[NOVO]`:

| Bloco | Área jogável | Tempo alvo | Beats |
|---|---|---|---|
| Prólogo (Oficina-Navio) | linear 150 m | 12 min | 5 |
| Caldeiral | 350 × 350 m | 70–90 min | 9 |
| Cada fuso do Ato 2 | 400 × 400 m | 50–70 min | 7 |
| A Corona | 500 × 300 m (vertical) | 60 min | 8 |
| Total jogo 1 | — | 4–6 h | 46 beats |

## 2. Kit modular: o "alfabeto" de construção `[NOVO, serve DEFINIDO]`

Cada fuso define **um kit de 8–12 peças** com regras de encaixe (grid 2 m). Os kits nascem no pipeline de assets (07) com prompt-base único do art-bible — tudo parece do mesmo jogo.

| Tipo de peça | Exemplo (Caldeiral) | Tris alvo | Observação |
|---|---|---|---|
| Chão/plataforma | viga-rebite 4 m | ≤ 600 (small_prop_item) | 2 variantes de estado |
| Paredão/torre | torre-caldeira 8 m | ≤ 1.500 | módulo de wall-run |
| Transição | catraca-porta (Nó de Re-Sincronia) | ≤ 800 | peça-chave do jogo |
| Trilha/rota | duto-curva, trilho | ≤ 900 | conecta beats |
| Decor | válvulas, cartaz "A HORA ETERNA" | ≤ 300 | geração em série |
| Hero piece | sirene-mestra do Caldeiral | ≤ 12.000 | 1 por fuso |
| Playable | NPC/inimigo (ver 02) | ≤ 8.000 | — |

Regra de ouro: **toda peça do kit existe nos 2 estados** (Desincronizado/Restaurado) — só texturas/propriedades mudam; geometria idêntica evita re-trabalho e mantém o platforming consistente entre estados (fairness: onde você anda num estado, você confia no outro).

## 3. Beat map — Caldeiral (exemplo completo, nível de implementação) `[NOVO]`

Cada **beat** = 1 momento memorável + 1 checkpoint implícito + 1 linha de diálogo/VO. Formato: `nº · nome · mecânica ensinada/usada · tensão · VO de referência (id no dialogues.yaml)`.

1. **Acordar na Sucata** · mover/câmera/pular · — · `cs01_dez_02`
2. **Oficina Íntima** · interagir (bancada), memória de Juno · — · `cs01_flash_01`
3. **Primeiro Censor** · Chave Sonora + Contratempo (o Censor só cai no parry) · combate · `cal_tuto_parry_01`
4. **Pátio das Sirenes** · Corrimão (wall-run entre dutos) · plataforma · `cal_vo_patro_02`
5. **Galeria de Fumaça** · Sopro (dash) sobre vãos; visibilidade baixa = tensão · plataforma · —
6. **A Sirene-Mestra** · puzzle de válvulas (3 válvulas, ordem indicada por batidas — ritmo! ) · quebra-cabeça · `cal_puzzle_01`
7. **Boss: O Contramestre** · Martelada Aérea (telegraph de junta exposta) · combate · `cal_boss_intro_01`
8. **Re-Sincronia** (Relíquia: Mola) · o momento assinatura: fumaça congelada cai como neve, mercado acorda · mini-cutscene in-engine · `cal_resync_01`
9. **Fuga ao Mercado** · corrida final com tudo que aprendeu + Rebote · celebração · `cal_fuga_01`

**Regra de pacing por fuso** `[NOVO]`: 2 beats de plataforma : 1 beat de combate : 1 beat de história/puzzle, repetido. Sessão mobile: cada beat ≤ 8 min; qualquer beat termina em Posto de Reparo ou checkpoint.

## 4. Beat map resumido — Vitralha (para validar a variabilidade entre fusos)

1. Entrada pela estação de luz · plataforma de feixes (introduz estado duplo em floresta) · `vit_chegada_01`
2. Bosque dos Pássaros-vidro · Rebote em plataformas móveis · —
3. Mirante do Rio Parado · vista narrativa (a água esperando) · `vit_vo_vo2_01`
4. Cachoeira de Vidro · Garra não disponível ainda se Vitralha for primeiro: rota de Rebote+Sopro · desafio de ordem
5. Boss: A Estufa Viva · arena de luz refratada (boss muda o estado da arena — primeiro boss que "briga" com a Re-Sincronia) · `vit_boss_intro_01`
6. Relíquia: Ponteiro Maior · cutscene curta · `vit_relic_01`
7. Consequência: o rio volta — câmera mostra o deságue em Marésia (conecta o grafo) · `vit_conseq_01`

Os demais fusos seguem o mesmo template de 7 beats com twists próprios (listados em 03). A Corona usa 8 beats com "greatest hits": um beat por habilidade adquirida no jogo + finale rítmico.

## 5. Sessões mobile e checkpoints `[DECISÃO: híbrido] + [NOVO]`

- **Ciclo de missão mobile:** "entrar em fuso → 1 beat → checkpoint → sair/continuar" em 5–10 min; notificações de continuação são opcionais e nunca agressivas.
- **Save:** a qualquer momento (menu), automático em checkpoint e pós-beat. Save = JSON versionado (10 slots rotativos + 1 slot auto).
- **Modo viagem (config):** reduz partículas/sombra para bateria; travas de 30 fps opcionais.
- **Pause real** em cutscenes (mobile interrupções) — retomar com resumo de 1 linha ("Você estava consertando o Rio de Vitralha...").

## 6. Diretrizes de puzzle `[NOVO]`

1. **Todo puzzle é rítmico ou de estado** (pilar 2): ou lê-se um padrão de batidas (válvulas da Sirene-Mestra, padrões do Arquivista-Mor), ou manipula-se estados (abrir feixe de luz em Restaurado e atravessar em Desincronizado).
2. **Um puzzle, uma ideia:** nunca misturar mais de 2 mecanismos.
3. **Falha é informativa:** a peça que "quase encaixa" pisca no ritmo errado (o jogo mostra o porquê, sem texto).
4. **Sem pixel-hunt mobile:** alvos ≥ 0,6 m; interação por proximidade com raio generoso.

## 7. Diretrizes de combate em arena `[NOVO]`

- Arena = 1 "sala" do kit com 2–4 rotas de verticalidade (nunca arena plana — o jogo é de movimento).
- Ondas de inimigo máx. 2 tipos simultâneos (leitura limpa `[DECISÃO]`).
- **Contratempo é sempre a chave de uma fase do boss** — o parry é a assinatura; cada boss tem 1 "golpe contratempos" que muda a música (o metrônomo do HUD pisca).
- Reinício de boss ≤ 8 s após morte (postos de reparo dentro da arena).
