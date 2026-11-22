# C1 — Diagrama Top-Down e Projeto de Level Design do Caldeiral (Vertical Slice M1–M2)

> **Estágio:** C1 (`studio-scene-director`) — **ZERO `GameObject`s criados ou editados no Unity nesta fase.**
> **Diagrama visual:** [caldeiral_c1_topdown.png](../../evidence/concepts/caldeiral_c1_topdown.png)
> **Base obrigatória:** `InteractionSystem.cs`, `DialoguePresenter.cs`, `CutscenePlayer.cs`, `docs/gdd/04-mecanicas.md`, `docs/gdd/05-level-design.md`, `docs/art/art-bible.yaml`, `docs/art/fab3d-asset-spec.yaml`.
> **Gate de transição (Adendo B):** Este documento só libera **C2 (Blockout/Greybox)** após aprovação explícita do Bruno gravada em `PROGRESS.md` (`C1 APROVADO por Bruno em <YYYY-MM-DD>`) **E** validação prévia dos **Adendos C (`validate_scene.py` Android URP + fixture estourada)** e **D (Baseline PlayMode M1–M2)**.

---

## 1. Contrato com a Mecânica Já Existente (Sem Inventar Mecânica Nova)

O C1 nasce estritamente das interfaces de código já presentes no repositório e das métricas numéricas de `docs/gdd/04-mecanicas.md`:

| Sistema / Contrato | Fonte no Repositório | Métrica que Governa o Level Design |
| --- | --- | --- |
| **Interação + IK** | `unity/Assets/Game/Scripts/Gameplay/InteractionSystem.cs` (`IInteractable`, `InteractableBase`, `PlayerInteractor`) | `interactRadius = 2.4 m`; cada ponto interagível (`I1..I6`) expõe `PromptText` e `IkHandTarget` (âncora física para a mão/braço auxiliar do Dez). |
| **Diálogos & Barks** | `unity/Assets/Game/Scripts/UI/DialoguePresenter.cs` + `unity/Assets/Game/Audio/VO/` *(CAMINHO CONGELADO)* | Consome os packs VO já prontos: `cs01_abertura`, `caldeiral_tutorial`, `caldeiral_patio_sirenes`, `caldeiral_resincronia`, `barks`. |
| **Cutscene Rails** | `unity/Assets/Game/Scripts/UI/CutscenePlayer.cs` | Shots declarativos (`cameraFrom`, `cameraTo`, `lookAt`, `fov`) que precisam de corredores e pés-direitos livres (`8–18 m`) para nunca cortar parede. |
| **Movimentação** | `docs/gdd/04-mecanicas.md` §1 | Correr `7 m/s`; Pulo `4.5 m` (patamares em `+3.5 m` garantem folga confortável); Rebote `+3.5 m` (alcança `+7.0 m`); Sopro/Dash `9 m` (vão de vapor em Z3 calibrado em `6.5 m`); Corrimão/Wall-run `2.5 s` (paredões de `8 m` em Z3). |
| **Combate & Re-Sincronia** | `docs/gdd/04-mecanicas.md` §1–2 | Faísca `12 m`; Contratempo (parry `0.18 s`); Nó de Re-Sincronia alterna bolha `Desync → Sync` em `0.8 s` com onda em anel e atordoamento `1 s`. |

---

## 2. Arquitetura Anti-Caixote (5 Zonas em Loop, 3 Níveis Verticais, 0% Void)

Em substituição total ao antigo caixote plano `28×28 m`, o Vertical Slice M1–M2 ocupa um **footprint total de `72 m × 56 m`** dividido em **5 zonas conectadas em loop**, **100% fechadas por paredes e tetos (`8 m` a `18 m`)** — eliminando qualquer horizonte *void* — e convergindo visualmente para **um único Focal Point: a Sirene-Mestra / Caldeiral Central (`H = 16 m`) em Z4**.

