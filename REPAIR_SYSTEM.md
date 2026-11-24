# REPAIR SYSTEM — "O Concerto" (Loop de Gameplay do Ato 2)

## 1. Filosofia & Escolha do Minigame (Pesquisa)
- Para um jogo atmosférico cujo tema central é **concertar**, o reparo não deve punir com QTEs estressantes — precisa ser um **ritual tátil e satisfatório**.
- **Modelo escolhido:** **Sintonização Harmônica por Canalização (`Segure [E] / Clique`, ~4.2s)** com pulsos de ressonância dourados que aceleram suavemente o progresso (nunca resetam nem punem). Pode ser cancelado a qualquer momento com **`[Esc]`** ou **`[Q]`** sem deixar nenhum estado preso.
- **Coração do Game Feel (Resolução):** ao chegar em 100%, a anomalia se desfaz (`Anomaly.Resolve()`), dispara um flash dourado sutil (`Repair_ResolveFlash_Volume`), um acorde cristalino (`repair_resolve_chime_TEMP.wav`) + **0.65s do motif da caixinha de música** (`cs01_musicbox_motif_TEMP.wav`), e atualiza o HUD (`Repare as anomalias (k/N)`).

## 2. Arquitetura Extensível (`Assets/Game/Scripts/Repair/`)
- **`IRepairable`**: interface genérica (`RepairableId`, `DisplayName`, `CanBeRepairedNow`, `IsRepaired`, `RepairDurationSec`, `FocusPoint`, `OnRepairCompleted()`, `OnRepaired`).
- **`Repairable`** (`IInteractable, IRepairable`): componente genérico reutilizável em **qualquer objeto futuro** (anomalias, máquinas, portas, NPCs).
  - Quando vinculado a uma `Anomaly`, só libera interação quando `Anomaly.IsAnomalous == true` (nunca reparável antes da Cutscene 3!).
  - **Prova de Extensibilidade (Gate 3):** adicionar um 6º `Repairable` na cena/config faz `RepairRitualController.RefreshTargets()` atualizar automaticamente o contador para `(0/6)` com **zero alteração de código**.
- **`RepairRitualController`**: orquestra câmera `VCam_Repair_Focus` (`CinemachineCamera`, Priority `15`), trava de movimento do Dez, ferramenta TripoSG na mão (`dez_repair_tool`), faíscas, HUD harmônico, cancelamento limpo e o **Beat Final do Ato 2**.

## 3. Beat Final Canônico do Ato 2
Assim que todas as anomalias ativas são reparadas (`repaired == total`):
1. **2.0s de alívio** (`Oficina estabilizada`, zumbido grave cessa).
2. `VCam_Repair_Focus` focaliza a bancada — **a bugiganga da esposa quebra SOZINHA** na frente do jogador com estalo seco (`act2_trinket_crack_TEMP.wav`, **sem música**).
3. **Todas as luzes da oficina (`Caldeiral_CeilingLights`) piscam juntas uma vez.**
4. Controle devolvido em estado 100% limpo (`CONCERTO_ACT2_COMPLETED = 1`, objetivo `"Fim do Ato 2 — Algo continua errado na oficina"`), pronto para a futura Cutscene 4 / cataclismo maior.

## 4. Assets 3D & Reuso
- **Ferramenta de Reparo do Dez (`dez_repair_tool`):** gerada **exclusivamente via TripoSG + PBR 4K** (`Assets/Game/Art/Generated/repair/dez_repair_tool.fbx` + prefab reutilizável `Assets/Game/Prefabs/Fab3D/dez_repair_tool.prefab` — pronta para reuso direto na task da Ferramenta Multi-Uso!).
- **Reuso direto:** `IInteractable`, `InteractionHighlight`, `ObjectiveSystem`, `Anomaly`, `WorkshopAnomalyManager`, `Cinemachine 3.1.4`, `cs01_musicbox_motif_TEMP.wav`.
