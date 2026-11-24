# VARREDURA DE BUGS DE PONTA A PONTA (#106)

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0
**Escopo auditado (estado atual do build):** Boot (`MainMenu`) → Ato 1 (`Caldeiral`, CS01, Loop de Peças, CS02) → Ato 2 (`Caldeiral`, CS03, Anomalias, Ritual de Reparo, Poderes Eco/Estase, Manifestações, Combate v2 + Teach Absorver) → Ato 3 (`SetorExterno`, CS04, Missão "Sinal", Portão Leste → `Epicentro`, Travessia + Puzzle Estase + Void + 2 Waves → Arena Final: Mini-boss Guardião da Fenda + Fragmento de Núcleo + Flash 0.52s + Cartão "Fim do Ato 3" + Auto-Save).
*(Nota honesta: o Ato 4 — desfecho final após o Cartão "Fim do Ato 3" — ainda não foi especificado/criado como cena; toda a campanha jogável atual do Boot ao Fim do Ato 3 foi varrida e validada de ponta a ponta.)*

---

## 1. Matriz de QA Executada

| Rota / Caso de Teste | Resultado | Prova |
|---|---|---|
| Boot (`MainMenu`) → Opções (5 volumes PlayerPrefs) → Novo Jogo | **PASS** | `MenuFlowValidationAndCaptureTest` + `AudioPassValidationAndCaptureTest` |
| Skip duplo rápido em `CS01` + `SaveSystem.Save` imediato | **PASS** | `EndToEndBugSweepAuditTest` |
| Pause durante `CS01`, `CS03`, `CS04`, Diálogo e Knockdown | **PASS (fix P0-02)** | `PauseMenuController.CanOpenPauseNow()` retorna `false` |
| Estase ativa + `Time.timeScale = 0.03f` (parry freeze) durante troca de cena | **PASS (fix P0-01)** | `SceneTransitionSafetyGuard` restaura `Time.timeScale = 1.000f` e limpa Estase + Tokens |
| Ritual de Reparo cancelado no meio + retomada | **PASS** | `RepairSystemValidationAndCaptureTest` |
| Missão "Sinal" interrompida e retomada + Portão Leste → `Epicentro` | **PASS** | `SignalQuestValidationAndCaptureTest` + `EastGateInteractable` |
| Queda no vazio no Epicentro → Respawn no checkpoint da seção | **PASS** | `EpicenterValidationAndCaptureTest` |
| Bater no trigger do Boss durante `Knockdown` no mesmo frame | **PASS (fix P0-03)** | `Boss` permanece `Dormant` até respawn limpo |
| Retry limpo no Boss + Fase 2 + Vitória + Flash 0.52s + Cartão Fim Ato 3 | **PASS** | `RiftGuardianBossValidationAndCaptureTest` |
| Console limpo em toda a rota + Compilação | **PASS (0 warnings, 0 errors)** | Logs PlayMode 6/6 suítes |

---

## 2. Bugs P0/P1 Encontrados e Consertados

### BUG-P0-01 — `Time.timeScale`, Estase e Tokens de IA vazando em transição de cena
- **Reprodução:** Acionar `Estase` (`WorldTimeScale = 0.50f`) ou `ABSORVER` (`Time.timeScale = 0.03f` durante `RunParryFreeze`) e disparar `SceneManager.LoadScene` antes do término do coroutine deixava `Time.timeScale` travado na cena seguinte.
- **Correção:** Criado [SceneTransitionSafetyGuard.cs](unity/Assets/Game/Scripts/Save/SceneTransitionSafetyGuard.cs) (`BeforeSceneLoad` → `SceneManager.sceneLoaded`) restaurando `Time.timeScale = 1f`, `AudioListener.pause = false`, `StasisFieldSystem.ClearAll()` e `CombatGroupCoordinator.ResetAll()`, além de guard no `OnDestroy()` de `ParryAndLockOnController`.
- **Prova:** `EndToEndBugSweepAuditTest` simula `Estase` + `Time.timeScale = 0.03f` ao carregar `SetorExterno` e verifica `1.000f` exato.

