# Cutscene 4 — "O Céu Rachou" (Ponte Oficina → Ato 3)

## Visão Geral
- **Arquivo de Controle:** `unity/Assets/Game/Scripts/Cutscenes/Cutscene04SkyCrackedController.cs`
- **Timeline:** `unity/Assets/Game/Cutscenes/CS04_O_Ceu_Rachou_Timeline.playable` (`36.0s`, 4 Beats)
- **Legendas & Metadados:** `unity/Assets/Game/Cutscenes/Data/cs04_subtitles_pt_br.json`
- **Gatilho Automático:** Assina `RepairRitualController.OnAct2FinalBeatCompleted` com pausa dramática de `1.6s` pós-piscar das luzes (ou atalho de debug `F8` em `Caldeiral.unity`).
- **Reuso 100%:** Reusa `CutsceneUI_Canvas.prefab` (letterbox, legendas, skip segurando `[Espaço]`/`[Esc]`, fade), `NPC_Juno_Oficina`, `Door_Caldeiral_To_Sector`, `SetorExterno.unity`, `NPC_Engenheira_Vera`, `SkyRift_Centerpiece` e `sector_cold_wind_ambience_TEMP.wav`. Nenhum modelo 3D novo foi necessário.

---

## Roteiro & Shot List (36s — 4 Beats Canônicos)

| Beat | Tempo | Cena | Câmera & Ação | Áudio | Legenda |
|---|---|---|---|---|---|
| **Beat 1** | `0:00–0:10` | `Caldeiral` | Luzes de teto caem para luzes vermelhas/âmbar de backup (`CS04_Workshop_BackupLights`). Câmera aproxima da bancada enquanto `NPC_Juno_Oficina` corre apressada até o Dez. | `cs04_alarm_backup_TEMP.wav` (alarme discreto industrial + queda de relé) | `3.4s–8.4s`: *"Juno: Dez. Tá acontecendo lá fora."* |
| **Beat 2** | `0:10–0:18` | `Caldeiral` → preload `SetorExterno` | Dez e Juno diante da `Door_Caldeiral_To_Sector` (corredor leste). Flag `CONCERTO_SECTOR_EXTERNO_UNLOCKED` destrava (luz vermelha → ciano). Porta pesada treme e desliza abrindo com esforço. `LoadSceneAsync("SetorExterno")` pré-carrega em background (`allowSceneActivation = false`). Em `17.55s` fade sutil cruza a abertura da porta. | `cs04_heavy_door_unlock_TEMP.wav` (**herói sonoro**: trancas metálicas, descarga pneumática, ranger pesado, lufada de vento) | *(sem fala)* |
| **Beat 3A** | `0:18–0:26` | `SetorExterno` (seamless) | `allowSceneActivation = true` em 1 frame sob o runner `DontDestroyOnLoad`. Fade abre direto na revelação: câmera inclinada para cima segurando no **Céu Errado** (rachadura luminosa HDR + aurora glitch). | Silêncio total + `sector_cold_wind_ambience_TEMP.wav` | *(sem fala)* |
| **Beat 3B** | `0:26–0:32` | `SetorExterno` | Câmera enquadra `NPC_Engenheira_Vera` ao longe no pátio, parada olhando para o céu rachado. | Só vento frio | *(sem fala)* |
| **Beat 4** | `0:32–0:36` | `SetorExterno` | Câmera pousa suavemente atrás do Dez na entrada do setor `(0, 0.05, 3.0)`. Letterbox sobe, `CinemachineBrain` e `DezLocomotionController` reativam, 3 anomalias do setor ativam, objetivo **"Fale com Vera"** pisca no HUD e `SaveSystem.Save()` dispara. | Vento frio contínuo da cena | *(sem fala)* |

---

## Flags de Porta & Integração com `SaveSystem`

- **Porta do Setor Externo:**
  - `SectorDoorInteractable.IsSectorUnlocked` (`PlayerPrefs` key `CONCERTO_SECTOR_EXTERNO_UNLOCKED`) é gravada como `true` no início do Beat 2 (e garantida também no `SkipCutscene()`).
  - Persistida também no JSON versionado em `SaveGameData.sectorDoorUnlocked`.
- **Flag de Cutscene Assistida:**
  - `Cutscene04SkyCrackedController.PlayedPrefKey = "CONCERTO_CS04_PLAYED"` e `SaveGameData.cs04Played = true`.
- **Auto-Save & Continuar:**
  - No final do Beat 4 (ou após `SkipCutscene()`), já dentro de `SetorExterno.unity`, executa `SaveSystem.Save("cp_pos_cs04_setor_externo", new Vector3(0f, 0.05f, 3.0f), 0f)`.
  - Ao clicar **Continuar** no `MainMenu`, `SaveSystem.MarkContinueRequested(out sceneToLoad)` retorna `"SetorExterno"`, e `SaveCheckpointHUD.Start()` aplica automaticamente `SaveSystem.TryApplyPendingLoadToWorkshop()`, restaurando posição, rotação, objetivo `"Fale com Vera"`, flag da porta e células de energia dentro do setor.
- **Comportamento de Skip:**
  - Segurar `[Espaço]` ou `[Esc]` (`0.85s`) a partir de `t = 2.0s` (ou chamar `SkipCutscene()`) destrava a porta, completa a carga de `SetorExterno.unity` se ainda estiver na oficina, devolve controle 100% limpo (`Time.timeScale = 1f`, `DezLocomotionController.enabled = true`, `CinemachineBrain.enabled = true`, letterbox fechado), seta objetivo `"Fale com Vera"` e dispara o auto-save.
