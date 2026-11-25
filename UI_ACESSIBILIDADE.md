# UI, HUD E ACESSIBILIDADE — CONCERTO

Documentação do menu de pausa, tela de opções completa, revisão de HUD, legendas globais e recursos de acessibilidade de CONCERTO.

---

## 1. Declaração de Conformidade — `DESIGN_BIBLE.md` v1.0

- **100% teclado e controle (sem mouse obrigatório):** Menu de Pausa (`PauseMenuController`) e Tela de Opções (`OptionsPanelUI`) operam integralmente por teclado (`Setas`/`WASD` + `Enter`/`Espaço` + `Esc`) e por controle (`D-Pad`/Analógico + `A` confirmar + `B` voltar + `Start` pausar).
- **Persistência limpa (`PlayerPrefs` SOMENTE para opções):** Todas as preferências de áudio, vídeo, câmera, acessibilidade e remapeamento de teclas são gravadas em `PlayerPrefs` via `GameSettingsManager` e **aplicadas na hora sem restart**. Todo estado de jogo permanece exclusivamente no `SaveSystem` JSON versionado em `Application.persistentDataPath`.
- **HUD legível e sem sobreposição:** Vida, objetivo atual e poder ativo permanecem legíveis em áreas não conflitantes e acompanham dinamicamente a escala de fonte escolhida pelo jogador.
- **Legendas em toda fala e som relevante:** Exibidas com nome do falante ou fonte sonora (`[JUNO]`, `[ECO TEMPORAL]`, `[ESTASE]`, `[SISTEMA]`) com alto contraste e escala sincronizada.
- **Modelos 3D TripoSG criados nesta tarefa:** `0 modelos criados`.

---

## 2. O Que é Acessível e Como Funciona

| Recurso | Gate | Como funciona | Aplicação |
|---|---|---|---|
| **Menu de Pausa completo** | GATE 1 | `Continuar` / `Opções` / `Menu Principal` (com confirmação segura). Navegação por `Start`/`Esc`, `D-Pad`/`Setas`, `A`/`Enter`, `B`/`Esc`. | `PauseMenuController.cs` |
| **Volume por bus (5 sliders)** | GATE 2 | Sliders independentes para `Master`, `Música`, `SFX`, `Ambiência` e `UI`, ligados ao `AudioMixController` (`ConcertoMasterMixer`). | Na hora, sem restart |
| **Brilho** | GATE 2 | Ajuste de `50%` a `150%` via camada não-interativa (`BrightnessOverlay`) em `AccessibilityRuntimeController`. | Na hora, sem restart |
| **Sensibilidade de Câmera** | GATE 2 | Ajuste `0.3x` a `2.5x` consumido diretamente por `DezLocomotionController`. | Na hora, sem restart |
| **Escala de Fonte (3 tamanhos)** | GATES 2 & 3 | `Pequena (0.85x)`, `Normal (1.00x)`, `Grande (1.25x)` aplicados ao HUD (vida, objetivo, poderes), diálogos, legendas e painel de opções. | Na hora, sem restart |
| **Legendas On/Off + Falante** | GATES 2 & 4 | Toggle liga/desliga; quando ligadas, `AccessibilityRuntimeController.ShowSubtitle(speaker, text)` exibe nome do falante/fonte sonora em falas e eventos sonoros relevantes (Eco Temporal, Estase, Save/Backup). | Na hora, sem restart |
| **Modo Daltônico (3 perfis)** | GATE 2 | `Desligado`, `Protanopia`, `Deuteranopia`, `Tritanopia` com filtro de adaptação cromática + badge de perfil ativo. | Na hora, sem restart |
| **Remapeamento de Teclas** | GATE 2 | Remapeamento em tempo real de `Pular`, `Interagir`, `Eco Temporal`, `Estase` e `Pausa` (suporta captura de tecla ou ciclo por controle) + `ResetActionKeys()`. | Na hora, sem restart |

---

## 3. Onde Cada Opção Salva (`PlayerPrefs` — `GameSettingsManager`)

| Opção | Chave `PlayerPrefs` | Tipo / Faixa | Padrão |
|---|---|---|---|
| Volume Master | `CONCERTO_OPT_MASTER_VOL` | `float` (`0.0–1.0`) | `0.85` |
| Volume Música | `CONCERTO_OPT_MUSIC_VOL` | `float` (`0.0–1.0`) | `0.70` |
| Volume SFX | `CONCERTO_OPT_SFX_VOL` | `float` (`0.0–1.0`) | `0.85` |
| Volume Ambiência | `CONCERTO_OPT_AMBIENCE_VOL` | `float` (`0.0–1.0`) | `0.70` |
| Volume UI | `CONCERTO_OPT_UI_VOL` | `float` (`0.0–1.0`) | `0.80` |
| Tela Cheia | `CONCERTO_OPT_FULLSCREEN` | `int` (`0`/`1`) | `1` |
| Qualidade | `CONCERTO_OPT_QUALITY` | `int` (`0..N`) | `2` |
| Brilho | `CONCERTO_OPT_BRIGHTNESS` | `float` (`0.5–1.5`) | `1.0` |
| Sensibilidade de Câmera | `CONCERTO_OPT_CAM_SENS` | `float` (`0.3–2.5`) | `1.0` |
| Reduzir Camera Shake | `CONCERTO_OPT_REDUCE_SHAKE` | `int` (`0`/`1`) | `0` |
| Escala de Fonte (3 tamanhos) | `CONCERTO_OPT_FONT_SCALE` | `int` (`0`/`1`/`2`) | `1` (`Normal`) |
| Legendas On/Off | `CONCERTO_OPT_SUBTITLES` | `int` (`0`/`1`) | `1` (`Ligadas`) |
| Modo Daltônico (3 perfis) | `CONCERTO_OPT_COLORBLIND` | `int` (`0..3`) | `0` (`None`) |
| Teclas Remapeadas | `CONCERTO_OPT_KEY_<ACTION>` | `int` (`KeyCode`) | `Space`, `E`, `C`, `R`, `Escape` |

---

## 4. Evidências e Testes Automatizados (GATES 5 e 7)

- **MP4 (GATE 5):** `captures/ui/ui_navegacao_controle_3min.mp4` (`1920x1080`, H.264, `180.0s` = **3:00 exatos**) — navegação completa pelos menus usando **SÓ controle** (`Start`, `D-Pad`, `A`, `B`), demonstrando os 5 buses de áudio, brilho, sensibilidade, 3 tamanhos de fonte, legendas On/Off, 3 perfis daltônicos (`Protanopia`, `Deuteranopia`, `Tritanopia`), remapeamento de teclas e retorno ao gameplay com HUD escalado e legendas com nome do falante/fonte sonora.
- **Teste Smoke Novo (`Smoke_12_UiAccessibilityOptionsAndPauseControllerNav`):** em `unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs`.
- **Suíte `Tests/SmokeTest/`:** **12/12 PASSED** (`0 failed`, `0 skipped`, `0 warnings CS`, `0 errors CS`) + `UiAccessibilityControllerNavCaptureTest` (**13/13 PASSED**).
