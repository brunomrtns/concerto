# CS02 "A OFERTA" — O TWIST DO MEIO DE JOGO (`GDD 01 §3 Ato 2` + `09-cutscenes`)

## Declaração de Conformidade
- **`docs/DESIGN_BIBLE.md`, `GDD 01 §3` & `09-cutscenes.md`:** 100% conforme — a tentação é real (o jogo **não** diz que Íria está mentindo), skippable apenas após `3,0s`, pause real funcional, travamento e liberação limpa de gameplay sem resetar estado de missão/objetivo.
- **Zero Modelos 3D Novos (Regra 2):** Íria fala através de um **Censor existente** (`Assets/Game/Art/Generated/combat_gdd/enemy_censor_patrulha.fbx`), acompanhada por `hero_dez_humanoid.prefab` e `hero_porca.prefab`.

---

## Roteiro & Pipeline VO (`docs/narrative/dialogues.yaml` → `tts_bridge.py` XTTS-v2)

| # | ID | Personagem | Emoção | Legenda PT-BR |
|---|----|------------|--------|---------------|
| 1 | `cs02_iria_01` | Íria (via Censor) | `ameaçador-suave` | *"Devolva minhas relíquias... e devolvo sua irmã."* |
| 2 | `cs02_dez_01` | Dez | `urgente` | *"Onde você tá guardando ela?"* |
| 3 | `cs02_iria_02` | Íria (via Censor) | `calmo-persuasivo` | *"No último segundo intacto da Corona. Traga as relíquias e eu abro a caixa."* |
| 4 | `cs02_porca_01` | Porca | `apreensivo` | *"A voz dela não tremeu nem meio compasso, Dez."* |
| 5 | `cs02_iria_03` | Íria (via Censor) | `firme` | *"Pense bem no que vale mais, Desidério."* |
| 6 | `cs02_dez_02` | Dez | `contido` | *"Vamos continuar andando, Porca. Mas eu ouvi cada palavra."* |

Áudios VO gerados localmente em `unity/Assets/Game/Audio/VO/cs02_oferta/*.wav` via `tools/audio/dialogue_adapter.py` + `tools/audio/tts_bridge.py`.

---

## Trigger, Controles & Persistência
- **Trigger (`CutsceneAOfetaController.EvaluateRelicCountTrigger`):** Dispara automaticamente **uma única vez** ao coletar a 2ª Relíquia (`RequiredRelicsToTrigger = 2`).
- **Skip & Pause:** Skip bloqueado em `ElapsedSec < 3,0s` e liberado em `>= 3,0s`; `SetPaused(true/false)` congela o tempo e pausa/retoma o áudio VO.
- **Save JSON:** Flag `cs02OfertaPlayed` persistida em `SaveGameData.cs` + `SaveSystem.cs` — reload nunca reprime nem repete a cutscene, e o objetivo atual (`ObjectiveSystem.Current`) permanece intacto.

---

## Evidências & Validação
- **Vídeo da Cutscene Completa:** `/captures/cutscenes/cs02_oferta.mp4`
- **SmokeTest automatizado:** `Smoke_34_Cs02AOfetaTriggerAt2RelicsSkipAfter3sPauseLockReleaseAndNoRepeat` (`34/34 PASSED`, `0 errors CS`, `0 warnings CS`).
