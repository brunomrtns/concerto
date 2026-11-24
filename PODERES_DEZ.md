# PODERES DO DEZ — ECO TEMPORAL E ESTASE (#93)

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0
**Pré-requisitos verificados:** `DezLocomotionController` + `MultiToolController` + `Cutscene03ReturnController` presentes no repo.

---

## 1. Resumo da Implementação

- **`DezPowersConfig.asset`** (`unity/Assets/Game/Config/DezPowersConfig.asset`): todos os parâmetros 100% data-driven (zero hardcode).
- **`DezPowersController.cs`** (`unity/Assets/Game/Scripts/Powers/DezPowersController.cs`): ligado ao `Player_Dez_Placeholder` em `Caldeiral.unity` e `SetorExterno.unity`.
  - **Desbloqueio pós-Cutscene 3:** escuta `Cutscene03ReturnController.OnCutscene03Ended` e lê a flag `CONCERTO_CS03_PLAYED` / `CONCERTO_POWERS_UNLOCKED`, exibindo a hint curta *"O tempo vazou pelos seus dedos... [ALT/C] Eco Temporal | [R] Estase"* sem explicar a origem ao jogador.
  - **Bloqueio de regressão:** desabilitado automaticamente durante cutscenes (`CS01–CS04`), diálogos (`DialogueController`), pause (`Time.timeScale == 0`) e ritual de reparo (`RepairRitualController`).
- **`Eco Temporal` (`[Alt]` ou `[C]`):**
  - Dash curto evasivo (`0.24s`, `12 m/s`) com **i-frames reais de `0.30s`** (`IsInvulnerable == true`, provado via `TryReceiveTestDamage`).
  - Rastro de **4 afterimages cianos** (`BakeMesh` leve do `SkinnedMeshRenderer` do Dez) com fade-out em `0.36s`.
  - Cooldown `3.0s`, som reverso ("ré") `sfx_eco_temporal_reverse_TEMP.wav` (CC0 procedural) roteado no bus `SFX`.
- **`Estase` (`[R]`):**
  - Pulsação radial local (`raio 15m`, duração `3.0s`, cooldown `10.0s`) via `StasisFieldSystem` (**timeScale local por área/objeto, NÃO `Time.timeScale` global**).
  - Desacelera objetos/inimigos/projéteis no raio para **50% (`0.50f`)** sem desacelerar o Dez (`DezTimeScale == 1.000f` sempre).
  - Ao encerrar (ou ao entrar em cutscene/destruir cena), restaura `WorldTimeScale` **EXATO para `1.000f`** com log explícito.
  - Som de pulsação grave `sfx_estase_pulse_TEMP.wav` (CC0 procedural) roteado no bus `SFX`.
- **HUD Discreto (`PowersHUD_Canvas`):** 2 slots no canto inferior direito (`ECO [ALT/C]` e `ESTASE [R]`) com barra de cooldown e dirty-check (zero alocação por frame).

---

## 2. API e Extensibilidade (Como Adicionar um 3º Poder)

1. Crie uma subclasse de `DezPowerBase` (`unity/Assets/Game/Scripts/Powers/DezPowerBase.cs`) implementando `PowerId`, `DisplayName`, `PrimaryKey`, `CooldownDuration` e `OnActivate(DezPowersController owner)`.
2. Registre a nova instância em `_powers` no `Awake()` de `DezPowersController` e adicione o slot correspondente no HUD.

### Uso Futuro em Combate (#94 / #103 / #109) e Puzzles (#102)
- **Combate:** Inimigos (`Estilhaço`, `Flutuante`, `Guardião`) e projéteis implementam `ITimeScalable` e se registram em `StasisFieldSystem.Register(this)` (ou consultam `StasisFieldSystem.GetTimeScaleAt(transform.position)`), sendo desacelerados a `50%` na Estase; o dano ao Dez consulta `DezPowersController.Instance.IsInvulnerable` para respeitar os i-frames do Eco Temporal.
- **Puzzles:** Plataformas móveis, pêndulos e portas temporizadas usam `StasisAffectedObject` (ou `ITimeScalable`) para serem segurados/desacelerados localmente pela Estase.

---

## 3. Modelos 3D (TripoSG) & Áudio Novo

| Asset | Tipo | Origem | Licença |
|---|---|---|---|
| Modelos 3D novos | N/A | Nenhum necessário (afterimage usa `BakeMesh` da malha TripoSG do Dez; Estase usa VFX leve) | — |
| `sfx_eco_temporal_reverse_TEMP.wav` | SFX Eco ("ré") | Síntese procedural original (`generate_powers_sfx.py`) | CC0 1.0 |
| `sfx_estase_pulse_TEMP.wav` | SFX Estase | Síntese procedural original (`generate_powers_sfx.py`) | CC0 1.0 |

---

## 4. Como Testar & Evidências (Gates)

- **Compilação:** `0 warnings CS`, `0 errors CS`.
- **Teste PlayMode:** `DezPowersValidationAndCaptureTest.ValidateEcoTemporalEstasePlaygroundAndCapture` — **PASSED (`7.59s`)**:
  - `afterimages=4`, `iFramesBlocked=1`, `worldDuring=0.50`, `worldAfter=1.000`, `dezDist=8.19m` (Dez livre durante Estase), bloqueio durante CS01 verificado.
- **MP4 de Evidência:** `captures/powers/dez_powers_eco_estase_1080p.mp4` (`1920x1080 H.264 + AAC 44.1kHz stereo`).

---

## CONFORMIDADE — DESIGN_BIBLE.md v1.0
- **Seção 2 (Character & Controls):** CONFORME — Eco Temporal (`0.30s` i-frames, `0.24s` dash) e Estase (`0.50x` local por `3.0s`) respondem no mesmo frame (`< 67ms`), sem travar o movimento do Dez e 100% tunáveis em `DezPowersConfig`.
- **Seção 3 (Métricas & Estabilidade):** CONFORME — `WorldTimeScale` garantido a retornar EXATO `1.000f`; zero alocação por frame no HUD.
- **Seção 4 (Cutscenes):** CONFORME — poderes bloqueados e Estase limpa automaticamente ao iniciar qualquer cutscene (`CS01–CS04`).
- **Seção 9 (Juice):** CONFORME — Eco tem afterimage ciano + som reverso; Estase tem pulso visual + som grave temporal; ambos roteados no bus `SFX` do `ConcertoMasterMixer`.
