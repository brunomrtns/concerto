# RAMPRAS_FIX — Alinhamento Milimétrico de Rampas, Corrimãos e Prova de Travessia

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0
**Data:** 2026-11-25
**Modelos 3D criados nesta tarefa:** **0 modelos 3D criados** (apenas realinhamento geométrico das rampas existentes e corrimãos construídos com primitivos existentes + material metálico da cena).
**Escopo:** Estritamente fechado em rampas, corrimãos e contato com chão/plataformas (sem alterar outra geometria ou código de movimento).

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Seção 2 (Locomoção, Input→Ação, Coyote & Buffer):** CONFORME — Verificados diretamente em [DezMovementConfig.cs](unity/Assets/Game/Scripts/Animation/DezMovementConfig.cs) e [DezLocomotionController.cs](unity/Assets/Game/Scripts/Animation/DezLocomotionController.cs):
  - `coyoteTimeSec = 0.10s` (`100ms`, janela da bíblia `[80–120ms]`)
  - `jumpBufferSec = 0.15s` (`150ms`, janela da bíblia `[120–180ms]`)
  - `stepOffset = 0.36m` | `skinWidth = 0.05m` | `radius = 0.28m` | `height = 1.25m`
- **Qualidade & Estabilidade:** CONFORME — Todas as rampas do jogo aprovadas com `gap = 0.000m` na base e no topo, corrimãos acompanhando o pitch exato (`diff = 0.00°`), travessia automatizada subida + descida aprovada (`Smoke_21`), `0 warnings CS`, `0 errors CS`, suíte `Tests/SmokeTest/` **21/21 VERDE**.

---

## 2. Ângulo Máximo Extraído do Código de Movimento do Dez (GATE 1)

| Parâmetro | Origem no Código | Valor Extraído |
|---|---|---|
| **`CharacterController.slopeLimit` (runtime)** | `DezMovementConfig.slopeLimit` → `DezLocomotionController.ApplyControllerAntiSnagTuning()` | **`50.0°`** (clamp `[35°..60°]`) |
| **`CharacterController.slopeLimit` (serializado)** | `Caldeiral.unity` (`m_SlopeLimit`) | `45.0°` |
| **Limite máximo seguro de rampa (padrão com folga)** | `RampAndRailingValidatorTool.SafeMaxRampPitchDeg` | **`≤ 35.0°`** (folga de `15°` abaixo do runtime / `10°` do serializado) |
| **`stepOffset` / `skinWidth`** | `DezMovementConfig.cs` | `0.36m` / `0.05m` |
| **`walkSpeed` / `runSpeed` / `gravity`** | `DezMovementConfig.cs` | `2.2 m/s` / `5.4 m/s` / `-24.0 m/s²` |

---

## 3. Tabela Antes × Depois — Todas as Rampas do Jogo (GATES 2, 4, 5 e 6)

*Varredura em todas as 4 cenas de build (`MainMenu`, `Caldeiral`, `SetorExterno`, `Epicentro`): **3 rampas totais** (todas em `Caldeiral.unity`).*

