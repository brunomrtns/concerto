# FASE NOVA: "O EPICENTRO" (#102)

**Projeto:** CONCERTO (`game-concerto`)
**Referência canônica:** [docs/DESIGN_BIBLE.md](docs/DESIGN_BIBLE.md) v1.0
**Pré-requisitos verificados:** Movimento + dash, Eco Temporal + Estase (`#93@8949c9a`), Manifestações + HP/knockdown (`#94@1ce2dd8`), `SaveSystem` e `EastGateInteractable` (agora conectado a `"Epicentro"`).

---

## 1. Mapa e Layout das 3 Seções (`Assets/Game/Scenes/Epicentro.unity`)

```text
[X=0] CP1     [X=10.5]      [X=19.5]        [X=28.5]        [X=38.0]
(Start) ──► (Dash Plat) ──► (Moving Z) ──► (Rotating) ──► [FAST STASIS PUZZLE]
  SEÇÃO 1 — TRAVESSIA (gravidade instável 0.84x | vazio mata em Y < -7.5)
                                                                 │
                                                                 ▼
[X=50] CP2   [X=58] Arena A (Wave 1)    [X=71]    [X=84] Arena B (Wave 2)
             (2 Estilhaços + 1 Flut.) ── Bridge ──► (2 Estilhaços + 2 Flut.)
             + Coberturas Verticais TripoSG (S2_Cover_A1/A2/B1/B2)
                                                                 │
                                                                 ▼
[X=102] CP3  [X=116] CORAÇÃO DA FENDA (Arena Circular Grande + 8 Monólitos)
             Centro: S3_HeartOfRift_BossTriggerStub (batida seca + tremor + "SOBREVIVA")
             Fundo (X=148): Céu-Rachadura Gigante (S3_GiantRiftSkyCrack)
```

- **Onde o Boss (`#103 — Guardião da Fenda`) pluga:** basta escutar `EpicenterArenaBossStub.OnBossArenaTriggered` em `S3_HeartOfRift_BossTriggerStub` (`X=116, Y=1.0, Z=0`), na arena circular de raio `~15m` com `Checkpoint_S3_ArenaFinal` já posicionado na entrada!

---

## 2. Valores Tunáveis (`EpicenterConfig.asset`)

| Parâmetro | Valor | Função |
|---|---:|---|
| `unstableGravityScale` | `0.84` | Gravidade levemente instável (jump feel diferente no rasgo) |
| `voidKillY` | `-7.5 m` | Queda no vazio → respawn limpo no checkpoint da seção |
| `normalMovingPlatformSpeed` | `3.2 m/s` | Plataforma móvel linear da Seção 1 |
| `fastStasisPlatformSpeed` | `10.5 m/s` | Puzzle de Estase (`5.25 m/s` quando congelada por Estase `[R]`) |
| `rotatingPlatformDegPerSec` | `42°/s` | Plataforma rotativa da Seção 1 |
| `arenaTriggerRadius` | `5.5 m` | Raio do trigger stub no Coração da Fenda |
| `omenTremorDurationSec` | `0.28 s` | Decay `< 300ms` (respeita `ReduceCameraShake`) |

---

## 3. Modelos TripoSG Gerados & Áudio Novo

| Asset | Pipeline / Registro | Polígonos | QA |
|---|---|---|---|
| `epicenter_crystal_platform.fbx` + PBR 4K (`Mat_Fab3D_epicenter_crystal_platform.mat`) | **TripoSG exclusivo** (`run_epicenter_triposg_factory.sh`, seed `1020`, `triposg_runner.py` + `pipeline/run_asset.py`) | `6500 polys`, topo plano em `Y=0` | `qa_pass=True` |
| `epicenter_rift_monolith.fbx` + PBR 4K (`Mat_Fab3D_epicenter_rift_monolith.mat`) | **TripoSG exclusivo** (`run_epicenter_triposg_factory.sh`, seed `1020`, `triposg_runner.py` + `pipeline/run_asset.py`) | `6499 polys`, `3.6m` | `qa_pass=True` |
| `sfx_epicenter_boss_omen_TEMP.wav` | Síntese procedural original (CC0 1.0) | 44.1 kHz mono WAV | OK |

---

## 4. Como Testar & Evidências (Gates)

- **Compilação:** `0 warnings CS`, `0 errors CS`.
- **Teste PlayMode:** `EpicenterValidationAndCaptureTest.ValidateEpicenterFullLevelStasisPuzzleVoidRespawnWavesAndArenaStub` — **PASSED (`11.65s`)**:
  - `voidRespawns=1, stasisPuzzle=True, wavesCleared=2, arenaStub=True, frames=231`.
- **MP4 de Evidência:** `captures/epicenter/epicentro_fase_completa_1080p.mp4` (`1920x1080 H.264 + AAC 44.1kHz stereo`).
- **Acesso em jogo:** no `SetorExterno`, após concluir a missão "Sinal", interagir com o Portão Leste (`[E]`) carrega diretamente `"Epicentro"`.

---

## CONFORMIDADE — DESIGN_BIBLE.md v1.0
- **Seção 2 (Character & Controls):** CONFORME — BoxColliders planos no topo de todas as plataformas TripoSG (zero enganchamento); gravidade instável 100% tunável em `EpicenterConfig`.
- **Seção 3 (Métricas):** CONFORME — tremor do Coração da Fenda com decay `0.28s (< 300ms)` e escala `ReduceCameraShake`.
- **Seção 5 (Teach → Test → Twist):** CONFORME — Estase ensinada no playground/combate (`#93/#94`) agora é testada como puzzle de plataforma rápida (`S1_FastStasisPuzzlePlatform`), seguida de 2 waves curtas com cobertura vertical.
- **Seção 9 (Juice):** CONFORME — batida seca grave + tremor + fade rápido + banner `"OBJETIVO: SOBREVIVA"` na chegada à Arena Final.
