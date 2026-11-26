# FUSO MARÉSIA — O MAR PARADO (`GDD 03 §2.3` + `GDD 05 §4`)

## Declaração de Conformidade
- **`docs/DESIGN_BIBLE.md`:** 100% conforme — input → ação no mesmo frame, momentum conservável no combo `Garra → Sopro → Rebote`, sem animação por código em personagens, zero warnings/erros CS, reinício do boss `≤ 8s` via Posto de Reparo na arena.
- **`docs/gdd/art-bible.yaml` & `docs/gdd/03-mundo-fusos.md §2.3`:** Paleta madeira/latão + mar congelado (`Mat_Maresia_Desincronizado` azul-aço/madeira úmida → `Mat_Maresia_Restaurado` turquesa/latão quente) e pontos de engate olho-de-lente dourado (`Mat_Maresia_OlhoLenteOuro`).
- **Regra TripoSG-Only:** 100% das 8 peças do kit modular têm origem exclusiva em TripoSG (4 peças hero/assinatura geradas em `Assets/Game/Art/Generated/maresia/` + 4 peças modulares TripoSG de `Assets/Game/Prefabs/Fab3D/` revestidas com as 2 variantes de material de Marésia). Nenhum primitivo Unity usado como arte.

---

## Kit Modular de 8 Peças (100% TripoSG — 2 Estados)

| # | Peça | Origem TripoSG | Uso em Marésia | Orçamento Tris |
|---|------|----------------|----------------|----------------|
| 1 | `mar_frozen_wave_crest.fbx` | TripoSG novo (`seed 101`) | Cristas de ondas congeladas de 10m + elevadores de onda pós-degelo | ≤ 1.400 tris |
| 2 | `mar_tilted_boat_plat.fbx` | TripoSG novo (`seed 102`) | Barcos inclinados como plataformas verticais, ledges de desvio e barco-drift | ≤ 1.400 tris |
| 3 | `mar_flooded_cavern_arch.fbx` | TripoSG novo (`seed 103`) | Arcos das cavernas-habitação alagadas pela consequência da Vitralha | ≤ 1.400 tris |
| 4 | `mar_hero_baleia_ancora.fbx` | TripoSG novo (`seed 104`) | **Hero Piece** — Boss *A Baleia de Âncora* | ≤ 9.500 tris |
| 5 | `caldeiral_env_floor_tile.prefab` | TripoSG (`Fab3D`) | Cais de madeira/latão, decks do desfiladeiro e arena da Baleia | ≤ 600 tris |
| 6 | `caldeiral_env_wall_panel.prefab` | TripoSG (`Fab3D`) | Paredões de farol/porto do Cais Congelado | ≤ 1.500 tris |
| 7 | `caldeiral_env_portal_pillar.prefab` | TripoSG (`Fab3D`) | Pontos de Garra *Olho-de-Lente Dourado* (`GrappleLensPoint`) e altar da Relíquia | ≤ 1.200 tris |
| 8 | `c5_prop_no_resincronia.prefab` | TripoSG (`Fab3D`) | Nó de Re-Sincronia do Beat 1 e Posto de Reparo da arena do Boss | ≤ 1.200 tris |

Materiais de dois estados:
- `Assets/Game/Materials/Maresia/Mat_Maresia_Desincronizado.mat` (aço congelado / madeira úmida)
- `Assets/Game/Materials/Maresia/Mat_Maresia_Restaurado.mat` (latão quente / madeira restaurada)
- `Assets/Game/Materials/Maresia/Mat_Maresia_OlhoLenteOuro.mat` (olho-de-lente dourado da Garra)
- `Assets/Game/Materials/Maresia/Mat_Maresia_AguaAlagada.mat` (nível de cheia da consequência da Vitralha)

---

## Os 7 Beats em `Assets/Game/Scenes/Maresia.unity` (`400×400m`)

1. **Beat 1 — Cais Congelado (`cp_maresia_b1_cais`):** Entrada com paredões de porto e zona de Re-Sincronia (`maresia_b1_cais`).
2. **Beat 2 — Cristas de Ondas de 10m (`cp_maresia_b2_cristas`):** Platforming vertical nas cristas congeladas (`mar_frozen_wave_crest`) usando barcos inclinados (`mar_tilted_boat_plat`) como plataformas.
3. **Beat 3 — Cavernas-Habitação Alagadas (`cp_maresia_b3_cavernas`):** Quando o evento `evt_cost_vitralha_flood_maresia` está ativo (consequência da Vitralha), a água sobe (`B3_FloodedCavernWater`) e habilita a **quest de desvio jogável** (`B3_FloodDetourQuest_HighRoute`, 3 waypoints elevados em ledges secos).
4. **Beat 4 — Desfiladeiro da Maré (`cp_maresia_b4_desfiladeiro`):** Rota alternativa com **Rebote + Sopro** quando Marésia é jogado antes da Garra existir (`GDD 05 §4.4`), convivendo com pontos olho-de-lente dourados para revisita com a Garra.
5. **Beat 5 — Arena do Boss: A Baleia de Âncora (`cp_maresia_b5_boss`):** Encontro de **DESFERRUJAR** em 3 fases (não é combate para matar):
   - Fase 1: soltar 1ª trava enferrujada.
   - Fase 2: exige **Contratempo** (parry) para abrir a trava central (`IsParryGateActive`).
   - Fase 3: soltar trava final → Baleia libertada (`IsDerusted == true`).
   - Posto de Reparo na arena (`B5_RepairStation_Boss`) garante reinício em `≤ 8s`.
6. **Beat 6 — Relíquia PÊNDULO → GARRA (`cp_maresia_b6_reliquia`):** Coleta da Relíquia libera a **Garra** (`grappleRangeM = 12m`, `grapplePullSpeed = 8 m/s`, swing com momentum circular em `GrappleLensPoint`, reseta Rebote e cancela em Sopro conservando momentum: combo `Garra → Sopro → Rebote`).
7. **Beat 7 — Pós-Conserto: Mar Degelado (`cp_maresia_b7_degelo`):** O mar degela, habilitando navegação em **barco-drift** (`B7_DriftBoat_Navigation`) e **ondas como elevadores verticais** (`B7_WaveElevatorPlatform`), persistido no Save JSON (`maresiaBeatIndex`, `maresiaFloodDetourCleared`, `maresiaBossDerusted`, `relicPenduloUnlocked`, `maresiaSeaThawed`).

---

## Evidências & Validação
- **Vídeo da Rota de Ouro:** `/captures/maresia/maresia_rota_ouro.mp4`
- **SmokeTest automatizado:** `Smoke_29_MaresiaWavesBossDerustPenduloGrappleDetourAndDrift` em `Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs` (`29/29 PASSED`, `0 warnings CS`, `0 errors CS`).
