# MOVEMENT FEEL — Base de Movimento 3ª Pessoa & Câmera do Dez

## 1. Auditoria & Decisões de Design
- **Perspectiva & Rig:** 3ª pessoa (`CharacterController` + `DezLocomotionController` + `DezProceduralAnimator` + `HeroDezController` com `Clean_Idle`, `Clean_Walk`, `Clean_Run` — **nenhum modelo 3D novo criado**, o Dez atual é definitivo).
- **Decisão sobre Pulo:** **Mantido e aprimorado** — a oficina (`Caldeiral.unity`) possui plataformas, canos, bancadas, escadarias e o sistema de **Ledge Grab / Mantle** já implementado. Adicionamos os padrões consagrados de plataforma/ação 3D: **Coyote Time**, **Jump Input Buffer**, **Corte de Altura (Variable Jump Height)** e **Fall Gravity Multiplier**.
- **Decisão sobre Impulso Mecânico (Dash):** Rajada curta dos propulsores das costas do Dez (`13.5 m/s` por `0.18s`, cooldown `2.0s`, partículas ciano + SFX TEMP). **Explicitamente SEM i-frames (`HasIFrames => false`)** — invulnerabilidade pertence exclusivamente ao poder **"Eco"** (task futura).
- **Câmera de Gameplay (Cinemachine 3.1.4):** `VCam_Gameplay_Dez` (`Priority = 10`, abaixo de Cutscene 1 `20` e Cutscene 2 `25`) usando `CinemachineThirdPersonFollow` com leve *shoulder offset* à direita (`(0.38, 0.12, 0)`) + `AvoidObstacles` (colisão real com paredes da oficina, ignorando a tag `Player`). Sem mira ADS sobre o ombro ainda (reservado para a task de combate).
- **Hook de Sensibilidade no Menu de Opções:** `GameSettingsManager.CameraSensitivity` (`CONCERTO_OPT_CAM_SENS`, range `0.3–2.5`, default `1.0`) multiplica `DezMovementConfig.baseMouseSensitivity` automaticamente.

## 2. Parâmetros Tunáveis (`Assets/Game/Config/DezMovementConfig.asset`)
**ZERO número mágico hardcoded** no controller — todos os valores abaixo vivem no `ScriptableObject` `DezMovementConfig`:

| Seção | Parâmetro | Default | O que faz no Game Feel |
|---|---|---:|---|
| **Locomoção** | `walkSpeed` | `2.2 m/s` | Velocidade de caminhada exploratória na oficina |
| | `runSpeed` | `5.4 m/s` | Velocidade de corrida com `Shift` (ritmo base de combate) |
| | `groundAccel` | `26.0 m/s²` | Resposta rápida ao iniciar movimento (~0.2s até corrida plena) |
| | `groundDecel` | `34.0 m/s²` | Parada brusca e precisa sem sensação de "patinar no gelo" |
| | `airAccel` / `airDecel` | `12.0` / `4.0` | Controle aéreo suficiente para corrigir aterrissagem sem perder peso |
| | `turnSpeedDeg` | `780 °/s` | Rotação ágil do corpo do Dez em mudanças bruscas de direção |
| **Pulo** | `gravity` | `-24.0 m/s²` | Gravidade firme (elimina sensação flutuante) |
| | `fallGravityMultiplier` | `1.35x` | Descida mais rápida que a subida (padrão Mario 3D / Celeste) |
| | `jumpHeight` | `1.35 m` | Altura máxima segurando `Espaço` |
| | `jumpCutMultiplier` | `0.45` | Corta a velocidade vertical ao soltar `Espaço` cedo (pulo curto) |
| | `coyoteTimeSec` | `0.14 s` | Tolerância para pular logo após sair de uma borda |
| | `jumpBufferSec` | `0.14 s` | Registra `Espaço` pressionado logo antes de tocar o chão |
| **Impulso (Dash)** | `dashSpeed` | `13.5 m/s` | Velocidade da rajada dos propulsores |
| | `dashDurationSec` | `0.18 s` | Duração curta e explosiva da rajada (~2.4m de alcance) |
| | `dashCooldownSec` | `2.0 s` | Cooldown curto para ritmo de esquiva mecânica (sem i-frames) |
| | `dashPrimaryKey` / `dashAltKey` | `LeftCtrl` / `F` | Atalhos do impulso (sem conflitar com `Shift`, `Espaço`, `E`, `Esc`) |
| **Ledge Mantle** | `enableLedgeMantle` | `true` | Escalada automática com as mãos ao pular em bordas/canos |
| | `ledgeDetectDist` / `mantleDuration` | `0.62 m` / `0.38 s` | Alcance e tempo da subida em 2 fases |
| **Câmera 3ª Pessoa** | `baseMouseSensitivity` | `2.6` | Sensibilidade base (multiplicada por `GameSettingsManager.CameraSensitivity`) |
| | `shoulderOffset` | `(0.38, 0.12, 0)` | Leve deslocamento sobre o ombro direito |
| | `cameraDistance` | `3.2 m` | Distância padrão (zoom com scroll entre `2.0m` e `5.2m`) |
| | `followDamping` | `(0.12, 0.16, 0.14)` | Suavização do follow da `CinemachineThirdPersonFollow` |
| | `wallCollisionRadius` | `0.24 m` | Raio do `AvoidObstacles` contra paredes/colunas |
| | `autoAlignSpeedDeg` | `95 °/s` | Alinhamento suave atrás das costas quando o mouse está parado |

## 3. Como Ajustar ou Estender
- Selecione `Assets/Game/Config/DezMovementConfig.asset` no Project do Unity e ajuste qualquer valor ao vivo em PlayMode.
- Para reconstruir a câmera `VCam_Gameplay_Dez` e os propulsores no Dez: `Studio > Concerto > Player > Setup Dez Movement Feel & Cinemachine 3P Camera`.
- **Nota de Animação:** o Dez usa atualmente `Clean_Idle`/`Clean_Walk`/`Clean_Run` + inclinação/landing dip procedural (`DezProceduralAnimator`). Animações dedicadas de Dash e Pulo ficam como decisão futura de arte com o Bruno.
