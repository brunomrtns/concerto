# ACT 2 RETURN — Cutscene 3 ("O Retorno") & Anomalias Ambientais

## 1. Visão Geral & Continuidade Canônica
- **Cena:** `Assets/Game/Scenes/Caldeiral.unity` (reusada — Unity `6000.3.26f1`, Cinemachine `3.1.4`, Timeline `1.8.13`, URP `17.3.0`).
- **Retomada após a Cutscene 2:** `Cutscene02TravelController.OnCutscene02CompletedNaturally` dispara ao fim natural do cartão `"FIM DO ATO 1 / CONCERTO"`. `Cutscene03ReturnController` pode encadear automaticamente (`autoChainAfterCutscene02NaturalEnd`) ou ser disparada via `PlayCutscene()` / atalho **`F7`** de debug replay.
- **Detalhe de continuidade:** a pequena bugiganga consertada no passado aparece **quebrada novamente na bancada** (`CS03_Bugiganga_Quebrada`, gerada via **TripoSG + PBR 4K**, sinalizada pois na CS02 apenas a caixinha de música estava sobre a bancada). Ninguém explica nada — apenas o incômodo e o zumbido grave da máquina que não parou.

## 2. Kit Econômico de Anomalia (Pesquisa)
Escolhemos um kit 100% guiado por script e componentes nativos do URP (flicker de `Light`, rotação reversa lenta, levitação senoidal com partículas ciano de gravidade negativa `-0.25`, `AudioSource` espacial 3D com melodia distorcida/reversa e quad de sombra desalinhada girando em ângulo impossível) — zero shader/render-feature caro, custo quase nulo e alternância instantânea entre `AnomalyState.Normal` e `AnomalyState.Anomalous`.

## 3. Beats & Shot List — Cutscene 3 (`CS03_Return_Timeline.playable`, `32.0s`)

| Beat | Tempo | Câmera (`CinemachineCamera`, Priority 30) | SignalEmitter | Direção |
|---|---:|---|---|---|
| **Beat 1 — Retorno** | `0:00 – 0:08` | `VCam_CS03_B1_ReturnFlash` (FOV 48°) | `SIG_CS03_B1_Flash` @ `0.1s` | Flash branco (`CS03_ReturnFlash_Volume`) decai para silêncio; oficina do presente aparentemente normal. |
| **Beat 2 — Paz Curta** | `0:08 – 0:18` | `VCam_CS03_B2_PhotoPeace` (FOV 36°) | `SIG_CS03_B2_PhotoPeace` @ `8.0s` | Dez junto à bancada olha/guarda a foto com um sorriso tranquilo e respira fundo. |
| **Beat 3 — O Arrepio** | `0:18 – 0:26` | `VCam_CS03_B3_BrokenTrinket` (FOV 30°) | `SIG_CS03_B3_BrokenHum` @ `18.2s` | Close na bugiganga quebrada de novo; lâmpada da oficina pisca; zumbido grave da máquina continua. |
| **Beat 3b/4 — Preto & Controle** | `0:26 – 0:32` | `VCam_CS03_B4_CutToBlack` (FOV 40°) | `SIG_CS03_B3_Black` @ `26.0s`, `SIG_CS03_End` @ `31.8s` | Corte seco pro preto sem música (só o zumbido) → devolve controle ao jogador, ativa as **5 anomalias** e define objetivo **`Investigue a oficina`**. |

## 4. As 5 Anomalias Ambientais (`Assets/Game/Anomalies/Data/act2_anomalies_config.json`)
*(Todas iniciam em `AnomalyState.Normal` — invisíveis e inaudíveis antes da Cutscene 3!)*

| # | ID | Tipo (`AnomalyKind`) | Posição `(x, y, z)` | Evidência Visual / Sonora |
|---:|---|---|---|---|
| 1 | `anom_01_luz_flickando` | `LightFlicker` | `(-28.6, 3.45, -17.2)` | Lâmpada sobre a bancada pisca irregularmente + poeira subindo ao contrário |
| 2 | `anom_02_objeto_reverso` | `ReverseSpin` | `(-26.2, 1.15, -18.8)` | Engrenagem metálica gira devagar ao contrário com oscilação |
| 3 | `anom_03_ferramenta_flutuando` | `FloatingTool` | `(-29.8, 1.08, -16.8)` | Ferramenta flutua centímetros acima do apoio com partículas ciano |
| 4 | `anom_04_som_errado_canto` | `WrongCornerSound` | `(-31.8, 1.35, -14.2)` | Áudio espacial 3D da caixinha com pitch/envelope reverso no canto da estante |
| 5 | `anom_05_sombra_trocada` | `MismatchedShadow` | `(-26.9, 0.04, -20.8)` | Sombra escura no chão gira/estica em ângulo que não corresponde ao objeto |

## 5. API do Componente `Anomaly` (Pronta para a Task de Reparo)
```csharp
public enum AnomalyState { Normal = 0, Anomalous = 1 }

// Studio.Game.Anomalies.Anomaly
anomaly.State;                 // AnomalyState.Normal ou AnomalyState.Anomalous
anomaly.IsAnomalous;           // bool
anomaly.ActivateAnomaly();     // liga visual/sonoro da anomalia
anomaly.Resolve();             // conserta (restaura pose, apaga luz/sombra/som/particulas)
anomaly.Rebreak();             // re-quebra futuramente (reuso em eventos de Ato 2/3)
anomaly.OnStateChanged += (a, newState) => { ... };
```
- **Orquestrador:** `WorkshopAnomalyManager.Instance.ActivateAllAnomalies()`, `ResetAllToNormal()`, `ActiveAnomalyCount`, `GetAllAnomalies()`.

## 6. Como Adicionar uma Nova Anomalia
1. Adicione um novo item em `Assets/Game/Anomalies/Data/act2_anomalies_config.json` com `id`, `displayName`, `kind` e `position`.
2. Rode `Studio > Concerto > Cutscenes > Build Cutscene 03 (O Retorno) & 5 Act 2 Anomalies` no Unity Editor (ou crie um GameObject com o componente `Anomaly` e adicione-o à lista `WorkshopAnomalyManager.anomalies`).
