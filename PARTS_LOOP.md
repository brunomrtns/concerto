# PARTS_LOOP — Loop das Peças do Ato 1 (Pickup + Contador + Quadro Diegético)

## 1. Visão Geral
- **Fluxo completo do Ato 1:**
  1. Dez conversa com **Juno** (`dlg_juno_oficina_intro.json`) → libera a coleta das **N peças** e exibe **`Peças (0/5) — Encontre as peças da máquina`** no HUD (`ObjectiveSystem.Set`).
  2. Dez explora a Oficina Z1 e coleta cada peça com **`[E] Pegar peça`** (reusando `IInteractable` + `InteractionHighlight` + `DialogueController`).
  3. A cada pickup: toca `sfx_part_pickup_TEMP.wav`, dá **flash luminoso** no contador do HUD (`ObjectiveSystem.Flash()`) e marca o slot correspondente como **`[ COLETADA ]`** em ciano no **quadro físico diegético** (`CS01_Quadro_Checklist_Pecas` na parede norte da oficina!).
  4. Ao coletar todas as `N` peças: o objetivo muda para **`Leve as peças para Juno`** e o diálogo da Juno troca automaticamente para **`dlg_juno_entrega_pecas.json`**.
  5. Ao falar com Juno e concluir a entrega: toca o stinger musical curto (`sfx_act1_complete_stinger_TEMP.wav`), atualiza o HUD para **`Ato 1 concluído — Peças entregues!`** e dispara `PartsLoopManager.OnAct1Completed` (pronto para a Cutscene 2 na próxima task da fila).

---

## 2. Posições das 5 Peças na Oficina Z1 (`parts_loop_config.json`)

| Slot | ID | Nome | Modelo 3D (**TripoSG**) | Local na Oficina Z1 | Coordenadas `(X, Y, Z)` |
|---|---|---|---|---|---|
| 01 | `part_01` | Núcleo de Ressonância | `part_nucleo_ressonancia.fbx` | Sobre o caixote noroeste | `(-34.6, 1.28, -13.2)` |
| 02 | `part_02` | Bobina de Fase Beta | `part_bobina_fase.fbx` | Ao lado da estante de cristais | `(-31.3, 0.42, -13.1)` |
| 03 | `part_03` | Regulador Crono | `part_regulador_crono.fbx` | Sob a bancada central | `(-29.0, 0.36, -16.45)` |
| 04 | `part_04` | Condensador de Fluxo | `part_bobina_fase.fbx` | Sobre o caixote sudeste | `(-24.0, 1.24, -22.6)` |
| 05 | `part_05` | Estabilizador Orbital | `part_nucleo_ressonancia.fbx` | Sobre o caixote sudoeste | `(-34.5, 1.24, -22.8)` |

---

## 3. Como Ajustar `N` (Quantidade de Peças) — ZERO Código!
1. Abra [`unity/Assets/Game/Parts/Data/parts_loop_config.json`](unity/Assets/Game/Parts/Data/parts_loop_config.json).
2. Altere apenas `"requiredCount": 5` para o valor desejado (ex.: **`3`**).
3. Salve. O `PartsLoopManager` ativa automaticamente apenas as primeiras `N` peças, ajusta o quadro diegético para `N` slots, atualiza o HUD para `(0/N)` e dispara a entrega para Juno ao coletar `N/N`. *(Validado automaticamente no Gate 3 de `PartsLoopValidationAndCaptureTest.cs`!)*

---

## 4. Como Adicionar uma Nova Peça
1. Adicione uma nova entrada no array `"parts"` em [`parts_loop_config.json`](unity/Assets/Game/Parts/Data/parts_loop_config.json) com `id`, `displayName`, `prefabSlug` (um dos modelos TripoSG em `Assets/Game/Art/Generated/parts/`) e `position`.
2. Ajuste `"requiredCount"` se quiser incluí-la na meta.
3. No Unity, execute o menu **`Studio > Concerto > Parts > Setup Act 1 Parts Loop in Caldeiral`** para instanciar automaticamente a nova peça com brilho, colisor trigger e vínculo no `PartsLoopManager`.
