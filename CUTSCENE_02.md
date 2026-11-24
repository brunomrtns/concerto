# CUTSCENE 02 — "A Viagem" (Fim do Ato 1)

## 1. Visão Geral & Lore Canônica
- **Cena:** `Assets/Game/Scenes/Caldeiral.unity` (reusada — sem duplicação de cena)
- **Duração total:** `85.0s` (5 Beats)
- **Ponte de gameplay:** Após entregar todas as peças da máquina do tempo no Ato 1 (`PartsLoopManager.Act1Completed`), o objetivo no HUD muda para **`Ative a máquina`**. Ao se aproximar da máquina sob lona (`CS01_Maquina_Coberta_Lona`) e pressionar **`[E] Ativar máquina`** (`TimeMachineInteractable`), a Cutscene 2 inicia automaticamente.
- **Personagem nova (Esposa):** **`Lira`** (`npc_esposa_lira`) — mesmo tipo de alienígena inteligente do Dez, paleta âmbar/creme/cobre coerente com a oficina. Nome curto, musical e harmonizado com *Dez* e *Concerto*.
  - *Mini-lore canônica:* Lira trabalhava lado a lado com Dez na bancada antes de falecer. Na cena do passado, a oficina é quente, viva e **o espaço onde hoje fica a máquina do tempo está completamente vazio**. No fim do reencontro, acontece o **primeiro errinho sutil** (som distorce brevemente, luz pisca, poeira cai do teto) — a interferência no espaço-tempo começa aqui, mas **nada é explicado** ao jogador.

## 2. Sistema Reusado (Padrão Cutscene 1)
- **Unity:** `6000.3.26f1` | **Cinemachine:** `3.1.4` (`Unity.Cinemachine`: `CinemachineCamera`, `CinemachineBrain`, `CinemachineTrack`, `CinemachineShot`) | **Timeline:** `1.8.13` | **URP:** `17.3.0`.
- **Reuso direto:**
  - Prefab de UI: `Assets/Game/Prefabs/UI/CutsceneUI_Canvas.prefab` (`CutsceneUIOverlay`: letterbox, legendas via JSON, title card, barra de skip).
  - Áudio reutilizado: `Assets/Game/Audio/Cutscenes/CS01/cs01_musicbox_motif_TEMP.wav` (tema da caixinha de música da Cutscene 1 volta no reencontro do Beat 3).
  - Porta-retrato da bancada e oficina (`Caldeiral.unity`) mantidos intactos.
  - Interação & Objetivo: `IInteractable` (`TimeMachineInteractable`) + `ObjectiveSystem`.
- **Isolamento de Post-Processing (Zero Vazamento — Gate 3):**
  - `CS02_WarpExposure_Volume` (`CS02_WarpExposureProfile.asset`) e `CS02_PastMemory_Volume` (`CS02_PastMemoryProfile.asset`) ficam desativados (`weight = 0`, `SetActive(false)`) fora da cutscene.
  - `RestorePresentStateImmediate()` desliga volumes, luzes do passado, partículas e `NPC_Lira_Passado`, e restaura `Caldeiral_Living_Alien_NPCs`, luzes da oficina e `DezLocomotionController` tanto no fim natural quanto no **Skip** (`Segure [Espaço] ou [Esc]`).

## 3. Beats & Shot List (`CS02_Travel_Timeline.playable`)

