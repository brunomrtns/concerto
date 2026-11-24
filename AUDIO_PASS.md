# PASS DE ÁUDIO — MIX GLOBAL, DUCKING DE CUTSCENE E FEEDBACK SONORO

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0
**Pré-requisito verificado:** Pass Global de Performance concluído, commitado e publicado (`game-concerto@1086c15`, `3dprinter-setup-ia@950bf63`).

---

## 1. Passo 0 — Auditoria Inicial

| Categoria | Estado Antes do Pass | Correção Aplicada |
|---|---|---|
| **AudioMixer Global** | Inexistente (nenhum `.mixer` no projeto; volumes aplicados manualmente por script). | Criado `Assets/Resources/Audio/ConcertoMasterMixer.mixer` com 5 buses e parâmetros expostos. |
| **Opções de Volume (PlayerPrefs)** | Apenas `Master`, `Music`, `SFX` no `GameSettingsManager` e `OptionsPanelUI`. | Estendidos `GameSettingsManager` e `OptionsPanelUI` (em `MainMenu` e `Caldeiral`) com `Ambience` e `UI` — persistidos exclusivamente em `PlayerPrefs` (separado do `SaveSystem` JSON). |
| **Ducking em Cutscenes/Diálogo** | Inexistente (música não reduzia nem retornava com fade). | Implementado ducking suave automático em `AudioMixController` (`-12 dB`, fade-in `0.35s`, fade-out `0.65s`, zero corte seco) para `CS01–CS04`, `DialogueController` e `DialoguePresenter`. |
| **Passos por Superfície** | Apenas 1 variação por superfície (`floor`, `metal`, `puddle`) e raycast usando `~0` (todas as layers). | Expandido para **3 variações distintas por superfície** (sem repetição consecutiva) + pitch aleatório leve (`0.94–1.06`) usando a máscara de layers canônica de `CollisionMatrixBootstrap` (`Default | Environment | Props`). |
| **Pulo (Takeoff)** | Sem som ao saltar (apenas aterrissagem `landing_thud_TEMP.wav`). | Adicionado `jump_takeoff_TEMP.wav` no `DezLocomotionController`. |
| **UI SFX (Menu/Pause/Opções)** | Completamente silencioso (abrir/fechar/confirmar/negar). | Adicionados `ui_open/close/confirm/deny_TEMP.wav` tocados em `AudioSource` com `ignoreListenerPause = true` (funciona mesmo com o jogo pausado). |
| **Ambiência Contínua + Crossfade** | `Caldeiral` sem loop contínuo de oficina; sem crossfade entre áreas. | Adicionado `workshop_warm_ambience_TEMP.wav` (oficina quente) com crossfade contínuo (`1.5s`) para `sector_cold_wind_ambience_TEMP.wav` (`SetorExterno`). |
| **Roteamento de AudioSources** | 27 `AudioSource`s soltos sem grupo de mixer nas 3 cenas. | Roteados automaticamente para `Music / SFX / Ambience / UI`: `MainMenu` (1), `Caldeiral` (18), `SetorExterno` (8). |

---

## 2. Mapa de Buses (`ConcertoMasterMixer.mixer`)

```text
Master  [Param: MasterVol]  (Default slider: 85% / -1.4 dB)
 ├── Music    [Param: MusicVol]    (Default slider: 70% / -3.1 dB | Gameplay bed: -9 dB | Ducked: -12 dB relativo)
 ├── SFX      [Param: SfxVol]      (Default slider: 85% / -1.4 dB | Impactos/Passos/Ferramenta/Reparo)
 ├── Ambience [Param: AmbienceVol] (Default slider: 70% / -3.1 dB | Loops contínuos por área com crossfade 1.5s)
 └── UI       [Param: UiVol]       (Default slider: 80% / -1.9 dB | ignoreListenerPause = true)
```

