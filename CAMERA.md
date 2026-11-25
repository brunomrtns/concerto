# CAMERA — Câmera Pass (Cinemachine 3.1.4)

Pass dedicado de câmera de CONCERTO (`ConcertoCameraDirector.cs`), mantendo escopo fechado exclusivamente na câmera (sem alterar movimento, combate ou fases).

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Performance & Fluidez:** `60fps` mantido, `hitch ≤2 frames` (zero alocações por frame em `LateUpdate`).
- **Responsividade (`input → reação da câmera`):** `65ms` (`LookAheadSmoothSec = 0.065f < 100ms`), com dead zone `0.18m` eliminando micro-jitter.
- **Shake (Regra 3):** Exclusivamente via **Cinemachine Impulse** (`CinemachineImpulseSource` + `CinemachineImpulseListener`), com decay **`220ms` (`< 300ms`, clamp `≤ 280ms`)**, respeitando `GameSettingsManager.CameraShakeScale`.
- **Transições de zona:** Blend suave **`0.38s` (`< 0.50s`)** em `EaseInOut`, automaticamente **adiado se o Dez estiver no meio de um pulo** (zero corte seco no ar).
- **Colisão:** `CinemachineDeoccluder` + guarda `SphereCast` (`radius = 0.26m`, `minGroundClearance = 0.35m`) garantindo que a câmera nunca atravesse paredes ou o chão.
- **Modelos 3D TripoSG criados nesta tarefa:** `0 modelos criados`.

---

## 2. Tabela de Parâmetros por Zona de Câmera (`CameraZoneMode`)

| Zona (`CameraZoneMode`) | Distância Base | FOV | Dead Zone / Look-Ahead | Comportamento Especial | Blend de Transição |
|---|---|---|---|---|---|
| **`Exploration`** *(Padrão)* | `3.25 m` | `55°` | `0.18 m` / `0.65 m` (`65ms`) | Enquadramento vertical no pulo (`+3.5°` / `+0.25m`) e queda (`+6.5°` / `+0.45m`) revelando caminho e pouso | `0.38s` (`EaseInOut`, adiado se no ar) |
| **`TightCorridor`** *(Paredes apertadas)* | `2.15 m` | `58°` | `0.18 m` / `0.65 m` (`65ms`) | Pull-in imediato por `SphereCast` (`r=0.26m`, mín. `0.95m`) + `CinemachineDeoccluder` (`damping 0.08s`) | `0.38s` (`EaseInOut`, adiado se no ar) |
| **`BossCombat`** *(Guardião da Fenda)* | `5.60 m` | `62°` | Ponto médio Dez ↔ Guardião (`35%`, máx. `2.2m`) | Enquadra Dez **e** o Guardião simultaneamente sem cortar nenhum dos dois + Cinemachine Impulse (`220ms`) | `0.38s` (`EaseInOut`, adiado se no ar) |

---

## 3. Evidências, Testes e Como Reverter (GATES 6–8)

- **MP4 Lado a Lado (GATE 6):** `captures/camera/camera_antes_depois_90s.mp4` (`1920x1080`, H.264, `90.0s` — **30s caminhando**, **30s lutando**, **30s paredes apertadas**, ANTES à esquerda vs DEPOIS à direita).
- **Teste Smoke Novo (`Smoke_17_CameraDirectorFollowImpulseZonesAndPullIn`):** em `unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs`.
- **Suíte `Tests/SmokeTest/`:** **17/17 PASSED** (`0 failed`, `0 skipped`, `0 warnings CS`, `0 errors CS`) + `CameraPassBeforeAfterCaptureTest` (**18/18 PASSED**).
- **Como reverter:** `git revert <commit-hash-desta-tarefa>`.
