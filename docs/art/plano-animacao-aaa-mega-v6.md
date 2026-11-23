# Plano de Execução — `RUN mega_v6`: Módulo de Animação AAA (100% Gratuito)

> **Contrato:** [ADR-012 — Lei 9 (`3dprinter-setup-ia`)](../../../3dprinter-setup-ia/docs/decisions/ADR-012-lei-9-animacao-aaa.md)
> **Piloto:** `Dez A` (`hero_dez`, `8.000` tris, visual 3D aprovado em `32e41bd`) — depois reutilizável 1-para-1 para **todos os personagens** do CONCERTO e futuros jogos do Studio Core.
> **Política de Custo:** **100% GRATUITO e LOCAL** (Cascadeur Indie e MagicaCloth 2 removidos por ordem explícita do dono em 2026-11-23; Mixamo proibido).
> **Escopo congelado neste run:** NÃO toca em Props Batch-2, Sirene FLUX nem Porca 3D.

---

## 0. Estado Real Confirmado no Disco (Pronto para Amanhã)

| Recurso | Estado Atual |
| :--- | :--- |
| `UniRig` (`MIT`) + `~/.venvs/unirig` | ✅ Pronto (`hero_dez_rigged.fbx` 22 ossos + `unirig_to_mecanim.py` já rodando) |
| `MoMask` (`MIT`) + `~/.venvs/momask` + 4 checkpoints `t2m/*` | ✅ **Já instalados no WSL — ZERO download de pesos do MoMask!** |
| `Quaternius UAL` (`UAL1 + UAL2`, `CC0`) | ✅ Já no repo (`86` clips Humanoid) |
| `Blender 5.0` headless (WSL) | ✅ Pronto |
| `Unity 6.3` (`manifest.json`) | ⏳ Falta adicionar `com.unity.animation.rigging` e `com.unity.recorder` (ambos oficiais e gratuitos) |
| `Kenney Character Animations` (`CC0`) + seleção `CMU MoCap` | ⏳ Download único com registro Lei 8 na TAREFA 1 |

---

## 1. TAREFA 1 — Converter `MoMask → FBX Humanoid` + Bibliotecas CC0 (`3dprinter-setup-ia`, ~90 min)

1. **Criar `tools/animation/momask_to_fbx.py`** (headless, usa `~/.venvs/momask` + Blender `bpy`):
   - Recebe `--prompt`, `--seed 42`, `--out <clip.fbx>`;
   - Roda inferência MoMask (guard 10 GB, logger VRAM) → gera joints 3D (22 joints SMPL/HumanML3D) → converte para Armature Humanoid-compatible em Blender (eixo Y-up Unity, T-pose no frame 0, escala 1:1, rotação de ossos via quaternion solve + foot-lock na fase de contato) → exporta `.fbx`.
   - **Smoke test:** 1 clip `"a person walks forward naturally"` (`seed 42`) → `.fbx` → importa no Unity como `AnimationType.Human` válido!
   - *(Stop Condition: se após 2 tentativas o retarget MoMask→FBX não produzir Humanoid válido, marcar `[!]` e usar CMU MoCap + Kenney/UAL CC0 como fonte do piloto.)*
2. **Baixar `Kenney Character Animations` (CC0)** + seleção enxuta de locomoção do **CMU MoCap** (Walk/Run/Turn/Jump, com crédito oficial `NSF EIA-0196217` em `LICENSES-ARTE.md`) e registrar tudo no inventário Lei 8.
3. **CHECKPOINT:** commit + push na `main` + `PROGRESS.md`.

---

## 2. TAREFA 2 — Rig Chibi Calibrado + Retarget + Camadas Procedurais (`game-concerto`, ~90 min)

1. **Adicionar pacotes oficiais gratuitos no `unity/Packages/manifest.json`:**
   - `com.unity.animation.rigging` (Foot IK + Look-At IK)
   - `com.unity.recorder` (captura obrigatória a 60 fps)