### Mix Alvo (Hierarquia de Inteligibilidade)
1. **Diálogo / VO (0 dB relativo):** sempre em primeiro plano e 100% inteligível; nunca mascarado pela música nem pela ambiência.
2. **Impactos de Gameplay / Combate / Ferramenta (`-1.5 dB` a `-3 dB`):** sincronizados ao hitstop (`65ms` leve / `95ms` pesado, decay `< 300ms`), sempre acima da cama musical.
3. **UI SFX (`-3 dB`):** resposta tátil limpa em menus e pause sem fadiga auditiva.
4. **Música de Gameplay (`-6 dB` a `-9 dB`):** acompanha a cena sem competir com SFX; em cutscenes ou diálogos entra em **ducking suave para `-12 dB` relativos** (`factor = 0.25`, fade-in `0.35s`, fade-out `0.65s`).
5. **Ambiência de Área (`-10 dB`):** cama atmosférica contínua (`workshop_warm_ambience_TEMP` ↔ `sector_cold_wind_ambience_TEMP`) com crossfade suave de `1.5s`.
6. **Headroom / Segurança:** nenhum evento excede `0 dBFS` (limiter suave em `-1 dBFS` nas capturas, nada no nível da dor).

### Persistência (PlayerPrefs vs SaveSystem)
- **PlayerPrefs (exclusivo para opções):** `CONCERTO_OPT_MASTER_VOL`, `CONCERTO_OPT_MUSIC_VOL`, `CONCERTO_OPT_SFX_VOL`, `CONCERTO_OPT_AMBIENCE_VOL`, `CONCERTO_OPT_UI_VOL`, `CONCERTO_OPT_FULLSCREEN`, `CONCERTO_OPT_QUALITY`, `CONCERTO_OPT_CAM_SENS`, `CONCERTO_OPT_REDUCE_SHAKE`.
- **SaveSystem JSON versionado:** permanece 100% intocado, exclusivo para estado de jogo.

---

## 3. Tabela Evento → Som → Bus → Volume / Pitch

