# COMBATE GDD — FAÍSCA, INIMIGOS DO CANON E FINALIZADOR (`GDD 04 §2`)

## Declaração de Conformidade
- **`docs/DESIGN_BIBLE.md` & `docs/gdd/04-mecanicas.md §2`:** 100% conforme — números exatos de Pressão, Faísca e telegraphs (`0,6s–1,2s` com brilho emissivo na junção), morte limpa sem sangue (*"o tempo voltou a passar"*: parafusos e molas voando), máximo de 2 tipos simultâneos por onda (`GDD 05 §7`) em arena com 3 rotas de verticalidade.
- **Regra TripoSG-Only:** 100% dos 5 novos inimigos canon foram gerados exclusivamente via TripoSG + decimação Blender em `Assets/Game/Art/Generated/combat_gdd/`.

---

## Modelos de Inimigos Canon (100% TripoSG)

| # | Inimigo Canon | Arquivo FBX TripoSG | Telegraph | Contra-Jogo Canônico | Orçamento Tris |
|---|---------------|---------------------|-----------|----------------------|----------------|
| 1 | **Ferrugem-Trabalhadora** | `enemy_ferrugem_trabalhadora.fbx` (`seed 401`) | `0,8s` (marreta) | Esquiva/punição após telegraph com brilho na junção | ≤ 2.200 tris |
| 2 | **Ferrugem-Espelho** | `enemy_ferrugem_espelho.fbx` (`seed 402`) | `1,2s` (carga Faísca) | **Contratempo** reflete a Faísca de volta e atordoa | ≤ 2.200 tris |
| 3 | **Ferrugem-Coletora** | `enemy_ferrugem_coletora.fbx` (`seed 403`) | `0,9s` (bolha) | **Crava-Mola** quebra a bolha protetora e atordoa | ≤ 2.200 tris |
| 4 | **Censor-Patrulha** | `enemy_censor_patrulha.fbx` (`seed 404`) | `0,6s` (combo 2+giro) | **Parry no 2º golpe** do combo quebra e atordoa | ≤ 2.200 tris |
| 5 | **Censor-Pesado** | `enemy_censor_pesado.fbx` (`seed 405`) | `1,1s` (golpe pesado) | **Imune a Faísca** — **Martelada Aérea** na nuca | ≤ 2.800 tris |

---

## Sistemas Implementados (Escopo Fechado: Combate)
1. **Recurso PRESSÃO (`PressureResourceSystem.cs`):** `100 pts` máximo, recarga passiva `25/s`, `+30` por golpe melee conectado (*"bater para poder dashar/atirar"*).
2. **FAÍSCA Ranged (`FaiscaRangedController.cs`):** Projétil de energia em arco curto `12m`, recarga `0,4s`, custo `20 Pressão`.
3. **Finalizador CONCERTO (`GddCanonEnemyBase.ExecuteConcertoFinisher`):** Inimigo atordoado → execução com *"afinação"* (12 partículas de notas musicais) + morte limpa com 16 parafusos e molas voando (`HasBloodEffect == false`).
4. **Diretor de Ondas (`GddCombatWaveDirector.cs`) & Arena (`CombateGddArena.unity`):**
   - **Onda 1:** Ferrugem-Trabalhadora + Ferrugem-Espelho (`2 tipos`).
   - **Onda 2:** Ferrugem-Coletora + Censor-Patrulha (`2 tipos`).
   - **Onda 3:** Censor-Pesado + Ferrugem-Espelho (`2 tipos`).
   - **Verticalidade:** 3 rotas elevadas (`WestMezzanine`, `EastMezzanine`, `NorthOverpass`).

---

## Evidências & Validação
- **Vídeo da Arena em Ondas:** `/captures/combate_gdd/combate_gdd_ondas.mp4`
- **SmokeTest automatizado:** `Smoke_32_GddCombatPressureFaiscaAndAll5CanonEnemiesCounterplayAndFinisher` em `Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs` (`32/32 PASSED`, `0 errors CS`, `0 warnings CS`).
