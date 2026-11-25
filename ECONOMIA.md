# ECONOMIA E PROGRESSÃO — CONCERTO

Sistema de economia de peças coletáveis → upgrades reais na bancada/posto de troca do Dez, com persistência exclusiva em JSON versionado (`SaveGameData` / `SaveSystem`, **zero `PlayerPrefs`**).

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Progressão e Save:** 100% persistido no **Save JSON versionado** (`SaveGameData` v3 em `Application.persistentDataPath/concerto_save.json` com SHA-256 e 3 slots rotativos) — **`PlayerPrefs` proibido e não utilizado**.
- **HUD legível e sem sobreposição:** Contador compacto `PEÇAS: X/6 (Saldo: Y)` integrado no HUD sem sobrepor vida, objetivo ou poderes; efeitos de upgrade refletem imediatamente nas barras de vida, células e cooldowns.
- **Escopo fechado:** Exclusivamente sistema de peças → upgrades; combate, inimigos e geometria das fases permanecem inalterados.
- **Modelos 3D TripoSG criados nesta tarefa:** `0 modelos criados` (o posto de troca utiliza a bancada existente do Dez na oficina `Caldeiral` com sinalização diegética emissiva).

---

## 2. Tabela de Upgrades (Definida ANTES do Código — GATE 1)

| # | ID (`upgradeId`) | Nome do Upgrade | Custo (Peças) | Efeito Real no Gameplay & HUD |
|---|---|---|---|---|
| 1 | `upg_vida_extra` | **Blindagem de Caldeiral** | `1 peça` | `+1` segmento de vida máxima (`PlayerHealthController`: `4 → 5`) + cura imediata de `+1` segmento; barra extra visível na hora no HUD de vida. |
| 2 | `upg_eco_recarga` | **Capacitor de Eco** | `1 peça` | Reduz cooldown do **Eco Temporal** em `33%` (`3.0s → 2.0s` em `DezPowersController.Cfg.ecoCooldownSec`); recarga mais rápida visível no HUD de poderes. |
| 3 | `upg_estase_duracao` | **Bobina de Estase** | `1 peça` | Aumenta duração da **Estase** em `+50%` (`3.0s → 4.5s` em `DezPowersController.Cfg.stasisDurationSec`); campo temporal ativo por mais tempo. |
| 4 | `upg_celula_extra` | **Núcleo de Reserva** | `2 peças` | `+2` células máximas de energia na ferramenta multi-uso (`6 → 8` em `MultiToolController`) + recarga imediata de `+2` células; visível no HUD `CÉLULAS`. |

---

## 3. Balanceamento de Custos (GATE 4)

- **Total de peças coletáveis no jogo:** **`6 peças`**
  - `5` peças da oficina em `Caldeiral.unity` (`part_01`..`part_05`)
  - `1` Fragmento de Núcleo em `Epicentro.unity` (`prop_fragmento_nucleo`, concede `+1` crédito de peça ao coletar)
- **Custo total de TODOS os 4 upgrades:** `1 + 1 + 1 + 2` = **`5 peças`**
- **Margem final:** `6 − 5` = **`1 peça` (`16.7%` de margem)**
  - O jogador consegue comprar todos os 4 upgrades sem precisar de grind nem ficar travado caso deixe passar 1 peça, e não sobra excesso sem uso.

---

## 4. Mapa de Onde Cada Peça Está

| # | ID | Nome | Cena | Localização (`parts_loop_config.json` / Cena) | Coordenadas `(x, y, z)` |
|---|---|---|---|---|---|
| 1 | `part_01` | Núcleo de Ressonância | `Caldeiral.unity` (Ato 1) | Sobre o caixote noroeste | `(-34.6, 1.28, -13.2)` |
| 2 | `part_02` | Bobina de Fase Beta | `Caldeiral.unity` (Ato 1) | Ao lado da estante de cristais | `(-31.3, 0.42, -13.1)` |
| 3 | `part_03` | Regulador Crono | `Caldeiral.unity` (Ato 1) | Sob a bancada central | `(-29.0, 0.36, -16.45)` |
| 4 | `part_04` | Condensador de Fluxo | `Caldeiral.unity` (Ato 1) | Sobre o caixote sudeste | `(-24.0, 1.24, -22.6)` |
| 5 | `part_05` | Estabilizador Orbital | `Caldeiral.unity` (Ato 1) | Sobre o caixote sudoeste | `(-34.5, 1.24, -22.8)` |
| 6 | `core_fragment` | Fragmento de Núcleo | `Epicentro.unity` (Ato 3) | Drop do Guardião da Fenda na arena central | Arena Central (`CoreFragmentInteractable`) |

---

## 5. Ponto de Troca, Testes e Evidências (GATES 2, 3, 5 e 6)

- **Ponto de Troca (GATE 3):** Posto/Bancada de Upgrades do Dez em `Caldeiral.unity` (`UpgradeBenchStation` — `[E]` / `[B]` próximo à bancada ou `[U]` na oficina), navegável 100% por teclado e controle (`Setas`/`D-Pad` + `Enter`/`A` comprar + `Esc`/`B` fechar).
- **Contador no HUD (GATE 2):** Pill compacto no canto superior direito (`PEÇAS: X/6  •  DISPONÍVEL: Y`) atualizado na hora e persistido em `SaveGameData` (`partsCurrencyEarned`, `partsCurrencySpent`, `purchasedUpgradeIds`).
- **Teste Smoke Novo (GATE 5):** `Smoke_14_EconomyPartsCollectAndUpgradePersistsAcrossReload` em `unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs`.
- **MP4 de Prova (GATE 6):** `captures/economia/economia_upgrades_fluxo.mp4` (`1920x1080`, H.264) — coletar peças → abrir posto de troca → comprar 2 upgrades → demonstrar efeitos rodando no HUD e no gameplay.

---

## 6. Como Reverter

```bash
git revert <commit-hash-desta-tarefa>
```

Arquivos tocados exclusivamente por esta tarefa:
- `ECONOMIA.md`
- `unity/Assets/Game/Scripts/Parts/EconomyUpgradeSystem.cs` (novo)
- `unity/Assets/Game/Scripts/Save/SaveGameData.cs` (campos de economia/upgrades no JSON)
- `unity/Assets/Game/Scripts/Save/SaveSystem.cs` (persistência/restauração no JSON)
- `unity/Assets/Game/Scripts/Parts/MachinePartPickup.cs` (notifica coleta ao `EconomyUpgradeSystem`)
- `unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs` (`Smoke_14`)
- `unity/Assets/Game/Tests/PlayMode/EconomyUpgradeCaptureTest.cs` (captura GATE 6)
- `captures/economia/economia_upgrades_fluxo.mp4`
