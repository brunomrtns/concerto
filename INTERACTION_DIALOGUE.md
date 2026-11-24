# INTERACTION_DIALOGUE — Sistema de Interação, Diálogo e Objetivo no HUD

## 1. Arquitetura & Decisões
- **Interação 3D (Híbrido Proximidade `<= 2.5m` + Cone de Mira):** `DialogueController.ScanFocusedInteractable()` usa `Physics.OverlapSphere` em torno do Dez (`2.5m`) combinado com produto escalar (*dot product*) contra a direção da câmera/personagem. Isso dá o conforto de proximidade em 3ª pessoa sem exigir mira milimétrica, mas escolhe com precisão quem você está encarando.
- **Highlight Sutil:** `InteractionHighlight.cs` destaca o interagível focado via `MaterialPropertyBlock` (sem duplicar materiais nem gerar vazamento de memória) e restaura 100% ao sair de foco.
- **Diálogo 100% Data-Driven (JSON chave → texto):**
  - Tabela de strings localizáveis: [`unity/Assets/Game/Dialogue/Data/strings_pt_br.json`](unity/Assets/Game/Dialogue/Data/strings_pt_br.json)
  - Conversa canônica da Juno: [`unity/Assets/Game/Dialogue/Data/dlg_juno_oficina_intro.json`](unity/Assets/Game/Dialogue/Data/dlg_juno_oficina_intro.json)
  - Conversa de prova de sistema (zero código): [`unity/Assets/Game/Dialogue/Data/dlg_juno_teste_sistema.json`](unity/Assets/Game/Dialogue/Data/dlg_juno_teste_sistema.json)
  - **ZERO string hardcoded em C#** — toda legenda, prompt e objetivo vem do JSON.
- **Datilografia (Typewriter) & Controles:**
  - Tecla **`E`** inicia a conversa quando `[E] Falar` está visível.
  - Durante o diálogo, `DezLocomotionController` e `AlienNpcRoutineController` ficam suspensos (personagens viram um para o outro).
  - Apertar **`E`** (ou `Espaço`) enquanto a linha está sendo digitada **revela a linha inteira primeiro**; apertar novamente **avança** para a próxima linha ou encerra devolvendo 100% o controle.
- **Objetivo Mínimo no HUD (`ObjectiveSystem`):**
  - Exibe **UM objetivo ativo por vez** no canto superior esquerdo (`DialogueHUD_Canvas.prefab`).
  - API pública reutilizável pela próxima task (peças da máquina):
    ```csharp
    ObjectiveSystem.Set($"Encontre as peças da máquina ({coletadas}/{total})");
    ObjectiveSystem.Clear();
    ```

---

## 2. Como Adicionar um Novo Diálogo (Passo a Passo — ZERO Código!)
1. **Adicione as chaves de texto** em [`unity/Assets/Game/Dialogue/Data/strings_pt_br.json`](unity/Assets/Game/Dialogue/Data/strings_pt_br.json):
   ```json
   { "key": "speaker_tio", "value": "Tio Horário" },
   { "key": "tio_01", "value": "Ei, Dez! Viu onde deixei minha chave de calibre?" },
   { "key": "tio_02", "value": "Sem ela a válvula não fecha nem a pau." }
   ```
2. **Crie um novo arquivo JSON** em `unity/Assets/Game/Dialogue/Data/dlg_tio_oficina.json`:
   ```json
   {
     "dialogueId": "dlg_tio_oficina",
     "promptKey": "prompt_talk",
     "postObjectiveKey": "",
     "partsTotal": 3,
     "lines": [
       { "speakerKey": "speaker_tio", "textKey": "tio_01" },
       { "speakerKey": "speaker_tio", "textKey": "tio_02" }
     ]
   }
   ```
3. **Atribua no Inspector:** Selecione o NPC na cena, adicione (ou selecione) o componente `NpcDialogueInteractable` e arraste `dlg_tio_oficina.json` para o campo **`Dialogue Json`**. Pronto — nenhuma linha de C# necessária!

---

## 3. Como Adicionar Outro Interagível (Ex.: Peça da Máquina, Terminal, Alavanca)
1. Crie uma classe `MonoBehaviour` implementando **`Studio.Gameplay.IInteractable`** (ou herde de `InteractableBase`):
   - `PromptText` → retorne `DialogueController.Instance.Localize("sua_chave_prompt")`
   - `CanInteract(GameObject interactor)` → retorne `true` quando disponível
   - `Interact(GameObject interactor)` → execute sua ação (ex.: coletar peça e chamar `ObjectiveSystem.Set(...)`)
2. Garanta que o GameObject (ou um filho) tenha um **`Collider`** (normal ou `isTrigger = true`) para ser detectado pelo `OverlapSphere`.
3. Opcionalmente adicione `InteractionHighlight` para ganhar o brilho sutil automático ao entrar em foco!
