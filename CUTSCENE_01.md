# CUTSCENE 01 — "Concerto" (Abertura Oficial)

## 1. Visão Geral & Lore Canônica
- **ID:** `cs01_abertura_concerto`
- **Duração:** `72.0s` (1080p, letterbox uGUI, legendas via JSON, sem voice acting nesta etapa)
- **Cena:** `unity/Assets/Game/Scenes/Caldeiral.unity` (roda **in-engine** dentro da oficina real, sem duplicar cena)
- **Stack:** Unity `6000.3.26f1` + `Timeline 1.8.13` + `Cinemachine 3.1.4` (`Unity.Cinemachine`) + `URP 17.3.0 Volume` + `uGUI Prefab`
- **Elenco:** **Dez** (`Player_Dez_Placeholder`, TripoSG) e **Juno** (`NPC_Juno_Oficina`, TripoSG)
- **Subtexto emocional:** Planta com sutileza o projeto ultrassecreto (**"Projeto Echo"** — sem mencionar "máquina do tempo") e o vínculo afetivo de Dez com a esposa falecida (foto antiga na bancada + caixinha de música + eco *"Você sempre conserta tudo, Dez."*).

---

## 2. Roteiro Final & Shot List (5 Beats)

| Beat | Tempo | Câmera (`CinemachineCamera`) | Enquadramento & Movimento | Áudio / SignalEmitter | Legendas (`cs01_abertura_subtitles.json`) |
|---|---|---|---|---|---|
| **Beat 1** | `0:00 – 0:10` | `VCam_B1_BenchClose` (FOV `36°`) | Close íntimo na bancada: porta-retrato antigo de Dez com a esposa + caixinha de música clockwork aberta (TripoSG). | `SIG_Beat1_MusicBox` (0.2s — motif calmo **TEMP**), `SIG_Beat1_RadioChime` (2.0s) | Rádio interno anunciando fase final do **Projeto Echo** (acesso restrito, sem revelar máquina do tempo). |
| **Beat 2** | `0:10 – 0:25` | `VCam_B2_WorkshopReveal` (FOV `48°`, `AnimationTrack` dolly de recuo) | Câmera recua suavemente revelando a oficina inteira, luz quente, estante de cristais e a **estrutura coberta com lona** (TripoSG) com brilho ciano discreto na base. | `SIG_Beat2_MachineHum` (10.2s — zumbido grave 55Hz **TEMP**) | Rádio reforça que o prazo da estrutura principal está mantido. |
| **Beat 3** | `0:25 – 0:45` | `VCam_B3_ChecklistAndJuno` (FOV `42°`) | Dez caminha até o **Quadro de Checklist** (`PROJETO ECHO — CONFIDENCIAL`, 3 slots vazios). **Juno** entra na conversa com tom leve e humor seco sobre o comboio perdido de peças. | `SIG_Beat3_DezWalkBoard` (25.0s) | Diálogo curto Dez + Juno estabelecendo o que falta (núcleo, bobina, cristal) e que Juno vai puxar o sinal. |
| **Beat 4** | `0:45 – 1:00` | `VCam_B4_PhotoEmotion` (FOV `32°`) | Close emocional na foto da bancada e em Dez. Eco abafado da esposa. Dez decide seguir em frente. | `SIG_Beat4_MemoryEcho` (47.5s — ressonância etérea **TEMP**) | *"Você sempre conserta tudo, Dez."* → Dez: *"...Desta vez não vai ser diferente."* |
| **Beat 5** | `1:00 – 1:12` | `VCam_B5_MachineTitle` (FOV `44°`) | Dez vira decidido para a máquina coberta. **Title Card: `CONCERTO`** (62.5s–69.5s). Motif musical fecha. Letterbox abre, corte seco para gameplay na oficina + objetivo suave **`"Fale com Juno"`**. | `SIG_Beat5_TitleMotif` (61.5s), `SIG_Cutscene_End` (71.8s) | Title Card `CONCERTO` → Toast pós-cutscene: `OBJETIVO • Fale com Juno`. |

---

## 3. Como Testar, Pular e Rejogar
1. **Primeiro Play (automático):** Ao dar Play em `Caldeiral.unity` pela primeira vez, a cutscene dispara automaticamente (salva flag `PlayerPrefs["CONCERTO_CS01_PLAYED"] = 1`).
2. **Pular no meio (Skippable):** Após `3.0s`, aparece no canto inferior direito **`"Segure [Espaço] ou [Esc] para pular"`**. Segure por `0.85s` para encerrar limpo e receber o controle imediato do Dez.
3. **Atalhos de Debug:**
   - **`F9`**: Rejoga a Cutscene 01 do início a qualquer momento durante o PlayMode.
   - **`F10`**: Reseta a flag `CONCERTO_CS01_PLAYED` para testar novamente o disparo automático de "Novo Jogo".

---

## 4. Como Editar os Beats da Cutscene 01
- **Alterar falas, tempos ou traduzir idioma:** Edite apenas [`unity/Assets/Game/Cutscenes/Data/cs01_abertura_subtitles.json`](unity/Assets/Game/Cutscenes/Data/cs01_abertura_subtitles.json) — nenhuma fala está hardcoded em C#.
- **Ajustar câmeras / tempos de corte / blends:** Abra `Window > Sequencing > Timeline`, selecione `CS01_Opening_Concerto` na Hierarchy e arraste os `CinemachineShot`s na track `Cinemachine Shot List` ou ajuste a posição/FOV das `VCam_B1..B5` em `CS01_Opening_Concerto/CS01_VirtualCameras`.
- **Ajustar iluminação ou color grade da cutscene:** Edite as luzes em `CS01_Opening_Concerto/CS01_CutsceneLightRig` ou o perfil `Assets/Game/Cutscenes/CS01_PostProcessProfile.asset` (só ficam ativos durante a cutscene!).
- **Regenerar do zero via script:** Menu `Studio > Concerto > Cutscenes > Build Cutscene 01 (Concerto Opening) in Caldeiral`.

---

## 5. Como Criar a Próxima Cutscene (`CS02`, `CS03`, ...) Reusando Este Sistema
Este sistema é o **padrão oficial** de todas as cutscenes do jogo:
1. **Crie o JSON de legendas:** Duplique `Assets/Game/Cutscenes/Data/cs01_abertura_subtitles.json` para `cs02_<nome>_subtitles.json` com os novos `lines`, `cutsceneId`, `totalDurationSec` e `postObjectiveText`.
2. **Crie um GameObject na cena** (ex.: `CS02_<Nome>`) com:
   - `PlayableDirector` (apontando para o novo `TimelineAsset`);
   - `SignalReceiver`;
   - `CutsceneManager` (apontando para o novo `subtitleJson`, `CutsceneUI_Canvas.prefab`, rig de luz local e `Volume` temporário).
3. **Adicione as `CinemachineCamera`s** filhas em `CS02_VirtualCameras` e crie uma `CinemachineTrack` no Timeline ligada ao `CinemachineBrain` da `Main Camera`.
4. **Sincronize áudio/eventos com `SignalTrack`:** Reuse os `SignalAsset`s existentes ou crie novos em `Assets/Game/Cutscenes/Signals/` ligando ao `SignalReceiver`.
5. **Dispare no jogo:** Chame `cutsceneManager.PlayCutscene()` via trigger ou evento de história — o `CutsceneManager` cuida automaticamente de suspender/restaurar input, câmera, luzes, post-processing, letterbox, skip e objetivo pós-cutscene.
