# LOCOMOTION_ANIM.md — Diagnóstico Científico e Correção da Locomoção do Dez

Documentação de causa raiz, correções aplicadas e checklist padrão para futuros personagens (em conformidade com `docs/DESIGN_BIBLE.md` v1.0, Seções 2 e 10).

---

## 1. Escada de Diagnóstico (Veredito por Degrau)

| Degrau | O que foi medido | Veredito |
|---|---|---|
| **1 — Conteúdo dos Clips (`Clean_Walk.anim` / `Clean_Run.anim`)** | Inspecionadas curvas Humanoid reais:<br>• `Clean_Walk`: `Arm Down-Up = -0.591..-0.448` (colado nas costelas), `Arm Front-Back = -0.073..+0.191` (range `0.264`), `Spine Front-Back = 0.000` (congelado).<br>• `Clean_Run`: `Arm Front-Back = +0.009..+0.249` (braço nunca vai pra trás!), `Spine Front-Back = 0.000` (estátua correndo). | **CULPADO #1 (Causa Raiz Principal).** Tentativas anteriores de mexer só no Animator falharam porque as curvas dentro dos clips estavam com `Spine = 0` e braços colados/sem balanço! |
| **2 — Avatar (`hero_dez_humanoid.fbx`)** | `Avatar.isValid = True`, `isHuman = True`. Porém o Dez tem proporção estilizada (tronco largo + braços curtos), então `Arm Down-Up = -0.59` (de humano alto Quaternius) enterra os braços no torso. | **Culpado secundário:** exige abertura lateral (`armAbduction`) de `10°..18°` afastando os braços do torso. |
| **3 — Layers / Masks (`HeroDezController.controller`)** | Apenas 1 layer (`Base Layer`), sem AvatarMask de upper-body. | **Inocente:** não havia máscara bloqueando os braços. |
| **4 — Velocidade & Foot Sliding (`HeroDezController` vs `DezMovementConfig`)** | BlendTree `Locomotion_1D` tinha thresholds `Idle=0 / Walk=1.6 / Run=3.6`, enquanto `DezMovementConfig.asset` tinha `walkSpeed=2.2 / runSpeed=5.4` + `m_Speed = 1` fixo. Resultado: caminhar a `2.2` misturava 30% Run + 70% Walk, e correr a `5.4` patinava **50% mais rápido** que a passada! | **CULPADO #2 (Causa Raiz da Corrida Estranha / Foot Slide).** |

---

## 2. Correções Aplicadas

1. **Degrau 4 (`LocomotionAnimatorSetupBuilder.cs` + `DezLocomotionController.cs`):**
   - Thresholds do BlendTree `Locomotion_1D` casados com `DezMovementConfig.asset`: **`Idle = 0.0 / Walk = 2.2 / Run = 5.4`** (zero mistura pastosa walk+run!).
   - Adicionado parâmetro `LocomotionSpeedMult` vinculado ao `speedParameter` do estado `Locomotion` (`DezLocomotionController` ajusta proporcionalmente à velocidade real → **zero foot slide**).
2. **Degraus 1 & 2 (`DezProceduralAnimator.cs` — camada aditiva acoplada ao ciclo real do Animator):**
   - **Abertura lateral (braços afastados do torso):** `armAbductionIdleDeg = 10°`, `armAbductionWalkDeg = 18°`, `armAbductionRunDeg = 14°`.
   - **Balanço acoplado à fase do ciclo (`normalizedTime * 2π`):** `armSwingWalkDeg = 28°` (antebraço `16°`), `armSwingRunDeg = 52°` (antebraço `54°`).
   - **Corrida com vida:** inclinação frontal do tronco `runForwardLeanDeg = 8.5°` (dentro de `5–10°`), contra-rotação de ombros `shoulderTwistRunDeg = 9.0°`, bob vertical sutil `runVerticalBobMeters = 0.028m`.

---

## 3. Regra Canônica da Ferramenta na Mão
- **Modo `CONCERTAR` (ferramenta guardada):** ambos os braços balançam `100%` livres em oposição às pernas.
- **Modo `COMBATE` / ferramenta empunhada na mão DIREITA:** braço **ESQUERDO** balança `100%` livre; braço **DIREITO** reduz balanço para `rightArmToolSwingFactor = 0.35` mantendo a ferramenta estável. Stance de duas mãos ocorre apenas durante golpes/apontar.

---

## 4. Fonte & Licença dos Clips de Locomoção
- Nenhum clip de terceiros novo foi baixado; os clips base (`Clean_Idle.anim`, `Clean_Walk.anim`, `Clean_Run.anim` derivados do Quaternius Universal Animation Library — **CC0 1.0 Public Domain**, já no repositório) foram preservados e corrigidos via camada aditiva acoplada ao ciclo + re-casamento de thresholds e speed ratio.

---

## 5. Checklist — Animação de Personagem Novo
1. Auditar curvas `Arm Down-Up`, `Arm Front-Back` e `Spine Front-Back` do clip isolado ANTES de ajustar Animator.
2. Casar 100% os thresholds do BlendTree com `walkSpeed` / `runSpeed` reais do config.
3. Ligar `LocomotionSpeedMult` ao `speedParameter` do estado de locomoção e validar em slow-mo `0.25x` lateral (zero foot slide).
4. Garantir abertura lateral (`armAbduction >= 10°`) em personagens estilizados com tronco largo para que os braços nunca encostem nas costelas.
5. Validar regressões de combate, reparo, interação e cutscenes após qualquer ajuste de rig/animator.