### BUG-P0-02 — Pause permitido durante `CS03`, `CS04` e `Knockdown`
- **Reprodução:** `PauseMenuController.CanOpenPauseNow()` checava apenas `_cs01` e `_cs02`, permitindo pausar durante `CS03`, `CS04` ou durante a rotina de knockdown/respawn.
- **Correção:** `PauseMenuController.cs` agora cacheia `_cs03`, `_cs04` e bloqueia `CanOpenPauseNow()` durante todas as cutscenes (`CS01–CS04`), diálogo e `PlayerHealthController.IsKnockedDown`.
- **Prova:** `EndToEndBugSweepAuditTest` + `MenuFlowValidationAndCaptureTest`.

### BUG-P0-03 — Trigger do Boss disparando com Dez em Knockdown no mesmo frame
- **Reprodução:** `EpicenterArenaBossStub.Update()` e `RiftGuardianBossController.StartBossFight()` não verificavam `PlayerHealthController.IsKnockedDown`, iniciando o boss com o jogador caído.
- **Correção:** Adicionado guard `IsKnockedDown` em ambos; a luta só inicia após o respawn limpo.
- **Prova:** `EndToEndBugSweepAuditTest` posiciona o Dez em knockdown no centro da arena e valida `Boss.State == Dormant`, iniciando normalmente após respawn.

---

## 3. Lista P2+ (Pequenos / Estéticos — Para Decisão do Bruno, NÃO Alterados)
1. **Música do Boss (`RiftGuardianBossController`):** usa `caldeiral_combat_contratempo.ogg` com `pitch = 0.86` (marcado como TEMP); vale compor tema exclusivo do Guardião na passada de polimento final.
2. **Ícones dos Slots de Poderes (`PowersHUD_Canvas`):** atualmente usam texto + barra colorida limpa; podem receber ícones vetoriais dedicados (ampulheta / silhueta) no pass de UI.
3. **Indicador 3D de Lock-On (`ParryAndLockOnController`):** lock-on suave já funciona logicamente e alterna alvos (`[V]`); pode ganhar um retículo sutil sobre o alvo selecionado.
4. **Ato 4 (Finalização da Campanha):** após o Cartão "Fim do Ato 3", criar a cena final *"O Último Reparo"* conectando a semente do flash de holograma da oficina + esposa.

---

## 4. Checklist de QA Permanente (Rotina para Próximas Tasks)
- [ ] Compilação com **0 warnings CS** e **0 errors CS**.
- [ ] Skip duplo rápido em todas as cutscenes + save imediato sem erro.
- [ ] Transição de cena durante Estase/Parry Freeze → `Time.timeScale == 1.000f` e `WorldTimeScale == 1.000f`.
- [ ] Pause bloqueado em todas as cutscenes, diálogos e knockdown.
- [ ] Regressão das suítes: `CollisionSystem`, `DesignBible`, `RepairSystem`, `SignalQuest`, `MenuFlow`, `AudioPass`, `DezPowers`, `Manifestacoes`, `Epicenter`, `RiftGuardianBoss`, `CombatV2`, `EndToEndBugSweep`.

---

## 5. Evidência & Conformidade

- **MP4 de Evidência:** `captures/bug_sweep/bug_sweep_rota_limpa_e_fixes_1080p.mp4` (`1920x1080 H.264 + AAC 44.1kHz stereo`).
- **Modelos 3D novos:** Nenhum (escopo fechado em correção de bugs P0/P1).

### CONFORMIDADE — DESIGN_BIBLE.md v1.0
- **Seção 3 (Estabilidade & Métricas):** CONFORME — `Time.timeScale` e `StasisFieldSystem.WorldTimeScale` garantidos em `1.000f` em todas as transições; console com `0` erros e `0` warnings.
- **Seção 4 (Cutscenes & Controle):** CONFORME — skip duplo seguro e pause bloqueado em `CS01–CS04` e knockdown.
- **Seção 8 (Save/Load & Resiliência):** CONFORME — `SaveSystem` JSON seguro após skip duplo e no encerramento do Ato 3; `PlayerPrefs` isolado apenas para opções.
