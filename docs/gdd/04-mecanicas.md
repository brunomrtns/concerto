# 04 — Mecânicas

> Movimentação e combate base `[DEFINIDO]` · nomes, números e sistemas de suporte `[NOVO]` · combate "energético limpo" `[DECISÃO]` · tags: ver README

## 1. Movimentação `[DEFINIDO: dash, wall-run, pulo duplo, gancho] + [NOVO: nomes/números]`

A plataformação é o coração do jogo. Números iniciais para calibração (unidades Unity; ajustáveis no M1 com playtest):

| Habilidade | Nome no jogo | Especificação inicial | Custo/Regra |
|---|---|---|---|
| Correr | — | 7 m/s, aceleração 40 m/s², atrito 30 m/s² | — |
| Pulo | — | 4,5 m de altura, coyote 0,12 s, buffer 0,15 s | — |
| **Pulo duplo** | Rebote | +3,5 m, animação de "mola no pé" | 1 por pulo, reseta no chão/parede/garra |
| **Dash** | Sopro | 9 m em 0,22 s, i-frames 0,1 s | 15 de Pressão; recarrega 25/s fora de uso |
| Dash longo | Sopro Estendido (Relíquia 2) | 14 m em 0,30 s, atravessa feixes de luz | 25 de Pressão |
| **Wall-run** | Corrimão | 2,5 s máximo, desliza 1,2 m/s para baixo ao expirar; mantém velocidade de entrada | gratuito; cancela com Sopro (mantém momentum) |
| **Gancho** | Garra | Alcance 12 m; puxa em 8 m/s; **swing** (Relíquia 3): conserva momentum circular | gratuito em pontos marcados (olho-de-lente dourado) |
| Queda pesada | Crava-Mola | Ao apertar baixo+ataque no ar: mergulho 15 m/s + onda de choque 3 m | dano 2; quebra cristas frágeis |

**Regras de game feel** `[NOVO]`: 1) todo input responde no mesmo frame; 2) momentum é sempre conservável (dash no fim do wall-run = combo mais longo — o "skating" do jogo é descoberta de comunidade); 3) a câmera segue com fov +6° em alta velocidade (feedback sem tremer); 4) som e partícula em cada transição de estado — o jogo "toca" a própria movimentação (pilar 2: tempo é ritmo).

**Re-Sincronia** `[DEFINIDO: dispositivo muda biomas] + [NOVO: regras]`: em zonas com **Nó de Re-Sincronia** (cristal de catraca), apertar o botão do Bis alterna o estado da zona em 0,8 s (onda visual em anel). Regras: (a) alterna só a "bolha" da zona, não o fuso inteiro (fuso inteiro muda por conserto de Relíquia); (b) objetos ganham 0,8 s de "escrita fantasma" (o novo estado aparece em wireframe antes — legibilidade, mobile-friendly); (c) inimigos no raio ficam atordoados 1 s (ferramenta de combate também); (d) cooldown 6 s (móvel tocável). No final do jogo (Escapamento) o cooldown cai para 1 s e zonas não-terminadas podem ser alternadas livremente — power fantasy honesta.

## 2. Combate `[DEFINIDO: carga aérea + energia à distância] + [NOVO: kit completo]`

**Recursos:**
- **Fusíveis (HP):** 4 slots. Dano remove 1 (ataques pesados: 2). Cura em **Postos de Reparo** (bancadas de oficina) e com item **Pacote-Elo** (drop raro). Morte: rebobinar no checkpoint com humor ("O Bis te rebobinou.").
- **Pressão (energia):** 100 pontos. Alimenta Sopro e Faísca. Recarrega: 25/s passivo, +30 por golpe de melee acertado (o loop: bater para poder atirar/dashar — combate encoraja proximidade).

**Golpes:**

| Golpe | Nome | Especificação | Custo |
|---|---|---|---|
| Melee 3-hit | Chave Sonora | combo 0,35 s por golpe; 3º golpe faz knockback + som harmônico | 0 (ganha +30 Pressão por acerto) |
| **Ranged** | Faísca | projétil de energia em arco curto (12 m), 0,4 s de recarga | 20 Pressão |
| **Carga aérea** | Martelada Aérea | segurar ataque no ar 0,6 s → mergulho com dano 3 + choque 3 m; i-frames na queda | 30 Pressão |
| **Parry** | Contratempo | janela 0,18 s antes do impacto; reflete projétil, atordoa melee 2 s, devolve 40 Pressão | 0; risco/recompensa central |
| Finalizador | Concerto | Ferrugem atordoada: apertar cerca executa com "afinação" (partícula de notas) | 0 |

**Inimigos e contra-jogo** (resumo — fichas completas em 02 e 07):