| Evento | Asset(s) | Bus | Volume Base | Pitch | Observação |
|---|---|---|---|---|---|
| Passo — Piso (Oficina/Rua) | `footstep_floor_TEMP.wav`, `_02_TEMP.wav`, `_03_TEMP.wav` | `SFX` | `0.36` (andar) / `0.54` (correr) | `0.94–1.06` | 3 variações sem repetição consecutiva; layer `Default/Environment` |
| Passo — Metal / Grade / Prop | `footstep_metal_TEMP.wav`, `_02_TEMP.wav`, `_03_TEMP.wav` | `SFX` | `0.36` (andar) / `0.54` (correr) | `0.94–1.06` | 3 variações; detecta nomes metálicos ou `LayerProps (25)` |
| Passo — Poça / Água / Óleo | `footstep_puddle_TEMP.wav`, `_02_TEMP.wav`, `_03_TEMP.wav` | `SFX` | `0.36` (andar) / `0.54` (correr) | `0.94–1.06` | 3 variações; detecta poça/água |
| Pulo (Takeoff) | `jump_takeoff_TEMP.wav` | `SFX` | `0.58` | `0.96–1.06` | Sincronizado ao gatilho `Jump` |
| Aterrissagem (Landing) | `landing_thud_TEMP.wav` | `SFX` | `0.55–0.90` (escala por impacto) | `0.92–1.05` | Sincronizado ao `landingBob` |
| Impulso Mecânico (Dash) | `dez_thruster_dash_TEMP.wav` | `SFX` | `0.80` | `1.00` | Sincronizado às partículas e FOV kick (`180ms`) |
| Ferramenta — Troca de Modo | `tool_mode_switch_TEMP.wav` | `SFX` | `0.72` | `1.00` | `[Tab] / 1 / 2` |
| Ferramenta — Golpe (Swing) | `tool_melee_swing_TEMP.wav` | `SFX` | `0.78` | `0.78` (pesado) / `0.98–1.12` (combo) | Sincronizado ao arco da ferramenta |
| Ferramenta — Impacto (Hit) | `tool_melee_hit_TEMP.wav` | `SFX` | `0.90` | `0.82` (pesado) / `1.05` (leve) | Sincronizado ao hitstop (`65–95ms`) e shake `< 280ms` |
| Ferramenta — Disparo (Bolt) | `tool_bolt_fire_TEMP.wav` | `SFX` | `0.85` | `1.00` | Consome 1 célula de energia |
| Reparo — Canalização / Resolução | `repair_tool_channel_TEMP.wav` / `repair_resolve_chime_TEMP.wav` | `SFX` | `0.75–0.85` | `1.00` | Ritual de reparo Ato 2 |
| Coleta de Peça / Conclusão Ato 1 | `sfx_part_pickup_TEMP.wav` / `sfx_act1_complete_stinger_TEMP.wav` | `SFX` | `0.80–0.85` | `1.00` | Feedback imediato |
| UI — Abrir Painel / Pause | `ui_open_TEMP.wav` | `UI` | `0.72` | `1.00` | `ignoreListenerPause = true` |
| UI — Fechar / Voltar / Retomar | `ui_close_TEMP.wav` | `UI` | `0.72` | `0.96` | `ignoreListenerPause = true` |
| UI — Confirmar / Trocar Opção | `ui_confirm_TEMP.wav` | `UI` | `0.72` | `1.00` | `ignoreListenerPause = true` |
| UI — Negar / Ação Bloqueada | `ui_deny_TEMP.wav` | `UI` | `0.72` | `0.95` | Ex.: `Esc` durante cutscene/diálogo ou Continuar sem save |
| Ambiência — Caldeiral (Oficina) | `workshop_warm_ambience_TEMP.wav` | `Ambience` | `0.32` | `1.00` | Loop contínuo sem clique; crossfade `1.5s` |
| Ambiência — Setor Externo | `sector_cold_wind_ambience_TEMP.wav` | `Ambience` | `0.32` | `1.00` | Loop contínuo; crossfade `1.5s` |
| Música — Menu Principal | `cs01_musicbox_motif_TEMP.wav` | `Music` | `0.42` | `1.00` | Motif da caixinha |
| Música — Gameplay (Caldeiral / Setor) | `caldeiral_sync_loop.ogg` / `concerto_tema_quebrado.ogg` | `Music` | `0.38` | `1.00` | Ducking suave `-12 dB` em cutscenes e diálogos |

---

## 4. Fonte e Licença de TODO Áudio Novo

Todos os **12 novos arquivos WAV** foram gerados proceduralmente do zero via síntese matemática em Python (`3dprinter-setup-ia/tools/audio_pass/generate_audio_pass_assets.py`), sem nenhum sample de terceiros:

| Arquivo (`unity/Assets/Game/Audio/...`) | Fonte | Licença |
|---|---|---|
| `SFX/Movement/footstep_floor_02_TEMP.wav` | Síntese procedural original (`generate_audio_pass_assets.py`) | **CC0 1.0 Universal (Domínio Público / Original)** |
| `SFX/Movement/footstep_floor_03_TEMP.wav` | Síntese procedural original (`generate_audio_pass_assets.py`) | **CC0 1.0 Universal** |
| `SFX/Movement/footstep_metal_02_TEMP.wav` | Síntese procedural original (`generate_audio_pass_assets.py`) | **CC0 1.0 Universal** |
| `SFX/Movement/footstep_metal_03_TEMP.wav` | Síntese procedural original (`generate_audio_pass_assets.py`) | **CC0 1.0 Universal** |
| `SFX/Movement/footstep_puddle_02_TEMP.wav` | Síntese procedural original (`generate_audio_pass_assets.py`) | **CC0 1.0 Universal** |
| `SFX/Movement/footstep_puddle_03_TEMP.wav` | Síntese procedural original (`generate_audio_pass_assets.py`) | **CC0 1.0 Universal** |
| `SFX/Movement/jump_takeoff_TEMP.wav` | Síntese procedural original (`generate_audio_pass_assets.py`) | **CC0 1.0 Universal** |
| `SFX/UI/ui_open_TEMP.wav` | Síntese procedural original (`generate_audio_pass_assets.py`) | **CC0 1.0 Universal** |
| `SFX/UI/ui_close_TEMP.wav` | Síntese procedural original (`generate_audio_pass_assets.py`) | **CC0 1.0 Universal** |
| `SFX/UI/ui_confirm_TEMP.wav` | Síntese procedural original (`generate_audio_pass_assets.py`) | **CC0 1.0 Universal** |
| `SFX/UI/ui_deny_TEMP.wav` | Síntese procedural original (`generate_audio_pass_assets.py`) | **CC0 1.0 Universal** |
| `Ambience/workshop_warm_ambience_TEMP.wav` | Síntese procedural original (`generate_audio_pass_assets.py`, loop 6.0s sem emenda) | **CC0 1.0 Universal** |

