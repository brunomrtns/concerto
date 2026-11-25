# REGRESSÃO VISUAL — Comparador de Screenshots entre Builds (CONCERTO)

Ferramenta determinista de captura e comparação pixel a pixel entre builds para detectar regressões visuais (iluminação, shaders, props, câmera, materiais) sem alterar nenhuma linha de gameplay.

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Zero impacto em gameplay:** Ferramenta 100% isolada em `Assets/Game/Editor/VisualRegressionCaptureTool.cs`, `Assets/Game/Tests/PlayMode/VisualRegressionCaptureRunnerTest.cs` e `tools/visual_regression/compare_visual_regression.py`.
- **Qualidade e estabilidade:** `0 warnings CS`, `0 errors CS`, suíte `Tests/SmokeTest/` **18/18 PASSED**.
- **Modelos 3D TripoSG criados nesta tarefa:** `0 modelos criados`.

---

## 2. Câmeras Fixas Capturadas (6 Pontos — `1920x1080`)

| # | Arquivo | Cena | Posição Câmera | Alvo (`LookAt`) | FOV |
|---|---|---|---|---|---|
| 1 | `01_mainmenu_title.png` | `MainMenu` | `(0, 1.0, -10)` | `(0, 1.0, 0)` | `60°` |
| 2 | `02_ato1_caldeiral_bancada.png` | `Caldeiral` (Ato 1) | `(-25.6, 2.2, -20.2)` | `(-29.2, 1.0, -16.4)` | `55°` |
| 3 | `03_ato1_caldeiral_corredor.png` | `Caldeiral` (Ato 1) | `(-31.5, 2.4, -18.5)` | `(-22.0, 1.1, -18.5)` | `55°` |
| 4 | `04_ato3_setorexterno_patio.png` | `SetorExterno` (Ato 3) | `(-6.0, 3.8, -10.0)` | `(4.0, 1.2, 2.0)` | `58°` |
| 5 | `05_ato4_epicentro_travessia.png` | `Epicentro` (Ato 4) | `(-6.0, 4.0, -5.0)` | `(16.0, 0.8, 0.0)` | `58°` |
| 6 | `06_ato4_epicentro_arena.png` | `Epicentro` (Ato 4) | `(96.0, 5.2, -8.5)` | `(116.0, 1.4, 0.0)` | `60°` |

---

## 3. Como Rodar

### A) Pelo Unity Editor (Menu)
- `Concerto → QA → Visual Regression — Capture Baseline` (salva em `captures/regressao/baseline/`)
- `Concerto → QA → Visual Regression — Capture Dated Run` (salva em `captures/regressao/<data>/`)

### B) Por Linha de Comando (CI / Automação)
```powershell
# 1. Capturar baseline + execução datada em PlayMode (determinista com URP)
Unity.exe -batchmode -projectPath C:\Users\Bruno\workspace\game-concerto\unity `
  -runTests -testPlatform PlayMode -testFilter VisualRegressionCaptureRunnerTest

# 2. Comparar pixel a pixel e gerar relatório + Top 10 lado a lado
python C:\Users\Bruno\workspace\3dprinter-setup-ia\tools\visual_regression\compare_visual_regression.py `
  --baseline C:\Users\Bruno\workspace\game-concerto\captures\regressao\baseline `
  --run C:\Users\Bruno\workspace\game-concerto\captures\regressao\<data>
```

---

## 4. Como Ler o Relatório

Em `captures/regressao/<data>/` são gerados automaticamente:
- **`relatorio_regressao.md` / `.json`:** tabela por screenshot com `PASS` ou `DIFF_DETECTED`, contagem exata de pixels alterados (`ΔRGB > 12`) e `%` total alterada (alerta acima de `0.15%`).
- **`top10_diferencas_lado_a_lado.png`:** folha visual com as **10 maiores regiões alteradas** lado a lado (**ESQ: Baseline ANTES | MEIO: Build atual DEPOIS | DIR: Heatmap Diff**).
- **`diff_heatmap_<shot>.png`:** imagem completa com pixels alterados destacados em vermelho e caixas amarelas nas regiões acima do limiar.

---

## 5. Como Atualizar a Baseline DE PROPÓSITO (Mudança Intencional)

Quando uma mudança visual for **intencional** (ex.: nova iluminação, prop TripoSG adicionado, ajuste de material):
1. Revise `top10_diferencas_lado_a_lado.png` e `relatorio_regressao.md` para confirmar que **apenas** a alteração esperada mudou.
2. No Unity Editor, clique em **`Concerto → QA → Visual Regression — Update Baseline (Intentional)`** (ou copie os PNGs aprovados de `captures/regressao/<data>/` para `captures/regressao/baseline/`).
3. Faça commit explícito da nova baseline explicando o motivo visual na mensagem do commit (ex.: `chore(vr): update visual baseline after Act 1 lighting pass`).

---

## 6. Evidência de Prova (GATE 5)

Executada captura da baseline (`captures/regressao/baseline/`) seguida de execução com 1 objeto visível temporário em memória na bancada do Ato 1 (`captures/regressao/2026-11-25_prova_diff/`):
- **`02_ato1_caldeiral_bancada.png`:** `DIFF_DETECTED` (`5.7856%`, `13` regiões acusadas) — veja `diff_heatmap_02_ato1_caldeiral_bancada.png` e `top10_diferencas_lado_a_lado.png`.
- **`04_ato3_setorexterno_patio.png` e `06_ato4_epicentro_arena.png`:** `PASS` (`0.0000%`, `0` regiões alteradas).