| Zona | Dimensões (m) | Pé-Direito (Teto) | Níveis Verticais | Beat de Gameplay (`05-level-design.md` §3) |
| --- | --- | --- | --- | --- |
| **Z1 — Oficina da Sucata & Entrada** | `14 m × 12 m` (`X: -36..-22`, `Z: -24..-12`) | `8 m` | `Y = 0.0 m` | **Beat 1 & 2:** Spawn (`Dez + Porca`), câmera, movimentação, primeira interação na Bancada (`I1`, `IkHandTarget`) e tutorial da Porca (`cal_porca_manual_01`). |
| **Z2 — Passarela da Bigorna** | `18 m × 14 m` (`X: -18..0`, `Z: -25..-11`) | `10 m` | `Y = 0.0 m` e Mezanino `Y = +3.5 m` | **Beat 3:** Primeiro combate (Chave Sonora + Contratempo/Parry contra `Ferrugem-Trabalhadora` no piso e `Ferrugem-Espelho` no mezanino; `cal_tuto_parry_01`) + NPC `Tio Horário`. |
| **Z3 — Pátio das Sirenes & Galeria de Fumaça** | `24 m × 14 m` + Galeria `14 m × 22 m` (`X: 6..30`, `Z: -24..+12`) | `14 m` | `Y = 0.0 m → +3.5 m → +7.0 m` | **Beat 4 & 5:** NPC `Vó Segunda` no patamar `+3.5 m` (`cal_vo_patro_02`); paredões de `8 m` para Wall-Run (Corrimão) entre dutos; Vão de Vapor de `6.5 m` calibrado para o Sopro/Dash (`9 m`). |
| **Z4 — Câmara do Caldeiral Central (Octógono)** | `22 m × 22 m` (`X: -11..+11`, `Z: -4..+18`) | `18 m` (cúpula) | 3 níveis: `Y = 0.0 m`, `+3.5 m`, `+7.0 m` | **Beat 6 & 8:** **Focal Point Único** (`Sirene-Mestra H=16 m`), puzzle rítmico das 3 Válvulas (`I2..I4`), 2 inimigos Ferrugem (incl. `Ferrugem-Coletora`), ativação do **Nó de Re-Sincronia (`I5`)** alternando a câmara `Desync → Sync` (`cal_resync_01`). |
| **Z5 — Galeria de Retorno (Fuga Sync)** | `21 m × 10 m` + Mirante `6 m × 16 m` (`X: -32..-11`, `Z: -12..+14`) | `10 m` | `Y = +3.5 m → +7.0 m` descendo ao mirante sobre Z1 | **Beat 9:** Catraca de Saída (`I6`) destravada no estado `Sync`, plataformas elevadas celebrando a restauração e fechando o loop arquitetônico com vista para a Oficina Z1. |

---

## 3. Fluxo do Jogador (`Spawn → M1 → M2`) e Pontos de Interação (`IInteractable`)

```text
[SPAWN Z1: Dez + Porca (-33, 0, -20)]
   ──> I1 Bancada de Relógio (-29, 0, -17.5) [Interagir + IkHandTarget + VO cal_porca_manual_01]
   ──> Arco 4m (sightline para Z2)
   ──> Z2: NPC Tio Horário (-16.5, 0, -16) [DialoguePresenter + barks]
   ──> Z2: Combate E1 Ferrugem-Trabalhadora + E2 Ferrugem-Espelho (+3.5m) [VO cal_tuto_parry_01]
   ──> Z3: NPC Vó Segunda (11, +3.5, -18) [DialoguePresenter + VO cal_vo_patro_02]
   ──> Z3: Wall-run nos paredões 8m → Vão de Vapor 6.5m (Sopro 9m) → Ponte +3.5m
   ──> Z4: Câmara Central — 3 Válvulas (I2, I3, I4) + E3 Coletora + E4 Trabalhadora
   ──> Z4: I5 Nó de Re-Sincronia (0, +3.5, +0.5) [Alterna Desync -> Sync + VO cal_resync_01]
   ──> Z5: I6 Catraca Saída Sync (-24, +3.5, +8) → Mirante sobre Z1 (fim do Vertical Slice)
```

