# ANTI_SOFTLOCK — Completabilidade dos 4 Atos (CONCERTO)

Auditoria de completabilidade ponta a ponta dos 4 atos de CONCERTO, correção de softlocks encontrados (cada um em commit próprio) e teste de regressão de morte/respawn.

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Respawn (`≤3s`):** `PlayerHealthController.RunKnockdownAndRespawn()` restaura HP, `Time.timeScale = 1.0`, objetivo da missão, portas destravadas e peças já coletadas no último checkpoint válido da cena atual.
- **Save JSON:** Portas de progressão (`sectorDoorUnlocked`, `eastGateUnlocked`) agora leem diretamente do `SaveSystem.CachedState` (JSON versionado), eliminando softlocks após migração/limpeza de `PlayerPrefs`.
- **Modelos 3D TripoSG criados nesta tarefa:** `0 modelos criados`.

---

## 2. Softlocks Encontrados e Corrigidos (GATE 4 — 1 Commit Próprio por Fix)

### `SL-001` — Portas de progressão (`SectorDoor` e `EastGate`) retrancando após migração/reload do Save JSON
- **Sintoma:** Após `SaveSystem` migrar e limpar chaves legadas de `PlayerPrefs`, `SectorDoorInteractable.IsSectorUnlocked` e `EastGateInteractable.IsEastGateUnlocked` retornavam `false`, trancando permanentemente o avanço para o Ato 3 (`SetorExterno`) e Ato 4 (`Epicentro`).
- **Causa Raiz:** Ambas as propriedades liam exclusivamente `PlayerPrefs.GetInt(...)` sem consultar `SaveSystem.CachedState` (`sectorDoorUnlocked` / `eastGateUnlocked`).
- **Fix (commit próprio `e871d3c`):** Atualizados `SectorDoorInteractable.cs` e `EastGateInteractable.cs` para consultar o estado em sessão + `SaveSystem.CachedState` (JSON) e sincronizar a gravação.
- **Teste de Regressão:** `Smoke_18_DeathRespawnPreservesMissionDoorsAndParts` + `AntiSoftlockFourActsCaptureTest`.

### `SL-002` — Loop infinito de morte no vazio no `Epicentro` e trava em slow-motion após morte durante hitstop
- **Sintoma:** Morrer em `Epicentro` antes de ativar um checkpoint podia teleportar o Dez para a coordenada padrão da oficina `(-28.5, 0.05, -18.5)` (no vazio fora das plataformas do `Epicentro`), e morrer durante hitstop (`Time.timeScale = 0.04f`) mantinha o jogo em câmera lenta permanente após respawn.
- **Causa Raiz:** `PlayerHealthController.Start()` só inicializava `respawnPosition = transform.position` para `Caldeiral` e `SetorExterno`, omitindo `Epicentro`; e `RunKnockdownAndRespawn()` não forçava `Time.timeScale = 1f` nem consultava `EpicenterCheckpointAndVoidZone.Instance.ActiveCheckpointPos`.
- **Fix (commit próprio `2071d58`):** `PlayerHealthController.cs` agora inicializa `respawnPosition = transform.position` em qualquer cena, consulta `EpicenterCheckpointAndVoidZone.Instance.ActiveCheckpointPos` no `Epicentro` e garante `Time.timeScale = 1f` no respawn.
- **Teste de Regressão:** `Smoke_18_DeathRespawnPreservesMissionDoorsAndParts` + `AntiSoftlockFourActsCaptureTest`.

---

## 3. Checklist Manual por Ato (GATE 3)

| Ato / Cena | Peças alcançáveis? | Portas abrem e permanecem abertas? | Lutas vencíveis com ferramentas daquele ponto? | Respawn mantém estado intacto? |
|---|---|---|---|---|
| **Ato 1 — `Caldeiral` (Oficina)** | ✅ 5/5 peças em `parts_loop_config.json` (altura ≤ `1.28m`, alcançáveis por pulo/coyote) | ✅ Máquina ativa após 5/5; bancada de upgrades acessível | ✅ Treino seguro (`ParryTeachStation`, dano 0) + Onboarding 5 passos | ✅ `Smoke_18` verificado |
| **Ato 2 — `Caldeiral` (Anomalias & Reparo)** | ✅ Anomalias da oficina acessíveis no chão/bancadas | ✅ `SectorDoor` destrava e persiste via JSON (`SL-001`) | ✅ Ferramenta Multi-Uso (Concertar/Combate) + Eco/Estase disponíveis | ✅ `Smoke_18` verificado |
| **Ato 3 — `SetorExterno` (Missão Sinal)** | ✅ Alimentadores + Antena + células acessíveis | ✅ `EastGate` destrava ao concluir Sinal e persiste via JSON (`SL-001`) | ✅ Manifestações (`Estilhaço`, `Flutuante`, `Égide`) vencíveis com Combate v2 + Eco/Estase | ✅ Checkpoint `cp_setor_externo_entrada` ativo |
| **Ato 4 — `Epicentro` (Travessia + Boss)** | ✅ Desvio secreto recompensado (`x=71, z=+6.8`) + Fragmento de Núcleo pós-Boss | ✅ 5 checkpoints V2 (`≤30s`), pórtico dourado aberto para a Arena | ✅ Guardião da Fenda vencível com Eco Temporal + Estase + Parry + Combate v2 | ✅ Respawn sincronizado com `EpicenterCheckpointAndVoidZone` (`SL-002`) |

---

## 4. Evidências e Testes (GATES 1, 5 e 7)

- **MP4 dos 4 Atos + Prova de Respawn (GATES 1 & 4):** `captures/anti_softlock/anti_softlock_4_atos.mp4` (`1920x1080`, H.264, `25.0s`).
- **Teste Smoke Novo (GATE 5):** `Smoke_18_DeathRespawnPreservesMissionDoorsAndParts` em `unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs`.
- **Suíte `Tests/SmokeTest/` (GATE 7):** **18/18 PASSED** (`0 failed`, `0 skipped`, `0 warnings CS`, `0 errors CS`) + `AntiSoftlockFourActsCaptureTest` (**19/19 PASSED**).
