# GAMEFEEL — O "Juice" do CONCERTO

Pass de feedback tátil de impacto, dano, morte de inimigos, movimento e coleta em CONCERTO, mantendo 100% do balanceamento e dano originais intactos.

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

| Parâmetro (`DESIGN_BIBLE.md`) | Alvo obrigatório | Valores implementados (`GameFeelJuiceController.cs`) | Status |
|---|---|---|---|
| **Hitstop** | `50–100ms` | `65ms` (golpe leve), `85ms` (golpe pesado), `95ms` (morte de inimigo) — com clamp `[50ms, 100ms]` | **CONFORME** |
| **Camera shake (decay)** | `<300ms` | `180ms` (golpe), `220ms` (dano direcional), `240ms` (morte) — com clamp `≤280ms` + respeito a `ReduceCameraShake` | **CONFORME** |
| **Telegraph de inimigo** | `≥300ms` | Inalterado (`ManifestationEnemyBase` / `#109` preservados) | **CONFORME** |
| **Hitch / alocações** | `≤2 frames` | Partículas em pool (`Emit()`), `AudioClip` sintetizado no `Awake`, zero `Instantiate` por golpe | **CONFORME** |
| **Escopo & Balanceamento** | Zero alteração de dano/balanceamento | Nenhum número de HP, dano ou cooldown foi alterado | **CONFORME** |
| **Modelos 3D TripoSG** | Nenhum modelo novo | `0 modelos criados` | **CONFORME** |

---

## 2. Tabela de Feedbacks (Onde, Valor e Por Quê)

| Gate | Evento / Onde dispara | Feedback implementado | Valores exatos | Por quê (Game Feel) |
|---|---|---|---|---|
| **GATE 1** | **Golpe do Dez** (`CombatToolModule.cs` → `TriggerDezAttackHit`) | Hitstop + micro screen shake + partícula de impacto + som sincronizados no **MESMO frame** (`Time.frameCount`) | Hitstop `65ms` (leve) / `85ms` (pesado); shake `0.085`–`0.14` com decay `180ms`; `12`–`18` faíscas + punch SFX | Confirma conexão física imediata e dá peso perceptível ao acerto sem travar o combo. |
| **GATE 2** | **Dez recebe dano não-fatal** (`PlayerHealthController.cs` → `TriggerPlayerDamaged`) | Flash emissivo no Dez + shake direcional afastando do atacante + pausa de input curta | Flash `140ms`; shake direcional `0.19` com decay `220ms`; pausa de input `90ms` (distinta do knockdown/morte) | Comunica direção do perigo e impacto real sem jamais confundir dano leve com morte. |
| **GATE 3** | **Inimigo morto** (`ManifestationEnemyBase.cs` → `TriggerEnemyKilled`) | Knockback + hitstop maior + shake + rajada de faíscas | Knockback `0.55m`; hitstop `95ms`; shake `0.16` com decay `240ms`; `24` faíscas + som grave | Marca claramente a finalização do inimigo e recompensa o golpe final sem quebrar o ritmo. |
| **GATE 4** | **Pulo e Aterrissagem** (`GameFeelJuiceController.Update` / `TriggerJumpStretch` / `TriggerLandingSquashAndDust`) | Squash & stretch sutil na escala do rig (retorno exato a `_dezRigBaseScale`) + poeira no chão + SFX suave | Stretch `(0.91, 1.12, 0.91)` no pulo; squash `(1.13, 0.86, 1.13)` no pouso; retorno `170ms`; `14` partículas de poeira | Dá vida e massa ao movimento do Dez sem deformar permanentemente a malha 3D. |
| **GATE 5** | **Coletar item** (`MachinePartPickup.cs` → `TriggerItemPickupJuice`) | Feedback em **3 camadas**: (1) visual no ponto do item, (2) chime harmônico, (3) "clim" animado no HUD | `22` partículas douradas/ciano + chime `660→990Hz` + pill HUD com pop `1.18x → 1.0x` por `1.45s` | Torna toda coleta imediatamente satisfatória e legível mesmo com o olhar focado no cenário. |

---

## 3. Evidências e Testes Automatizados (GATES 6 e 8)

- **MP4 Lado a Lado (GATE 6):** `captures/gamefeel/gamefeel_antes_depois_60s.mp4` (`1920x1080`, H.264, `60.0s` — **60s ANTES sem juice** à esquerda vs **60s DEPOIS com juice** à direita, cobrindo todos os 5 gates).
- **Teste Smoke Novo (`Smoke_13_GameFeelJuiceMatchesDesignBible`):** em `unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs`, valida hitstop `50–100ms`, shake decay `<300ms`, sincronização no mesmo frame de hitstop + partícula + som, dano não-fatal, morte de inimigo sem travar `Time.timeScale`, squash/stretch + poeira e 3 camadas na coleta.
- **Suíte `Tests/SmokeTest/`:** **13/13 PASSED** (`0 failed`, `0 skipped`, `0 warnings CS`, `0 errors CS`) + `GameFeelBeforeAfterCaptureTest` (**14/14 PASSED**).