| ID | Objeto Interagível (`InteractableBase`) | Zona / Coordenada `(X, Y, Z)` | Prompt / Evento | Áudio / VO Congelado |
| --- | --- | --- | --- | --- |
| `I1` | Bancada de Relógio (Oficina) | Z1 `(-29.0, 0.0, -17.5)` | `"Examinar Bancada [E/B]"` (`IkHandTarget`) | `cal_porca_manual_01.wav` |
| `NPC1` | Tio Horário | Z2 `(-16.5, 0.0, -16.0)` | `"Conversar com Tio Horário"` | `barks/` (`DialoguePresenter`) |
| `NPC2` | Vó Segunda | Z3 `(11.0, 3.5, -18.0)` | `"Ouvir Vó Segunda"` | `cal_vo_patro_02.wav` |
| `I2` | Válvula Rítmica A (Oeste) | Z4 `(-5.5, 0.0, 4.0)` | `"Girar Válvula A"` (`IkHandTarget`) | `bis_tick_resync.wav` |
| `I3` | Válvula Rítmica B (Leste) | Z4 `(5.5, 0.0, 4.0)` | `"Girar Válvula B"` (`IkHandTarget`) | `bis_tick_resync.wav` |
| `I4` | Válvula Rítmica C (Norte) | Z4 `(0.0, 0.0, 12.8)` | `"Girar Válvula C"` (`IkHandTarget`) | `bis_tick_resync.wav` |
| `I5` | Nó de Re-Sincronia (Catraca-Cristal) | Z4 `(0.0, 3.5, 0.5)` | `"Ativar Re-Sincronia (Bis)"` | `cal_resync_01.wav` + `resincronia_chime.wav` |
| `I6` | Catraca de Saída (Estado Sync) | Z5 `(-24.0, 3.5, 8.0)` | `"Abrir Passagem ao Mercado"` | `resincronia_chime.wav` |

---

## 4. Plano de Iluminação (Orçamento Android URP: ≤ 5 Luzes Reais)

Alinhado ao `art-bible.yaml` (`lighting_desync: "frozen shadows, no flicker"` vs `lighting_sync: "breathing light, subtle candle flicker"`):

| Luz | Tipo (URP) | Zona / Posição | Estado `Desync` | Estado `Sync` |
| --- | --- | --- | --- | --- |
| `L0` | Directional Light (principal, shadows soft) | Global (ângulo 52°, -35°) | Frio opaco `#8c7a68`, intensidade `0.65` | Quente dourado `#ffd28a`, intensidade `1.15` |
| `L1` | Point Light (alcance 12 m) | Z1 Bancada `(-29, 4.5, -18)` | Âmbar oficina `#d89448` (estável) | Âmbar quente `#ffb454` (pulso leve) |
| `L2` | Point Light (alcance 14 m) | Z2 Passarela `(-9, 6.0, -18)` | Neutro frio `#7a8896` (tensão combate) | Cobre acolhedor `#e8a13c` |
| `L3` | Point Light (alcance 16 m) | Z3 Duto de Vapor `(22, 9.0, -16)` | Azulado vapor `#688494` | Dourado claro `#f0b868` |
| `L4` | Spot/Point Zenital (alcance 22 m) | Z4 Cúpula Sirene-Mestra `(0, 16.5, 7)` | Luz dura congelada `#9c6a3c` (foco na Sirene) | Pulso energético `#ffb454` (revela câmara inteira) |

*(Total de luzes reais simultâneas = 5; zonas periféricas e Z5 usam emissivos bakeados / ambient hemisphere sem estourar budget mobile URP).*

---

## 5. Lista Completa de Entidades e Props por Área (`[PLACEHOLDER CC0/Kenney]` vs `[HERO Fab3D]`)