| Beat | Tempo | Câmera (`CinemachineCamera`) | SignalEmitter | Ação / Direção |
|---|---:|---|---|---|
| **Beat 1 — A Ativação** | `0.0s – 15.0s` | `VCam_CS02_B1_MachineActivate` (FOV 44°) | `SIG_CS02_B1_Charge` @ `1.5s` | Dez liga a máquina; luzes da oficina piscam; lona cai revelando `CS02_Maquina_Completa` brilhando em ciano; áudio de carga crescente. |
| **Beat 2 — A Viagem** | `15.0s – 30.0s` | `VCam_CS02_B2_TimeWarp` (FOV 56°) | `SIG_CS02_B2_Warp` @ `15.0s` | Efeito econômico de dobra temporal: curva de `postExposure` + `Bloom` + `ChromaticAberration` (`CS02_WarpExposure_Volume`) + `CS02_WarpParticles` + som esticado terminando em silêncio súbito. |
| **Beat 3 — O Passado** | `30.0s – 65.0s` | `VCam_CS02_B3_PastReunion` (FOV 45°) | `SIG_CS02_B3_Past` @ `30.0s` | Oficina sob `CS02_PastMemory_Volume` (dourado quente, saturação suave) + luz quente. **Espaço da máquina totalmente vazio.** `NPC_Lira_Passado` na bancada + tema da caixinha de música (`CS01`) + diálogo curto e íntimo com Dez. |
| **Beat 4 — O Primeiro Errinho** | `65.0s – 78.5s` | `VCam_CS02_B4_FirstGlitch` (FOV 36°) | `SIG_CS02_B4_Glitch` @ `65.0s` | Sem explicação: caixinha de música para, áudio distorce sutilmente (`cs02_glitch_distort_TEMP.wav`), luzes piscam, `CS02_CeilingDustParticles` cai do teto. Dez olha para cima, tenso. |
| **Beat 5 — Fim do Ato 1** | `78.5s – 85.0s` | `VCam_CS02_B5_BlackEndCard` (FOV 40°) | `SIG_CS02_B5_BlackCard` @ `78.5s`, `SIG_CS02_End` @ `84.8s` | Corte seco para preto absoluto (`CS02_CutToBlack_Backdrop`) → cartão TEMP `"FIM DO ATO 1\nCONCERTO"` → restaura 100% a oficina normal do presente. |

## 4. Assets 3D Novos (Exclusivamente TripoSG + PBR 4K)

| Asset | Ferramenta 3D | Rig / Texturas | Caminho no Unity |
|---|---|---|---|
| **`npc_esposa_lira`** (Esposa — Lira) | **TripoSG** (`triposg_runner.py`) | **UniRig** Humanoid + PBR 4K (`pipeline/run_asset.py`) | `Assets/Game/Art/Generated/npcs/npc_esposa_lira/npc_esposa_lira_humanoid.fbx` |
| **`cs02_prop_maquina_completa`** (Máquina do Tempo descoberta) | **TripoSG** (`triposg_runner.py`) | Estático watertight (7.5k tris) + PBR 4K + máscara emissiva ciano | `Assets/Game/Art/Generated/cs02/cs02_prop_maquina_completa.fbx` |

**Áudios TEMP:** `cs02_machine_charge_TEMP.wav`, `cs02_timewarp_stretch_TEMP.wav`, `cs02_glitch_distort_TEMP.wav` em `Assets/Game/Audio/Cutscenes/CS02/` (+ reuso de `cs01_musicbox_motif_TEMP.wav`).

## 5. Como Editar e Rejogar
- **Rejogar em PlayMode:** pressione **`F8`** a qualquer momento (debug replay), ou complete o Ato 1 e interaja com `[E] Ativar máquina`.
- **Editar falas e tempos de legenda:** edite `Assets/Game/Cutscenes/Data/cs02_viagem_subtitles.json` (sem recompilar código).
- **Editar câmeras, beats e sinais:** abra `Assets/Game/Cutscenes/CS02_Travel_Timeline.playable` na janela **Timeline** do Unity, ou ajuste `Assets/Editor/Cutscene02TravelBuilder.cs` e rode `Studio > Concerto > Cutscenes > Build Cutscene 02 (A Viagem) in Caldeiral`.

## 6. Como a Cutscene 3 Deve Reusar Este Padrão
1. Crie um novo `TimelineAsset` + novo `Data/cs03_*_subtitles.json` + novo `Cutscene03*Controller` (ou um `CutsceneControllerBase` se houver >3 cutscenes).
2. Reutilize o mesmo prefab `CutsceneUI_Canvas.prefab` e o mesmo `CinemachineBrain` da `Main Camera`.
3. Mantenha **todo** pós-processamento específico da cutscene em um `Volume` filho desativado por padrão e limpo em `RestorePresentStateImmediate()` — nunca altere o Volume global da cena.
4. Valide com um PlayMode Test seguindo `Cutscene02ValidationAndCaptureTest.cs` (asserts de estado limpo pós-fim e pós-skip).
