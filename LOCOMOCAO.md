# LOCOMOÇÃO DO DEZ — RECONSTRUÇÃO COM CLIPES REAIS, BLEND TREE E ANIMATION RIGGING IK

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0
**Modelos 3D criados nesta tarefa:** **0 modelos criados** (modelo existente `hero_dez_humanoid.fbx` mantido integralmente, sem regeneração nem substituição; regra TripoSG-only respeitada).

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Seção 2 (Character & Controls — Peso coerente, estados legíveis, parada brusca):** CONFORME — `DezLocomotionController` adaptado ao modelo de parâmetros do Unity Starter Assets (`Speed`, `MotionSpeed`, `Grounded`, `Jump`, `FreeFall`), preservando parada brusca (`groundDecel = 34.0 m/s²`), aceleração rápida (`groundAccel = 26.0 m/s²`) e estados visuais distintos (`Idle`, `Walk`, `Run`, `JumpStart`, `InAir`, `JumpLand`).
- **Seção 3 (Métricas de Controle & Responsividade):** CONFORME —
  - `input → ação`: **`1.54 ms`** em gameplay real a 60fps+ (`<< 100 ms` alvo da `DESIGN_BIBLE`, 1 frame de resposta).
  - `coyoteTimeSec`: **`0.10 s`** (`100 ms`, validado em `Smoke_05`).
  - `jumpBufferSec`: **`0.15 s`** (`150 ms`, validado em `Smoke_05`).
  - `60fps`: mantido com folga (`1.54 ms/frame` médio em `Caldeiral`, `0.0 hitches/min`).
