# QA AUTOMATIZADO — FASE 1 (PLAYCALLER + SMOKE TESTS)

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0
**Unity:** `6000.3.26f1` | **Python:** `3.13.15` | **`uv` / `uvx`:** `0.13.1`
**Modelos 3D criados nesta tarefa:** **0 modelos 3D criados**.
**Alterações em código de gameplay:** **0 arquivos de gameplay alterados** (ver prova por `git diff` no relatório final).

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **Seção 2 (Character & Controls):** CONFORME — `Smoke_04` valida spawn e resposta imediata de movimento do Dez em `Caldeiral`; `Smoke_05` valida pulo real com elevação vertical (`> 0.35m`), `coyoteTimeSec = 0.10s` (`~100ms`, janela `[0.08s..0.12s]`) e `jumpBufferSec = 0.15s` (`~150ms`, janela `[0.12s..0.18s]`).
- **Seção 3 (Métricas de Estabilidade / Qualidade):** CONFORME — carregamento das cenas principais, transições de cena e sessão de 125s via Playcaller concluídos com **0 erros/exceptions no console** e **0 warnings CS** na compilação.
- **Seção 8 (Data-Driven / Persistência):** CONFORME — `Smoke_06` valida escrita e recarregamento de save em JSON versionado (`schemaVersion >= 1`) com integridade de cena, checkpoint e posição, sem estado de jogo em `PlayerPrefs`.

---

## 2. Tabela da Suíte Smoke Test (`unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs`)

| Teste (Play Mode) | O que verifica | Resultado Esperado | Resultado Medido |
|---|---|---|---|
| `Smoke_01_MainMenuLoadsWithoutConsoleErrors` | Carrega `MainMenu` e monitora `Application.logMessageReceived` por 6 frames. | Cena ativa `MainMenu`, 0 `Error`/`Exception`/`Assert`. | **PASSED** (`0.50s`) |
| `Smoke_02_Act1_CaldeiralLoadsWithoutConsoleErrors` | Carrega Ato 1 (`Caldeiral`) e monitora erros no console. | Cena ativa `Caldeiral`, 0 `Error`/`Exception`/`Assert`. | **PASSED** (`1.14s`) |
| `Smoke_03_Act2_SetorExternoLoadsWithoutConsoleErrors` | Carrega Ato 2 (`SetorExterno`) e monitora erros no console. | Cena ativa `SetorExterno`, 0 `Error`/`Exception`/`Assert`. | **PASSED** (`0.57s`) |
| `Smoke_04_DezSpawnsAndRespondsToMovementInput` | Spawna Dez em `Caldeiral` e aplica input de movimento por `0.75s`. | Deslocamento horizontal `> 0.35m`, 0 erros no console. | **PASSED** (`1.63s`) |
| `Smoke_05_JumpCoyoteAndBufferMatchDesignBible` | Valida `DezMovementConfig` (`coyoteTimeSec` ~100ms, `jumpBufferSec` ~150ms) e executa pulo real. | Parâmetros dentro da `DESIGN_BIBLE`, ganho vertical `> 0.35m`, 0 erros. | **PASSED** (`0.62s`) |
| `Smoke_06_VersionedJsonSaveWriteAndReloadIntegrity` | Grava save JSON versionado em path isolado, lê o JSON em disco e recarrega via `SaveSystem.TryLoad`. | `schemaVersion >= 1`, `sceneName`, `currentCheckpointId` e `checkpointPosition` íntegros. | **PASSED** (`0.57s`) |
| `Smoke_07_SceneTransitionsWithoutExceptions` | Transita `MainMenu → Caldeiral → SetorExterno → MainMenu`. | Todas as transições concluem sem exceptions ou erros no console. | **PASSED** (`2.27s`) |

**Resumo da Suíte Smoke:** `7 / 7 PASSED`, `0 warnings CS`, `0 errors CS`.

---

## 3. Evidências da Sessão Playcaller (GATE 2 e GATE 4)

- **Pacote Unity:** `company.takashi.playcaller` (`https://github.com/takashicompany/playcaller.git`) instalado em `unity/Packages/manifest.json`.
- **Configuração MCP do projeto:** `.mcp.json` na raiz de `game-concerto` (`uvx --with "mcp<2" playcaller`).
- **GATE 2 (Conexão & Screenshot):**
  - `playcaller_get_editor_state` retornou `unityVersion: "6000.3.26f1"`, `platform: "WindowsEditor"`, `activeScene: "Caldeiral"`.
  - Screenshot capturada via `playcaller_screenshot` em `captures/qa/playcaller_conexao.png` (`1280x720`, PNG).
- **GATE 4 (Sessão jogada pela IA — ≥ 2 minutos):**
  - Gravada via **Unity Recorder** em `captures/qa/sessao_playcaller.mp4` (`1280x720 H.264 + AAC`, `20 fps`, **`125.05s`**, `17.6 MB`).
  - Ações executadas via Playcaller: `spawn → W/D/S/A → Space (pulo) → Escape (Pause) → Escape (Voltar)`.
  - **Console durante a sessão:** `0` erros/exceptions (`RUNTIME_ERRORS=0`).

