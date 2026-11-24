# CUTSCENE_QA.md — Padrão de Qualidade e Auditoria de Cutscenes (CONCERTO)

Toda cutscene nova ou modificada em **CONCERTO** só é considerada entregue quando **100% dos itens deste checklist** passarem via teste automatizado (`CutscenesFullQAAuditTest.cs`) + inspeção visual do MP4 1080p completo.

---

## Regras Invioláveis de QA de Cutscene
1. **Movimento Scriptado é Coreografado, Nunca Físico:** durante qualquer cutscene ativa, `CutsceneActorGuard.BeginCutscene()` **desabilita** o `CharacterController` do Dez e de todos os NPCs e pausa `AlienNpcRoutineController`. Nenhum ator empurra prop, desliza preso em quina ou anda travado contra objeto. No `FinishCutscene()` / `SkipCutscene()`, `CutsceneActorGuard.EndCutscene()` restaura tudo.
2. **Zero Escuridão >3s:** a luminância média da área útil central (descontando letterbox) não pode ficar abaixo de `0.042` por mais de `3.0s` contínuos — nem no início da cutscene, nem em flickers, nem em cartões de encerramento.
3. **Câmera Limpa na Entrega:** `CinemachineBrain` permanece habilitado ao encerrar a cutscene para devolver o controle suavemente para `VCam_Gameplay_Dez` (nunca desabilitar o `CinemachineBrain` no `FinishCutscene()`).
4. **Skip Determinístico em Qualquer Beat:** chamar `SkipCutscene()` (ou segurar `[Espaço]`/`[Esc]`) restaura `Time.timeScale = 1f`, `CharacterController.enabled = true`, `DezLocomotionController.enabled = true`, `CinemachineBrain.enabled = true`, luzes da oficina (`ceilingLightsRoot`), e zera volumes temporários (`pastMemoryVolume`, `warpExposureVolume`, `returnFlashVolume`).

---

## Checklist de Auditoria — Cutscenes 1 a 4 (Antes → Correção → Depois)

| Cutscene | Sintoma / Bug Encontrado (Antes) | Correção Aplicada | Estado Verificado (Depois) |
|---|---|---|---|
| **CS01 — "Concerto"** | 1) Beat 3 usava `_playerCc.Move(...)` com `CharacterController` ativo → Dez travava contra quinas da bancada andando no lugar.<br>2) NPCs (`AlienNpcRoutineController`) continuavam patrulhando no fundo.<br>3) `FinishCutscene()` desligava `CinemachineBrain`. | 1) Integrado `CutsceneActorGuard` + movimento coreografado via `Vector3.MoveTowards` sem física.<br>2) Todos os NPCs pausados durante a cutscene.<br>3) `CinemachineBrain` mantido ativo ao devolver controle. | **PASS** (`2154 frames`): `initialDark=0.00s`, `maxDarkStreak=0.00s`, `cc=OFF` durante / `ON` depois, Skip limpo OK; MP4 re-gravado (`cs01_concerto_qa_fixed_1080p.mp4`). |
| **CS02 — "A Viagem"** | 1) `CharacterController` do Dez permanecia ativo durante a cutscene e NPCs seguiam patrulhando nos Beats 1–2.<br>2) `CS02_CutToBlack_Backdrop` + `OnSignalBeat5CutToBlackCard` deixavam a tela preta por **`6,30s`** contínuos (medido antes!), além de flicker agressivo (`Sin > -0.35`).<br>3) `FinishCutscene()` desligava `CinemachineBrain`. | 1) Integrado `CutsceneActorGuard` + teleporte coreografado.<br>2) `CS02_CutToBlack_Backdrop` restrito aos últimos `1.8s` + flicker breve (`Sin > -0.78`).<br>3) `CinemachineBrain` mantido ativo ao encerrar. | **PASS** (`2544 frames`): `initialDark=0.00s`, **`maxDarkStreak`: `6,30s` → `1,60s`**, `cc`/`loco`/`brain` restaurados, Skip limpo OK; MP4 re-gravado (`cs02_viagem_qa_fixed_1080p.mp4`). |
| **CS03 — "O Retorno"** | 1) `CharacterController` ativo e NPCs patrulhando no fundo.<br>2) `CS03_CutToBlack_Backdrop` deixava a tela preta por **`5,80s`** contínuos (medido antes!) e `returnFlashVolume` durava `3.5s`. | 1) Integrado `CutsceneActorGuard`.<br>2) `CS03_CutToBlack_Backdrop` restrito aos últimos `2.0s`, rampa do `returnFlashVolume` reduzida para `1.8s` e flicker breve (`Sin > -0.75`). | **PASS** (`954 frames`): `initialDark=0.00s`, **`maxDarkStreak`: `5,80s` → `1,80s`**, `cc`/`loco`/`brain` restaurados, Skip limpo OK; MP4 re-gravado (`cs03_retorno_qa_fixed_1080p.mp4`). |
| **CS04 — "O Céu Rachou"** | 1) `CharacterController` de Dez e Juno permanecia ativo durante Beat 1 e Beat 2.<br>2) Luzes de backup da oficina estavam fracas (`intensity=3.2`), deixando o Beat 2 escuro e com ângulo fechado na porta. | 1) Integrado `CutsceneActorGuard` com restauração pós-transição seamless.<br>2) Intensidade das luzes de backup elevada (`>= 7.2`, alcance `24m`) e câmera do Beat 2 aberta enquadrando Dez, Juno e a porta pesada abrindo. | **PASS** (`728 frames`): `initialDark=0.00s`, `maxDarkStreak=0.00s`, transição seamless para `SetorExterno` + auto-save intactos, Skip limpo OK; MP4 re-gravado (`cs04_ceu_rachou_qa_fixed_1080p.mp4`). |

---

## Como Rodar o QA Automatizado em Novas Cutscenes
```powershell
$env:CAPTURE_QA = "1"
& "C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" -batchmode -projectPath "C:\Users\Bruno\workspace\game-concerto\unity" -runTests -testPlatform PlayMode -testFilter "CutscenesFullQAAuditTest"
```
Qualquer falha em `initialDark >= 3.0s`, `maxDarkStreak >= 3.0s`, `CharacterController` ativo durante a cutscene ou estado sujo após Skip falha o teste imediatamente.