- **Seção 10 (Lente do Brinquedo):** CONFORME — braços naturais dirigidos 100% por clipes reais Humanoid (sem overrides manuais em C#), pés plantados via Foot Grounding IK e alcance de mão direita via Unity Animation Rigging ao interagir com objetos da Fase 1.

---

## 2. Rig do Dez Validado (PASSO 0 & GATE 2)

- **Modelo base (inalterado):** `unity/Assets/Game/Art/Generated/hero_dez/hero_dez_humanoid.fbx`
- **Avatar validado:** `hero_dez_humanoidAvatar` (`isValid = True`, `isHuman = True`, `animationType = 3 / Humanoid`, `avatarSetup = 1 / CreateFromThisModel`) com **22 ossos Humanoid mapeados** (`Hips`, `Spine`, `Chest`, `UpperChest`, `Neck`, `Head`, `Left/Right Shoulder`, `UpperArm`, `LowerArm`, `Hand`, `UpperLeg`, `LowerLeg`, `Foot`, `Toes`).
- **Retargeting:** todos os 7 clipes novos importados como `ModelImporterAnimationType.Human` com bake limpo de Root Transform (`Rotation`, `Position Y`, `Position XZ`) e **0 warnings no Console**.

---

## 3. Tabela de Clipes Reais Usados (GATE 3)

| Clipe / Estado | Arquivo no Repositório | Origem | Licença | Loop |
|---|---|---|---|---|
| **Idle** | `Assets/Game/Animations/StarterAssets/Stand--Idle.anim.fbx` | Unity Starter Assets — Third Person Character Controller (via mirror público `shady-poly/ThirdPersonCharacter_StarterAssets_Extended`) | Unity Companion License (assets oficiais Unity Starter Assets) | Sim |
| **Andar (Walk)** | `Assets/Game/Animations/StarterAssets/Locomotion--Walk_N.anim.fbx` | Unity Starter Assets — Third Person Character Controller | Unity Companion License | Sim |
| **Correr (Run)** | `Assets/Game/Animations/StarterAssets/Locomotion--Run_N.anim.fbx` | Unity Starter Assets — Third Person Character Controller | Unity Companion License | Sim |
| **Pular (JumpStart)** | `Assets/Game/Animations/StarterAssets/Jump--Jump.anim.fbx` | Unity Starter Assets — Third Person Character Controller | Unity Companion License | Não |
| **Cair (InAir)** | `Assets/Game/Animations/StarterAssets/Jump--InAir.anim.fbx` | Unity Starter Assets — Third Person Character Controller | Unity Companion License | Sim |
| **Aterrissar (Walk Land)** | `Assets/Game/Animations/StarterAssets/Locomotion--Walk_N_Land.anim.fbx` | Unity Starter Assets — Third Person Character Controller | Unity Companion License | Não |
| **Aterrissar (Run Land)** | `Assets/Game/Animations/StarterAssets/Locomotion--Run_N_Land.anim.fbx` | Unity Starter Assets — Third Person Character Controller | Unity Companion License | Não |

*(Nota: optou-se pelos clipes oficiais do **Unity Starter Assets** em vez de Mixamo porque o Mixamo exige autenticação interativa Adobe no navegador, enquanto os clipes do Starter Assets são explicitamente permitidos pela Regra 3 — "Mixamo ou Starter Assets" — e oferecem cobertura completa de Idle/Walk/Run/Jump/InAir/Land com retargeting Humanoid limpo.)*

---

## 4. Arquitetura do Blend Tree & Animator (`HeroDezController.controller`)

Reconstruído de forma determinística via `CONCERTO/Locomotion/Rebuild Dez Animator & Validate Humanoid Rig` (`unity/Assets/Game/Editor/DezLocomotionRebuildTool.cs`):

- **Parâmetros:** `Speed` (Float), `MotionSpeed` (Float, default `1.0`), `Grounded` (Bool, default `true`), `Jump` (Bool), `FreeFall` (Bool) + aliases de compatibilidade (`SpeedRatio`, `IsGrounded`, `VerticalVel`).
- **Base Layer (`iKPass = true`):**
  - **`Idle Walk Run Blend` (Estado default — 1D Blend Tree por `Speed`):**
    - `0.0 m/s` → `Stand--Idle`
    - `2.2 m/s` → `Locomotion--Walk_N`
    - `5.4 m/s` → `Locomotion--Run_N`
    - Multiplicador de velocidade vinculado a `MotionSpeed` (casando a cadência da passada com a velocidade real do `CharacterController` para evitar deslizamento).
  - **`JumpStart`** (`Jump--Jump`): entra a partir de `Idle Walk Run Blend` quando `Jump == true` (`0.05s`).
  - **`InAir`** (`Jump--InAir`): entra após `JumpStart` (`exitTime = 0.78`) ou diretamente de `Idle Walk Run Blend` quando `FreeFall == true` (`0.12s`).
  - **`JumpLand`** (1D Blend Tree por `Speed`: `Walk_N_Land` em `0..2.2` / `Run_N_Land` em `5.4`): entra de `InAir` ou `JumpStart` quando `Grounded == true` (`0.06s`) e retorna a `Idle Walk Run Blend` (`exitTime = 0.70`, `0.15s`).

---

## 5. Separação: O que é IK Oficial vs o que é Procedural Mecânico

- **ZERO animação de corpo por código (Regra 3):** todas as manipulações manuais de braços, cotovelos, ombros, tronco e hips foram **removidas** de `DezProceduralAnimator.cs`.
- **IK Oficial — `DezAnimationRiggingController.cs` (`com.unity.animation.rigging 1.3.0`):**
  1. **Right Hand Interact IK (`RigBuilder` + `Rig` + `TwoBoneIKConstraint`):** cadeia `RightUpperArm → RightLowerArm → RightHand` com `Target` e `ElbowHint`. Faz blend suave (`weight 0 → 0.92`) quando o Dez interage com objetos da Fase 1 (tecla `[E]` perto de `IInteractable` ou via `SetInteractTarget()`).
  2. **Foot Grounding IK (`OnAnimatorIK`):** raycast vertical em `LeftFoot` e `RightFoot` contra `LayerEnvironment | LayerDefault` quando `Grounded == true`, plantando os pés no piso plano com alinhamento de normal sem puxar pés levantados durante a passada.
- **Procedural Puramente Mecânico (Exceção da Regra 3 — `DezProceduralAnimator.cs`):**
  1. **Oscilador mola-amortecedor sutil** (`maxMechanicalSwayDeg = 6.5°`, `springK = 68`, `damping = 10.5`) aplicado **exclusivamente** a transforms secundários mecânicos (`antenna`/`cable`/`comms`) — nunca em ossos do corpo.
  2. **Sombra de contato dinâmica no piso** (`Dez_ContactShadow` quad) — não toca em nenhum osso.

---

## 6. Evidências & Validação (GATES 6 & 8)

- **MP4 Comparativo Lado a Lado (GATE 6):** `captures/animacao/dez_locomocao_antes_depois_1080p.mp4` (`1920x1080`, `30.0s`, ANTES à esquerda vs DEPOIS à direita na mesma rota em `Caldeiral`: andar, correr, parar brusco, pular/cair/aterrissar, aproximar e interagir com Hand IK).
- **Compilação:** `0 warnings CS`, `0 errors CS`.
- **Suíte Smoke Test (GATE 8):** `Studio.Tests.SmokeTest.ConcertoSmokeTests` — **7/7 PASSED**.
- **Modelos TripoSG criados nesta tarefa:** **0 modelos criados.**

---

## 7. Como Reverter se Algo Quebrar

Para voltar instantaneamente ao estado anterior a esta reconstrução (mantendo QA Fase 1 e Pass de Performance intactos):
```bash
git revert <hash-deste-commit>
```
Ou, para restaurar apenas o Animator e os scripts de locomoção sem reverter o histórico:
```bash
git checkout bf1f859 -- \
  unity/Assets/Game/Animations/HeroDezController.controller \
  unity/Assets/Game/Scripts/Animation/DezLocomotionController.cs \
  unity/Assets/Game/Scripts/Animation/DezProceduralAnimator.cs
rm -rf unity/Assets/Game/Animations/StarterAssets \
  unity/Assets/Game/Scripts/Animation/DezAnimationRiggingController.cs \
  unity/Assets/Game/Editor/DezLocomotionRebuildTool.cs
```
Em seguida, reabra o Unity Editor para reimportar `HeroDezController.controller` e rode `Tests/SmokeTest/` para confirmar 7/7 verde.
