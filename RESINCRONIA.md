# RE-SINCRONIA — SISTEMA DE DOIS ESTADOS (GDD 04 §1)

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0 · [docs/gdd/04-mecanicas.md](docs/gdd/04-mecanicas.md) §1 · [docs/gdd/05-level-design.md](docs/gdd/05-level-design.md) §2
**Data:** 2026-11-25
**Nota sobre fila:** `CANON.md` (Prompt 34) ainda não existe no repositório; a implementação seguiu estritamente `docs/DESIGN_BIBLE.md`, `docs/gdd/04-mecanicas.md` §1 e `docs/gdd/05-level-design.md` §2 sem alterar conteúdo existente fora da zona demo.

---

## 1. Declaração de Conformidade (`DESIGN_BIBLE.md` + `GDD 04 §1` + `GDD 05 §2`)

- **Regra de Ouro (GDD 05 §2 — Fairness entre estados):** CONFORME — Todos os objetos de dois estados (`ResyncDualStateObject`) preservam **geometria idêntica** (`MeshFilter.sharedMesh` e `Transform` nunca mudam entre `Desincronizado` e `Restaurado`); somente material e colisão mudam.
- **Regras da Re-Sincronia (GDD 04 §1):** CONFORME — Alterna apenas a bolha da zona (`bubbleRadius = 14m`) em `0,8s` com onda visual em anel, `0,8s` de escrita fantasma (*wireframe*), atordoamento de inimigos no raio por `1,0s` e cooldown de `6,0s`.
- **Persistência (DESIGN_BIBLE §8):** CONFORME — Estado de cada zona persistido exclusivamente em JSON versionado (`SaveGameData.resyncZoneStates`, zero `PlayerPrefs`).
- **Qualidade & Estabilidade:** CONFORME — `0 warnings CS`, `0 errors CS`, suíte `Tests/SmokeTest/` **22/22 VERDE** (incluindo `Smoke_22_ResyncDualStateGhostStunCooldownAndSave`).

---

## 2. Números Implementados vs GDD (`04-mecanicas.md` §1)

| Parâmetro | GDD `04-mecanicas.md` §1 | Implementado (`ResyncZoneController.cs`) | Conformidade |
|---|---|---|---|
| **Duração da troca (onda em anel)** | `0,8 s` | `TransitionDurationSec = 0.8f` | **EXATO** |
| **Escrita fantasma (wireframe)** | `0,8 s` antes da troca | `BeginGhostPreview` ativo por `0.8f` (`LineRenderer` 12 arestas) | **EXATO** |
| **Atordoamento de inimigos no raio** | `1,0 s` | `EnemyStunDurationSec = 1.0f` (`ApplyParryStagger(1.0f)`) | **EXATO** |
| **Cooldown entre ativações** | `6,0 s` | `CooldownSec = 6.0f` | **EXATO** |
| **Escopo da alternância** | Somente a bolha da zona | `bubbleRadius = 14.0f` por `ResyncZoneController` (`zoneId` único) | **EXATO** |
| **Botão de ativação** | Botão do Bis (`Y` / Gamepad `Y`) | `KeyCode.Y` / `KeyCode.JoystickButton3` no alcance do Nó (`≤ 4m`) | **EXATO** |

---

## 3. Zona de Demonstração (`caldeiral_demo_resync`)

Posicionada no pátio aberto de Z3 em `Caldeiral` (`(11.5, 0.0, -14.5)`), sem interferir em nenhuma rota existente:
1. **Nó de Re-Sincronia (`ResyncDemo_Node`):** ponto interativo central da bolha (`bubbleRadius = 14m`).
2. **Uso 1 — Feixe de Luz (`ResyncDemo_LightBeamBarrier`, `DualStateMode.PassableOnlyInRestored`):** bloqueia em `Desincronizado` (`Collider.enabled = true`, âmbar-vermelho); fica **atravessável** em `Restaurado` (`Collider.enabled = false`, translúcido ciano) — **geometria idêntica**.
3. **Uso 2 — Plataforma Temporal (`ResyncDemo_DesyncPlatform`, `DualStateMode.SolidOnlyInDesync`):** **sólida/caminhável** apenas em `Desincronizado`; desmaterializa em `Restaurado` — **geometria idêntica**.

### Evidências
- **MP4 (2 alternâncias completas, wireframe fantasma, onda em anel e atordoamento):** [captures/resincronia/resincronia_demo.mp4](captures/resincronia/resincronia_demo.mp4)
- **Smoke Test:** `Smoke_22_ResyncDualStateGhostStunCooldownAndSave` em [ConcertoSmokeTests.cs](unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs).

---

## 4. Receita para Novas Zonas Aderirem ao Sistema

1. Crie um `GameObject` raiz na cena e adicione **`ResyncZoneController`** ([ResyncZoneController.cs](unity/Assets/Game/Scripts/Resync/ResyncZoneController.cs)).
2. Defina um **`zoneId`** único (ex.: `"vitralha_estacao_luz"`), o **`bubbleRadius`** e atribua o **`nodeAnchor`** (instanciando o prefab TripoSG `Assets/Game/Prefabs/Fab3D/c5_prop_no_resincronia.prefab`).
3. Em cada peça da zona que alterna entre estados, adicione **`ResyncDualStateObject`** ([ResyncDualStateObject.cs](unity/Assets/Game/Scripts/Resync/ResyncDualStateObject.cs)), mantendo o **mesmo `MeshFilter`/`Collider`** e configurando:
   - `mode`: `SolidOnlyInDesync`, `PassableOnlyInRestored` ou `VisualSwapOnly`;
   - `desyncMaterial` e `restoredMaterial` (e `ghostPassableMaterial` opcional).
4. Arraste os `ResyncDualStateObject` para a lista `managedObjects` do `ResyncZoneController`.
5. **Persistência automática:** o `SaveSystem` captura e restaura automaticamente todas as zonas ativas em `SaveGameData.resyncZoneStates` por `zoneId` — zero código extra necessário.

---

## 5. Lista de Modelos 3D TripoSG

- **0 modelos 3D novos criados nesta tarefa.**
- **Asset TripoSG existente reutilizável para o Nó de Re-Sincronia:** `Assets/Game/Prefabs/Fab3D/c5_prop_no_resincronia.prefab` (`Assets/Game/Art/Generated/c5_prop_no_resincronia/`, gerado anteriormente no pipeline TripoSG/Fab3D do projeto).