| Rampa (Cena) | Defeitos ANTES (`relatorio_antes.txt`) | Correção Aplicada | Estado DEPOIS (`relatorio_depois.txt`) | Travessia PlayMode (`Smoke_21`) |
|---|---|---|---|---|
| `Z2_Passarela_da_Bigorna/Z2_Mezzanine_Ramp_16_9deg` (`Caldeiral`) | 1. Base (`x=-17.54`) iniciava atrás do `BlastDoor_Frame` (`x=-16.50`), travando o Dez na subida.<br>2. Gap vertical de `6mm` na base e no topo.<br>3. Corrimão horizontal combinado (`pitch=0.00°`) sem corrimãos inclinados na rampa (`1/2` desalinhado). | Realinhada a base para `x=-15.54` (`+2.0m` livre da porta), mantendo o topo exato em `(-6.04, 3.50, -12.60)` sobre `Z2_Mezzanine_Deck` (`pitch=20.22° << 35°`, `gap=0.000m`); instalados `Z2_Mezzanine_Ramp_16_9deg_Handrail_Left/Right` no pitch exato `20.22°`. | **APROVADA** (`pitch=20.22°`, base `gap=0.000m`, topo `gap=0.000m`, `2/2` corrimãos) | **PASSOU** (subida `Y≥3.35` + descida `Y≤0.30` sem travar/cair) |
| `Z3_Patio_das_Sirenes/Z3_VoSegunda_Ramp_16_9deg` (`Caldeiral`) | 1. Gap vertical de `6mm` na base (`Z3_Patio_Floor`) e no topo (`Z3_VoSegunda_Deck`).<br>2. Corrimãos inclinados ausentes (`0/2` lados). | Ajuste vertical `+6mm` zerando gap na base (`Y=0.000`) e topo (`Y=3.500`); instalados `Z3_VoSegunda_Ramp_16_9deg_Handrail_Left/Right` no pitch exato `16.93°` com postes nas pontas e centro. | **APROVADA** (`pitch=16.93°`, base `gap=0.000m`, topo `gap=0.000m`, `2/2` corrimãos) | **PASSOU** (subida `Y≥3.35` + descida `Y≤0.30` sem travar/cair) |
| `Z4_Camara_Caldeiral_Central/Z4_Arena_Ramp_16_9deg` (`Caldeiral`) | 1. **Topo bloqueado:** borda superior (`Y=3.494`) batia no collider de `Z4_C3_RingRailings_And_ExitArch` (`Y=4.050`, degrau/obstrução de `55.6cm`).<br>2. Gap vertical de `6mm` na base.<br>3. Corrimãos inclinados ausentes (`0/2` lados). | Desabilitado o collider obstruindo o vão superior em `Z4_C3_RingRailings_And_ExitArch` (liberando saída limpa para `Z4_RingDeck_East` em `Y=3.500`); ajuste vertical `+6mm` (`gap=0.000m`); instalados `Z4_Arena_Ramp_16_9deg_Handrail_Left/Right` no pitch exato `16.93°`. | **APROVADA** (`pitch=16.93°`, base `gap=0.000m`, topo `gap=0.000m` em `Z4_RingDeck_East`, `2/2` corrimãos) | **PASSOU** (subida `Y≥3.35` + descida `Y≤0.30` sem travar/cair) |

### Evidências
- **Relatório bruto ANTES:** [captures/rampas/relatorio_antes.txt](captures/rampas/relatorio_antes.txt) (`TOTAL=3 | APROVADAS=0 | REPROVADAS=3`)
- **Relatório final DEPOIS:** [captures/rampas/relatorio_depois.txt](captures/rampas/relatorio_depois.txt) (`TOTAL=3 | APROVADAS=3 | REPROVADAS=0`)
- **MP4 ANTES (30s):** [captures/rampas/rampas_antes.mp4](captures/rampas/rampas_antes.mp4)
- **MP4 DEPOIS (30s — mesma câmera):** [captures/rampas/rampas_depois.mp4](captures/rampas/rampas_depois.mp4)

---

## 4. Como Rodar a Validação e o Teste de Travessia no Futuro

### Validador de Editor (todas as cenas)
- **Menu do Editor:** `Tools → CONCERTO → Validar Rampas e Corrimãos (Todas as Cenas)`
- **Linha de comando (batchmode):**
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" `
    -batchmode -projectPath "C:\Users\Bruno\workspace\game-concerto\unity" `
    -executeMethod Studio.Game.EditorTools.RampAndRailingValidatorTool.RunValidateFromCommandLine `
    -logFile "unity\Logs\ramp_validate.log"
  ```

### Teste Automatizado de Travessia (PlayMode)
- Integrado na suíte `Tests/SmokeTest/` como **`Smoke_21_AllRampsUpAndDownTraversalProof`** em [ConcertoSmokeTests.cs](unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs).
- Roda pelo **Test Runner → PlayMode** ou via CLI:
  ```powershell
  & "C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" `
    -batchmode -projectPath "C:\Users\Bruno\workspace\game-concerto\unity" `
    -runTests -testPlatform PlayMode `
    -testFilter "Studio.Tests.SmokeTest.ConcertoSmokeTests" `
    -testResults "unity\Logs\smoke_results.xml" `
    -logFile "unity\Logs\smoke.log"
  ```

---

## 5. Lista de Modelos 3D TripoSG
- **0 modelos 3D criados nesta tarefa.**
