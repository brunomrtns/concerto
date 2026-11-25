# ASSETS 3D GERADOS — PEÇAS E PROPS FALTANTES (TRIPOSG EXCLUSIVO)

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0
**Pipeline exclusivo:** **TripoSG** (`triposg_runner.py`, seed `1125`) + **PBR 4K** (`pipeline/run_asset.py`, `qa_pass=True`) + exportação FBX sólida (`4000` polígonos, UV + `albedo`, `emission`, `metallicsmooth`).
**Zero fallback / Zero primitiva substituta:** todos os 5 modelos abaixo foram gerados exclusivamente via TripoSG.

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Seção 7 (Legibilidade & Silhueta):** CONFORME — cada peça e prop agora possui silhueta própria distinta (Condensador de Fluxo com bobinas duplas, Estabilizador Orbital com anéis giroscópicos, Fragmento de Núcleo cristalino dourado/ciano, Célula de Energia hexagonal emissiva e Estação de Parry octogonal), mantendo a paleta bronze/latão + emissão ciano da oficina.
- **Seção 8 (Data-Driven):** CONFORME — `parts_loop_config.json` atualizado para referenciar `part_condensador_fluxo` (`part_04`) e `part_estabilizador_orbital` (`part_05`), sem hardcode.
- **Seção 3 (Performance / Estabilidade):** CONFORME — malhas otimizadas em `4000` polígonos com materiais URP Lit limpos (`0` materiais rosa/erro), compilação com `0 warnings CS` e `Tests/SmokeTest/` **7/7 PASSED**.

---

## 2. Lista Completa dos Modelos Gerados — Lote 1 (5 Modelos)

| # | Nome do Modelo (`slug`) | Arquivo FBX / Prefab | Prompt Usado no Concept → TripoSG (`seed=1125`) | Cena / Onde Entra | Escala / Collider | Data |
|---|---|---|---|---|---|---|
| 1 | **`part_condensador_fluxo`** *(Peça 4 — Condensador de Fluxo)* | `Assets/Game/Art/Generated/parts/part_condensador_fluxo.fbx` / `Assets/Game/Prefabs/Fab3D/part_condensador_fluxo.prefab` | `"3D game prop render of a compact stylized steampunk brass flux capacitor module with twin copper coils, riveted dark steel housing, and a glowing cyan energy prism core, Ratchet and Clank style, centered on pure solid white background, studio lighting, no shadow, no text"` | `Caldeiral.unity` (Ato 1 — Peça 4 sobre o caixote sudeste, via `parts_loop_config.json` e `PartsLoopManager`) | `0.44 m` / `BoxCollider (isTrigger=true)` | 2026-11-25 |
| 2 | **`part_estabilizador_orbital`** *(Peça 5 — Estabilizador Orbital)* | `Assets/Game/Art/Generated/parts/part_estabilizador_orbital.fbx` / `Assets/Game/Prefabs/Fab3D/part_estabilizador_orbital.prefab` | `"3D game prop render of a compact stylized clockwork bronze orbital stabilizer gyroscope with three brass rings and a glowing turquoise crystal orb in the center, Ratchet and Clank style, centered on pure solid white background, studio lighting, no shadow, no text"` | `Caldeiral.unity` (Ato 1 — Peça 5 sobre o caixote sudoeste, via `parts_loop_config.json` e `PartsLoopManager`) | `0.44 m` / `BoxCollider (isTrigger=true)` | 2026-11-25 |
| 3 | **`prop_fragmento_nucleo`** *(Fragmento de Núcleo)* | `Assets/Game/Art/Generated/epicenter/prop_fragmento_nucleo.fbx` / `Assets/Game/Prefabs/Fab3D/prop_fragmento_nucleo.prefab` | `"3D game prop render of a stylized sci-fi crystalline energy core shard encased in ornate dark bronze clockwork filigree with bright glowing cyan and gold veins, hero relic artifact, Ratchet and Clank style, centered on pure solid white background, studio lighting, no shadow, no text"` | `Epicentro.unity` (Ato 3 — recompensa dropada ao derrotar o Guardião da Fenda) | `0.58 m` / `BoxCollider (isTrigger=true)` | 2026-11-25 |
| 4 | **`prop_celula_energia`** *(Célula de Energia)* | `Assets/Game/Art/Generated/props/prop_celula_energia.fbx` / `Assets/Game/Prefabs/Fab3D/prop_celula_energia.prefab` | `"3D game prop render of a compact stylized sci-fi hexagonal energy battery canister with dark gunmetal brass endcaps and a bright glowing cyan plasma cylinder core, pickup item, Ratchet and Clank style, centered on pure solid white background, studio lighting, no shadow, no text"` | `Caldeiral`, `SetorExterno`, `Epicentro` (drop de energia em combate) | `0.34 m` / `BoxCollider (isTrigger=true)` | 2026-11-25 |
| 5 | **`prop_estacao_parry`** *(Estação de Treino de Parry)* | `Assets/Game/Art/Generated/props/prop_estacao_parry.fbx` / `Assets/Game/Prefabs/Fab3D/prop_estacao_parry.prefab` | `"3D game prop render of a stylized steampunk bronze combat training totem pedestal with an octagonal brass shield emitter disc and glowing cyan energy rings on top, Ratchet and Clank style, centered on pure solid white background, studio lighting, no shadow, no text"` | `Caldeiral.unity` (`ParryTeachStation` — estação de ensino de Absorver/Parry) | `1.28 m` / `BoxCollider (sólido)` | 2026-11-25 |

---

## 3. Evidências & Validação (Gates)

- **Compilação (GATE 1):** `0 warnings CS`, `0 errors CS`.
- **Importação & Materiais URP (GATE 2):** 5 materiais `Mat_Fab3D_<slug>.mat` (`URP/Lit` com `_BaseMap`, `_MetallicGlossMap`, `_EmissionMap`) + 5 prefabs normalizados em `Assets/Game/Prefabs/Fab3D/` + `Caldeiral.unity` atualizada.
- **MP4 Showcase em Cena Real (GATE 4):** `captures/assets/lote1_5_modelos_triposg_1080p.mp4` (`1920x1080 H.264`, rotação 360° dos 5 modelos na oficina `Caldeiral`).
- **Suíte Smoke Test (GATE 5):** `Tests/SmokeTest/` — **7/7 PASSED**.
- **Modelos que falharam:** **0** (todos os 5 gerados com `qa_pass=True`).