| Inimigo | Padrão | Contra |
|---|---|---|
| Ferrugem-Trabalhadora | telegraph 0,8 s, marreta em arco | dash lateral → Chave Sonora |
| Ferrugem-Espelho | carrega Faísca 1,2 s | Contratempo (reflete) ou cobertura |
| Ferrugem-Coletora | escuda aliados (bolha 3 s) | prioridade; Crava-Mola quebra bolha |
| Censor-patrulha | combo de 2 + giro | Contratempo no 2º golpe |
| Censor-pesado | cargas lentas, imune a Faísca | Martelada Aérea na nuca |

**Leitura limpa** `[DECISÃO: energético limpo]`: telegraphs visuais de 0,6–1,2 s (brilho na junção que vai bater), partículas de energia nas 3 cores funcionais (dano, defesa, sincronia — ver art-bible), sem sangue: máquinas se desferrujam (parafusos e molas voando) e Censores "desarmonizam" (caem sentados, tique-taque lento). Morte de inimigo é sempre "o tempo voltou a passar" — até derrotar é tematicamente conserto.

## 3. Progressão `[NOVO]`

- **Relíquias (6):** habilidades listadas em 03; cada uma também abre rotas nos fusos anteriores (metroidvania leve).
- **Melhorias (loja do Tio Horário, ~12 itens):** +1 Fusível (2x), Pressão máx +25 (3x), Faísca dupla, Sopro no ar, Contratempo largo (+0,06 s janela), Rebote extra em mola, Garra alcance +4 m, etc. Preços em Segundos.
- **Memórias-Eco (21):** colecionável narrativo; completar todos desbloqueia o epílogo estendido.
- Curva: poder de movimentação cresce sempre; poder de combate cresce por escolha de compra — quem gosta de plataforma compra mobilidade, quem gosta de combate compra ofensa. Nenhum conteúdo exige compra específica.

## 4. Controles `[NOVO, mobile-first DECISÃO híbrida]`

### Gamepad (PC e Android com controle)
| Ação | Botão |
|---|---|
| Movimento | analógico esquerdo |
| Câmera | analógico direito |
| Pulo / Rebote | A / Cruz |
| Sopro (dash) | RB |
| Chave Sonora | X |
| Faísca | RT |
| Garra | LT |
| **Re-Sincronia (Bis)** | Y |
| Contratempo | LB (segurar = bloquear; soltar na janela = parry) |
| Interagir | B |
| Martelada Aérea | X no ar (segurar) |

### Touch (Android, play com 2 polegares)
- **Esquema A (padrão):** stick virtual esquerdo (movimento); lado direito: 4 botões (Pulo, Chave, Faísca, Bis) + 2 de contexto (Garra/Interagir). Swipe direito = Sopro na direção do movimento. Segurar Chave no ar = Martelada Aérea.
- **Swipe de Câmera** no espaço restante; **auto-aim assiste Faísca** (magnetismo 8°).
- **Regra mobile `[NOVO]`:** Contratempo é botão único (segurar=guarda; soltar na janela=parry) — funciona igual no gamepad; jamais exigir dois toques simultâneos precisos.
- Opções: layout ambidestro, tamanho de botões (3 presets), modo "só um polegar" com assistência de câmera (câmera segue alvo mais próximo).

## 5. Morte, checkpoint e dificuldade `[NOVO]`

- **Checkpoints:** Postos de Reparo + checkpoints invisíveis por trecho de plataforma (a cada "beat" — ver 05). Morte sem carga: rebobinar em 1,5 s (sessão mobile não pune espera).
- **Dificuldade:** 3 presets (Brando/Padrão/Forte) que ajustam dano recebido (0,5x/1x/1,5x) e janela de Contratempo (0,24/0,18/0,12 s). Nunca muda puzzles.
- **Assistências (ligáveis):** auto-aim mais largo, plataforma "ghost" repetindo o caminho de um NPC-guia ("trilha da Vó"), pulo assistido (alturas mínimas garantidas). Acessibilidade tratada como opção, não como modo separado.
- **Chefe derrotado = teletransporte** para a sala de Relíquia (respeito ao tempo do jogador mobile).

## 6. UI/HUD `[NOVO, alinhado à direção de arte 06]`

- HUD mínimo: 4 Fusíveis (ícone de fusível de vidro), medidor de Pressão (manômetro circular no pulso do Dez — diegético), contador de Segundos (canto).
- **Tela de Fusos:** mapa-grafo em tela cheia; cada fuso mostra estado, Relíquia, benefício/custo pendentes — o "mapa mental" da Linha do Equilíbrio.
- Tudo em PT-BR claro, fontes ≥ 14 pt equivalentes; menus navegáveis por toque sem 120 fps de requisito.
