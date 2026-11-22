# 07 — Pipeline de Assets

> Fecha o ciclo texto → 2D → 3D → rig → Unity com o arsenal local (RTX 3060 12GB, WSL2) `[DEFINIDO: compatível com objetos 3D gerados por IA]` · tags: ver README

## 1. Visão do fluxo (Fábrica de Games, alinhada ao seu mapa de pilares) `[NOVO]`

```
[dialogues/art-bible/cutscene YAMLs no Git]
        │
        ├── ComfyUI (FLUX.1-schnell, D:\)  ── conceito 2D/texturas/UI (pilar 3)
        │         │
        │         └── imagem de referência padrão
        │                   │
        │         TRELLIS (imagem→3D) ── malha bruta (pilar 2, Modo 1/Modo 2)
        │                   │
        │         Blender headless (forja): repair → decimate → validação
        │                   │
        │         fab3d_bridge.py ──> Fab3dImporter.cs ──> Unity (prefab validado)
        │                   │
        │         auto_rig.py (bipeds) + biblioteca de poses JSON ──> retarget animações
        │
        ├── tts_bridge.py (dialogues.yaml → .wav/.ogg + legendas)  (pilar 5)
        ├── CutsceneBuilder.cs (cutscene_*.yaml → Timeline+Cinemachine)  (pilar 7)
        └── validate_asset / validate_scene + evidence/captures (gate de QA)
```

Regra de ouro do estúdio (que este jogo respeita): **tudo que a IA produz entra no Unity só após validador**; nenhum asset via importação manual `[NOVO, deriva dos seus validadores V2]`.

## 2. Fluxo por tipo de asset `[NOVO]`

### 2.1 Peças de kit e props (Modo 1 — o caminho já provado)
1. Prompt no art-bible (`prompt_base` + modificador do fuso) → ComfyUI gera 4 candidatas → Art Director (agente) escolhe 1 e registra no provenance log.
2. Imagem de referência padronizada → TRELLIS → malha GLB.
3. Forja Blender: `REPAIR` (manifold, espessura mínima) → `DECIMATE` (alvo por classe; **small_prop_item ≤ 600 tris** — ajuste pendente que você já mapeou) → `VALIDATION` (orçamento, escala, pivô, LOD).
4. `fab3d_bridge.py` entrega; `Fab3dImporter.cs` monta prefab com componentes de gameplay (ex.: `ReSyncToggle` na catraca-porta).
5. `validate_asset` confere tris/escala/nome → `evidence/` guarda preview.

### 2.2 Personagens (Modo 2 + rig)
1. Conceito com **régua de escala** na imagem (art-bible exige régua) → TRELLIS → malha em T-pose aproximada.
2. Forja: limpeza + padronização de altura (Dez 1,25 m; Íria 2,1 m).
3. `auto_rig.py`: esqueleto bípede humanoide padrão (Mixamo-compatible) — por isso **Dez é bípede com 2 braços principais** e os 2 braços-auxiliares são bones acessórios fora do Humanoid `[NOVO, decisão de compatibilidade]`.
4. Retarget das animações base (Idle/Walk/Run/Attack/Talk + dash/parry/aerial do jogo) a partir da biblioteca de poses JSON do Pose Studio; export para Unity como AnimationClip.
5. Personagens flutuantes (Porca) ficam fora do auto_rig: TRELLIS gera peça única; animação procedural no Unity (bob + garra IK simples).

### 2.3 Texturas tileáveis e UI
- ComfyUI gera tileables 1024 (chão/parede por fuso, 2 estados) com tiling verificado no validador (seam check).
- Ícones/HUD/retratos: prompt-base de UI (linha grossa 2 px + canto de rebite); retratos de diálogo 512 px por personagem (para o box de diálogo com TTS).
- Regra: **texto nunca é gerado na imagem** — rótulos entram como UI do Unity (fontes localizáveis).

### 2.4 Orçamentos (budget) resumidos — versão completa no art-bible.yaml

| Classe | Tris máx | Textura | Observação |
|---|---|---|---|
| small_prop_item | **600** (seu número) | 512 | decor/props |
| kit_piece | 1.500 | 512–1024 | plataformas/torres |
| enemy | 3.500 | 512 | Ferrugens |
| elite (Censor) | 6.000 / 8.000 | 512 | 2 formas |
| npc | 4.000 | 512 | Vó Segunda etc. |
| hero (Dez) | 8.000 | 1024 atlas | 1 no jogo |
| boss | 12.000 | 1024 + emissive | 6 no jogo 1 |

## 3. Organização no repo do jogo (nascido limpo do Studio Core) `[NOVO, pilar 9]`

```
concerto/                    # repo do jogo, criado do template do Studio Core
  docs/                      # este pacote (md + yamls) já no padrão
  art-bible.yaml             # contrato visual (pilar 8)
  assets/Game/
    Models/  Textures/  Audio/VO/  Music/  UI/
  scenes/fusos/caldeiral/    # 1 pasta por fuso (Addressables por fuso)
  cutscenes/                 # cutscene_*.yaml (fonte) + Timeline gerado
  dialogue/dialogues.yaml    # fonte única de VO
  tools/  tests/  evidence/
```

Git LFS para `.fbx/.png/.wav` (pilar 9). O Studio Core (validadores, skills, bridges) fica no gamedev-setup-ia; nada de ferramenta do estúdio misturada no repo do jogo — o contrário também vale.

## 4. Portões de qualidade (gates) por asset `[NOVO]`

1. **Gate de provenance:** todo asset registra origem (prompt, seed, modelo, licença) — herda o provenance tracking do 3dprinter-setup-ia.
2. **Gate técnico:** validate_asset (tris ≤ budget, escala, pivô, LOD2 opcional, colisão simples gerada).
3. **Gate de arte:** captura in-engine (evidence/captures) comparada ao conceito; Strict Reviewer pode vetar com razão registrada.
4. **Gate de cena:** validate_scene (orçamento total de draw calls/tris por zona mobile — ver 10).

## 5. Riscos de pipeline e mitigação `[NOVO]`

| Risco | Mitigação |
|---|---|
| TRELLIS ruim em personagens orgânicos | Estilo estilizado-mecânico favorece TRELLIS; personagens têm "carne" mínima (Dez é quase um boneco mecânico — proposital) |
| 4 braços quebram rig Humanoid | 2 braços Humanoid + 2 acessórios (02 §1) |
| Decimate destrói detalhe de mostrador | Detalhe por normal-map/bake (512–1024), não geometria |
| VRAM 12 GB com ComfyUI+TRELLIS juntos | Execução sequencial por job (fila no .studio/tasks); modelos em D:\ |
| Tempo de iteração de arte | 1 fuso por vez (M1 = Caldeiral só); kit fechado antes de espalhar |

## 6. O que já existe vs. o que este GDD pede `[NOVO, mapa honesto]`

- **Pronto:** Unity 6.3 URP headless + validadores + capturas; Modo 1 Fab3D ponta-a-ponta; auto_rig.py básico; Pose Studio.
- **A construir (seus pilares):** ComfyUI + concept_bridge (pilar 3); integração Fab3D→auto_rig→retarget validada no Unity (pilar 4); tts_bridge.py + vozes (pilar 5); SFX/trilha (pilar 6); CutsceneBuilder.cs (pilar 7); Art Director/Reviewer (pilar 8); limpeza Studio Core vs. jogo (pilar 9).
- O jogo 1 foi desenhado para consumir os pilares **na ordem do seu roadmap** (ver 11): nada do design depende de capacidade que não está no mapa do estúdio.
