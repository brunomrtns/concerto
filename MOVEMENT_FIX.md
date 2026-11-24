# MOVEMENT_FIX.md — Caça aos Travamentos & Polimento de Game Feel do Dez

## Causas Raiz Encontradas e Corrigidas (Classe Inteira Eliminada)

| # | Classe de Problema | Onde Ocorria | Causa Raiz Técnica | Correção Aplicada |
|---|---|---|---|---|
| **1** | **Ghost Collision em emendas de piso / quinas baixas** | Emendas entre `BoxCollider` de `Caldeiral_FloorTile` (`x=-28..-16, z=-19..-13`) e bordas de props em `SetorExterno` | `CharacterController.skinWidth = 0.01f` (muito menor que os ~15% recomendados para `radius = 0.28f`) + `stepOffset = 0.28f` + `minMoveDistance = 0.001f`. | `skinWidth = 0.05f`, `stepOffset = 0.36f`, `slopeLimit = 50f`, `minMoveDistance = 0f` (tunáveis em `DezMovementConfig`) + `PhysMat_ZeroFriction.physicMaterial` (`FrictionCombine = Minimum`). |
| **2** | **Enganchamento diagonal em paredes e quinas de props** | Quinas da bancada (`-26.2, -18.0`), máquina coberta (`-25.8, -19.2`), pilares do corredor leste (`-18.5, -13.5`) | `_cc.Move()` cru sem projeção tangencial em `OnControllerColliderHit`: `_horizVelocity` continuava empurrando para dentro da parede. | Implementado `OnControllerColliderHit`: projeta `_horizVelocity` no plano tangente da parede (`Vector3.ProjectOnPlane`) + micro-desvio lateral (`cornerNudgeStrength = 0.45f`) em quinas estreitas. |
| **3** | **Dash enganchando contra parede** | Qualquer parede/pilar em `Caldeiral` e perímetro de `SetorExterno` | Durante `_isDashing`, `_horizVelocity = _dashDir * dashSpeed` sobrescrevia o movimento todo frame empurrando o Dez contra a parede por `0.18s`. | Dash frontal (`dot < -0.78f`) encerra limpo sem empurrar; dash oblíquo redireciona `_dashDir` ao longo da tangente da parede (`0.85x`). |
| **4** | **Conflito de câmera pós-CS03/CS04 & clipping em paredes** | Transição de câmera perto de paredes da oficina | `IsAnyCutscenePlaying()` não verificava CS03/CS04; câmera encostando em parede clipava na cabeça do Dez. | `IsAnyCutscenePlaying()` agora cobre CS01–CS04; adicionado `UpdateCameraProximityPlayerFade` (`playerHideDist = 0.68m`) + `CinemachineDeoccluder`. |
| **5** | **Feel incompleto (passos / pouso)** | Oficina e pátio | Sem variação de áudio por superfície nem peso de câmera no pouso. | Adicionados passos por superfície (`floor` / `metal` / `puddle`) via Raycast + `landingThudClip` + `_landingBobOffset` sutil (`landingBobAmplitude = 0.11m`) no pivot da câmera. |

---

## Valores Finais de Feel (`DezMovementConfig.cs` — Zero Hardcode)

| Parâmetro | Valor Final | Função |
|---|---|---|
| `walkSpeed` / `runSpeed` | `2.2` / `5.4 m/s` | Caminhada e corrida |
| `groundAccel` / `groundDecel` | `26.0` / `34.0 m/s²` | Resposta rápida e parada firme |
| `turnSpeedDeg` | `780 °/s` | Giro responsivo sem teleporte |
| `capsuleSkinWidth` | `0.05 m` | Elimina ghost collision em emendas |
| `stepOffset` / `slopeLimit` | `0.36 m` / `50°` | Sobe degraus e rampas sem travar |
| `wallSlideEnabled` / `cornerNudgeStrength` | `true` / `0.45` | Deslize suave em paredes e quinas |
| `dashSpeed` / `dashDurationSec` / `dashCooldownSec` | `13.5 m/s` / `0.18 s` / `2.0 s` | Impulso mecânico (sem i-frames) |
| `dashWallStopDot` | `-0.78` | Encerra dash frontal / desliza oblíquo |
| `walkStepIntervalSec` / `runStepIntervalSec` | `0.44 s` / `0.28 s` | Cadência de passos por superfície |
| `landingBobAmplitude` / `landingBobRecoverSpeed` | `0.11 m` / `8.5` | Peso sutil no pouso |
| `playerHideDist` | `0.68 m` | Oculta player quando câmera encosta em parede |

---

## Checklist — Testar Movimentação em Cena Nova
1. Conferir `CharacterController` do Dez: `skinWidth = 0.05`, `stepOffset = 0.36`, `slopeLimit = 50`, `minMoveDistance = 0`, `material = PhysMat_ZeroFriction`.
2. Correr diagonalmente contra todas as paredes e quinas de props: o Dez deve deslizar suavemente sem parar ou vibrar.
3. Dar Dash frontal e oblíquo contra paredes/pilares: nunca deve enganchar ou atravessar.
4. Cruzar todas as emendas de piso e degraus baixos: zero micro-parada ("ghost collision").
5. Encostar a câmera em uma parede: `CinemachineDeoccluder` aproxima sem atravessar e o player oculta abaixo de `0.68m`.
6. Rodar `MovementAntiSnagAndFeelValidationTest` em PlayMode e confirmar **PASS (0 erros)**.