---

## 4. Instruções de Reprodução Passo a Passo (Editor Aberto + Claude Code)

### A. Pré-requisitos
1. **Unity `6000.3.26f1`** (ou ≥ 2022.3) com o projeto `unity/` aberto.
2. **Python 3.10+** e **`uv` / `uvx`** instalados no PATH.
3. No `Player Settings`, manter **Run In Background = habilitado** (já ativo no projeto).

### B. Registrar o Playcaller no Claude Code
> **Nota técnica importante:** o pacote `playcaller` (PyPI `1.30.0`) importa `mcp.server.fastmcp` (API do SDK `mcp 1.x`). Em ambientes onde `uvx` baixa `mcp 2.x`, é obrigatório fixar `--with "mcp<2"`. O arquivo `.mcp.json` na raiz do repositório já está configurado com esse pin.

Para registrar manualmente via CLI do Claude Code:
```sh
claude mcp add playcaller -- uvx --with "mcp<2" playcaller
```

### C. Rodar a verificação via Playcaller (Editor aberto)
1. Abra o Unity Editor em `C:\Users\Bruno\workspace\game-concerto\unity`.
2. Inicie o Claude Code na raiz do projeto (ele detectará `.mcp.json` automaticamente) ou execute o driver reproduzível:
   ```sh
   python C:\Users\Bruno\workspace\3dprinter-setup-ia\tools\qa_playcaller\run_playcaller_qa.py
   ```
3. No Claude Code, peça `playcaller_get_editor_state` e `playcaller_screenshot` para validar a ponte ativa.
4. Para gravar sessões MP4 via Unity Recorder, use os itens de menu Editor-only `CONCERTO/QA/Open Act 1 Caldeiral`, `CONCERTO/QA/Start Recorder MP4` e `CONCERTO/QA/Stop Recorder MP4`.

### D. Rodar a Suíte Smoke Test (Unity Test Framework)
Via **Window → General → Test Runner → PlayMode → `Studio.Tests.SmokeTest.ConcertoSmokeTests`**, ou via linha de comando:
```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.26f1\Editor\Unity.exe" `
  -batchmode -projectPath "C:\Users\Bruno\workspace\game-concerto\unity" `
  -runTests -testPlatform PlayMode `
  -testFilter "Studio.Tests.SmokeTest.ConcertoSmokeTests" `
  -testResults "unity\Logs\smoke_results.xml" `
  -logFile "unity\Logs\smoke.log"
```

---

## 5. Lista de Bugs Encontrados na Sessão (REPORTADOS — SEM CORREÇÃO APLICADA)

Conforme a Regra 3 desta tarefa, **nenhum bug abaixo foi corrigido**; todos ficam registrados para triagem posterior:

| ID | Severidade | Local / Sistema | Descrição Observada | Evidência |
|---|---|---|---|---|
| **QA-01** | **Médio** | HUD superior esquerdo (`PlayerHealthController` × `ObjectiveSystem`) | Em `1280x720` (e também em ultrawide), os 4 segmentos de HP verde-ciano sobrepõem diretamente o texto `"OBJETIVO / Fale com Juno"`, prejudicando a legibilidade do objetivo. | `captures/qa/playcaller_conexao.png` |
| **QA-02** | **Médio** | HUD inferior direito (`MultiToolController` × `DezPowersController`) | Em `1280x720`, o painel `[TAB] MODO: CONCERTAR / CÉLULAS: 3/6` sobrepõe parcialmente o indicador `ECO [Q] / PRONTO`, cortando parte do texto do poder. | `captures/qa/playcaller_conexao.png` |
| **QA-03** | **Médio** | Menu de Pause + Input externo (`PauseMenuController` × Playcaller) | Após o 2º ciclo de abrir/fechar Pause via tecla `Escape` durante gravação no Unity Recorder, os comandos subsequentes do Playcaller deram timeout até o fim da gravação (comportamento ligado a `Time.timeScale = 0` ou foco da janela Game View ao pausar). | Log da sessão `playcaller_session_report.json` |
| **QA-04** | **Baixo** | Ambiente / Ferramenta MCP (`playcaller` PyPI `1.30.0`) | Executar `uvx playcaller` sem pin falha com `ModuleNotFoundError: No module named 'mcp.server.fastmcp'` devido à atualização do pacote `mcp` para `2.x`. Contornado em `.mcp.json` com `--with "mcp<2"`. | Log de inicialização MCP |
| **QA-05** | **Baixo** | Layout HUD em Game View ultrawide (`2359x718`) | Antes de travar o Game View em `1280x720`, os elementos ancorados nos cantos mantêm as mesmas sobreposições de QA-01 e QA-02 sem adaptação responsiva de espaçamento. | Captura inicial ultrawide |

---

## 6. Nota sobre Repositório (PASSO 0)
- Foram preservados sem alteração 19 arquivos modificados pré-existentes no workspace de sessões anteriores (`Calibration.unity`, materiais em `Fab3D/`, metadados `MoMask/` e scripts de áudio) — apenas os arquivos novos desta tarefa QA foram adicionados e commitados.
- **0 modelos 3D criados.**
