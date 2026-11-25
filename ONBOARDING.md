# ONBOARDING — Primeiros 15 Minutos (CONCERTO)

Sequência diegética de abertura e ensino jogável de CONCERTO, iniciada automaticamente ao selecionar **"Novo Jogo"** no Menu Principal (`MainMenu` → `Caldeiral`), cobrindo a progressão canônica até o primeiro uso dos poderes temporais (**Eco Temporal** e **Estase**).

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

| Item da `DESIGN_BIBLE.md` | Alvo | Implementado / Validado |
|---|---|---|
| Cutscene de abertura (`CS01`) | `≤90s`, skip limpo após `3s` | **CONFORME** — `CutsceneManager` (`≤60s`, skip após `3s`), onboarding aguarda fim/skip limpo antes de iniciar. |
| Skip de onboarding | Skip por passo após `3s` + skip total | **CONFORME** — `[Tab]` / Gamepad `Back` pula o passo atual após `3.0s`; `[Segure Esc/Backspace]` (`0.85s`) pula a sequência inteira limpando barreiras e salvando estado. |
| Telegraph visual | `≥300ms` antes do desafio | **CONFORME** — `TelegraphDurationSec = 0.45f` (`450ms`) com anel emissivo âmbar→ciano e chime em cada uma das 5 estações. |
| Responsividade (`input → ação`) | `<100ms` (ideal `~67ms` a 60fps) | **CONFORME** — leitura direta em `Update` / eventos síncronos (`DezLocomotionController`, `IInteractable`, `DezPowersController`). |
| Pulo com Coyote / Buffer | Coyote `~100ms` / Buffer `~150ms` | **CONFORME** — `DezMovementConfig` (`coyoteTimeSec = 0.10f`, `jumpBufferSec = 0.15f`) validado em `Smoke_05` e usado no Passo 2 (`JumpPlateau` `0.62m > stepOffset 0.36m`). |
| Onboarding diegético | Zero paredes de texto; máx. 1 linha | **CONFORME** — `CurrentPromptLineCount <= 1` validado em teste; cada estação bloqueia passagem fisicamente até o jogador executar a ação. |
| Persistência de estado | JSON versionado em `persistentDataPath`, **nunca `PlayerPrefs`** | **CONFORME** — `SaveGameData` (`onboardingCompleted`, `onboardingSkipped`, `onboardingStepIndex`, `completedOnboardingSteps`) gravado via `SaveSystem.Save()`. |
| Suporte teclado + gamepad | 100% dos fluxos em ambos | **CONFORME** — prompts e bindings para Teclado (`WASD`, `Espaço`, `E`, `Alt/C`, `R`, `Tab`, `Esc`) e Gamepad (`Analógico`, `A`, `X`, `RB`, `Back`). |

---

## 2. Tabela de Passos do Onboarding (Ordem Canônica — GATE 1)

Cada passo possui **telegraph visual de `450ms` (`≥300ms`)**, **máximo 1 linha de texto** e uma **barreira holográfica física (`BoxCollider`)** que impede avançar antes de aprender (ou pular explicitamente após `3s`).

| # | Passo (`OnboardingStep`) | O que ensina | Como ensina (diegético) | Prompt (1 linha) | Tempo medido (PlayMode) |
|---|---|---|---|---|---|
| 1 | `Move` | Locomoção 3ª pessoa | Trilha luminosa no chão até o farol ciano no corredor da oficina (`+X`); barreira 1 dissolve ao alcançar. | `Caminhe ate o sinal luminoso  [WASD / Analogico]` | `1.59s` |
| 2 | `Jump` | Pulo + Coyote (`100ms`) + Buffer (`150ms`) | Platô físico elevado (`0.62m`, acima do `stepOffset = 0.36m`) exigindo pulo real; barreira 2 dissolve ao subir/saltar. | `Pule sobre o plato elevado  [Espaco / A]` | `1.63s` |
| 3 | `Interact` | Interação contextual (`IInteractable`) | Terminal diegético (`OnboardingEnergyTerminal`) com núcleo brilhante; `[E]`/`[X]` energiza o núcleo e dissolve barreira 3. | `Ative o terminal de energia  [E / X]` | `2.14s` |
| 4 | `EcoTemporal` | Poder #93 — Eco Temporal (i-frames + afterimage) | Arco de pilares ciano com energia temporal pulsando; desbloqueia poderes e dissolve barreira 4 ao ativar Eco (`[Alt]`/`[C]`). | `Atravesse a energia com Eco Temporal  [Alt / C]` | `0.92s` |
| 5 | `Estase` | Poder #93 — Estase (desaceleração 50% por 3s) | Porta temporal oscilando rapidamente (`StasisGate_Blade`); ativar Estase (`[R]`/`[RB]`) desacelera, trava aberta e conclui o onboarding. | `Desacelere a porta temporal com Estase  [R / RB]` | `1.09s` |

- **Tempo total medido dos 5 passos (PlayMode automatizado):** `7.37s` (fluxo completo do jogador com menu + cutscene `CS01 ≤90s` + exploração inicial permanece confortavelmente dentro da janela dos primeiros 15 minutos).

---

## 3. Modelos 3D TripoSG Criados

- **`0 modelos 3D criados`** — Toda a sinalização diegética (anéis de telegraph, platô, pedestal de energia, pilares e porta temporal) utiliza geometria procedural com materiais URP Lit / transparente emissivo, respeitando o escopo fechado e a regra TripoSG-only.

---

## 4. Evidências e Testes Automatizados

- **MP4 (GATE 3):** `captures/onboarding/onboarding_completo.mp4` (`1920x1080`, H.264, `11.50s`, `138 frames`) — mostra do zero: `MainMenu` → "Novo Jogo" → `CS01` → Passos 1 a 5 até Eco Temporal e Estase ativados.
- **Teste Smoke Novo (GATE 4):** `Smoke_09_NewGameOnboardingUpdatesSaveState` em `unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs` — inicia pelo `MainMenu` ("Novo Jogo"), conclui os 5 passos em ordem, valida barreira física, prompt ≤1 linha e confirma `onboardingCompleted = true`, `onboardingStepIndex = 5` e `completedOnboardingSteps.Count = 5` no JSON versionado.
- **Resultado da Suíte `Tests/SmokeTest/` (GATE 6):** **9/9 PASSED** (`0 failed`, `0 skipped`, `0 warnings CS`, `0 errors CS`) + `OnboardingFullFlowCaptureTest` (**10/10 PASSED**).

---

## 5. Como Reverter

Para reverter integralmente esta entrega sem afetar nenhuma fase posterior:

```bash
git revert <commit-hash-desta-tarefa>
```

Arquivos tocados exclusivamente por esta tarefa:
- `unity/Assets/Game/Scripts/Onboarding/OnboardingSequenceController.cs` (novo)
- `unity/Assets/Game/Scripts/Save/SaveGameData.cs` (campos de onboarding no JSON)
- `unity/Assets/Game/Scripts/Save/SaveSystem.cs` (persistência/restauração de onboarding + flag `IsPendingNewGameOnboarding`)
- `unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs` (`Smoke_09_NewGameOnboardingUpdatesSaveState`)
- `unity/Assets/Game/Tests/PlayMode/OnboardingFullFlowCaptureTest.cs` (captura GATE 3)
- `captures/onboarding/onboarding_completo.mp4`
- `ONBOARDING.md`
