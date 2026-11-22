# 00 — Visão Geral

> CONCERTO · Jogo 1 da série ORA · GDD v0.3 `[NOVO]` · tags: ver README

## 1. Pitch

**CONCERTO** é um jogo 3D de aventura com plataformação e combate energético, ambientado em um planeta que foi estilhaçado em zonas de tempo congeladas. O jogador é **Desiderio "Dez"** `[DECISÃO]`, um mecânico humilde, carismático e não-humano `[DEFINIDO]`, que causou um erro técnico ao consertar o Grande Relógio do planeta — o mesmo erro que o transformou em fugitivo `[DEFINIDO]`. Com o dispositivo temporal **Bis** no pulso, Dez viaja entre os **Fusos** (biomas/dimensões modulares `[DEFINIDO]`), coleta as **Relíquias do Compasso** para restaurar o equilíbrio `[DEFINIDO]` e descobre que cada conserto temporal tem um preço em outro lugar `[DEFINIDO]` — enquanto foge do **Sincronato**, um regime de coletivismo temporal extremo `[DEFINIDO]`.

O título **CONCERTO** `[NOVO]` é um trocadilho deliberado em português: "concerto" é tanto o **reparo** (consertar o tempo) quanto a **harmonia musical** (fazer o mundo voltar a tocar junto, cada instrumento no seu tempo). A palavra resume o tema, o tom (drama + humor `[DECISÃO]`) e o sistema central do jogo (sincronizar sem uniformizar).

O nome da série/marca é **ORA** `[NOVO]`: o planeta se chama Ora, e "ora" é também interjeição — piada recorrente do protagonista ("Ora, ora."). Jogo 1 = CONCERTO; jogos futuros exploram outros fusos/planetas do universo `[DEFINIDO: série de jogos]`.

## 2. Ficha técnica

| Campo | Valor | Origem |
|---|---|---|
| Título | CONCERTO | `[NOVO]` |
| Série | ORA (jogo 1 de N) | `[DEFINIDO: série]` + `[NOVO: nome]` |
| Gênero | Aventura 3D com plataformação, leve metroidvania | `[DEFINIDO]` + `[NOVO]` |
| Câmera | Terceira pessoa, seguimento suave | `[NOVO]` |
| Plataformas | Android (alvo de build do estúdio) + PC/Windows (alvo de dev) | `[DECISÃO: híbrido]` |
| Engine | Unity 6.3 URP, pipeline do gamedev-setup-ia | `[DEFINIDO: Unity]` |
| Sessão média | 8–12 min por sessão mobile; save a qualquer momento | `[NOVO]` |
| Duração do jogo 1 | 4–6 h (jogador médio), ~8 h 100% | `[NOVO]` |
| Classificação sugerida | Livre / 10+ (combate energético limpo, sem sangue) | `[DECISÃO]` + `[NOVO]` |
| Idiomas de lançamento | PT-BR (voz) + EN (voz) — TTS local cobre ambos | `[DEFINIDO: PT-BR/EN no TTS]` |
| Monetização | Premium (compra única) — decisão final em M4 | `[NOVO]` |

## 3. Pilares de design `[NOVO]`

Todo sistema, asset e diálogo precisa servir a pelo menos um pilar. Eles existem para arbitrar decisões futuras sem te interromper.

1. **"Consertar tem consequência."** — A fantasia central, herdada diretamente da conversa `[DEFINIDO]`: reparar um Fuso melhora um lugar e complica outro. Nenhum puzzle, quest ou diálogo pode terminar com "tudo resolvido, sem custo". É o coração do jogo e o que o diferencia de coletor-de-relíquias genérico.
2. **"Tempo é ritmo."** — Movimentação, combate, música e UI compartilham a mesma gramática de ritmo: dash no tempo, parry no contratempo, boss rítmico, HUD que pulsa. Se um sistema não pode ser descrito em termos de ritmo, provavelmente não pertence ao jogo.
3. **"Oficina viva."** — O mundo é construído por gente que conserta coisas: rebites, molas, catracas, caldeiras. Assets, inimigos e cenários nascem de uma linguagem de forma mecânica-acolhedora (nada de "tecnologia fria"), o que também facilita geração procedural com TRELLIS (peças mecânicas são o ponto forte de imagem→3D).
4. **"O regime tem razão — e mesmo assim está errado."** — A Maestrina não é malvada cartoon: ela sincronizou o mundo para acabar com o sofrimento imprevisível do tempo (luto, envelhecimento, acidente). O jogo nunca zomba de quem aceita a Hora Eterna; o drama nasce de entender o porquê e ainda assim discordar `[NOVO, deriva de DEFINIDO: antagonista coletivista]`.

## 4. Promessas de experiência (USPs)

- **Um mundo em dois estados.** Cada Fuso tem estado **Desincronizado** e **Restaurado**; o jogador alterna entre eles no meio do platforming, e cenários, inimigos e rotas mudam na hora. Isso materializa o "biomas que mudam com alterações temporais" `[DEFINIDO]` em uma mecânica única e legível.
- **Rede de equilíbrio.** Um mapa "Tela de Fusos" mostra o efeito de cada conserto sobre os outros fusos (benefício + custo) — o jogador sente o peso das escolhas sem sistema de moralidade artificial.
- **Plataformação rítmica acessível.** Movimentação com dash, wall-run, pulo duplo e gancho `[DEFINIDO]` é treinada em 10 minutos e segue aumentando por 6 horas — o clássico "fácil de aprender, profundo de dominar".
- **Vozes locais, roteiros versionados.** Todo diálogo nasce em `dialogues.yaml` no Git e vira voz via TTS local (XTTS-v2 / F5-TTS / Kokoro-82M) — custo marginal zero por linha, permitindo um jogo de diálogo generoso (o que raramente acontece em indie solo).

## 5. O que este jogo NÃO é `[NOVO]`

Delimitar o escopo evita "projeto infinito" (preocupação explícita do seu mapa de pilares):

- Não é soulslike: morte é checkpoint com humor ("O Bis te rebobinou."), não punição pesada.
- Não é roguelite: mundo persistente, progressão salva.
- Não tem crafting, base-building, multiplayer, cosméticos gacha ou sistema de inventário complexo: inventário é "Relíquias + Melhorias", uma tela só.
- Não tem escolha de diálogo ramificado no jogo 1: o drama vem das escolhas de **ordem** (qual fuso consertar primeiro) e da decisão final; diálogos são lineares com personalidade forte.
- Não usa assets 3D comprados: tudo nasce do pipeline local (ComfyUI → TRELLIS → Blender → Unity), respeitando as limitações do arsenal que você está construindo.

## 6. Metas de avaliação (targets de "jogo bom")

| Dimensão | Meta |
|---|---|
| Retenção D1 (soft launch Android) | ≥ 35% |
| Tempo até primeiro "momento assinatura" (Re-Sincronia) | ≤ 12 min de jogo |
| Crítica/jogador: "o jogo tem identidade visual própria" | menção em ≥ 70% das reviews |
| Crash rate Android | < 0,5% de sessões |
| Instalação ≤ 400 MB (sem mídia de alta densidade no APK; download por fuso via Addressables) | obrigatório |
