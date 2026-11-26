# FUSO ECODIA — A CIDADE QUE REPETE 6 SEGUNDOS (`GDD 03 §2.4`)

## Declaração de Conformidade
- **`docs/DESIGN_BIBLE.md`:** 100% conforme — loop determinístico previsível sem jitter, input → ação no mesmo frame, reinício de boss `≤ 8s` via Posto de Reparo na arena, `0 errors CS`, `0 warnings CS`.
- **`docs/gdd/art-bible.yaml` & `docs/gdd/03-mundo-fusos.md §2.4`:** Estética **Art Déco Relojoeiro** em azul-madrugada + neons quentes (`Mat_Ecodia_Desincronizado` azul-madrugada congelado → `Mat_Ecodia_Restaurado` ouro/latão Art Déco acordado + `Mat_Ecodia_NeonQuente` + `Mat_Ecodia_Censor`).
- **`CANON.md` (Poderes):** Relíquia **Ponteiro Menor → Micra** (`2,0s` câmera lenta `0.35x`, cooldown `8,0s`, tecla `[T]`) registrada como 3º poder em `DezPowersController` **SEM substituir a Estase** (`[R]`, `3s`, cooldown `10s`).
- **Regra TripoSG-Only:** 100% das 8 peças do kit modular têm origem exclusiva em TripoSG (4 peças hero/assinatura Art Déco geradas em `Assets/Game/Art/Generated/ecodia/` + 4 peças modulares TripoSG de `Assets/Game/Prefabs/Fab3D/` revestidas com os materiais de 2 estados de Ecodia).

---

## Kit Modular de 8 Peças (100% TripoSG — 2 Estados Art Déco)

| # | Peça | Origem TripoSG | Uso em Ecodia | Orçamento Tris |
|---|------|----------------|---------------|----------------|
| 1 | `eco_artdeco_tram.fbx` | TripoSG novo (`seed 201`) | Bonde Art Déco em loop previsível de 6s (plataforma-puzzle) | ≤ 1.400 tris |
| 2 | `eco_clockwork_door_arch.fbx` | TripoSG novo (`seed 202`) | Porta relojoeira Art Déco que abre exatamente em `t = 2,0s` | ≤ 1.400 tris |
| 3 | `eco_artdeco_facade.fbx` | TripoSG novo (`seed 203`) | Fachadas/torres Art Déco relojoeiras e torre de anúncio neon | ≤ 1.400 tris |
| 4 | `eco_hero_boss_ecodez.fbx` | TripoSG novo (`seed 204`) | **Hero Piece** — Boss *Eco Dez* | ≤ 9.500 tris |
| 5 | `caldeiral_env_floor_tile.prefab` | TripoSG (`Fab3D`) | Bulevares, Praça Neon, Ponte Espelho e Arena do Boss | ≤ 600 tris |
| 6 | `caldeiral_env_wall_panel.prefab` | TripoSG (`Fab3D`) | Paredões Art Déco da Torre Espelho | ≤ 1.500 tris |
| 7 | `caldeiral_env_portal_pillar.prefab` | TripoSG (`Fab3D`) | Patrulheiros Censores e Altar da Relíquia Ponteiro Menor | ≤ 1.200 tris |
| 8 | `c5_prop_no_resincronia.prefab` | TripoSG (`Fab3D`) | Nó de Re-Sincronia do Beat 1 e Posto de Reparo da Arena | ≤ 1.200 tris |

---

## Sistemas & Os 7 Beats em `Assets/Game/Scenes/Ecodia.unity` (`400×400m`)

1. **GATE 1 — Loop de 6,0s (`EcodiaTimeLoopSystem.cs`):**
   - Distrito inteiro em ciclo determinístico de `6,0s` (`LoopDurationSec = 6.0f`): bonde Art Déco cruza o trilho continuamente, porta relojoeira abre exatamente em `t = 2,0s` (janela `2,0s..4,5s`), anúncio neon alterna a cada 3s e 3 moradores repetem gestos. O jogador é o único fora do tempo.
2. **GATE 2 — Pós-Conserto (`EcodiaFusoController.cs`):**
   - Quebra o loop de 6s (`evt_benefit_ecodia_free_loop`): moradores acordam com **3 micro-quests de memória** (*devolver a cada um o segundo que perderam*).
   - Custo visível no grafo (`evt_cost_ecodia_realtime_censors`): **Censores** mudam de `FrozenLoop6s` para `RealTimePatrol` com velocidade aumentada (`1.65x`).
3. **GATE 3 — Relíquia Ponteiro Menor → Micra (`MicraPower.cs`, `DezPowersController.cs`):**
   - `2,0s` de câmera lenta (`0.35x` no mundo, Dez permanece em `1.0x`), cooldown `8,0s`, coexistindo no menu de poderes (`RegisteredPowerCount == 3`: `eco_temporal`, `estase`, `micra`).
4. **GATE 4 — Boss ECO DEZ (`EcodiaBossEcoDez.cs`):**
   - Eco de Desidério que repete seu erro e conta piadas na ordem errada (*punchline antes do setup* — drama+humor), espelha o moveset do player (`Mirror_Sopro`, `Mirror_Rebote`, `Mirror_CravaMola`), exige **Contratempo** na Fase 2 e reinicia em `≤ 8s` no Posto de Reparo.
5. **GATE 5 & 6 — Persistência, Evidência & SmokeTest:**
   - Save JSON persiste `ecodiaBeatIndex`, `ecodiaBossDefeated`, `relicPonteiroMenorUnlocked`, `ecodiaCityAwake` e `ecodiaMemoryQuestsCleared`.
   - Vídeo da Rota de Ouro: `/captures/ecodia/ecodia_rota_ouro.mp4` (mostrando o loop de 6s ANTES e a cidade acordada DEPOIS).
   - Teste automatizado: `Smoke_30_EcodiaLoop6sEcoDezMicraMemoryQuestsAndRealTimeCensors` (`30/30 PASSED`, `0 errors CS`, `0 warnings CS`).
