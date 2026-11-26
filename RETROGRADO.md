# FUSO RETRÓGRADO — O DESERTO DO TEMPO REVERSO (`GDD 03 §2.5`)

## Declaração de Conformidade
- **`docs/DESIGN_BIBLE.md`:** 100% conforme — input → ação no mesmo frame, feedback claro nas janelas invertidas, reinício de boss `≤ 8s` via Posto de Reparo na arena, `0 errors CS`, `0 warnings CS`.
- **`docs/gdd/art-bible.yaml` & `docs/gdd/03-mundo-fusos.md §2.5`:** Paleta **âmbar-areia** com dunas em espiral de mola (`Mat_Retrogrado_Desincronizado` âmbar invertido → `Mat_Retrogrado_Restaurado` ouro antigo + `Mat_Retrogrado_AreiaReversa` + `Mat_Retrogrado_RuinaPreFratura`).
- **Regra TripoSG-Only:** 100% das 8 peças do kit modular têm origem exclusiva em TripoSG (4 peças hero/assinatura geradas em `Assets/Game/Art/Generated/retrogrado/` + 4 peças modulares TripoSG de `Assets/Game/Prefabs/Fab3D/` revestidas com os materiais de 2 estados do Retrógrado).

---

## Kit Modular de 8 Peças (100% TripoSG — 2 Estados Âmbar-Areia)

| # | Peça | Origem TripoSG | Uso em Retrógrado | Orçamento Tris |
|---|------|----------------|-------------------|----------------|
| 1 | `ret_spiral_dune_coil.fbx` | TripoSG novo (`seed 301`) | Dunas em espiral de mola da entrada e horizonte | ≤ 1.400 tris |
| 2 | `ret_reverse_hourglass_plat.fbx` | TripoSG novo (`seed 302`) | Plataformas de janelas invertidas que existem cada vez mais conforme se espera | ≤ 1.400 tris |
| 3 | `ret_prefracture_ruin_arch.fbx` | TripoSG novo (`seed 303`) | Ruínas do pré-Fratura dos *Outros Relógios* (gancho da série) | ≤ 1.400 tris |
| 4 | `ret_hero_arquivista_mor.fbx` | TripoSG novo (`seed 304`) | **Hero Piece** — Boss *O Arquivista-Mor* | ≤ 9.500 tris |
| 5 | `caldeiral_env_floor_tile.prefab` | TripoSG (`Fab3D`) | Bacias de areia, pegadas à frente, ponte e arena do astrolábio | ≤ 600 tris |
| 6 | `caldeiral_env_wall_panel.prefab` | TripoSG (`Fab3D`) | Paredões do cânion da Ampulheta | ≤ 1.500 tris |
| 7 | `caldeiral_env_portal_pillar.prefab` | TripoSG (`Fab3D`) | Grãos/colunas de areia subindo (`+Y`) e Altar da Relíquia Aro | ≤ 1.200 tris |
| 8 | `c5_prop_no_resincronia.prefab` | TripoSG (`Fab3D`) | Nó de Re-Sincronia do Beat 1 e Posto de Reparo da Arena | ≤ 1.200 tris |

---

## Sistemas & Os 7 Beats em `Assets/Game/Scenes/Retrogrado.unity` (`400×400m`)

1. **GATE 1 — Tempo Reverso (`RetrogradoReverseTimeSystem.cs` + `ReverseWindowPlatform`):**
   - Areia cai **para cima** (`risingSandVelocityY = +3.2f`), pegadas se desmancham **à frente** do jogador (`FootprintsFadeAheadOfPlayer`), artefatos voltam a ser novos (`ArtifactRestorationProgress01`) e plataformas de **janelas invertidas** começam desmaterializadas e **existem cada vez mais conforme se espera** (`Stability01 >= 0.50f` ativa colisão e escala completa).
2. **GATE 2 — Pós-Conserto (`RetrogradoFusoController.cs`):**
   - Revela as **ruínas do pré-Fratura** (`B7_PreFractureRuins_OutrosRelogios`, lore dos *Outros Relógios* plantando o gancho da série — `evt_benefit_retrogrado_ruins`).
   - Melhorias cosméticas do Caldeiral "se desfazem" como custo cômico leve (`CaldeiralCosmeticUndoApplied == true`, `evt_cost_retrogrado_undo_caldeiral` — nada crítico perdido).
3. **GATE 3 — Relíquia ARO → ARO-ESCUDO (`ParryAndLockOnController.cs`):**
   - Integrada diretamente ao **Contratempo** existente: ao acertar parry com `HasRelicAroEscudo`, reflete **Faísca em área** (`aroEscudoFaiscaRadius = 6.0m`), atordoando inimigos no raio (`AroEscudoFaiscaReflectCount`).
4. **GATE 4 — Boss O ARQUIVISTA-MOR (`RetrogradoBossArquivistaMor.cs`):**
   - Registra os padrões do jogador (`KnownPatterns`) e **antecipa/bloqueia** qualquer padrão repetido (`AnticipatedBlockCount`).
   - Quebra-se sua previsão variando para **3 padrões inéditos** + **Contratempo** na Fase 2; Posto de Reparo garante reinício `≤ 8s`.
5. **GATE 5 & 6 — Persistência, Evidência & SmokeTest:**
   - Save JSON persiste `retrogradoBeatIndex`, `retrogradoBossDefeated`, `relicAroEscudoUnlocked`, `retrogradoRuinsRevealed` e `caldeiralCosmeticsUndoneComic`.
   - Vídeo da Rota de Ouro: `/captures/retrogrado/retrogrado_rota_ouro.mp4` (areia subindo + janelas invertidas + Arquivista-Mor + ruínas pré-Fratura).
   - Teste automatizado: `Smoke_31_RetrogradoReverseSandInvertedPlatformsArquivistaAroEscudoAndRuins` (`31/31 PASSED`, `0 errors CS`, `0 warnings CS`).