Todos os demais arquivos (BGM `.ogg`, VO `.wav`, SFX base de movimento/ferramenta/reparo/setor/cutscenes) são **reutilizados diretamente do repositório existente** (`unity/Assets/Game/Audio/`).

---

## 5. Validação, Testes e Evidências (Gates 1–4)

- **Gate 1 (Compilação):** `0 warnings CS`, `0 errors CS`.
- **Gate 2 (MP4s 1080p COM ÁUDIO em `captures/audio_pass/`):**
  - `captures/audio_pass/audio_pass_gameplay_60s_1080p.mp4` — 60s de gameplay em `Caldeiral` mostrando passos nas 3 superfícies (`floor`, `metal`, `puddle`, 69 passos em 3 variações/superfície), 2 pulos + pousos, troca/golpe/impacto com hitstop da ferramenta, UI SFX de Pause/Confirmar/Fechar e crossfade contínuo de ambiência (oficina ↔ vento frio do Setor).
  - `captures/audio_pass/audio_pass_cutscene_ducking_1080p.mp4` — Transição Gameplay → Cutscene 1 (`CS01`) → Skip/Retorno ao Gameplay mostrando ducking suave de `-12 dB` na entrada (`0.35s`) e retorno suave na saída (`0.65s`), sem corte seco.
- **Gate 3 (Documentação):** este arquivo (`AUDIO_PASS.md`).
- **Gate 4 (Persistência & Regressão):**
  - `AudioPassValidationAndCaptureTest`: **3/3 PASSED** (`T01` 5 buses + persistência PlayerPrefs, `T02` 60s gameplay áudio + 3 superfícies + UI + ambiência, `T03` ducking suave entrada/saída).
  - Suíte de Regressão Core: **4/4 PASSED** (`CollisionSystem`, `DesignBible`, `RepairSystem`, `SignalQuest`).

---

## CONFORMIDADE — DESIGN_BIBLE.md v1.0
- **Seção 3 (Hitstop 50–100ms com peso sonoro sincronizado e decay < 300ms):** CONFORME — `tool_melee_hit_TEMP.wav` dispara sincronizado ao hitstop (`65ms` leve / `95ms` pesado) no bus `SFX` (`-1.4 dB`), acima da cama musical, com shake decay `< 280ms`.
- **Seção 4 (Cutscene com música em ducking e skip ≤ 3s):** CONFORME — `AudioMixController` reduz automaticamente o bus `Music` em `-12 dB` com fade suave (`0.35s` entrada / `0.65s` saída, zero corte seco) durante `CS01–CS04` e retorna suavemente no término/skip.
- **Seção 8.3 (Identidade sonora — motif da caixinha e diálogo sempre inteligível):** CONFORME — motif preservado no menu/CS01; diálogo e VO nunca são duckados e ficam sempre em primeiro plano sobre música (`-12 dB` ducked) e ambiência (`-10 dB`).
- **Seção 9 (Tabela de Juice — feedback sonoro imediato):** CONFORME — passos por superfície (3 variações + pitch leve), pulo, pouso, dash, ferramenta, reparo, coleta e UI (abrir/fechar/confirmar/negar) todos sonorizados e roteados no mixer global.
