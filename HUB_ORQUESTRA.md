# HUB "A ORQUESTRA" + TELA DE FUSOS + LINHA DO EQUILÍBRIO

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0 · [docs/gdd/05-level-design.md](docs/gdd/05-level-design.md) §1 · [docs/gdd/01-narrativa.md](docs/gdd/01-narrativa.md) §5 · [docs/gdd/04-mecanicas.md](docs/gdd/04-mecanicas.md) §6
**Data:** 2026-11-25
**Modelos 3D criados nesta tarefa:** **0 modelos novos** (reutilizados exclusivamente os prefabs TripoSG já existentes em `Assets/Game/Prefabs/Fab3D/`).
**Escopo:** Fechado em Hub (`AOrquestra.unity`), Tela de Fusos fullscreen e dados da Linha do Equilíbrio (sem construir os fusos ainda).

---

## 1. Declaração de Conformidade (`DESIGN_BIBLE.md` + `GDD`)

- **GDD 05 §1 (Hub "A Orquestra"):** CONFORME — Antiga sala de concertos em [AOrquestra.unity](unity/Assets/Game/Scenes/AOrquestra.unity) com Posto de Reparo (`c5_prop_bancada_relogio`), Console da Tela de Fusos (`c5_prop_no_resincronia`), órgão central (`c5_prop_sirene_mestra`) e 5 slots de chegada de NPCs.
- **GDD 01 §5 (Linha do Equilíbrio — decisão informada sem tela de moralidade):** CONFORME — Todos os 5 fusos estão no Data Asset [BalanceLineDatabase.asset](unity/Assets/Game/Data/BalanceLineDatabase.asset) com 1 benefício + 1 custo exibidos **antes** de confirmar na Tela de Fusos.
- **GDD 04 §6 (UI/HUD):** CONFORME — Tela de Fusos em tela cheia (`1920x1080 Expand`), PT-BR claro, fontes `17–30pt` (`≥ 14pt`).
- **Hub Vivo & Áudio:** CONFORME — A cada fuso restaurado, o NPC correspondente chega ao hub e uma camada harmônica adicional da marchinha passa a tocar roteada no `MusicGroup` do `AudioMixController`.
- **Save JSON Versionado (`DESIGN_BIBLE` §8):** CONFORME — `SaveGameData.restoredFusoIds` e `SaveGameData.appliedBalanceEvents` persistidos exclusivamente em JSON (`0` `PlayerPrefs`).

---

## 2. Tabela da Linha do Equilíbrio (`BalanceLineDatabase.asset` — GDD 01 §5)

| Fuso | Relíquia | NPC que Chega ao Hub | Benefício | Custo (Fuso Alvo) |
|---|---|---|---|---|
| **Caldeiral** | Relíquia: Mola | Tio Horário | Energia estável para A Orquestra (hub ganha NPCs e loja) | Fumaça cessa → céu limpo congela o Retrógrado (`retrogrado`) |
| **Vitralha** | Relíquia: Ponteiro Maior | Vó Segunda | Rio volta a correr → novo acesso aquático em Marésia | Enchente alaga cavernas-habitação de Marésia (`maresia`) |
| **Marésia** | Relíquia: Pêndulo-Âncora | Mecânico Brito | Mar degela → rotas de barco entre fusos | Ondas soltas arrastam um pedaço da Ecodia (`ecodia`) |
| **Ecodia** | Relíquia: Diapasão | Analista Luma | Habitantes saem do loop de 6 s (quests de memória) | Sem o loop, Censores patrulham em tempo real (`ecodia`) |
| **Retrógrado** | Relíquia: Ampulheta Invertida | Engenheira Vera | Areia-parada revela ruínas do pré-Fratura | Tempo reverso "desfaz" melhorias cosméticas em Caldeiral (`caldeiral`) |

---

## 3. Evidências (GATES 6 e 7)

- **Smoke Test:** `Smoke_27_HubOrquestraTelaDeFusosNpcArrivalAndSave` em [ConcertoSmokeTests.cs](unity/Assets/Game/Tests/SmokeTest/ConcertoSmokeTests.cs) — **PASSED** (`27/27` suíte completa verde, `0 warnings CS`, `0 errors CS`).
- **MP4 (navegar grafo → ler benefício/custo → confirmar → ver NPCs chegarem):** [captures/hub/hub_orquestra_demo.mp4](captures/hub/hub_orquestra_demo.mp4).

---

## 4. Lista de Modelos 3D TripoSG Utilizados no Hub

*0 modelos 3D novos criados nesta tarefa; todos os modelos abaixo são prefabs TripoSG já existentes no catálogo do projeto (`Assets/Game/Prefabs/Fab3D/`):*
1. `caldeiral_env_portal_pillar.prefab` (6 pilares da sala de concertos)
2. `c5_prop_sirene_mestra.prefab` (órgão/palco central)
3. `c5_prop_bancada_relogio.prefab` (Posto de Reparo)
4. `c5_prop_no_resincronia.prefab` (Console da Tela de Fusos)
5. `hero_dez_humanoid.prefab` (Dez no hub)
6. `npc_tio_horario_humanoid.prefab`, `npc_vo_segunda_humanoid.prefab`, `npc_mecanico_brito_humanoid.prefab`, `npc_analista_luma_humanoid.prefab`, `npc_engenheira_vera_humanoid.prefab` (5 NPCs que chegam conforme os fusos são restaurados)
