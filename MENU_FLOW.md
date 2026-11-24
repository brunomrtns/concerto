# MENU FLOW — Boot, Menu Principal, Opções & Pause

## 1. Fluxo Completo de Telas
```
[Boot Splash (Logo TEMP)] ──(fade ~2.8s ou qualquer tecla)──► [Menu Principal (MainMenu.unity)]
                                                                 │
      ┌──────────────────────────────────────────────────────────┼─────────────────────────────┐
      ▼                                                          ▼                             ▼
[Novo Jogo]                                                 [Continuar]                    [Opções]
Reseta flags PlayerPrefs                                    Carrega Caldeiral              Painel compartilhado
+ limpa CONCERTO_CS01_PLAYED                                + restaura objetivo            (OptionsPanel.prefab)
      │                                                     + peças coletadas              Volume Master/Música/SFX
      ▼                                                          │                         Tela Cheia + Qualidade
[Caldeiral.unity] ──► Cutscene 1 ──► Gameplay ◄──────────────────┘                         (persiste entre sessões)
                                         │
                                    [Tecla Esc] (bloqueado em Cutscene e Diálogo!)
                                         ▼
                                   [Menu Pause]
                                   - Time.timeScale = 0
                                   - AudioListener.pause = true
                                   - DezLocomotionController desabilitado
                                   ├── Continuar
                                   ├── Opções (mesmo OptionsPanel.prefab)
                                   └── Voltar ao Menu (modal de confirmação se houver progresso) ──► MainMenu.unity
```

## 2. Identidade Visual & Reuso (Zero Asset 3D Novo)
- **Foto da bancada:** `Assets/Game/Art/Textures/Cutscenes/cs01_foto_dez_esposa.png` emoldurada em cobre/dourado no Menu Principal.
- **Áudio do menu:** `Assets/Game/Audio/Cutscenes/CS01/cs01_musicbox_motif_TEMP.wav` tocando suavemente em loop (`baseMusicVolume = 0.42f * MusicVolume`).
- **Coerência visual:** mesma paleta escura/cobre/dourado/ciano e tipografia (`LegacyRuntime.ttf`) do HUD de diálogo e cutscenes.
- **Painel único de Opções:** `Assets/Game/Prefabs/UI/OptionsPanel.prefab` instanciado tanto em `MainMenu.unity` quanto em `Caldeiral.unity` (`PauseMenu_Canvas`).

## 3. Decisão de Design: Pause Bloqueado em Cutscene e Diálogo
- `PauseMenuController.CanOpenPauseNow()` bloqueia a abertura do Pause sempre que:
  1. `CutsceneManager.IsPlaying` (Cutscene 1) for `true`;
  2. `Cutscene02TravelController.IsPlaying` (Cutscene 2) for `true`;
  3. `DialogueController.IsDialogueActive` for `true`.
- **Motivo:** durante cutscenes, a tecla `Esc` já é usada para o **Skip Hold** (`Segure [Espaço] ou [Esc] para pular`), evitando conflito de input; durante diálogos, o foco do jogador e a trava do `DezLocomotionController` pertencem ao `DialogueController`, impedindo que o Pause devolva o controle do personagem fora de hora.

## 4. Flags de Save & Opções (`PlayerPrefs`)
*(Save mínimo temporário — sistema de save estruturado em disco fica para task futura.)*

| Chave `PlayerPrefs` | Tipo | Descrição |
|---|---|---|
| `CONCERTO_SAVE_EXISTS` | `int` (`0`/`1`) | Habilita o botão **Continuar** no Menu Principal |
| `CONCERTO_SAVE_SCENE` | `string` | Última cena de gameplay salva (default `"Caldeiral"`) |
| `CONCERTO_SAVE_OBJECTIVE` | `string` | Texto do objetivo atual (`ObjectiveSystem.CurrentObjective`) |
| `CONCERTO_SAVE_PARTS_MASK` | `int` | Bitmask das peças já coletadas em `PartsLoopManager` |
| `CONCERTO_SAVE_CAN_COLLECT` | `int` (`0`/`1`) | Se o loop de coleta das peças já foi liberado pela Juno |
| `CONCERTO_SAVE_ACT1_DONE` | `int` (`0`/`1`) | Se as peças já foram entregues para a Juno |
| `CONCERTO_LOAD_MODE` | `string` | Flag transiente `"new_game"` ou `"continue"` lida no boot da oficina |
| `CONCERTO_CS01_PLAYED` | `int` (`0`/`1`) | Apagada em **Novo Jogo** (roda CS01); setada em **Continuar** (pula CS01) |
| `CONCERTO_OPT_MASTER_VOL` | `float` | Volume Master (`AudioListener.volume`) |
| `CONCERTO_OPT_MUSIC_VOL` | `float` | Volume da música do menu / trilhas |
| `CONCERTO_OPT_SFX_VOL` | `float` | Volume de efeitos sonoros |
| `CONCERTO_OPT_FULLSCREEN` | `int` (`0`/`1`) | Modo Tela Cheia (`Screen.fullScreen`) |
| `CONCERTO_OPT_QUALITY` | `int` | Índice de `QualitySettings.SetQualityLevel` |

## 5. Como Adicionar um Novo Item de Menu
1. **No Menu Principal (`MainMenu.unity`):**
   - Adicione um novo botão filho dentro de `MainMenu_Canvas/MainMenu_Root/MenuButtonStack` (que já usa `VerticalLayoutGroup`, então o espaçamento se ajusta automaticamente), ou adicione uma chamada `CreateStyledButton(...)` em `Assets/Editor/MenuAndPauseSetup.cs`.
   - Exponha a referência `public Button meuNovoBotao;` em `BootAndMainMenuController.cs` e registre o listener em `Awake()`.
2. **No Pause (`Caldeiral.unity`):**
   - Adicione o botão dentro de `PauseMenu_Canvas/Pause_Root/PauseCard/PauseButtonStack` e conecte o listener em `PauseMenuController.cs`.
3. **Para reconstruir tudo de forma reprodutível:** rode no menu do Unity `Studio > Concerto > Menu > Build Boot, MainMenu & Pause Flow`.