2. **T-Pose & Rig Chibi no Blender (`tools/art/ai_generator/unirig_to_mecanim.py` v2):**
   - Calibrar ombros/braços para T-Pose real (evita colapso de axila e braço rígido no retarget Humanoid);
   - Adicionar **ossos secundários** (fora do avatar Humanoid) para **Orelha Esquerda (`Ear_L`)**, **Orelha Direita (`Ear_R`)** e **Antenas (`Antenna_Top`)** com pesos suaves na cabeça da Dez A!
3. **Retarget com Compensação Chibi (fim do "empalhado"):**
   - Ajustar escala de passada/quadril para altura `1,25 m` (cabeça ~40% da altura) preservando o arco de movimento e travando o pé no chão (`foot-lock`).
   - Gerar **evidência de overlay** esqueleto-fonte vs Dez por clip.
4. **Camadas Procedurais no Unity (100% C# + Animation Rigging gratuito):**
   - `DezFootIK.cs`: raycast por pé + `TwoBoneIKConstraint` nos joelhos/tornozelos (adapta a pisos e rampas sem flutuar ou atravessar);
   - `DezLandingDip.cs`: afundamento elástico do quadril proporcional à velocidade vertical de aterrissagem;
   - `DezLookAt.cs`: `MultiAimConstraint` na cabeça acompanhando direção da curva / Porca / interativos;
   - `SpringBone.cs` (nosso, gratuito): física de mola/amortecimento nas orelhas L/R e antenas (balanço natural em idle, corrida, giro e pouso).
5. **Gerar conjunto base de clips no MoMask** (seeds `42/43/44` + apoio CMU/Kenney/UAL): `Idle_Breath`, `Walk_Start/Loop/Stop`, `Run_Start/Loop/Stop`, `TurnInPlace_L/R`, `Jump_Takeoff/Air/Land`.
6. **CHECKPOINT:** commit + push na `main` + `PROGRESS.md`.

---

## 3. TAREFA 3 — Locomoção Viva + QA Automático Lei 9.1 (`game-concerto`, ~120 min)

1. **`DezLocomotionController.cs`:**
   - `CharacterController` + gravidade real (`g = -18 m/s²`), altura de pulo proporcional (`~1,15 m`), `slopeLimit = 45°`, aceleração/desaceleração por curvas físicas (zero teleporte de velocidade) + inclinação lateral nas curvas (**banking**).
2. **Animator Controller v2 (`HeroDezController.controller`):**
   - **Blend Tree 2D Freeform** (`Speed × TurnRate`), transições suaves com `Start / Loop / Stop`, **Turn-in-Place em 3 tempos** (cabeça → ombros → pés), ciclo completo de pulo (`Takeoff → Air → Land / Land_High`).
   - **Idle vivo:** respiração contínua (~4 s) + micro-shift de peso + `SpringBone` ativo em repouso.
3. **QA Automático (`tools/validation/qa_animation_playmode.py` + teste PlayMode):**
   - Mede **foot-slide real** (distância horizontal do pé durante contato com o chão `<= 2 cm/frame`);
   - Valida **`ffprobe` `fps == 60`** no MP4 gravado;
   - Valida `validate_scene.py --strict` (`exit 0`) + Baseline PlayMode M1–M2 (`2/2 PASSED`).
4. **CHECKPOINT:** commit + push na `main` + `PROGRESS.md`.

---

## 4. TAREFA 4 — Showcase 60 fps + QA Dupla + Gate Único do Dono (~45 min)

1. **Gravar via `Unity Recorder` a `60 fps` exatos (`1280×720`)** com 3 ângulos (perfil, frente, 3/4):
   `Idle 5 s → Walk Start/Loop/Stop → Run Start/Loop/Stop → Turn-in-Place 90°/180° → Pulo com gravidade + Landing Dip → Idle respirando`.
2. **Preencher os 8 itens do Checklist ADR-012 com evidência** (JSON + frames + `ffprobe`).
3. **Abrir o MP4 local direto no player do Windows** para o Bruno + publicar link `media.githubusercontent.com` para outras IAs.
4. **PARADA HUMANA ÚNICA:** Bruno julga contra o checklist. Aprovado → vira o **Padrão-de-Ouro da Lei 9.3** para todos os personagens do CONCERTO e do Studio Core!