> **Regra de Substituição In-Place (C2 → C5):** Todo item `[HERO Fab3D]` nasce em **C2** como `[PLACEHOLDER CC0/Kenney]` na escala, classe (`is_biped`) e pivô exatos, passando pelos mesmos gates (`validate_visual.py`). Em **C5**, o modelo final da Fábrica 3D (`TRELLIS.2-4B + Hunyuan3D-Paint + pack_glb modo game`) entra no **mesmo transform**, 1 por vez.

### 5.1. Personagens, Companheira e Inimigos (Vertical Slice M1–M2)

| ID | Entidade | Zona | Fase C2–C4 | Fase C5 (Final) | Classe `validate_visual` | `is_biped` | Budget (`max_tris`) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `CHAR_PORCA` | **Porca (Drone Companheira / Player Companion)** | Z1 (segue Dez) | **`[PLACEHOLDER CC0/Kenney]`** *(esfera/drone modular CC0 Ø 0.45 m, pivô central, hover C#)* | **`[HERO Fab3D]`** *(Porca v3 sobre conceito aprovado `porca_v2_2_concept.png`, hover procedural C#)* | `drone` | `false` | **≤ 2.500** |
| `CHAR_DEZ` | **Dez (Protagonista Jogável)** | Z1 Spawn | **`[PLACEHOLDER CC0/Kenney]`** *(bípede Quaternius/CC0 H=1.55 m, Y-up +Z-frente)* | **`[HERO Fab3D]`** *(Dez + UniRig Humanoid UAL + 2 braços auxiliares IK)* | `player_character` | `true` | **≤ 8.000** *(gate padrão 4.5k, override herói 8k)* |
| `NPC_TIO` | Tio Horário (Mercador) | Z2 | **`[PLACEHOLDER CC0/Kenney]`** *(bípede CC0 H=1.65 m)* | **`[HERO Fab3D]`** *(Humanoid UAL)* | `player_character` (NPC) | `true` | **≤ 4.000** |
| `NPC_VO` | Vó Segunda (Guia) | Z3 | **`[PLACEHOLDER CC0/Kenney]`** *(bípede CC0 H=1.50 m)* | **`[HERO Fab3D]`** *(Humanoid UAL)* | `player_character` (NPC) | `true` | **≤ 4.000** |
| `EN_TRAB` | Ferrugem-Trabalhadora (×2) | Z2, Z4 | **`[PLACEHOLDER CC0/Kenney]`** *(bípede CC0 H=1.80 m)* | **`[HERO Fab3D]`** | `enemy_character` | `true` | **≤ 3.500** |
| `EN_ESP` | Ferrugem-Espelho (×1) | Z2 (+3.5 m) | **`[PLACEHOLDER CC0/Kenney]`** *(bípede CC0 H=1.90 m)* | **`[HERO Fab3D]`** | `enemy_character` | `true` | **≤ 3.500** |
| `EN_COL` | Ferrugem-Coletora (×1) | Z4 | **`[PLACEHOLDER CC0/Kenney]`** *(quadrúpede/prop-drone CC0 1.2 m)* | **`[HERO Fab3D]`** *(UniRig non-humanoid)* | `drone` (quadrúpede) | `false` | **≤ 3.500** |
| `ENT_SIRENE` | **Sirene-Mestra / Caldeiral Central (Focal Point, `H = 16 m`)** | Z4 `(0, 0, 7)` | **`[PLACEHOLDER CC0/Kenney]`** *(torre/caldeira modular CC0 H=16 m, pivô na base)* | **`[HERO Fab3D]`** *(Hero piece da Fábrica 3D + normal-map bake)* | `large_prop_building` | `false` | **≤ 12.000** |
| `ENT_VALVULAS` | **3 Válvulas Rítmicas Interagíveis (`I2, I3, I4`)** | Z4 `(-5.5,0,4)`, `(5.5,0,4)`, `(0,0,12.8)` | **`[PLACEHOLDER CC0/Kenney]`** *(volante/pedestal CC0 com `InteractableBase` + `IkHandTarget`)* | **`[HERO Fab3D]`** *(válvula clockwork da Fábrica 3D nos 2 estados)* | `small_prop_item` | `false` | **≤ 600** *(cada)* |
| `ENT_NO_RESYNC` | **Nó de Re-Sincronia — Catraca-Cristal (`I5`)** | Z4 `(0, 3.5, 0.5)` | **`[PLACEHOLDER CC0/Kenney]`** *(pedestal/cristal CC0 com `InteractableBase` + onda visual)* | **`[HERO Fab3D]`** *(catraca-cristal da Fábrica 3D com emissão `#ffb454`)* | `small_prop_item` | `false` | **≤ 800** |

### 5.2. Arquitetura Modular e Props por Zona

| Zona | Asset / Prop | Qtd. | Tipo no Pipeline | Classe `validate_visual` | `is_biped` | Budget (`max_tris`) |
| --- | --- | --- | --- | --- | --- | --- |
| **Todas (Z1–Z5)** | Piso Modular Grid 2×2 m (`1 tile = 1 m` em C2; PBR `1–2 m` em C3) | ~120 inst. (batch estático) | **`[PLACEHOLDER CC0/Kenney]`** *(Kenney Prototype Textures → Kenney City Kit Industrial + PBR Poly Haven)* | `large_prop_building` | `false` | ≤ 600 / peça |
| **Todas (Z1–Z5)** | Paredes Fechadas & Tetos Modulares (`H = 8–18 m`, zero void) | ~90 inst. (batch estático) | **`[PLACEHOLDER CC0/Kenney]`** *(Kenney City Kit Industrial + PBR)* | `large_prop_building` | `false` | ≤ 1.500 / peça |
| **Z1 (Oficina)** | Bancada de Relógio (`I1`, Posto de Reparo) | 1 | **`[HERO Fab3D]`** *(placeholder mesa CC0 em C2–C4)* | `large_prop_building` | `false` | ≤ 1.500 |
| **Z1 (Oficina)** | Caixotes de Peças, Prateleiras e Ferramentas | 6 | **`[PLACEHOLDER CC0/Kenney]`** *(Kenney City Kit Industrial)* | `small_prop_item` | `false` | ≤ 600 |
| **Z2 (Bigorna)** | Pilares de Cobertura & Vigas do Mezanino (`+3.5 m`) | 6 | **`[PLACEHOLDER CC0/Kenney]`** *(Kenney City Kit Industrial)* | `large_prop_building` | `false` | ≤ 1.200 |
| **Z2 (Bigorna)** | Carrinho / Banca do Tio Horário | 1 | **`[HERO Fab3D]`** *(placeholder banca CC0 em C2–C4)* | `large_prop_building` | `false` | ≤ 2.000 |
| **Z3 (Pátio)** | Paredões de Caldeira para Wall-Run (`8 m` altura) | 4 | **`[PLACEHOLDER CC0/Kenney]`** *(Kenney City Kit Industrial + `ProceduralMeshLibrary` dutos)* | `large_prop_building` | `false` | ≤ 1.500 |
| **Z3 (Pátio)** | Dutos de Vapor e Tubulações de Cobre | 8 | **`[PLACEHOLDER CC0/Kenney]`** *(`ProceduralMeshLibrary` + PBR Cobre)* | `small_prop_item` | `false` | ≤ 600 |
| **Z4 (Caldeiral)** | **Sirene-Mestra / Caldeiral Central (Focal Point, `H = 16 m`)** | 1 | **`[HERO Fab3D]`** *(placeholder torre modular CC0 em C2–C4)* | `large_prop_building` (Hero) | `false` | ≤ 12.000 |
| **Z4 (Caldeiral)** | **Nó de Re-Sincronia — Catraca-Cristal (`I5`)** | 1 | **`[HERO Fab3D]`** *(placeholder pedestal CC0 em C2–C4)* | `small_prop_item` (Hero) | `false` | ≤ 800 |
| **Z4 (Caldeiral)** | Válvulas Rítmicas Interagíveis (`I2, I3, I4`) | 3 | **`[HERO Fab3D]`** *(placeholder volante CC0 em C2–C4)* | `small_prop_item` | `false` | ≤ 600 |
| **Z4 (Caldeiral)** | Passarelas Elevadas em Anel (`+3.5 m` e `+7.0 m`) | 8 | **`[PLACEHOLDER CC0/Kenney]`** *(Kenney City Kit Industrial)* | `large_prop_building` | `false` | ≤ 1.200 |
| **Z5 (Retorno)** | Catraca-Porta de Saída Sync (`I6`) + Plataformas Elevadas | 4 | **`[PLACEHOLDER CC0/Kenney]`** *(Kenney City Kit Industrial + shader 2 estados)* | `large_prop_building` | `false` | ≤ 800 |

---

## 6. Roadmap `C2 → C5` (Com Pré-Requisitos Obrigatórios dos Adendos B, C e D)

| Etapa | Pré-Requisito Bloqueante | Entregável & Gate |
| --- | --- | --- |
| **Gate C1 → C2** | 1) Linha `C1 APROVADO por Bruno em <YYYY-MM-DD>` gravada em `PROGRESS.md` (Adendo B).<br>2) **Adendo C:** `validate_scene.py` com orçamento Android URP (tris totais, luzes reais ≤ 5, draw calls, memória de textura, batching estático) provado com `exit 0` na cena limpa e `exit 1` em fixture de cena estourada.<br>3) **Adendo D:** Baseline PlayMode M1–M2 rodando verde em `evidence/tests/` (cena carrega, player spawna, objetivo M1 completa, 0 exceções). | Todos os 3 pré-requisitos verdes antes de criar o primeiro bloco do Caldeiral. |
| **C2 — Greybox Fechado Jogável** | Gate C1→C2 verde. | Construir Z1–Z5 fechadas (piso grid `Kenney Prototype Textures 1 tile = 1 m`, paredes + teto cinza, zero void, placeholders CC0 da Porca, Dez, NPCs e Ferrugens no eixo correto). Re-executar Baseline PlayMode + `validate_scene.py` + captura MCP multi-ângulo + aprovação gravada em `PROGRESS.md`. |
| **C3 — Kit CC0 + PBR World-Scale** | `C2 APROVADO` em `PROGRESS.md`. | Vestir Z1–Z5 com `Kenney City Kit (Industrial)` + materiais PBR CC0 (Poly Haven / AmbientCG) com tiling constante `1 tile = 1–2 m` + `ProceduralMeshLibrary` (zero `CreatePrimitive`) + 2 estados (`Desync`/`Sync`). Re-executar Baseline + `validate_scene.py` + capturas + aprovação. |
| **C4 — Composição & Auditoria de Câmeras CS01** | `C3 APROVADO` em `PROGRESS.md`. | Posicionar e validar os shots de `cutscene_01_abertura.yaml` zona por zona via capturas MCP (mostrar as **3 piores imagens** ao Bruno; zero corte em parede, zero void) + aprovação. |
| **C5 — Substituição In-Place por `[HERO Fab3D]`** | `C4 APROVADO` em `PROGRESS.md`. | Produzir e substituir **1 asset por vez** no mesmo transform (`Porca v3` → `Dez` → `Sirene-Mestra` → `Nó Re-Sincronia` → `Ferrugens` → `NPCs`), sempre via `fab3d_bridge.py` (`TRELLIS.2-4B + Hunyuan3D-Paint + pack_glb modo game`) + ordem completa de gates (`glTF-Validator` → `validate_asset` → `validate_visual` → `validate_scene` → `Fab3dImporter` + captura datada) + aprovação individual. |
